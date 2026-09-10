using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace RasidAccountingSystem.Helpers
{
    public static class CurrencyHelper
    {
        private static string _connectionString;
        private static string _currentCurrencySymbol = "ر.س";
        private static string _currentCurrencyCode = "SAR";
        private static string _currentCurrencyNameAr = "ريال سعودي";
        private static string _currentCurrencyNameEn = "Saudi Riyal";
        private static int _decimalPlaces = 2;
        private static bool _isInitialized = false;

        public static string ConnectionString
        {
            set => _connectionString = value;
        }

        public static string CurrentCurrencySymbol => _currentCurrencySymbol;
        public static string CurrentCurrencyCode => _currentCurrencyCode;
        public static string CurrentCurrencyNameAr => _currentCurrencyNameAr;
        public static string CurrentCurrencyNameEn => _currentCurrencyNameEn;
        public static int DecimalPlaces => _decimalPlaces;

        /// <summary>
        /// الحصول على رمز العملة الحالي
        /// </summary>
        public static string GetCurrencySymbol()
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            return _currentCurrencySymbol;
        }

        /// <summary>
        /// الحصول على كود العملة الحالي
        /// </summary>
        public static string GetCurrencyCode()
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            return _currentCurrencyCode;
        }

        /// <summary>
        /// الحصول على اسم العملة بالعربية
        /// </summary>
        public static string GetCurrencyNameAr()
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            return _currentCurrencyNameAr;
        }

        /// <summary>
        /// الحصول على اسم العملة بالإنجليزية
        /// </summary>
        public static string GetCurrencyNameEn()
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            return _currentCurrencyNameEn;
        }

        /// <summary>
        /// الحصول على عدد الأرقام العشرية
        /// </summary>
        public static int GetDecimalPlaces()
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            return _decimalPlaces;
        }

        /// <summary>
        /// تحميل إعدادات العملة من قاعدة البيانات
        /// </summary>
        public static void LoadCurrencySettings()
        {
            if (string.IsNullOrEmpty(_connectionString))
                return;

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    // التحقق من وجود جدول SystemSettings
                    string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='SystemSettings'";
                    using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                    {
                        object result = checkCmd.ExecuteScalar();
                        if (result == null)
                        {
                            // إنشاء الجدول إذا لم يكن موجوداً
                            string createTableQuery = @"
                                CREATE TABLE IF NOT EXISTS SystemSettings (
                                    SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                                    SettingKey TEXT NOT NULL UNIQUE,
                                    SettingValue TEXT NOT NULL,
                                    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                                )";
                            using (var createCmd = new SQLiteCommand(createTableQuery, connection))
                            {
                                createCmd.ExecuteNonQuery();
                            }

                            // إدراج القيم الافتراضية
                            var defaultSettings = new Dictionary<string, string>
                            {
                                { "CurrencySymbol", "ر.س" },
                                { "CurrencyCode", "SAR" },
                                { "CurrencyNameAr", "ريال سعودي" },
                                { "CurrencyNameEn", "Saudi Riyal" },
                                { "DecimalPlaces", "2" }
                            };

                            string insertSql = "INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue) VALUES (@key, @value)";
                            foreach (var setting in defaultSettings)
                            {
                                using (var insertCmd = new SQLiteCommand(insertSql, connection))
                                {
                                    insertCmd.Parameters.AddWithValue("@key", setting.Key);
                                    insertCmd.Parameters.AddWithValue("@value", setting.Value);
                                    insertCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

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
                                    _currentCurrencySymbol = value;
                                    break;
                                case "CurrencyCode":
                                    _currentCurrencyCode = value;
                                    break;
                                case "CurrencyNameAr":
                                    _currentCurrencyNameAr = value;
                                    break;
                                case "CurrencyNameEn":
                                    _currentCurrencyNameEn = value;
                                    break;
                                case "DecimalPlaces":
                                    int.TryParse(value, out _decimalPlaces);
                                    break;
                            }
                        }
                    }
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading currency settings: {ex.Message}");
            }
        }

        /// <summary>
        /// حفظ إعدادات العملة إلى قاعدة البيانات
        /// </summary>
        public static bool SaveCurrencySettings(string symbol, string code, string nameAr, string nameEn, int decimalPlaces)
        {
            if (string.IsNullOrEmpty(_connectionString))
                return false;

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    // التأكد من وجود الجدول
                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS SystemSettings (
                            SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                            SettingKey TEXT NOT NULL UNIQUE,
                            SettingValue TEXT NOT NULL,
                            UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";
                    using (var createCmd = new SQLiteCommand(createTableQuery, connection))
                    {
                        createCmd.ExecuteNonQuery();
                    }

                    string upsertSql = @"
                        INSERT OR REPLACE INTO SystemSettings (SettingKey, SettingValue, UpdatedDate)
                        VALUES (@key, @value, CURRENT_TIMESTAMP)";

                    var settings = new Dictionary<string, string>
                    {
                        { "CurrencySymbol", symbol },
                        { "CurrencyCode", code },
                        { "CurrencyNameAr", nameAr },
                        { "CurrencyNameEn", nameEn },
                        { "DecimalPlaces", decimalPlaces.ToString() }
                    };

                    foreach (var setting in settings)
                    {
                        using (var cmd = new SQLiteCommand(upsertSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@key", setting.Key);
                            cmd.Parameters.AddWithValue("@value", setting.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // إعادة تحميل الإعدادات بعد الحفظ
                LoadCurrencySettings();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving currency settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تنسيق مبلغ مع العملة
        /// </summary>
        public static string FormatAmount(decimal amount)
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            string format = $"N{_decimalPlaces}";
            return $"{amount.ToString(format)} {_currentCurrencySymbol}";
        }

        /// <summary>
        /// تنسيق مبلغ بدون العملة
        /// </summary>
        public static string FormatAmountOnly(decimal amount)
        {
            if (!_isInitialized && !string.IsNullOrEmpty(_connectionString))
            {
                LoadCurrencySettings();
            }
            string format = $"N{_decimalPlaces}";
            return amount.ToString(format);
        }
    }
}