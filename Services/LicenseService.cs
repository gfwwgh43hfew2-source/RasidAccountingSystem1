using System;
using System.Data.SQLite;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace RasidAccountingSystem.Services
{
    public class LicenseService
    {
        private string _databasePath;
        private string _connectionString;
        private const int GRACE_PERIOD_DAYS = 30;

        // ==================== الكونستركتور الافتراضي (بدون معاملات) ====================
        public LicenseService()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RasidERP");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _databasePath = Path.Combine(appDataPath, "RasidERP.db");
            _connectionString = $"Data Source={_databasePath};Version=3;FailIfMissing=False;Journal Mode=WAL;Pooling=True;Max Pool Size=100;";
        }

        // ==================== الكونستركتور الأصلي (للتوافق مع الكود القديم) ====================
        public LicenseService(string databasePath)
        {
            _databasePath = databasePath;
            _connectionString = $"Data Source={databasePath};Version=3;FailIfMissing=False;Journal Mode=WAL;Pooling=True;Max Pool Size=100;";
        }

        // ==================== خاصية للوصول إلى مسار قاعدة البيانات ====================
        public string DatabasePath
        {
            get { return _databasePath; }
        }

        public bool IsLicenseValid()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT LicenseKey, ExpiryDateEncrypted, IsActive 
                        FROM SystemLicense 
                        WHERE IsActive = 1 
                        ORDER BY LicenseID DESC LIMIT 1";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string licenseKey = reader.GetString(0);
                            string expiryDateEncrypted = reader.GetString(1);
                            int isActive = reader.GetInt32(2);

                            if (isActive == 0)
                                return false;

                            string expiryDateStr = Decrypt(expiryDateEncrypted);
                            if (DateTime.TryParse(expiryDateStr, out DateTime expiryDate))
                            {
                                return expiryDate > DateTime.Now;
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool IsInGracePeriod()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        SELECT InstallationDate, GracePeriodEndDate, IsUsed 
                        FROM GracePeriod 
                        WHERE IsUsed = 0 
                        ORDER BY GracePeriodID DESC LIMIT 1";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string installationDateStr = reader.GetString(0);
                            string gracePeriodEndDateStr = reader.GetString(1);
                            int isUsed = reader.GetInt32(2);

                            if (isUsed == 1)
                                return false;

                            if (DateTime.TryParse(gracePeriodEndDateStr, out DateTime gracePeriodEndDate))
                            {
                                return gracePeriodEndDate > DateTime.Now;
                            }
                        }
                    }

                    // إذا لم تكن هناك فترة تجربة، قم بإنشائها
                    if (!HasGracePeriodRecord())
                    {
                        CreateGracePeriod();
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool HasGracePeriodRecord()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT COUNT(*) FROM GracePeriod";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        private void CreateGracePeriod()
        {
            DateTime installationDate = DateTime.Now;
            DateTime gracePeriodEndDate = installationDate.AddDays(GRACE_PERIOD_DAYS);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO GracePeriod (InstallationDate, GracePeriodEndDate, IsUsed)
                    VALUES (@installDate, @endDate, 0)";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@installDate", installationDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@endDate", gracePeriodEndDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool ActivateLicense(string licenseKey)
        {
            try
            {
                if (!ValidateLicenseKey(licenseKey))
                    return false;

                string machineHash = GetMachineHash();
                DateTime expiryDate = GetExpiryDateFromLicenseKey(licenseKey);
                string encryptedExpiryDate = Encrypt(expiryDate.ToString("yyyy-MM-dd HH:mm:ss"));

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    string sql = @"
                        INSERT INTO SystemLicense (LicenseKey, LicenseType, ActivationDateEncrypted, ExpiryDateEncrypted, IsActive, MachineHash)
                        VALUES (@key, 'Permanent', @activationDate, @expiryDate, 1, @machineHash)";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@key", licenseKey);
                        cmd.Parameters.AddWithValue("@activationDate", Encrypt(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                        cmd.Parameters.AddWithValue("@expiryDate", encryptedExpiryDate);
                        cmd.Parameters.AddWithValue("@machineHash", machineHash);
                        cmd.ExecuteNonQuery();
                    }

                    // تعطيل فترة التجربة
                    string updateGracePeriod = "UPDATE GracePeriod SET IsUsed = 1";
                    using (var cmd = new SQLiteCommand(updateGracePeriod, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateLicenseKey(string licenseKey)
        {
            // التحقق من صحة مفتاح الترخيص
            // هذا مثال بسيط - يمكنك استخدام خوارزمية أكثر تعقيداً
            if (string.IsNullOrEmpty(licenseKey) || licenseKey.Length < 16)
                return false;

            // مثال: يجب أن يبدأ المفتاح بـ "RS-" ويتكون من 16 حرفاً أو أكثر
            return licenseKey.StartsWith("RS-") && licenseKey.Length >= 16;
        }

        private DateTime GetExpiryDateFromLicenseKey(string licenseKey)
        {
            // استخراج تاريخ الانتهاء من مفتاح الترخيص
            // هذا مثال بسيط - يمكنك تخصيصه حسب نظام الترخيص الخاص بك
            return DateTime.Now.AddYears(1);
        }

        private string GetMachineHash()
        {
            try
            {
                string cpuId = GetCpuId();
                string driveId = GetDriveId();
                string combined = cpuId + driveId;

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch
            {
                return "UNKNOWN_HASH";
            }
        }

        private string GetCpuId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["ProcessorId"]?.ToString() ?? "UNKNOWN";
                    }
                }
            }
            catch { }
            return "UNKNOWN_CPU";
        }

        private string GetDriveId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["SerialNumber"]?.ToString() ?? "UNKNOWN";
                    }
                }
            }
            catch { }
            return "UNKNOWN_DRIVE";
        }

        private string Encrypt(string plainText)
        {
            // تشفير بسيط - يمكنك تحسينه حسب الحاجة
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainBytes);
        }

        private string Decrypt(string cipherText)
        {
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                return Encoding.UTF8.GetString(cipherBytes);
            }
            catch
            {
                return "";
            }
        }

        public int GetRemainingGracePeriodDays()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT GracePeriodEndDate FROM GracePeriod WHERE IsUsed = 0 ORDER BY GracePeriodID DESC LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            if (DateTime.TryParse(result.ToString(), out DateTime endDate))
                            {
                                int remainingDays = (endDate - DateTime.Now).Days;
                                return remainingDays > 0 ? remainingDays : 0;
                            }
                        }
                    }
                }
            }
            catch { }

            return GRACE_PERIOD_DAYS;
        }
    }
}