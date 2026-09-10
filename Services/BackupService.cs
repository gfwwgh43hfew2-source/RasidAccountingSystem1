using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Models;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// نتيجة محاولة تنفيذ نسخة احتياطية
    /// </summary>
    public class BackupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string BackupFilePath { get; set; }
    }

    /// <summary>
    /// خدمة النسخ الاحتياطي التلقائي المجدول.
    ///
    /// الفرق الجوهري عن النسخ الاحتياطي القديم (نسخ ملف .sqlite مباشرة بـ File.Copy):
    /// قاعدة البيانات تعمل بوضع WAL (Write-Ahead Logging)، ما يعني أن أحدث الحركات المحفوظة
    /// قد تكون موجودة فعلياً في ملف مساعد (.db-wal) ولم تُكتب بعد داخل ملف القاعدة الرئيسي.
    /// نسخ ملف القاعدة الرئيسي فقط بـ File.Copy في هذه اللحظة قد ينتج نسخة احتياطية تنقصها
    /// آخر الحركات، أو حتى نسخة غير متسقة لو حدثت كتابة أثناء النسخ.
    ///
    /// هذه الخدمة تستخدم بدلاً من ذلك أمر SQLite الآمن (VACUUM INTO)، الذي يضمن دمج كل بيانات
    /// WAL أولاً، ثم إنشاء نسخة كاملة ومتسقة تماماً من القاعدة في ملف واحد جديد - بأمان تام
    /// حتى أثناء استخدام البرنامج بشكل طبيعي في نفس الوقت.
    ///
    /// كما تضيف جدولة حقيقية (فحص دوري كل فترة قصيرة أثناء عمل البرنامج) بدلاً من انتظار
    /// إغلاق البرنامج فقط لتنفيذ النسخة الاحتياطية، حتى لا يفوت المستخدم نسخاً كاملة لو ترك
    /// البرنامج مفتوحاً لأيام متواصلة.
    /// </summary>
    public class BackupService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public BackupService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _connectionString = databaseService.GetConnectionString();
        }

        #region قراءة/كتابة الإعدادات (Settings)

        /// <summary>
        /// قراءة إعدادات النسخ الاحتياطي المخزَّنة (تُنشئ الجدول لو لم يكن موجوداً بعد)
        /// </summary>
        public BackupSettings GetBackupSettings()
        {
            var settings = new BackupSettings();

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    EnsureBackupSettingsTableExists(connection);

                    string sql = "SELECT SettingKey, SettingValue FROM BackupSettings";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader["SettingKey"]?.ToString();
                            string value = reader["SettingValue"]?.ToString();

                            switch (key)
                            {
                                case "AutoBackupEnabled":
                                    settings.AutoBackupEnabled = value == "True";
                                    break;
                                case "BackupPath":
                                    settings.BackupPath = value;
                                    break;
                                case "BackupFrequency":
                                    settings.BackupFrequency = string.IsNullOrEmpty(value) ? "OnSave" : value;
                                    break;
                                case "BackupRetention":
                                    if (int.TryParse(value, out int retention) && retention > 0)
                                        settings.BackupRetention = retention;
                                    break;
                                case "LastAutoBackupDate":
                                    if (DateTime.TryParse(value, out DateTime lastDate))
                                        settings.LastBackupDate = lastDate;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل قراءة إعدادات النسخ الاحتياطي", ex, "BackupService");
            }

            return settings;
        }

        private void EnsureBackupSettingsTableExists(SQLiteConnection connection)
        {
            string createTableSql = @"
                CREATE TABLE IF NOT EXISTS BackupSettings (
                    SettingKey TEXT PRIMARY KEY,
                    SettingValue TEXT,
                    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                )";

            using (var cmd = new SQLiteCommand(createTableSql, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateLastBackupDate(DateTime backupDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    EnsureBackupSettingsTableExists(connection);

                    string sql = @"
                        INSERT INTO BackupSettings (SettingKey, SettingValue, UpdatedDate)
                        VALUES ('LastAutoBackupDate', @date, @date)
                        ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = @date, UpdatedDate = @date";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@date", backupDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحديث تاريخ آخر نسخة احتياطية", ex, "BackupService");
            }
        }

        #endregion

        #region تنفيذ النسخة الاحتياطية (Backup Execution)

        /// <summary>
        /// تنفيذ نسخة احتياطية فورية وآمنة الآن، بغض النظر عن الجدولة.
        /// تُستخدم لكل من: الزر اليدوي "نسخة احتياطية الآن"، النسخة عند إغلاق البرنامج،
        /// والتشغيل المجدول التلقائي.
        /// </summary>
        public async Task<BackupResult> CreateBackupNowAsync(string backupFolder, int retentionCount = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(backupFolder))
                {
                    return new BackupResult { Success = false, Message = "مسار النسخ الاحتياطي غير محدد" };
                }

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"RasidBackup_{timestamp}.sqlite";
                string backupFilePath = Path.Combine(backupFolder, backupFileName);

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // ✅ VACUUM INTO: يضمن دمج بيانات WAL أولاً وإنشاء نسخة متسقة وكاملة تماماً
                    // في ملف واحد جديد، بأمان حتى مع استمرار استخدام البرنامج بشكل طبيعي.
                    // المسار يُمرَّر كنص مقتبس مباشرة داخل جملة SQL (VACUUM INTO لا يدعم Parameters)،
                    // لذلك نستبدل أي علامة اقتباس مفردة داخل المسار (احتياطاً) لمنع كسر بناء الجملة.
                    string safePath = backupFilePath.Replace("'", "''");
                    string sql = $"VACUUM INTO '{safePath}'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                if (!File.Exists(backupFilePath))
                {
                    return new BackupResult { Success = false, Message = "لم يتم العثور على ملف النسخة الاحتياطية بعد إنشائها" };
                }

                CleanupOldBackups(backupFolder, retentionCount);
                UpdateLastBackupDate(DateTime.Now);

                Logger.LogInfo($"تم إنشاء نسخة احتياطية بنجاح: {backupFilePath}", "BackupService");

                return new BackupResult
                {
                    Success = true,
                    Message = "تم إنشاء النسخة الاحتياطية بنجاح",
                    BackupFilePath = backupFilePath
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ النسخة الاحتياطية", ex, "BackupService");
                return new BackupResult { Success = false, Message = $"فشل إنشاء النسخة الاحتياطية: {ex.Message}" };
            }
        }

        /// <summary>
        /// حذف أقدم النسخ الاحتياطية في المجلد إن تجاوز عددها الحد الأقصى المسموح به (Retention)
        /// </summary>
        private void CleanupOldBackups(string backupFolder, int retentionCount)
        {
            try
            {
                if (retentionCount <= 0) return;

                var backupFiles = Directory.GetFiles(backupFolder, "RasidBackup_*.sqlite")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (backupFiles.Count <= retentionCount) return;

                foreach (var oldFile in backupFiles.Skip(retentionCount))
                {
                    try
                    {
                        oldFile.Delete();
                        Logger.LogInfo($"تم حذف نسخة احتياطية قديمة: {oldFile.Name}", "BackupService");
                    }
                    catch (Exception delEx)
                    {
                        Logger.LogWarning($"تعذّر حذف نسخة احتياطية قديمة ({oldFile.Name}): {delEx.Message}", "BackupService");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنظيف النسخ الاحتياطية القديمة", ex, "BackupService");
            }
        }

        /// <summary>
        /// تنفيذ نسخة احتياطية آمنة إلى مسار ملف محدد بدقة (يختاره المستخدم بنفسه عبر مربع حوار
        /// "حفظ باسم"). بدون تنظيف نسخ قديمة تلقائياً، لأن هذا استخدام يدوي لمرة واحدة والمستخدم
        /// يتحكم بمكان واسم الملف بنفسه.
        /// </summary>
        public async Task<BackupResult> CreateBackupToExactPathAsync(string exactFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exactFilePath))
                {
                    return new BackupResult { Success = false, Message = "مسار الملف غير محدد" };
                }

                string targetFolder = Path.GetDirectoryName(exactFilePath);
                if (!string.IsNullOrEmpty(targetFolder) && !Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // لو الملف موجود بالفعل (المستخدم اختار اسم ملف موجود)، لازم نحذفه أولاً
                // لأن VACUUM INTO يرفض الكتابة فوق ملف موجود مسبقاً
                if (File.Exists(exactFilePath))
                {
                    File.Delete(exactFilePath);
                }

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string safePath = exactFilePath.Replace("'", "''");
                    string sql = $"VACUUM INTO '{safePath}'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                if (!File.Exists(exactFilePath))
                {
                    return new BackupResult { Success = false, Message = "لم يتم العثور على ملف النسخة الاحتياطية بعد إنشائها" };
                }

                Logger.LogInfo($"تم إنشاء نسخة احتياطية يدوية بنجاح: {exactFilePath}", "BackupService");

                return new BackupResult
                {
                    Success = true,
                    Message = "تم إنشاء النسخة الاحتياطية بنجاح",
                    BackupFilePath = exactFilePath
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ النسخة الاحتياطية اليدوية لمسار محدد", ex, "BackupService");
                return new BackupResult { Success = false, Message = $"فشل إنشاء النسخة الاحتياطية: {ex.Message}" };
            }
        }

        #endregion

        #region الجدولة التلقائية (Scheduling)

        /// <summary>
        /// يحدد هل حان وقت تنفيذ نسخة احتياطية مجدولة جديدة، بناءً على تكرار الجدولة المختار
        /// (يومي / أسبوعي / شهري) وتاريخ آخر نسخة احتياطية مسجَّل.
        /// </summary>
        public bool IsScheduledBackupDue(BackupSettings settings)
        {
            if (!settings.AutoBackupEnabled || string.IsNullOrWhiteSpace(settings.BackupPath))
                return false;

            // "OnSave" تعني أن النسخ يتم فقط عند إغلاق البرنامج يدوياً، وليس عبر الجدولة الدورية
            if (string.Equals(settings.BackupFrequency, "OnSave", StringComparison.OrdinalIgnoreCase))
                return false;

            if (settings.LastBackupDate == null)
                return true; // لم يتم عمل أي نسخة احتياطية مجدولة من قبل إطلاقاً

            TimeSpan interval;
            switch (settings.BackupFrequency)
            {
                case "Weekly":
                    interval = TimeSpan.FromDays(7);
                    break;
                case "Monthly":
                    interval = TimeSpan.FromDays(30);
                    break;
                case "Daily":
                default:
                    interval = TimeSpan.FromDays(1);
                    break;
            }

            return DateTime.Now - settings.LastBackupDate.Value >= interval;
        }

        /// <summary>
        /// نقطة الدخول الرئيسية للجدولة التلقائية: تُستدعى دورياً (كل عدة دقائق) من مؤقّت في
        /// الشاشة الرئيسية. تتحقق أولاً هل حان وقت نسخة احتياطية جديدة، وتنفذها تلقائياً بصمت
        /// في الخلفية دون أي تدخل من المستخدم إن كان الوقت قد حان.
        /// </summary>
        public async Task<BackupResult> RunScheduledBackupIfDueAsync()
        {
            var settings = GetBackupSettings();

            if (!IsScheduledBackupDue(settings))
            {
                return new BackupResult { Success = false, Message = "لم يحن موعد النسخة الاحتياطية المجدولة بعد" };
            }

            Logger.LogInfo("بدء تنفيذ نسخة احتياطية مجدولة تلقائياً", "BackupService");
            return await CreateBackupNowAsync(settings.BackupPath, settings.BackupRetention);
        }

        #endregion
    }
}
