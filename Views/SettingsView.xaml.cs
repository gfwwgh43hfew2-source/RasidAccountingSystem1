using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RasidAccountingSystem.Services;
using RasidAccountingSystem.Helpers;

namespace RasidAccountingSystem.Views
{
    public partial class SettingsView : UserControl
    {
        #region المتغيرات الخاصة

        private readonly DatabaseService _dbService;
        private readonly BackupService _backupService;
        private readonly MainWindow _mainWindow;

        #endregion

        #region المنشئ

        public SettingsView(DatabaseService dbService, MainWindow mainWindow)
        {
            InitializeComponent();

            _dbService = dbService;
            _backupService = new BackupService(_dbService);
            _mainWindow = mainWindow;

            AttachEventHandlers();

            LoadSettings();
        }

        #endregion

        #region ربط الأحداث

        private void AttachEventHandlers()
        {
            btnBrowseLogo.Click += BtnBrowseLogo_Click;
            btnUploadLogo.Click += BtnUploadLogo_Click;
            btnResetLogo.Click += BtnResetLogo_Click;
            btnSave.Click += BtnSave_Click;
            btnCancel.Click += BtnCancel_Click;
            btnChangePassword.Click += BtnChangePassword_Click;
            btnBackup.Click += BtnBackup_Click;
            btnRestore.Click += BtnRestore_Click;
            btnBrowseBackupPath.Click += BtnBrowseBackupPath_Click;
            btnSaveBackupSettings.Click += BtnSaveBackupSettings_Click;
            chkAutoBackup.Checked += ChkAutoBackup_Checked;
            chkAutoBackup.Unchecked += ChkAutoBackup_Unchecked;
        }

        #endregion

        #region دوال تحميل الإعدادات

        private void LoadSettings()
        {
            try
            {
                string currentLogoPath = GetCurrentLogoPath();

                if (!string.IsNullOrEmpty(currentLogoPath) && File.Exists(currentLogoPath))
                {
                    txtLogoPath.Text = currentLogoPath;
                    LoadImagePreview(currentLogoPath);
                }
                else
                {
                    PreviewImage.Source = null;
                }

                LoadCompanySettings();
                LoadCurrencySettings();
                LoadBackupSettings();
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في تحميل الإعدادات: {ex.Message}", false);
            }
        }

        private string GetCurrentLogoPath()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                if (File.Exists(databasePath))
                {
                    using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                    {
                        connection.Open();

                        string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='CompanySettings'";

                        using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                        {
                            object result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                string selectQuery = "SELECT LogoPath FROM CompanySettings LIMIT 1";

                                using (var selectCmd = new SQLiteCommand(selectQuery, connection))
                                {
                                    object logoPathObj = selectCmd.ExecuteScalar();

                                    if (logoPathObj != null)
                                    {
                                        return logoPathObj.ToString();
                                    }
                                }
                            }
                        }
                    }
                }

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Rasid.png");
            }
            catch
            {
                return "";
            }
        }

        private void LoadCompanySettings()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                if (File.Exists(databasePath))
                {
                    using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                    {
                        connection.Open();

                        string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='CompanySettings'";

                        using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                        {
                            object result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                string selectQuery = "SELECT CompanyName, Email, Phone, Address, TaxNumber FROM CompanySettings LIMIT 1";

                                using (var selectCmd = new SQLiteCommand(selectQuery, connection))
                                {
                                    using (var reader = selectCmd.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            txtCompanyName.Text = reader["CompanyName"]?.ToString() ?? "";
                                            txtEmail.Text = reader["Email"]?.ToString() ?? "";
                                            txtPhone.Text = reader["Phone"]?.ToString() ?? "";
                                            txtAddress.Text = reader["Address"]?.ToString() ?? "";
                                            txtTaxNumber.Text = reader["TaxNumber"]?.ToString() ?? "";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading company settings: {ex.Message}");
            }
        }

        private void LoadCurrencySettings()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                Helpers.CurrencyHelper.ConnectionString = _dbService.GetConnectionString();
                Helpers.CurrencyHelper.LoadCurrencySettings();

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string sql = "SELECT SettingKey, SettingValue FROM SystemSettings WHERE SettingKey LIKE 'Currency%' OR SettingKey = 'DecimalPlaces'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            string value = reader.GetString(1);

                            switch (key)
                            {
                                case "CurrencySymbol":
                                    txtCurrencySymbol.Text = value;
                                    break;

                                case "CurrencyCode":
                                    txtCurrencyCode.Text = value;
                                    break;

                                case "CurrencyNameAr":
                                    txtCurrencyNameAr.Text = value;
                                    break;

                                case "CurrencyNameEn":
                                    txtCurrencyNameEn.Text = value;
                                    break;

                                case "DecimalPlaces":
                                    for (int i = 0; i < cmbDecimalPlaces.Items.Count; i++)
                                    {
                                        var item = cmbDecimalPlaces.Items[i] as ComboBoxItem;

                                        if (item != null && item.Tag.ToString() == value)
                                        {
                                            cmbDecimalPlaces.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading currency settings: {ex.Message}");
            }
        }

        private void LoadBackupSettings()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='BackupSettings'";

                    using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                    {
                        object result = checkCmd.ExecuteScalar();

                        if (result == null)
                        {
                            return;
                        }
                    }

                    string sql = "SELECT SettingKey, SettingValue FROM BackupSettings";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            string value = reader.GetString(1);

                            switch (key)
                            {
                                case "AutoBackupEnabled":
                                    chkAutoBackup.IsChecked = value == "True";
                                    break;

                                case "BackupPath":
                                    txtBackupPath.Text = value;
                                    break;

                                case "BackupFrequency":
                                    for (int i = 0; i < cmbBackupFrequency.Items.Count; i++)
                                    {
                                        var item = cmbBackupFrequency.Items[i] as ComboBoxItem;

                                        if (item != null && item.Tag.ToString() == value)
                                        {
                                            cmbBackupFrequency.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                    break;

                                case "BackupRetention":
                                    txtBackupRetention.Text = value;
                                    break;
                            }
                        }
                    }
                }

                bool isEnabled = chkAutoBackup.IsChecked == true;

                txtBackupPath.IsEnabled = isEnabled;
                btnBrowseBackupPath.IsEnabled = isEnabled;
                cmbBackupFrequency.IsEnabled = isEnabled;
                txtBackupRetention.IsEnabled = isEnabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading backup settings: {ex.Message}");
            }
        }

        private void LoadImagePreview(string imagePath)
        {
            try
            {
                if (File.Exists(imagePath))
                {
                    BitmapImage bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    PreviewImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image preview: {ex.Message}");
            }
        }

        #endregion

        #region دوال حفظ الإعدادات

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCompanySettings();
                SaveCurrencySettings();
                SaveBackupSettings();

                ShowStatus("تم حفظ جميع الإعدادات بنجاح", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في حفظ الإعدادات: {ex.Message}", false);
            }
        }

        private void SaveCompanySettings()
        {
            string databasePath = _dbService.DatabasePath;

            using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
            {
                connection.Open();

                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS CompanySettings (
                        SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                        LogoPath TEXT,
                        CompanyName TEXT,
                        Email TEXT,
                        Phone TEXT,
                        Address TEXT,
                        TaxNumber TEXT,
                        UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                using (var createCmd = new SQLiteCommand(createTableQuery, connection))
                {
                    createCmd.ExecuteNonQuery();
                }

                string upsertQuery = @"
                    INSERT OR REPLACE INTO CompanySettings (SettingID, LogoPath, CompanyName, Email, Phone, Address, TaxNumber, UpdatedDate)
                    VALUES (1, @LogoPath, @CompanyName, @Email, @Phone, @Address, @TaxNumber, CURRENT_TIMESTAMP)";

                using (var upsertCmd = new SQLiteCommand(upsertQuery, connection))
                {
                    upsertCmd.Parameters.AddWithValue("@LogoPath", txtLogoPath.Text ?? "");
                    upsertCmd.Parameters.AddWithValue("@CompanyName", txtCompanyName.Text ?? "");
                    upsertCmd.Parameters.AddWithValue("@Email", txtEmail.Text ?? "");
                    upsertCmd.Parameters.AddWithValue("@Phone", txtPhone.Text ?? "");
                    upsertCmd.Parameters.AddWithValue("@Address", txtAddress.Text ?? "");
                    upsertCmd.Parameters.AddWithValue("@TaxNumber", txtTaxNumber.Text ?? "");
                    upsertCmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveCurrencySettings()
        {
            try
            {
                string symbol = txtCurrencySymbol.Text.Trim();
                string code = txtCurrencyCode.Text.Trim().ToUpper();
                string nameAr = txtCurrencyNameAr.Text.Trim();
                string nameEn = txtCurrencyNameEn.Text.Trim();
                int decimalPlaces = 2;

                if (cmbDecimalPlaces.SelectedItem is ComboBoxItem selected)
                {
                    int.TryParse(selected.Tag.ToString(), out decimalPlaces);
                }

                if (string.IsNullOrEmpty(symbol))
                {
                    symbol = "ر.س";
                }

                if (string.IsNullOrEmpty(code))
                {
                    code = "SAR";
                }

                if (string.IsNullOrEmpty(nameAr))
                {
                    nameAr = "ريال سعودي";
                }

                if (string.IsNullOrEmpty(nameEn))
                {
                    nameEn = "Saudi Riyal";
                }

                Helpers.CurrencyHelper.ConnectionString = _dbService.GetConnectionString();

                bool success = Helpers.CurrencyHelper.SaveCurrencySettings(symbol, code, nameAr, nameEn, decimalPlaces);

                if (!success)
                {
                    ShowStatus("حدث خطأ في حفظ إعدادات العملة", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في حفظ إعدادات العملة: {ex.Message}", false);
            }
        }

        private void SaveBackupSettings()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS BackupSettings (
                            SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                            SettingKey TEXT NOT NULL,
                            SettingValue TEXT NOT NULL,
                            UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";

                    using (var createCmd = new SQLiteCommand(createTableQuery, connection))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    var settings = new Dictionary<string, string>
                    {
                        { "AutoBackupEnabled", (chkAutoBackup.IsChecked == true).ToString() },
                        { "BackupPath", txtBackupPath.Text ?? "" },
                        { "BackupFrequency", (cmbBackupFrequency.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OnSave" },
                        { "BackupRetention", txtBackupRetention.Text ?? "10" }
                    };

                    foreach (var setting in settings)
                    {
                        string upsertQuery = @"
                            INSERT OR REPLACE INTO BackupSettings (SettingKey, SettingValue, UpdatedDate)
                            VALUES (@Key, @Value, CURRENT_TIMESTAMP)";

                        using (var upsertCmd = new SQLiteCommand(upsertQuery, connection))
                        {
                            upsertCmd.Parameters.AddWithValue("@Key", setting.Key);
                            upsertCmd.Parameters.AddWithValue("@Value", setting.Value);
                            upsertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في حفظ إعدادات النسخ الاحتياطي: {ex.Message}", false);
            }
        }

        #endregion

        #region دوال إدارة الشعار

        private void BtnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "اختر صورة الشعار";
            openFileDialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                txtLogoPath.Text = openFileDialog.FileName;
                LoadImagePreview(openFileDialog.FileName);
            }
        }

        private void BtnUploadLogo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtLogoPath.Text) || !File.Exists(txtLogoPath.Text))
                {
                    ShowStatus("الرجاء اختيار صورة أولاً", false);
                    return;
                }

                string assetsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");

                if (!Directory.Exists(assetsFolder))
                {
                    Directory.CreateDirectory(assetsFolder);
                }

                string fileName = "Rasid.png";
                string destinationPath = Path.Combine(assetsFolder, fileName);

                File.Copy(txtLogoPath.Text, destinationPath, true);

                SaveLogoPathToDatabase(destinationPath);

                _mainWindow?.UpdateCompanyLogo(destinationPath);

                ShowStatus("تم رفع الشعار بنجاح", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في رفع الشعار: {ex.Message}", false);
            }
        }

        private void BtnResetLogo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string defaultLogoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Rasid.png");

                if (File.Exists(defaultLogoPath))
                {
                    SaveLogoPathToDatabase(defaultLogoPath);

                    _mainWindow?.UpdateCompanyLogo(defaultLogoPath);

                    txtLogoPath.Text = defaultLogoPath;
                    LoadImagePreview(defaultLogoPath);

                    ShowStatus("تم استعادة الشعار الافتراضي", true);
                }
                else
                {
                    ShowStatus("الملف الافتراضي غير موجود", false);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في استعادة الشعار: {ex.Message}", false);
            }
        }

        private void SaveLogoPathToDatabase(string logoPath)
        {
            try
            {
                string databasePath = _dbService.DatabasePath;

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS CompanySettings (
                            SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                            LogoPath TEXT,
                            CompanyName TEXT,
                            Email TEXT,
                            Phone TEXT,
                            Address TEXT,
                            TaxNumber TEXT,
                            UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";

                    using (var createCmd = new SQLiteCommand(createTableQuery, connection))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    string upsertQuery = @"
                        INSERT OR REPLACE INTO CompanySettings (SettingID, LogoPath, UpdatedDate)
                        VALUES (1, @LogoPath, CURRENT_TIMESTAMP)";

                    using (var upsertCmd = new SQLiteCommand(upsertQuery, connection))
                    {
                        upsertCmd.Parameters.AddWithValue("@LogoPath", logoPath);
                        upsertCmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving logo path: {ex.Message}");
            }
        }

        #endregion

        #region دوال إدارة النسخ الاحتياطي اليدوي

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Title = "حفظ النسخة الاحتياطية";
                saveFileDialog.Filter = "SQLite files (*.sqlite)|*.sqlite|All files (*.*)|*.*";
                saveFileDialog.FileName = $"RasidBackup_{DateTime.Now:yyyyMMdd_HHmmss}.sqlite";

                if (saveFileDialog.ShowDialog() == true)
                {
                    btnBackup.IsEnabled = false;

                    var result = await _backupService.CreateBackupToExactPathAsync(saveFileDialog.FileName);

                    btnBackup.IsEnabled = true;
                    ShowStatus(result.Message, result.Success);
                }
            }
            catch (Exception ex)
            {
                btnBackup.IsEnabled = true;
                Logger.LogError("فشل إنشاء نسخة احتياطية يدوية من شاشة الإعدادات", ex, "SettingsView");
                ShowStatus($"خطأ في إنشاء النسخة الاحتياطية: {ex.Message}", false);
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Title = "اختر ملف النسخة الاحتياطية";
                openFileDialog.Filter = "SQLite files (*.sqlite)|*.sqlite|All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == true)
                {
                    MessageBoxResult result = MessageBox.Show(
                        "تحذير: استعادة النسخة الاحتياطية ستحل محل البيانات الحالية.\nهل أنت متأكد؟",
                        "تأكيد الاستعادة",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        string destinationPath = _dbService.DatabasePath;

                        File.Copy(openFileDialog.FileName, destinationPath, true);
                        ShowStatus("تم استعادة النسخة الاحتياطية بنجاح، الرجاء إعادة تشغيل البرنامج", true);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في استعادة النسخة: {ex.Message}", false);
            }
        }

        #endregion

        #region دوال إدارة النسخ الاحتياطي التلقائي

        private void ChkAutoBackup_Checked(object sender, RoutedEventArgs e)
        {
            bool isEnabled = true;

            txtBackupPath.IsEnabled = isEnabled;
            btnBrowseBackupPath.IsEnabled = isEnabled;
            cmbBackupFrequency.IsEnabled = isEnabled;
            txtBackupRetention.IsEnabled = isEnabled;
        }

        private void ChkAutoBackup_Unchecked(object sender, RoutedEventArgs e)
        {
            bool isEnabled = false;

            txtBackupPath.IsEnabled = isEnabled;
            btnBrowseBackupPath.IsEnabled = isEnabled;
            cmbBackupFrequency.IsEnabled = isEnabled;
            txtBackupRetention.IsEnabled = isEnabled;
        }

        private void BtnBrowseBackupPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // استخدام System.Windows.Forms.FolderBrowserDialog
                // يجب إضافة مرجع System.Windows.Forms
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "اختر مجلد حفظ النسخ الاحتياطية";
                    dialog.ShowNewFolderButton = true;

                    if (!string.IsNullOrEmpty(txtBackupPath.Text))
                    {
                        dialog.SelectedPath = txtBackupPath.Text;
                    }

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtBackupPath.Text = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في اختيار المجلد: {ex.Message}", false);
            }
        }

        private void BtnSaveBackupSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (chkAutoBackup.IsChecked == true)
                {
                    if (string.IsNullOrEmpty(txtBackupPath.Text))
                    {
                        ShowStatus("الرجاء تحديد مسار لحفظ النسخ الاحتياطية", false);
                        return;
                    }

                    if (!Directory.Exists(txtBackupPath.Text))
                    {
                        try
                        {
                            Directory.CreateDirectory(txtBackupPath.Text);
                        }
                        catch
                        {
                            ShowStatus("المسار المحدد غير صحيح أو غير قابل للكتابة", false);
                            return;
                        }
                    }

                    if (!int.TryParse(txtBackupRetention.Text, out int retention) || retention < 1)
                    {
                        ShowStatus("عدد النسخ الاحتياطية يجب أن يكون رقماً أكبر من 0", false);
                        return;
                    }
                }

                SaveBackupSettings();
                ShowStatus("تم حفظ إعدادات النسخ الاحتياطي التلقائي بنجاح", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في حفظ إعدادات النسخ الاحتياطي: {ex.Message}", false);
            }
        }

        #endregion

        #region دوال تغيير كلمة المرور

        private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPassword = txtCurrentPassword.Password;
            string newPassword = txtNewPassword.Password;
            string confirmPassword = txtConfirmPassword.Password;

            if (string.IsNullOrEmpty(currentPassword))
            {
                ShowStatus("الرجاء إدخال كلمة المرور الحالية", false);
                return;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                ShowStatus("الرجاء إدخال كلمة المرور الجديدة", false);
                return;
            }

            if (newPassword.Length < 6)
            {
                ShowStatus("كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل", false);
                return;
            }

            if (newPassword != confirmPassword)
            {
                ShowStatus("كلمتا المرور غير متطابقتين", false);
                return;
            }

            try
            {
                var user = _dbService.ValidateUserAsync(LoginView.CurrentUsername, currentPassword).Result;

                if (user == null)
                {
                    ShowStatus("كلمة المرور الحالية غير صحيحة", false);
                    return;
                }

                string newHash = PasswordHelper.HashPassword(newPassword);
                string databasePath = _dbService.DatabasePath;

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string updateQuery = "UPDATE Users SET PasswordHash = @hash WHERE UserID = @userId";

                    using (var updateCmd = new SQLiteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@hash", newHash);
                        updateCmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId);
                        updateCmd.ExecuteNonQuery();
                    }
                }

                txtCurrentPassword.Clear();
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();

                ShowStatus("تم تغيير كلمة المرور بنجاح", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"خطأ في تغيير كلمة المرور: {ex.Message}", false);
            }
        }

        // ملاحظة: تم نقل منطق تشفير كلمات المرور إلى RasidAccountingSystem.Helpers.PasswordHelper (معيار PBKDF2 مع Salt)

        #endregion

        #region دوال الإلغاء

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            ShowStatus("تم إلغاء التغييرات", true);
        }

        #endregion

        #region دوال عرض الحالة

        private void ShowStatus(string message, bool isSuccess)
        {
            lblStatus.Text = message;

            if (isSuccess)
            {
                lblStatus.Foreground = (Brush)FindResource("SuccessColor");
            }
            else
            {
                lblStatus.Foreground = (Brush)FindResource("DangerButtonColor");
            }

            lblStatus.Visibility = Visibility.Visible;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, args) =>
            {
                lblStatus.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        #endregion
    }
}