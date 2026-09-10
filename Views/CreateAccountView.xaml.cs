using RasidAccountingSystem.Services;
using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RasidAccountingSystem.Helpers;

namespace RasidAccountingSystem.Views
{
    public partial class CreateAccountView : Window
    {
        #region Private Fields

        private readonly DatabaseService _dbService;

        #endregion

        #region Constructors

        // التعديل: إضافة كونستركتور بدون معاملات
        public CreateAccountView() : this(new DatabaseService())
        {
        }

        public CreateAccountView(DatabaseService dbService)
        {
            try
            {
                InitializeComponent();
                _dbService = dbService;

                // ربط الأحداث
                btnCreate.Click += BtnCreate_Click;
                btnCancel.Click += (s, e) => this.DialogResult = false;

                // التنقل بين الحقول بالضغط على Enter
                txtUsername.KeyDown += (s, e) => { if (e.Key == Key.Enter) txtFullName.Focus(); };
                txtFullName.KeyDown += (s, e) => { if (e.Key == Key.Enter) txtPassword.Focus(); };
                txtPassword.KeyDown += (s, e) => { if (e.Key == Key.Enter) txtConfirmPassword.Focus(); };
                txtConfirmPassword.KeyDown += (s, e) => { if (e.Key == Key.Enter) txtEmail.Focus(); };
                txtEmail.KeyDown += (s, e) => { if (e.Key == Key.Enter) txtPhone.Focus(); };
                txtPhone.KeyDown += (s, e) => { if (e.Key == Key.Enter) BtnCreate_Click(s, e); };

                // تعيين التركيز على حقل اسم المستخدم عند فتح النافذة
                this.Loaded += (s, e) => txtUsername.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAccountView Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة نافذة إنشاء الحساب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Event Handlers

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // جلب البيانات من الواجهة
                string username = txtUsername.Text.Trim();
                string fullName = txtFullName.Text.Trim();
                string password = txtPassword.Password;
                string confirmPassword = txtConfirmPassword.Password;
                string email = txtEmail.Text.Trim();
                string phone = txtPhone.Text.Trim();

                // ✅ إصلاح أمني: الحسابات المُنشأة ذاتياً من شاشة الدخول العامة (بدون تسجيل دخول
                // مسبق) تُنشأ دائماً بأقل صلاحية ("مستخدم عادي")، بغض النظر عن أي اختيار في الواجهة.
                // سابقاً كان بإمكان أي شخص غير مسجّل دخول اختيار "مدير النظام" مباشرة من هذه الشاشة
                // العامة - وهي ثغرة أمنية حقيقية تم إغلاقها هنا بالكامل. ترقية صلاحية أي مستخدم بعد
                // إنشاء حسابه تتم فقط عبر مدير نظام حقيقي مسجَّل دخوله من شاشة إدارة الصلاحيات.
                string userRole = "مستخدم عادي";

                // ============================================================
                // التحقق من صحة البيانات
                // ============================================================

                // التحقق من اسم المستخدم
                if (string.IsNullOrEmpty(username))
                {
                    ShowError("يرجى إدخال اسم المستخدم");
                    txtUsername.Focus();
                    return;
                }

                if (username.Length < 3)
                {
                    ShowError("اسم المستخدم يجب أن يكون 3 أحرف على الأقل");
                    txtUsername.Focus();
                    txtUsername.SelectAll();
                    return;
                }

                // التحقق من الاسم الكامل
                if (string.IsNullOrEmpty(fullName))
                {
                    ShowError("يرجى إدخال الاسم الكامل");
                    txtFullName.Focus();
                    return;
                }

                if (fullName.Length < 3)
                {
                    ShowError("الاسم الكامل يجب أن يكون 3 أحرف على الأقل");
                    txtFullName.Focus();
                    txtFullName.SelectAll();
                    return;
                }

                // التحقق من كلمة المرور
                if (string.IsNullOrEmpty(password))
                {
                    ShowError("يرجى إدخال كلمة المرور");
                    txtPassword.Focus();
                    return;
                }

                if (password.Length < 6)
                {
                    ShowError("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
                    txtPassword.Focus();
                    txtPassword.SelectAll();
                    return;
                }

                // التحقق من تطابق كلمتي المرور
                if (password != confirmPassword)
                {
                    ShowError("كلمتا المرور غير متطابقتين");
                    txtConfirmPassword.Focus();
                    txtConfirmPassword.SelectAll();
                    return;
                }

                // التحقق من البريد الإلكتروني (اختياري)
                if (!string.IsNullOrEmpty(email) && !IsValidEmail(email))
                {
                    ShowError("البريد الإلكتروني غير صحيح");
                    txtEmail.Focus();
                    txtEmail.SelectAll();
                    return;
                }

                // ============================================================
                // تنفيذ عملية إنشاء المستخدم مع إعادة المحاولة التلقائية
                // ============================================================

                try
                {
                    btnCreate.IsEnabled = false;
                    btnCreate.Content = "جاري الإنشاء...";
                    Mouse.OverrideCursor = Cursors.Wait;

                    // ✅ التحقق من عدم وجود اسم المستخدم باستخدام DatabaseExecutor
                    bool userExists = await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                    {
                        string sql = "SELECT COUNT(*) FROM Users WHERE Username = @username AND IsActive = 1";
                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@username", username);
                            long count = (long)await cmd.ExecuteScalarAsync();
                            return count > 0;
                        }
                    }, CancellationToken.None, 3);

                    if (userExists)
                    {
                        ShowError("اسم المستخدم موجود مسبقاً");
                        txtUsername.Focus();
                        txtUsername.SelectAll();
                        return;
                    }

                    // ✅ إنشاء المستخدم باستخدام DatabaseExecutor
                    bool success = await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                    {
                        string hash = PasswordHelper.HashPassword(password);

                        string sql = @"
                            INSERT INTO Users (Username, PasswordHash, FullName, Email, Phone, UserRole, IsActive, CreatedDate)
                            VALUES (@username, @hash, @fullName, @email, @phone, @role, 1, CURRENT_TIMESTAMP)";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@username", username);
                            cmd.Parameters.AddWithValue("@hash", hash);
                            cmd.Parameters.AddWithValue("@fullName", fullName);
                            cmd.Parameters.AddWithValue("@email", email ?? "");
                            cmd.Parameters.AddWithValue("@phone", phone ?? "");
                            cmd.Parameters.AddWithValue("@role", userRole);
                            return await cmd.ExecuteNonQueryAsync() > 0;
                        }
                    }, CancellationToken.None, 3);

                    if (success)
                    {
                        MessageBox.Show("تم إنشاء الحساب بنجاح!", "نجاح",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        this.DialogResult = true;
                        this.Close();
                    }
                    else
                    {
                        ShowError("فشل إنشاء الحساب. يرجى المحاولة مرة أخرى");
                    }
                }
                catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
                {
                    ShowError("تعذر إنشاء الحساب بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.");
                    System.Diagnostics.Debug.WriteLine($"Database locked: {ex.Message}");
                }
                catch (Exception ex)
                {
                    ShowError($"خطأ في إنشاء الحساب: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"BtnCreate_Click Error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
                finally
                {
                    btnCreate.IsEnabled = true;
                    btnCreate.Content = "إنشاء حساب";
                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطأ غير متوقع: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"BtnCreate_Click Outer Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Helper Methods

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }

        private void ClearError()
        {
            txtError.Text = "";
            txtError.Visibility = Visibility.Collapsed;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // ملاحظة: تم نقل منطق تشفير كلمات المرور إلى RasidAccountingSystem.Helpers.PasswordHelper (معيار PBKDF2 مع Salt)

        #endregion

        #region Additional Event Handlers for Better UX

        // عند الضغط على Esc، إغلاق النافذة
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Escape)
                {
                    this.DialogResult = false;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Window_PreviewKeyDown Error: {ex.Message}");
            }
        }

        // عند الضغط على Ctrl+Enter، تنفيذ عملية إنشاء الحساب
        private void Window_PreviewKeyDown_WithCtrlEnter(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    BtnCreate_Click(sender, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Window_PreviewKeyDown_WithCtrlEnter Error: {ex.Message}");
            }
        }

        // عند تغيير أي حقل، إخفاء رسالة الخطأ
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ClearError();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ClearError();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearError();
        }

        #endregion
    }
}