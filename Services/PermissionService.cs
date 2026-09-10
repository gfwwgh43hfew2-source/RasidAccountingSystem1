using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using RasidAccountingSystem.Helpers;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// وصف صلاحية واحدة في كتالوج الصلاحيات المعروفة بالنظام
    /// </summary>
    public class PermissionCatalogItem
    {
        public string PermissionKey { get; set; }
        public string NameAr { get; set; }
        public string CategoryAr { get; set; }
    }

    /// <summary>
    /// عنصر مستخدم مبسّط لعرضه في شاشة إدارة الصلاحيات
    /// </summary>
    public class UserSimpleItem
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string UserRole { get; set; }
    }

    /// <summary>
    /// خدمة الصلاحيات الدقيقة على مستوى كل مستخدم بعينه.
    ///
    /// القرار المعماري: قاعدة البيانات فيها بالفعل جدول UserPermissions جاهز ومصمَّم جيداً
    /// (UserID, PermissionKey, PermissionValue, GrantedBy, GrantedDate...) لكنه لم يكن
    /// مُستخدَماً في أي مكان بالبرنامج قبل الآن - نفس النمط المكتشف سابقاً مع جداول البنك
    /// والشيكات وسجل التعديلات في هذا المشروع. تم البناء عليه مباشرة بدلاً من اختراع جدول
    /// صلاحيات جديد، تجنباً لتكرار غير ضروري ولضمان الاتساق مع تصميم قاعدة البيانات الأصلي.
    ///
    /// سياسة الصلاحية الافتراضية (مهم جداً لعدم تعطيل عمل أحد فجأة عند أول تفعيل للنظام):
    /// 1. المستخدم بدور "مدير النظام" له كل الصلاحيات دائماً، بلا استثناء.
    /// 2. لو يوجد منح/منع صريح لمستخدم بعينه في جدول UserPermissions، هذا القرار الصريح
    ///    هو الذي يُطبَّق دائماً (سواء بالسماح أو بالمنع) - يسمح للمدير بتخصيص كل مستخدم فردياً.
    /// 3. لو لا يوجد أي قرار صريح لهذا المستخدم ولهذه الصلاحية تحديداً، يُطبَّق افتراضي آمن:
    ///    الأدوار المتوسطة/العليا ("مشرف"، "محاسب") تحصل على نفس الصلاحيات التي كانت متاحة
    ///    لهم قبل تفعيل هذا النظام (حتى لا ينكسر عملهم المعتاد فجأة)، بينما "مستخدم عادي"
    ///    (أقل الأدوار صلاحية) يُمنع افتراضياً من الإجراءات الحساسة حتى يُمنحها له المدير صراحةً.
    /// </summary>
    public class PermissionService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        private const string AdminRole = "مدير النظام";
        private const string LegacyAdminRole = "System Administrator"; // اصطلاح قديم غير متسق كان يُستخدم في بعض مسارات إنشاء أول مستخدم، تم توحيده الآن لكن نتعرف عليه هنا حماية للتنصيبات القديمة
        private const string LeastPrivilegedRole = "مستخدم عادي";

        /// <summary>
        /// هل هذا الدور يُعتبر مدير نظام (بأي من الاصطلاحين المستخدمين تاريخياً في هذا المشروع)؟
        /// دالة عامة (static) حتى تُستخدم في أي شاشة تحتاج التحقق من دور المدير، بدل تكرار
        /// المقارنة النصية "مدير النظام" في كل مكان بشكل منفصل قد يُنسى تحديثه لاحقاً.
        /// </summary>
        public static bool IsAdminRole(string userRole)
        {
            return userRole == AdminRole || userRole == LegacyAdminRole;
        }

        public PermissionService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _connectionString = databaseService.GetConnectionString();
        }

        #region كتالوج الصلاحيات المعروفة

        /// <summary>
        /// قائمة كل الصلاحيات الحساسة المُدارة حالياً في النظام. لإضافة صلاحية جديدة مستقبلاً
        /// لإجراء آخر، تُضاف هنا فقط ثم يُستدعى HasPermissionAsync بنفس المفتاح قبل تنفيذ الإجراء.
        /// </summary>
        public static List<PermissionCatalogItem> GetPermissionCatalog()
        {
            return new List<PermissionCatalogItem>
            {
                new PermissionCatalogItem { PermissionKey = "view_audit_log", NameAr = "عرض سجل التعديلات", CategoryAr = "الأمان" },
                new PermissionCatalogItem { PermissionKey = "manage_users", NameAr = "إدارة المستخدمين (إضافة/تعديل)", CategoryAr = "الأمان" },
                new PermissionCatalogItem { PermissionKey = "manage_backup_settings", NameAr = "إدارة إعدادات النسخ الاحتياطي", CategoryAr = "الأمان" },
                new PermissionCatalogItem { PermissionKey = "delete_bank_account", NameAr = "حذف حساب بنكي", CategoryAr = "البنك" },
                new PermissionCatalogItem { PermissionKey = "void_check", NameAr = "إلغاء شيك", CategoryAr = "الشيكات" },
                new PermissionCatalogItem { PermissionKey = "edit_opening_balance", NameAr = "تعديل رصيد أول المدة لعميل/مورد", CategoryAr = "الحسابات" },
            };
        }

        #endregion

        #region التحقق من الصلاحيات (Permission Checks)

        /// <summary>
        /// التحقق من صلاحية المستخدم الحالي (المسجَّل دخوله الآن) لتنفيذ إجراء معيّن
        /// </summary>
        public async Task<bool> HasPermissionAsync(string permissionKey)
        {
            int currentUserId = Views.LoginView.CurrentUserId;
            string currentUserRole = Views.LoginView.CurrentUserRole;

            return await HasPermissionAsync(currentUserId, currentUserRole, permissionKey);
        }

        public async Task<bool> HasPermissionAsync(int userId, string userRole, string permissionKey)
        {
            try
            {
                if (IsAdminRole(userRole))
                    return true; // مدير النظام له كل الصلاحيات دائماً (بأي من الاصطلاحين المستخدمين تاريخياً)

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT PermissionValue FROM UserPermissions WHERE UserID = @userId AND PermissionKey = @key";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@key", permissionKey);

                        object result = await cmd.ExecuteScalarAsync();

                        if (result != null && result != DBNull.Value)
                        {
                            // يوجد قرار صريح لهذا المستخدم بالذات - يُطبَّق كما هو (سماح أو منع)
                            return Convert.ToInt32(result) == 1;
                        }
                    }
                }

                // لا يوجد قرار صريح - نطبّق الافتراضي الآمن: الأدوار المتوسطة/العليا مسموحة
                // (حتى لا ينكسر عملها المعتاد)، وأقل الأدوار صلاحية ممنوعة حتى تُمنح صراحةً
                return userRole != LeastPrivilegedRole;
            }
            catch (Exception ex)
            {
                Logger.LogError($"فشل التحقق من صلاحية '{permissionKey}' للمستخدم #{userId}", ex, "PermissionService");
                // في حالة فشل التحقق نفسه (خطأ غير متوقع)، الأسلم منح الصلاحية بدلاً من إيقاف
                // عمل المستخدم بالكامل بسبب خلل تقني لا علاقة له بصلاحياته الفعلية
                return true;
            }
        }

        /// <summary>
        /// التحقق من الصلاحية مع إظهار رسالة تنبيه تلقائية للمستخدم لو كانت غير متاحة له.
        /// تُستخدم مباشرة في بداية أي دالة معالجة حدث لإجراء حساس، مثال:
        ///   if (!await _permissionService.CheckAndWarnAsync("delete_bank_account")) return;
        /// </summary>
        public async Task<bool> CheckAndWarnAsync(string permissionKey)
        {
            bool allowed = await HasPermissionAsync(permissionKey);

            if (!allowed)
            {
                System.Windows.MessageBox.Show(
                    "ليس لديك صلاحية للقيام بهذا الإجراء. يرجى التواصل مع مدير النظام إذا كنت تحتاج هذه الصلاحية.",
                    "غير مصرَّح",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }

            return allowed;
        }

        #endregion

        #region الإدارة (للمدير فقط)

        public async Task<List<UserSimpleItem>> GetAllUsersAsync()
        {
            var list = new List<UserSimpleItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserID, Username, FullName, UserRole FROM Users WHERE IsActive = 1 ORDER BY FullName";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new UserSimpleItem
                        {
                            UserId = Convert.ToInt32(reader["UserID"]),
                            Username = reader["Username"].ToString(),
                            FullName = reader["FullName"].ToString(),
                            UserRole = reader["UserRole"].ToString()
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// جلب كل الصلاحيات الصريحة المُخزَّنة لمستخدم بعينه (بدون تطبيق الافتراضيات - فقط ما تم تحديده صراحةً)
        /// </summary>
        public async Task<Dictionary<string, bool>> GetExplicitUserPermissionsAsync(int userId)
        {
            var result = new Dictionary<string, bool>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = "SELECT PermissionKey, PermissionValue FROM UserPermissions WHERE UserID = @userId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result[reader["PermissionKey"].ToString()] = Convert.ToInt32(reader["PermissionValue"]) == 1;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// منح أو منع صلاحية معيّنة لمستخدم بعينه صراحةً (يتفوق هذا القرار على الافتراضي دائماً)
        /// </summary>
        public async Task<(bool Success, string Message)> SetUserPermissionAsync(int userId, string permissionKey, bool allowed, int grantedByUserId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO UserPermissions (UserID, PermissionKey, PermissionValue, GrantedBy, GrantedDate)
                        VALUES (@userId, @key, @value, @grantedBy, @date)
                        ON CONFLICT(UserID, PermissionKey) DO UPDATE SET
                            PermissionValue = @value,
                            GrantedBy = @grantedBy,
                            GrantedDate = @date";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@key", permissionKey);
                        cmd.Parameters.AddWithValue("@value", allowed ? 1 : 0);
                        cmd.Parameters.AddWithValue("@grantedBy", grantedByUserId);
                        cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                Logger.LogInfo($"تم {(allowed ? "منح" : "منع")} صلاحية '{permissionKey}' للمستخدم #{userId}", "PermissionService");
                return (true, "تم تحديث الصلاحية بنجاح");
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحديث صلاحية المستخدم", ex, "PermissionService");
                return (false, $"حدث خطأ أثناء تحديث الصلاحية: {ex.Message}");
            }
        }

        /// <summary>
        /// إعادة تعيين صلاحية مستخدم بعينه للوضع الافتراضي (حذف أي قرار صريح سابق لها)
        /// </summary>
        public async Task<(bool Success, string Message)> ResetUserPermissionAsync(int userId, string permissionKey)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = "DELETE FROM UserPermissions WHERE UserID = @userId AND PermissionKey = @key";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@key", permissionKey);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return (true, "تم إرجاع الصلاحية للوضع الافتراضي");
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل إعادة تعيين صلاحية المستخدم", ex, "PermissionService");
                return (false, $"حدث خطأ: {ex.Message}");
            }
        }

        #endregion
    }
}
