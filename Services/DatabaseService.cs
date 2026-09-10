using RasidAccountingSystem.Views;
using RasidAccountingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace RasidAccountingSystem.Services
{
    // كلاس لتخزين بيانات المستخدم
    public class UserData
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string UserRole { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    // كلاس المنتج
    // كلاس المنتج - معدل لنظام الوحدات الثلاثة
    public class ProductItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private decimal _currentQuantity;
        private long _quantityInBaseUnit;
        private int _reorderLevelInBaseUnit;
        private decimal _price1;
        private decimal _price2;
        private decimal _price3;
        private int _unit1Factor;
        private int _unit2Factor;
        private string _unit1;
        private string _unit2;
        private string _unit3;
        private string _reorderUnit;
        private string _barcode;

        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductNameAr { get; set; }
        public string ProductNameEn { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }

        // ==================== الباركود ====================
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(nameof(Barcode)); }
        }

        // ==================== نظام الوحدات الثلاثة ====================
        public string Unit1
        {
            get => _unit1;
            set { _unit1 = value; OnPropertyChanged(nameof(Unit1)); }
        }

        public string Unit2
        {
            get => _unit2;
            set { _unit2 = value; OnPropertyChanged(nameof(Unit2)); }
        }

        public string Unit3
        {
            get => _unit3;
            set { _unit3 = value; OnPropertyChanged(nameof(Unit3)); }
        }

        public int Unit1Factor
        {
            get => _unit1Factor;
            set
            {
                _unit1Factor = value;
                OnPropertyChanged(nameof(Unit1Factor));
                OnPropertyChanged(nameof(QuantityInUnit1));
                OnPropertyChanged(nameof(ReorderLevelInUnit1));
                OnPropertyChanged(nameof(DisplayUnit1Factor));
            }
        }

        public int Unit2Factor
        {
            get => _unit2Factor;
            set
            {
                _unit2Factor = value;
                OnPropertyChanged(nameof(Unit2Factor));
                OnPropertyChanged(nameof(QuantityInUnit2));
                OnPropertyChanged(nameof(ReorderLevelInUnit2));
                OnPropertyChanged(nameof(DisplayUnit2Factor));
            }
        }

        // ==================== الأسعار حسب الوحدة ====================
        public decimal Price1
        {
            get => _price1;
            set { _price1 = value; OnPropertyChanged(nameof(Price1)); OnPropertyChanged(nameof(DisplayPrice1)); }
        }

        public decimal Price2
        {
            get => _price2;
            set { _price2 = value; OnPropertyChanged(nameof(Price2)); OnPropertyChanged(nameof(DisplayPrice2)); }
        }

        public decimal Price3
        {
            get => _price3;
            set { _price3 = value; OnPropertyChanged(nameof(Price3)); OnPropertyChanged(nameof(DisplayPrice3)); }
        }

        // ==================== الكمية بالوحدة الأساسية ====================
        public long QuantityInBaseUnit
        {
            get => _quantityInBaseUnit;
            set
            {
                _quantityInBaseUnit = value;
                OnPropertyChanged(nameof(QuantityInBaseUnit));
                OnPropertyChanged(nameof(QuantityInUnit1));
                OnPropertyChanged(nameof(QuantityInUnit2));
                OnPropertyChanged(nameof(QuantityInUnit3));
                OnPropertyChanged(nameof(DisplayQuantityInBaseUnit));
                OnPropertyChanged(nameof(DisplayQuantityInUnit1));
                OnPropertyChanged(nameof(DisplayQuantityInUnit2));
                OnPropertyChanged(nameof(DisplayQuantityInUnit3));
            }
        }

        // ==================== الخصائص المحسوبة للكمية ====================
        public decimal QuantityInUnit1 => Unit1Factor > 0 ? (decimal)QuantityInBaseUnit / Unit1Factor : 0;
        public decimal QuantityInUnit2 => Unit2Factor > 0 ? (decimal)QuantityInBaseUnit / Unit2Factor : 0;
        public long QuantityInUnit3 => QuantityInBaseUnit;

        // ==================== حد الطلب ====================
        public int ReorderLevelInBaseUnit
        {
            get => _reorderLevelInBaseUnit;
            set
            {
                _reorderLevelInBaseUnit = value;
                OnPropertyChanged(nameof(ReorderLevelInBaseUnit));
                OnPropertyChanged(nameof(ReorderLevelInUnit1));
                OnPropertyChanged(nameof(ReorderLevelInUnit2));
                OnPropertyChanged(nameof(ReorderLevelInUnit3));
                OnPropertyChanged(nameof(DisplayReorderLevelInBaseUnit));
                OnPropertyChanged(nameof(DisplayReorderLevelInUnit1));
                OnPropertyChanged(nameof(DisplayReorderLevelInUnit2));
                OnPropertyChanged(nameof(DisplayReorderLevelInUnit3));
            }
        }

        public decimal ReorderLevelInUnit1 => Unit1Factor > 0 ? (decimal)ReorderLevelInBaseUnit / Unit1Factor : 0;
        public decimal ReorderLevelInUnit2 => Unit2Factor > 0 ? (decimal)ReorderLevelInBaseUnit / Unit2Factor : 0;
        public int ReorderLevelInUnit3 => ReorderLevelInBaseUnit;

        public string ReorderUnit
        {
            get => _reorderUnit;
            set { _reorderUnit = value; OnPropertyChanged(nameof(ReorderUnit)); OnPropertyChanged(nameof(DisplayReorderUnit)); }
        }

        // ==================== خصائص للتوافق مع الكود القديم ====================
        public string Unit { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal MinimumQuantity { get; set; }
        public decimal MaximumQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsActive { get; set; }

        public decimal CurrentQuantity
        {
            get => _currentQuantity;
            set
            {
                _currentQuantity = value;
                OnPropertyChanged(nameof(CurrentQuantity));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        // ==================== ✅ خصائص مساعدة للعرض (Display Properties) ====================

        // عرض عوامل التحويل
        public string DisplayUnit1Factor => Unit1Factor.ToString("N0");
        public string DisplayUnit2Factor => Unit2Factor.ToString("N0");

        // عرض الأسعار
        public string DisplayPrice1 => Price1.ToString("N2");
        public string DisplayPrice2 => Price2.ToString("N2");
        public string DisplayPrice3 => Price3.ToString("N2");

        // عرض الكميات
        public string DisplayQuantityInBaseUnit => QuantityInBaseUnit.ToString("N0");
        public string DisplayQuantityInUnit1 => QuantityInUnit1.ToString("N2");
        public string DisplayQuantityInUnit2 => QuantityInUnit2.ToString("N2");
        public string DisplayQuantityInUnit3 => QuantityInUnit3.ToString("N0");

        // عرض حد الطلب
        public string DisplayReorderLevelInBaseUnit => ReorderLevelInBaseUnit.ToString("N0");
        public string DisplayReorderLevelInUnit1 => ReorderLevelInUnit1.ToString("N2");
        public string DisplayReorderLevelInUnit2 => ReorderLevelInUnit2.ToString("N2");
        public string DisplayReorderLevelInUnit3 => ReorderLevelInUnit3.ToString("N0");
        public string DisplayReorderUnit => string.IsNullOrEmpty(ReorderUnit) ? Unit3 : ReorderUnit;

        // عرض الوحدات
        public string DisplayUnit1 => string.IsNullOrEmpty(Unit1) ? "-" : Unit1;
        public string DisplayUnit2 => string.IsNullOrEmpty(Unit2) ? "-" : Unit2;
        public string DisplayUnit3 => string.IsNullOrEmpty(Unit3) ? "-" : Unit3;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // كلاس العميل
    public class CustomerItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int CustomerID { get; set; }
        public string CustomerCode { get; set; }
        public string CustomerName { get; set; }
        public string CustomerNameAr { get; set; }
        public string CustomerNameEn { get; set; }
        public int AccountID { get; set; }
        public string AccountName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public int PaymentTerms { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Fax { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Address { get; set; }
        public string TaxNumber { get; set; }
        public string CommercialRegister { get; set; }
        public string ContactPerson { get; set; }
        public string ContactPersonPhone { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // كلاس المورد
    public class SupplierItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int SupplierID { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierName { get; set; }
        public string SupplierNameAr { get; set; }
        public string SupplierNameEn { get; set; }
        public int AccountID { get; set; }
        public string AccountName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public int PaymentTerms { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Fax { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }
        public string Address { get; set; }
        public string TaxNumber { get; set; }
        public string CommercialRegister { get; set; }
        public string ContactPerson { get; set; }
        public string ContactPersonPhone { get; set; }
        public bool IsActive { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // كلاس التصنيف
    public class CategoryItem
    {
        public int CategoryID { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryNameAr { get; set; }
        public string CategoryNameEn { get; set; }
    }

    // كلاس عناصر فاتورة المشتريات


    // كلاس بيانات أساسية للخزينة
    public class TreasuryBasicItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal CurrentBalance { get; set; }
    }

    // كلاس بيانات تحصيل عميل مع بيانات الخزينة المرتبطة
    public class CustomerPaymentWithTreasuryItem
    {
        public int PaymentID { get; set; }
        public string VoucherNumber { get; set; }
        public string PaymentDate { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCode { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
        public string InvoiceNumber { get; set; }
        public int TreasuryID { get; set; }
        public string TreasuryName { get; set; }
    }

    // كلاس بيانات فاتورة عميل
    public class CustomerInvoiceItem
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    // كلاس بيانات سند الصرف مع التفاصيل
    public class PaymentVoucherWithDetails
    {
        public int VoucherID { get; set; }
        public string VoucherNumber { get; set; }
        public string VoucherDate { get; set; }
        public int CustomerID { get; set; }
        public int SupplierID { get; set; }
        public string PartyName { get; set; }
        public string PartyCode { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
        public int TreasuryID { get; set; }
        public string TreasuryName { get; set; }
    }

    // كلاس عناصر فاتورة البيع (Sales Invoice Item) - يجب إضافته


    // ==================== كلاسات العهد (Custody Classes) ====================

    // كلاس بيانات الموظف
    public class EmployeeItem
    {
        public int EmployeeID { get; set; }
        public string EmployeeCode { get; set; }
        public string EmployeeNameAr { get; set; }
        public string EmployeeNameEn { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }

        public string DisplayName => $"{EmployeeCode} - {EmployeeNameAr}";
    }

    // كلاس بيانات العهدة
    public class CustodyItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int CustodyID { get; set; }
        public string CustodyNumber { get; set; }
        public DateTime CustodyDate { get; set; }
        public int EmployeeID { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeCode { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string CustodyType { get; set; }
        public string Status { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
        public DateTime? ActualReturnDate { get; set; }
        public decimal SettledAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Description { get; set; }
        public int TreasuryID { get; set; }
        public string TreasuryName { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }

        public string StatusColor
        {
            get
            {
                switch (Status)
                {
                    case "Active": return "#10B981";
                    case "PartiallySettled": return "#F59E0B";
                    case "Settled": return "#6B7280";
                    case "Cancelled": return "#EF4444";
                    default: return "#6B7280";
                }
            }
        }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case "Active": return "نشطة";
                    case "PartiallySettled": return "مسددة جزئياً";
                    case "Settled": return "مسددة";
                    case "Cancelled": return "ملغية";
                    default: return Status;
                }
            }
        }

        public string FormattedAmount => $"{Amount:N2}";
        public string FormattedRemaining => $"{RemainingAmount:N2}";
        public string FormattedSettled => $"{SettledAmount:N2}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // كلاس بيانات عمليات العهدة
    public class CustodyTransactionItem
    {
        public int TransactionID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string ExpenseType { get; set; }
        public string Description { get; set; }
        public string ReceiptNumber { get; set; }
        public string ReferenceNumber { get; set; }

        public string TransactionTypeText
        {
            get
            {
                switch (TransactionType)
                {
                    case "Issue": return "إنشاء عهدة";
                    case "Return": return "صرف / إرجاع";
                    case "Settlement": return "تسوية";
                    case "Adjustment": return "تعديل";
                    default: return TransactionType;
                }
            }
        }

        public string FormattedDate => TransactionDate.ToString("yyyy-MM-dd HH:mm");
        public string FormattedAmount => $"{Amount:N2}";
    }


    public class DatabaseService : IDisposable
    {
        private string _connectionString;
        private string _databasePath;
        private bool _isDatabaseFixed = false;
        private const int BUSY_TIMEOUT_SECONDS = 120;
        private DatabaseConnectionManager _connectionManager;  // ✅ أضف هذا
        private bool _disposed = false;  // ✅ أضف هذا

        // ==================== الكونستركتور الافتراضي (بدون معاملات) ====================
        // ==================== الكونستركتور الافتراضي (بدون معاملات) ====================
        public DatabaseService()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RasidERP10");

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _databasePath = Path.Combine(appDataPath, "RasidERP10.db");
            _connectionString = $"Data Source={_databasePath};Version=3;FailIfMissing=False;Journal Mode=WAL;Pooling=True;Max Pool Size=200;BusyTimeout={BUSY_TIMEOUT_SECONDS};Synchronous=NORMAL;Cache Size=10000;Page Size=4096";

            // ✅ تهيئة مدير الاتصالات المركزي
            _connectionManager = new DatabaseConnectionManager(_databasePath);
        }

        // ==================== الكونستركتور الأصلي (للتوافق مع الكود القديم) ====================
        public DatabaseService(string databasePath)
        {
            _databasePath = databasePath;
            _connectionString = $"Data Source={databasePath};Version=3;FailIfMissing=False;Journal Mode=WAL;Pooling=True;Max Pool Size=200;BusyTimeout={BUSY_TIMEOUT_SECONDS};Synchronous=NORMAL;Cache Size=10000;Page Size=4096";

            // ✅ تهيئة مدير الاتصالات المركزي
            _connectionManager = new DatabaseConnectionManager(databasePath);
        }

        // ==================== خاصية للوصول إلى مسار قاعدة البيانات ====================
        public string DatabasePath
        {
            get { return _databasePath; }
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        // ==================== إنشاء جميع الجداول ====================

        public void CreateAllDatabaseTables()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                CreateLicenseTables(connection);
                CreateGracePeriodTable(connection);
                CreateUsersAndSecurityTables(connection);
                CreateChartOfAccountsTables(connection);
                CreateTreasuryAndBankingTables(connection);
                CreateVoucherTables(connection);
                CreateCheckManagementTables(connection);
                CreateCustomerSupplierTables(connection);
                CreateInventoryTables(connection);

                // ==================== جداول الأرصدة الافتتاحية ====================
                CreateOpeningBalanceTables(connection);
                // ==================== نهاية جداول الأرصدة الافتتاحية ====================

                CreateInvoiceTables(connection);
                CreateBalanceAndStatementTables(connection);
                CreateAccountingEntryTables(connection);
                CreateFinancialPeriodTables(connection);
                CreateSystemSettingsTable(connection);
                CreateAllIndexes(connection);
                CreateAllTriggers(connection);
                CreateAuditTrail(connection);
                UpdateDatabaseSchema(connection);
                UpdateChecksTableSchema(connection);
                InitializeBasicData(connection);
                BackfillMissingInvoiceTransactions(connection);
                ValidateAndRepairAllBalances(connection);

                FixJournalEntryDetailsStructure(connection);

                // أضف هذه الأسطر بعد FixJournalEntryDetailsStructure
                CreateCustodyTables(connection);
                InitializeCustodyBasicData(connection);
                CreateCustodyTriggers(connection);

                // 🔴 أضف هذا السطر هنا 🔴
                CreateInstallmentTables(connection);

                // ✅ ✅ ✅ أضف هذا السطر الجديد ✅ ✅ ✅
                CreateInventoryAdjustmentTables(connection);

                System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جميع جداول قاعدة البيانات بنجاح");
            }
        }

        /// <summary>
        /// إصلاح هيكل جدول JournalEntryDetails - إزالة قيد NOT NULL من عمود AccountID
        /// </summary>
        private void FixJournalEntryDetailsStructure(SQLiteConnection connection)
        {
            try
            {
                string pragmaSql = "PRAGMA table_info(JournalEntryDetails)";
                bool hasNotNullConstraint = false;
                bool tableExists = false;

                using (var cmd = new SQLiteCommand(pragmaSql, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "AccountID")
                        {
                            tableExists = true;
                            int notNull = Convert.ToInt32(reader["notnull"]);
                            if (notNull == 1)
                            {
                                hasNotNullConstraint = true;
                            }
                            break;
                        }
                    }
                }

                if (!tableExists)
                {
                    System.Diagnostics.Debug.WriteLine("جدول JournalEntryDetails غير موجود، سيتم إنشاؤه لاحقاً");
                    return;
                }

                if (hasNotNullConstraint)
                {
                    System.Diagnostics.Debug.WriteLine("إزالة قيد NOT NULL من عمود AccountID في جدول JournalEntryDetails...");

                    string createTempTableSql = @"
                        CREATE TABLE JournalEntryDetails_temp (
                            DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                            EntryID INTEGER NOT NULL,
                            AccountID INTEGER,
                            DebitAmount DECIMAL(18,2) DEFAULT 0,
                            CreditAmount DECIMAL(18,2) DEFAULT 0,
                            Description TEXT,
                            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                            FOREIGN KEY(EntryID) REFERENCES JournalEntries(EntryID) ON DELETE CASCADE,
                            FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID)
                        )";
                    using (var cmd = new SQLiteCommand(createTempTableSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    string copyDataSql = @"
                        INSERT INTO JournalEntryDetails_temp (DetailID, EntryID, AccountID, DebitAmount, CreditAmount, Description, CreatedDate)
                        SELECT DetailID, EntryID, AccountID, DebitAmount, CreditAmount, Description, CreatedDate FROM JournalEntryDetails";
                    using (var cmd = new SQLiteCommand(copyDataSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    string dropOldTableSql = "DROP TABLE JournalEntryDetails";
                    using (var cmd = new SQLiteCommand(dropOldTableSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    string renameTableSql = "ALTER TABLE JournalEntryDetails_temp RENAME TO JournalEntryDetails";
                    using (var cmd = new SQLiteCommand(renameTableSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    System.Diagnostics.Debug.WriteLine("تم إزالة قيد NOT NULL من عمود AccountID بنجاح");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إصلاح هيكل JournalEntryDetails: {ex.Message}");
            }
        }

        #region 1. جداول نظام الترخيص

        private void CreateLicenseTables(SQLiteConnection connection)
        {
            string createLicenseTable = @"
                CREATE TABLE IF NOT EXISTS SystemLicense (
                    LicenseID INTEGER PRIMARY KEY AUTOINCREMENT,
                    LicenseKey TEXT UNIQUE NOT NULL,
                    LicenseType TEXT NOT NULL,
                    ActivationDateEncrypted TEXT NOT NULL,
                    ExpiryDateEncrypted TEXT,
                    LastRunDateEncrypted TEXT,
                    IsActive INTEGER DEFAULT 1,
                    ValidationCounter INTEGER DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastModifiedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    MachineHash TEXT,
                    CompanyName TEXT,
                    ContactEmail TEXT,
                    ContactPhone TEXT
                );";

            using (var cmd = new SQLiteCommand(createLicenseTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createAuditLogTable = @"
                CREATE TABLE IF NOT EXISTS LicenseAuditLog (
                    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    LicenseID INTEGER,
                    ActionType TEXT NOT NULL,
                    ActionDetails TEXT,
                    OldValue TEXT,
                    NewValue TEXT,
                    MachineInfo TEXT,
                    IPAddress TEXT,
                    CreatedBy TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (LicenseID) REFERENCES SystemLicense(LicenseID)
                );";

            using (var cmd = new SQLiteCommand(createAuditLogTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateGracePeriodTable(SQLiteConnection connection)
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS GracePeriod (
                    GracePeriodID INTEGER PRIMARY KEY AUTOINCREMENT,
                    InstallationDate DATETIME NOT NULL,
                    FirstRunDate DATETIME,
                    GracePeriodEndDate DATETIME NOT NULL,
                    IsUsed INTEGER DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP
                );";

            using (var cmd = new SQLiteCommand(sql, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 2. جداول المستخدمين والأمان

        private void CreateUsersAndSecurityTables(SQLiteConnection connection)
        {
            string createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    Email TEXT,
                    Phone TEXT,
                    UserRole TEXT NOT NULL,
                    IsActive INTEGER DEFAULT 1,
                    IsLocked INTEGER DEFAULT 0,
                    LockedUntil DATETIME,
                    LoginAttempts INTEGER DEFAULT 0,
                    LastLoginDate DATETIME,
                    LastLoginIP TEXT,
                    LastActivityDate DATETIME,
                    MustChangePassword INTEGER DEFAULT 0,
                    PasswordChangedDate DATETIME,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createUsersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createUserPermissionsTable = @"
                CREATE TABLE IF NOT EXISTS UserPermissions (
                    PermissionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER NOT NULL,
                    PermissionKey TEXT NOT NULL,
                    PermissionValue INTEGER DEFAULT 0,
                    GrantedBy INTEGER,
                    GrantedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    RevokedBy INTEGER,
                    RevokedDate DATETIME,
                    Notes TEXT,
                    FOREIGN KEY(UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    FOREIGN KEY(GrantedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(RevokedBy) REFERENCES Users(UserID),
                    UNIQUE(UserID, PermissionKey)
                );";

            using (var cmd = new SQLiteCommand(createUserPermissionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createUserSessionsTable = @"
                CREATE TABLE IF NOT EXISTS UserSessions (
                    SessionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER NOT NULL,
                    SessionToken TEXT UNIQUE NOT NULL,
                    LoginTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LastActivityTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                    LogoutTime DATETIME,
                    IPAddress TEXT,
                    UserAgent TEXT,
                    IsActive INTEGER DEFAULT 1,
                    FOREIGN KEY(UserID) REFERENCES Users(UserID) ON DELETE CASCADE
                );";

            using (var cmd = new SQLiteCommand(createUserSessionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createAuditLogTable = @"
                CREATE TABLE IF NOT EXISTS AuditLog (
                    LogID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER,
                    ActionType TEXT NOT NULL,
                    TableName TEXT,
                    RecordID INTEGER,
                    OldValue TEXT,
                    NewValue TEXT,
                    IPAddress TEXT,
                    UserAgent TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(UserID) REFERENCES Users(UserID) ON DELETE SET NULL
                );";

            using (var cmd = new SQLiteCommand(createAuditLogTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 3. شجرة الحسابات (Chart of Accounts)

        private void CreateChartOfAccountsTables(SQLiteConnection connection)
        {
            string createAccountCategoriesTable = @"
                CREATE TABLE IF NOT EXISTS AccountCategories (
                    CategoryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryCode TEXT UNIQUE NOT NULL,
                    CategoryNameAr TEXT NOT NULL,
                    CategoryNameEn TEXT,
                    ParentCategoryID INTEGER,
                    CategoryLevel INTEGER DEFAULT 1,
                    CategoryType TEXT CHECK(CategoryType IN ('Assets', 'Liabilities', 'Equity', 'Revenue', 'Expenses')),
                    IsActive INTEGER DEFAULT 1,
                    SortOrder INTEGER DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(ParentCategoryID) REFERENCES AccountCategories(CategoryID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createAccountCategoriesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createChartOfAccountsTable = @"
                CREATE TABLE IF NOT EXISTS ChartOfAccounts (
                    AccountID INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountCode TEXT UNIQUE NOT NULL,
                    AccountNameAr TEXT NOT NULL,
                    AccountNameEn TEXT,
                    CategoryID INTEGER NOT NULL,
                    ParentAccountID INTEGER,
                    AccountLevel INTEGER DEFAULT 1,
                    AccountType TEXT CHECK(AccountType IN ('Main', 'Sub', 'Detail')),
                    IsActive INTEGER DEFAULT 1,
                    IsDebit INTEGER DEFAULT 1,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    CurrentBalance DECIMAL(18,2) DEFAULT 0,
                    SortOrder INTEGER DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(CategoryID) REFERENCES AccountCategories(CategoryID),
                    FOREIGN KEY(ParentAccountID) REFERENCES ChartOfAccounts(AccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createChartOfAccountsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 4. الخزينة والبنوك (Treasury and Banking)

        private void CreateTreasuryAndBankingTables(SQLiteConnection connection)
        {
            string createTreasuryTable = @"
                CREATE TABLE IF NOT EXISTS Treasury (
                    TreasuryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    TreasuryCode TEXT UNIQUE NOT NULL,
                    TreasuryNameAr TEXT NOT NULL,
                    TreasuryNameEn TEXT,
                    AccountID INTEGER,
                    CurrentBalance DECIMAL(18,2) DEFAULT 0,
                    IsActive INTEGER DEFAULT 1,
                    ResponsiblePerson TEXT,
                    Location TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createTreasuryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createBankAccountsTable = @"
                CREATE TABLE IF NOT EXISTS BankAccounts (
                    BankAccountID INTEGER PRIMARY KEY AUTOINCREMENT,
                    BankCode TEXT UNIQUE NOT NULL,
                    BankName TEXT NOT NULL,
                    BankNameAr TEXT NOT NULL,
                    BankNameEn TEXT,
                    AccountNumber TEXT NOT NULL,
                    IBAN TEXT,
                    AccountID INTEGER,
                    BranchName TEXT,
                    BranchCode TEXT,
                    SwiftCode TEXT,
                    CurrentBalance DECIMAL(18,2) DEFAULT 0,
                    Currency TEXT DEFAULT 'SAR',
                    IsActive INTEGER DEFAULT 1,
                    ContactPerson TEXT,
                    Phone TEXT,
                    Email TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createBankAccountsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createTreasuryTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS TreasuryTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    TreasuryID INTEGER NOT NULL,
                    TransactionDate DATE NOT NULL,
                    TransactionType TEXT CHECK(TransactionType IN ('Receipt', 'Payment', 'Transfer')),
                    Amount DECIMAL(18,2) NOT NULL,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(TreasuryID) REFERENCES Treasury(TreasuryID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createTreasuryTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createBankTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS BankTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    BankAccountID INTEGER NOT NULL,
                    TransactionDate DATE NOT NULL,
                    TransactionType TEXT CHECK(TransactionType IN ('Deposit', 'Withdrawal', 'Transfer', 'Check')),
                    Amount DECIMAL(18,2) NOT NULL,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(BankAccountID) REFERENCES BankAccounts(BankAccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createBankTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 5. سندات القبض والصرف (Vouchers)

        private void CreateVoucherTables(SQLiteConnection connection)
        {
            string createReceiptVouchersTable = @"
                CREATE TABLE IF NOT EXISTS ReceiptVouchers (
                    VoucherID INTEGER PRIMARY KEY AUTOINCREMENT,
                    VoucherNumber TEXT UNIQUE NOT NULL,
                    VoucherDate DATE NOT NULL,
                    CustomerID INTEGER,
                    SupplierID INTEGER,
                    TreasuryID INTEGER,
                    BankAccountID INTEGER,
                    Amount DECIMAL(18,2) NOT NULL,
                    PaymentMethod TEXT CHECK(PaymentMethod IN ('Cash', 'Bank', 'Check', 'Transfer')),
                    CheckNumber TEXT,
                    CheckDate DATE,
                    BankName TEXT,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    IsPosted INTEGER DEFAULT 0,
                    PostedDate DATETIME,
                    PostedBy INTEGER,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy INTEGER,
                    ModifiedDate DATETIME,
                    Notes TEXT,
                    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID),
                    FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID),
                    FOREIGN KEY(TreasuryID) REFERENCES Treasury(TreasuryID),
                    FOREIGN KEY(BankAccountID) REFERENCES BankAccounts(BankAccountID),
                    FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createReceiptVouchersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createPaymentVouchersTable = @"
                CREATE TABLE IF NOT EXISTS PaymentVouchers (
                    VoucherID INTEGER PRIMARY KEY AUTOINCREMENT,
                    VoucherNumber TEXT UNIQUE NOT NULL,
                    VoucherDate DATE NOT NULL,
                    CustomerID INTEGER,
                    SupplierID INTEGER,
                    TreasuryID INTEGER,
                    BankAccountID INTEGER,
                    Amount DECIMAL(18,2) NOT NULL,
                    PaymentMethod TEXT CHECK(PaymentMethod IN ('Cash', 'Bank', 'Check', 'Transfer')),
                    CheckNumber TEXT,
                    CheckDate DATE,
                    BankName TEXT,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    IsPosted INTEGER DEFAULT 0,
                    PostedDate DATETIME,
                    PostedBy INTEGER,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy INTEGER,
                    ModifiedDate DATETIME,
                    Notes TEXT,
                    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID),
                    FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID),
                    FOREIGN KEY(TreasuryID) REFERENCES Treasury(TreasuryID),
                    FOREIGN KEY(BankAccountID) REFERENCES BankAccounts(BankAccountID),
                    FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createPaymentVouchersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createVoucherDetailsTable = @"
                CREATE TABLE IF NOT EXISTS VoucherDetails (
                    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                    VoucherID INTEGER NOT NULL,
                    VoucherType TEXT CHECK(VoucherType IN ('Receipt', 'Payment')),
                    AccountID INTEGER NOT NULL,
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    Description TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(VoucherID) REFERENCES ReceiptVouchers(VoucherID),
                    FOREIGN KEY(VoucherID) REFERENCES PaymentVouchers(VoucherID),
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID)
                );";

            using (var cmd = new SQLiteCommand(createVoucherDetailsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 6. الشيكات (Checks)

        private void CreateCheckManagementTables(SQLiteConnection connection)
        {
            string createChecksTable = @"
                CREATE TABLE IF NOT EXISTS Checks (
                    CheckID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CheckNumber TEXT NOT NULL,
                    CheckDate DATE NOT NULL,
                    CheckType TEXT CHECK(CheckType IN ('Receivable', 'Payable')),
                    Amount DECIMAL(18,2) NOT NULL,
                    BankName TEXT NOT NULL,
                    AccountNumber TEXT,
                    PayeeName TEXT,
                    PayerName TEXT,
                    DueDate DATE,
                    Status TEXT CHECK(Status IN ('Issued', 'Received', 'Deposited', 'Cleared', 'Bounced', 'Cancelled')) DEFAULT 'Issued',
                    CustomerID INTEGER,
                    SupplierID INTEGER,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    DepositDate DATETIME,
                    ClearanceDate DATETIME,
                    BounceReason TEXT,
                    IsVoid INTEGER DEFAULT 0,
                    VoidReason TEXT,
                    VoidDate DATETIME,
                    VoidBy INTEGER,
                    Description TEXT,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy INTEGER,
                    ModifiedDate DATETIME,
                    Notes TEXT,
                    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID),
                    FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID),
                    FOREIGN KEY(VoidBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createChecksTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createCheckTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS CheckTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CheckID INTEGER NOT NULL,
                    TransactionDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FromStatus TEXT NOT NULL,
                    ToStatus TEXT NOT NULL,
                    TransactionType TEXT,
                    Description TEXT,
                    PerformedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(CheckID) REFERENCES Checks(CheckID) ON DELETE CASCADE,
                    FOREIGN KEY(PerformedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createCheckTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 7. العملاء والموردين (Customers and Suppliers)

        private void CreateCustomerSupplierTables(SQLiteConnection connection)
        {
            string createCustomersTable = @"
                CREATE TABLE IF NOT EXISTS Customers (
                    CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerCode TEXT UNIQUE NOT NULL,
                    CustomerName TEXT NOT NULL,
                    CustomerNameAr TEXT NOT NULL,
                    CustomerNameEn TEXT,
                    AccountID INTEGER,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    CurrentBalance DECIMAL(18,2) DEFAULT 0,
                    CreditLimit DECIMAL(18,2) DEFAULT 0,
                    PaymentTerms INTEGER DEFAULT 0,
                    Phone TEXT,
                    Mobile TEXT,
                    Fax TEXT,
                    Email TEXT,
                    Website TEXT,
                    Address TEXT,
                    TaxNumber TEXT,
                    CommercialRegister TEXT,
                    ContactPerson TEXT,
                    ContactPersonPhone TEXT,
                    IsActive INTEGER DEFAULT 1,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createCustomersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createSuppliersTable = @"
                CREATE TABLE IF NOT EXISTS Suppliers (
                    SupplierID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierCode TEXT UNIQUE NOT NULL,
                    SupplierName TEXT NOT NULL,
                    SupplierNameAr TEXT NOT NULL,
                    SupplierNameEn TEXT,
                    AccountID INTEGER,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    CurrentBalance DECIMAL(18,2) DEFAULT 0,
                    CreditLimit DECIMAL(18,2) DEFAULT 0,
                    PaymentTerms INTEGER DEFAULT 0,
                    Phone TEXT,
                    Mobile TEXT,
                    Fax TEXT,
                    Email TEXT,
                    Website TEXT,
                    Address TEXT,
                    TaxNumber TEXT,
                    CommercialRegister TEXT,
                    ContactPerson TEXT,
                    ContactPersonPhone TEXT,
                    IsActive INTEGER DEFAULT 1,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedDate DATETIME,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createSuppliersTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createCustomerTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS CustomerTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerID INTEGER NOT NULL,
                    TransactionDate DATE NOT NULL,
                    TransactionType TEXT CHECK(TransactionType IN ('Invoice', 'Receipt', 'Refund', 'Check', 'Adjustment')),
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createCustomerTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createSupplierTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS SupplierTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierID INTEGER NOT NULL,
                    TransactionDate DATE NOT NULL,
                    TransactionType TEXT CHECK(TransactionType IN ('Purchase', 'Payment', 'Refund', 'Check', 'Adjustment')),
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID) ON DELETE CASCADE,
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createSupplierTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 8. المخازن والمخزون (Inventory and Warehouse)

        private void CreateInventoryTables(SQLiteConnection connection)
        {
            // ============================================================
            // 1️⃣ جدول المخازن (Stores)
            // ============================================================
            string createStoresTable = @"
        CREATE TABLE IF NOT EXISTS Stores (
            StoreID INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreCode TEXT UNIQUE NOT NULL,
            StoreNameAr TEXT NOT NULL,
            StoreNameEn TEXT,
            StoreType TEXT CHECK(StoreType IN ('Main', 'Sub', 'Consignment')),
            Location TEXT,
            ResponsiblePerson TEXT,
            Phone TEXT,
            Email TEXT,
            IsActive INTEGER DEFAULT 1,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ModifiedDate DATETIME,
            CreatedBy INTEGER,
            Notes TEXT,
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createStoresTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ============================================================
            // 2️⃣ جدول تصنيفات المنتجات (ProductCategories)
            // ============================================================
            string createProductCategoriesTable = @"
        CREATE TABLE IF NOT EXISTS ProductCategories (
            CategoryID INTEGER PRIMARY KEY AUTOINCREMENT,
            CategoryCode TEXT UNIQUE NOT NULL,
            CategoryNameAr TEXT NOT NULL,
            CategoryNameEn TEXT,
            ParentCategoryID INTEGER,
            IsActive INTEGER DEFAULT 1,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ModifiedDate DATETIME,
            CreatedBy INTEGER,
            FOREIGN KEY(ParentCategoryID) REFERENCES ProductCategories(CategoryID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createProductCategoriesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ============================================================
            // 3️⃣ ✅ جدول المنتجات (Products) - معدل مع نظام الوحدات الثلاثة
            // ============================================================
            string createProductsTable = @"
        CREATE TABLE IF NOT EXISTS Products (
            ProductID INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductCode TEXT UNIQUE NOT NULL,
            ProductNameAr TEXT NOT NULL,
            ProductNameEn TEXT,
            CategoryID INTEGER,
            Unit TEXT,
            
            -- ✅ الأعمدة الجديدة لنظام الوحدات الثلاثة
            Barcode TEXT,
            Unit1 TEXT,
            Unit2 TEXT,
            Unit3 TEXT,
            Unit1Factor INTEGER DEFAULT 1,
            Unit2Factor INTEGER DEFAULT 1,
            Price1 DECIMAL(18,2) DEFAULT 0,
            Price2 DECIMAL(18,2) DEFAULT 0,
            Price3 DECIMAL(18,2) DEFAULT 0,
            ReorderUnit TEXT,
            ReorderLevelInBaseUnit INTEGER DEFAULT 0,
            QuantityInBaseUnit DECIMAL(18,2) DEFAULT 0,
            
            -- الأعمدة القديمة (للتوافق مع الكود الحالي)
            CostPrice DECIMAL(18,2) DEFAULT 0,
            SalePrice DECIMAL(18,2) DEFAULT 0,
            WholesalePrice DECIMAL(18,2) DEFAULT 0,
            MinimumQuantity DECIMAL(18,2) DEFAULT 0,
            MaximumQuantity DECIMAL(18,2) DEFAULT 0,
            ReorderLevel DECIMAL(18,2) DEFAULT 0,
            Weight DECIMAL(18,2) DEFAULT 0,
            
            -- الأعمدة الأساسية
            IsActive INTEGER DEFAULT 1,
            HasExpiry INTEGER DEFAULT 0,
            HasBatch INTEGER DEFAULT 0,
            ImagePath TEXT,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ModifiedDate DATETIME,
            CreatedBy INTEGER,
            Notes TEXT,
            FOREIGN KEY(CategoryID) REFERENCES ProductCategories(CategoryID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createProductsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ============================================================
            // 4️⃣ جدول مخزون المخازن (StoreInventory)
            // ============================================================
            string createStoreInventoryTable = @"
        CREATE TABLE IF NOT EXISTS StoreInventory (
            InventoryID INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreID INTEGER NOT NULL,
            ProductID INTEGER NOT NULL,
            BatchNumber TEXT,
            ExpiryDate DATE,
            Quantity DECIMAL(18,2) DEFAULT 0,
            ReservedQuantity DECIMAL(18,2) DEFAULT 0,
            AvailableQuantity DECIMAL(18,2) DEFAULT 0,
            CostPrice DECIMAL(18,2) DEFAULT 0,
            LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(StoreID) REFERENCES Stores(StoreID) ON DELETE CASCADE,
            FOREIGN KEY(ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE,
            UNIQUE(StoreID, ProductID)
        );";

            using (var cmd = new SQLiteCommand(createStoreInventoryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ============================================================
            // 5️⃣ الفهارس (Indexes)
            // ============================================================
            string createIndexes = @"
        CREATE UNIQUE INDEX IF NOT EXISTS idx_store_product_unique ON StoreInventory(StoreID, ProductID) WHERE BatchNumber IS NULL OR BatchNumber = '';
        CREATE INDEX IF NOT EXISTS idx_store_inventory_store ON StoreInventory(StoreID);
        CREATE INDEX IF NOT EXISTS idx_store_inventory_product ON StoreInventory(ProductID);
        CREATE INDEX IF NOT EXISTS idx_products_code ON Products(ProductCode);
        CREATE INDEX IF NOT EXISTS idx_products_barcode ON Products(Barcode);
        CREATE INDEX IF NOT EXISTS idx_products_name ON Products(ProductNameAr);
        CREATE INDEX IF NOT EXISTS idx_products_category ON Products(CategoryID);
        CREATE INDEX IF NOT EXISTS idx_products_isactive ON Products(IsActive);
    ";

            using (var cmd = new SQLiteCommand(createIndexes, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ============================================================
            // 6️⃣ جدول حركات المخزون (InventoryTransactions)
            // ============================================================
            string createInventoryTransactionsTable = @"
        CREATE TABLE IF NOT EXISTS InventoryTransactions (
            TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
            StoreID INTEGER NOT NULL,
            ProductID INTEGER NOT NULL,
            BatchNumber TEXT,
            TransactionDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            TransactionType TEXT CHECK(TransactionType IN ('Purchase', 'Sale', 'Return', 'Transfer', 'Adjustment', 'Opening')),
            Quantity DECIMAL(18,2) NOT NULL,
            QuantityBefore DECIMAL(18,2) NOT NULL,
            QuantityAfter DECIMAL(18,2) NOT NULL,
            UnitPrice DECIMAL(18,2) DEFAULT 0,
            TotalAmount DECIMAL(18,2) DEFAULT 0,
            ReferenceType TEXT,
            ReferenceID INTEGER,
            ReferenceNumber TEXT,
            Description TEXT,
            CreatedBy INTEGER,
            FOREIGN KEY(StoreID) REFERENCES Stores(StoreID),
            FOREIGN KEY(ProductID) REFERENCES Products(ProductID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createInventoryTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جداول المخازن والمخزون والمنتجات (مع نظام الوحدات الثلاثة)");
        }

        private void CreateOpeningBalanceTables(SQLiteConnection connection)
        {
            // جدول الأرصدة الافتتاحية للمخزون
            string createOpeningInventoryTable = @"
                CREATE TABLE IF NOT EXISTS OpeningInventory (
                    OpeningID INTEGER PRIMARY KEY AUTOINCREMENT,
                    OpeningNumber TEXT UNIQUE NOT NULL,
                    OpeningDate DATE NOT NULL,
                    StoreID INTEGER NOT NULL,
                    Description TEXT,
                    IsPosted INTEGER DEFAULT 0,
                    PostedDate DATETIME,
                    PostedBy INTEGER,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy INTEGER,
                    ModifiedDate DATETIME,
                    FOREIGN KEY(StoreID) REFERENCES Stores(StoreID),
                    FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createOpeningInventoryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // جدول تفاصيل الأرصدة الافتتاحية
            string createOpeningInventoryDetailsTable = @"
                CREATE TABLE IF NOT EXISTS OpeningInventoryDetails (
                    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                    OpeningID INTEGER NOT NULL,
                    ProductID INTEGER NOT NULL,
                    BatchNumber TEXT,
                    ExpiryDate DATE,
                    Quantity DECIMAL(18,2) NOT NULL,
                    CostPrice DECIMAL(18,2) NOT NULL,
                    TotalAmount DECIMAL(18,2) NOT NULL,
                    Notes TEXT,
                    FOREIGN KEY(OpeningID) REFERENCES OpeningInventory(OpeningID) ON DELETE CASCADE,
                    FOREIGN KEY(ProductID) REFERENCES Products(ProductID)
                );";

            using (var cmd = new SQLiteCommand(createOpeningInventoryDetailsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جداول الأرصدة الافتتاحية للمخزون");
        }
        #endregion

        #region 9. الفواتير (Invoices)

        private void CreateInvoiceTables(SQLiteConnection connection)
        {
            string createSalesInvoicesTable = @"
CREATE TABLE IF NOT EXISTS SalesInvoices (
    InvoiceID INTEGER PRIMARY KEY AUTOINCREMENT,
    InvoiceNumber TEXT UNIQUE NOT NULL,
    InvoiceDate DATE NOT NULL,
    CustomerID INTEGER,
    StoreID INTEGER,
    InvoiceType TEXT CHECK(InvoiceType IN ('Sales', 'SalesReturn')),
    SubTotal DECIMAL(18,2) DEFAULT 0,
    DiscountAmount DECIMAL(18,2) DEFAULT 0,
    DiscountPercent DECIMAL(18,2) DEFAULT 0,
    TaxAmount DECIMAL(18,2) DEFAULT 0,
    TaxPercent DECIMAL(18,2) DEFAULT 0,
    TotalAmount DECIMAL(18,2) DEFAULT 0,
    PaidAmount DECIMAL(18,2) DEFAULT 0,
    RemainingAmount DECIMAL(18,2) DEFAULT 0,
    PaymentMethod TEXT CHECK(PaymentMethod IN ('Cash', 'Bank', 'Check', 'Credit', 'Transfer')),
    PaymentStatus TEXT CHECK(PaymentStatus IN ('Paid', 'Partial', 'Pending')) DEFAULT 'Pending',
    CheckNumber TEXT,
    CheckDate DATE,
    BankName TEXT,
    ReferenceNumber TEXT,
    IsPosted INTEGER DEFAULT 0,
    PostedDate DATETIME,
    PostedBy INTEGER,
    CreatedBy INTEGER,
    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    ModifiedBy INTEGER,
    ModifiedDate DATETIME,
    IsVoid INTEGER DEFAULT 0,
    VoidReason TEXT,
    VoidDate DATETIME,
    VoidBy INTEGER,
    Notes TEXT,
    -- 🔴 الأعمدة الجديدة لنظام الأقساط 🔴
    IsInstallment INTEGER DEFAULT 0,
    TotalInstallmentAmount DECIMAL(18,2) DEFAULT 0,
    PaidUpfront DECIMAL(18,2) DEFAULT 0,
    InstallmentCount INTEGER DEFAULT 0,
    InstallmentStatus TEXT DEFAULT 'Active',
    -- 🔴 نهاية الأعمدة الجديدة 🔴
    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID),
    FOREIGN KEY(StoreID) REFERENCES Stores(StoreID),
    FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID),
    FOREIGN KEY(VoidBy) REFERENCES Users(UserID)
);";

            using (var cmd = new SQLiteCommand(createSalesInvoicesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ✅ جدول SalesInvoiceItems - مع إضافة عمود QuantityInBaseUnit
            string createSalesInvoiceItemsTable = @"
        CREATE TABLE IF NOT EXISTS SalesInvoiceItems (
            ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
            InvoiceID INTEGER NOT NULL,
            ProductID INTEGER NOT NULL,
            BatchNumber TEXT,
            Quantity DECIMAL(18,2) NOT NULL,
            QuantityInBaseUnit DECIMAL(18,2) DEFAULT 0,
            UnitPrice DECIMAL(18,2) NOT NULL,
            DiscountPercent DECIMAL(18,2) DEFAULT 0,
            DiscountAmount DECIMAL(18,2) DEFAULT 0,
            TotalAmount DECIMAL(18,2) NOT NULL,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(InvoiceID) REFERENCES SalesInvoices(InvoiceID) ON DELETE CASCADE,
            FOREIGN KEY(ProductID) REFERENCES Products(ProductID)
        );";

            using (var cmd = new SQLiteCommand(createSalesInvoiceItemsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createPurchaseInvoicesTable = @"
        CREATE TABLE IF NOT EXISTS PurchaseInvoices (
            InvoiceID INTEGER PRIMARY KEY AUTOINCREMENT,
            InvoiceNumber TEXT UNIQUE NOT NULL,
            InvoiceDate DATE NOT NULL,
            SupplierID INTEGER,
            StoreID INTEGER,
            InvoiceType TEXT CHECK(InvoiceType IN ('Purchase', 'PurchaseReturn')),
            SubTotal DECIMAL(18,2) DEFAULT 0,
            DiscountAmount DECIMAL(18,2) DEFAULT 0,
            DiscountPercent DECIMAL(18,2) DEFAULT 0,
            TaxAmount DECIMAL(18,2) DEFAULT 0,
            TaxPercent DECIMAL(18,2) DEFAULT 0,
            TotalAmount DECIMAL(18,2) DEFAULT 0,
            PaidAmount DECIMAL(18,2) DEFAULT 0,
            RemainingAmount DECIMAL(18,2) DEFAULT 0,
            PaymentMethod TEXT CHECK(PaymentMethod IN ('Cash', 'Bank', 'Check', 'Credit', 'Transfer')),
            PaymentStatus TEXT CHECK(PaymentStatus IN ('Paid', 'Partial', 'Pending')) DEFAULT 'Pending',
            CheckNumber TEXT,
            CheckDate DATE,
            BankName TEXT,
            ReferenceNumber TEXT,
            IsPosted INTEGER DEFAULT 0,
            PostedDate DATETIME,
            PostedBy INTEGER,
            CreatedBy INTEGER,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ModifiedBy INTEGER,
            ModifiedDate DATETIME,
            IsVoid INTEGER DEFAULT 0,
            VoidReason TEXT,
            VoidDate DATETIME,
            VoidBy INTEGER,
            Notes TEXT,
            FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID),
            FOREIGN KEY(StoreID) REFERENCES Stores(StoreID),
            FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
            FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID),
            FOREIGN KEY(VoidBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createPurchaseInvoicesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ✅ جدول PurchaseInvoiceItems - مع إضافة عمود QuantityInBaseUnit
            string createPurchaseInvoiceItemsTable = @"
        CREATE TABLE IF NOT EXISTS PurchaseInvoiceItems (
            ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
            InvoiceID INTEGER NOT NULL,
            ProductID INTEGER NOT NULL,
            BatchNumber TEXT,
            ExpiryDate DATE,
            Quantity DECIMAL(18,2) NOT NULL,
            QuantityInBaseUnit DECIMAL(18,2) DEFAULT 0,
            UnitPrice DECIMAL(18,2) NOT NULL,
            DiscountPercent DECIMAL(18,2) DEFAULT 0,
            DiscountAmount DECIMAL(18,2) DEFAULT 0,
            TotalAmount DECIMAL(18,2) NOT NULL,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(InvoiceID) REFERENCES PurchaseInvoices(InvoiceID) ON DELETE CASCADE,
            FOREIGN KEY(ProductID) REFERENCES Products(ProductID)
        );";

            using (var cmd = new SQLiteCommand(createPurchaseInvoiceItemsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }
        #endregion

        #region 10. ميزان وكشوف الحسابات (Balance and Statement)

        private void CreateBalanceAndStatementTables(SQLiteConnection connection)
        {
            string createCustomerBalanceSummaryTable = @"
                CREATE TABLE IF NOT EXISTS CustomerBalanceSummary (
                    SummaryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerID INTEGER NOT NULL,
                    BalanceDate DATE NOT NULL,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    ClosingBalance DECIMAL(18,2) DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID) ON DELETE CASCADE,
                    UNIQUE(CustomerID, BalanceDate)
                );";

            using (var cmd = new SQLiteCommand(createCustomerBalanceSummaryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createSupplierBalanceSummaryTable = @"
                CREATE TABLE IF NOT EXISTS SupplierBalanceSummary (
                    SummaryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierID INTEGER NOT NULL,
                    BalanceDate DATE NOT NULL,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    ClosingBalance DECIMAL(18,2) DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(SupplierID) REFERENCES Suppliers(SupplierID) ON DELETE CASCADE,
                    UNIQUE(SupplierID, BalanceDate)
                );";

            using (var cmd = new SQLiteCommand(createSupplierBalanceSummaryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createAccountBalanceSummaryTable = @"
                CREATE TABLE IF NOT EXISTS AccountBalanceSummary (
                    SummaryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountID INTEGER NOT NULL,
                    BalanceDate DATE NOT NULL,
                    OpeningBalance DECIMAL(18,2) DEFAULT 0,
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    ClosingBalance DECIMAL(18,2) DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID) ON DELETE CASCADE,
                    UNIQUE(AccountID, BalanceDate)
                );";

            using (var cmd = new SQLiteCommand(createAccountBalanceSummaryTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createTrialBalanceTable = @"
                CREATE TABLE IF NOT EXISTS TrialBalance (
                    TrialBalanceID INTEGER PRIMARY KEY AUTOINCREMENT,
                    PeriodID INTEGER NOT NULL,
                    AccountID INTEGER NOT NULL,
                    OpeningDebit DECIMAL(18,2) DEFAULT 0,
                    OpeningCredit DECIMAL(18,2) DEFAULT 0,
                    PeriodDebit DECIMAL(18,2) DEFAULT 0,
                    PeriodCredit DECIMAL(18,2) DEFAULT 0,
                    ClosingDebit DECIMAL(18,2) DEFAULT 0,
                    ClosingCredit DECIMAL(18,2) DEFAULT 0,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(PeriodID) REFERENCES FinancialPeriods(PeriodID),
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID),
                    UNIQUE(PeriodID, AccountID)
                );";

            using (var cmd = new SQLiteCommand(createTrialBalanceTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 11. قيود محاسبية (Accounting Entries) - معدلة

        private void CreateAccountingEntryTables(SQLiteConnection connection)
        {
            string createJournalEntriesTable = @"
                CREATE TABLE IF NOT EXISTS JournalEntries (
                    EntryID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntryNumber TEXT UNIQUE NOT NULL,
                    EntryDate DATE NOT NULL,
                    ReferenceType TEXT,
                    ReferenceID INTEGER,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    IsPosted INTEGER DEFAULT 0,
                    PostedDate DATETIME,
                    PostedBy INTEGER,
                    CreatedBy INTEGER,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    ModifiedBy INTEGER,
                    ModifiedDate DATETIME,
                    FOREIGN KEY(PostedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createJournalEntriesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string createJournalEntryDetailsTable = @"
                CREATE TABLE IF NOT EXISTS JournalEntryDetails (
                    DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntryID INTEGER NOT NULL,
                    AccountID INTEGER,
                    DebitAmount DECIMAL(18,2) DEFAULT 0,
                    CreditAmount DECIMAL(18,2) DEFAULT 0,
                    Description TEXT,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(EntryID) REFERENCES JournalEntries(EntryID) ON DELETE CASCADE,
                    FOREIGN KEY(AccountID) REFERENCES ChartOfAccounts(AccountID)
                );";

            using (var cmd = new SQLiteCommand(createJournalEntryDetailsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 12. الفترات المالية (Financial Periods)

        private void CreateFinancialPeriodTables(SQLiteConnection connection)
        {
            string createFinancialPeriodsTable = @"
                CREATE TABLE IF NOT EXISTS FinancialPeriods (
                    PeriodID INTEGER PRIMARY KEY AUTOINCREMENT,
                    PeriodCode TEXT UNIQUE NOT NULL,
                    PeriodNameAr TEXT NOT NULL,
                    PeriodNameEn TEXT,
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    PeriodType TEXT CHECK(PeriodType IN ('Year', 'Quarter', 'Month')),
                    IsClosed INTEGER DEFAULT 0,
                    ClosedDate DATETIME,
                    ClosedBy INTEGER,
                    IsActive INTEGER DEFAULT 1,
                    CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    CreatedBy INTEGER,
                    Notes TEXT,
                    FOREIGN KEY(ClosedBy) REFERENCES Users(UserID),
                    FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
                );";

            using (var cmd = new SQLiteCommand(createFinancialPeriodsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 13. إعدادات النظام (System Settings)

        private void CreateSystemSettingsTable(SQLiteConnection connection)
        {
            string createSettingsTable = @"
                CREATE TABLE IF NOT EXISTS SystemSettings (
                    SettingID INTEGER PRIMARY KEY AUTOINCREMENT,
                    SettingKey TEXT UNIQUE NOT NULL,
                    SettingValue TEXT NOT NULL,
                    Description TEXT,
                    UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedBy INTEGER
                );";

            using (var cmd = new SQLiteCommand(createSettingsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string insertDefaultCurrency = @"
                INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, Description)
                VALUES ('CurrencyCode', 'SAR', 'رمز العملة (SAR, USD, EGP, etc.)');
                
                INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, Description)
                VALUES ('CurrencySymbol', 'ر.س', 'رمز العملة المعروض (ريال سعودي، دولار، جنيه، إلخ)');
                
                INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, Description)
                VALUES ('CurrencyNameAr', 'ريال سعودي', 'اسم العملة بالعربية');
                
                INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, Description)
                VALUES ('CurrencyNameEn', 'Saudi Riyal', 'اسم العملة بالإنجليزية');
                
                INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, Description)
                VALUES ('DecimalPlaces', '2', 'عدد المنازل العشرية');";

            using (var cmd = new SQLiteCommand(insertDefaultCurrency, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 14. الفهارس (Indexes)

        private void CreateAllIndexes(SQLiteConnection connection)
        {
            string createIndexes = @"
                CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);
                CREATE INDEX IF NOT EXISTS idx_users_userrole ON Users(UserRole);
                CREATE INDEX IF NOT EXISTS idx_users_isactive ON Users(IsActive);
                CREATE INDEX IF NOT EXISTS idx_users_lastlogin ON Users(LastLoginDate);
                CREATE INDEX IF NOT EXISTS idx_userpermissions_userid ON UserPermissions(UserID);
                CREATE INDEX IF NOT EXISTS idx_userpermissions_permissionkey ON UserPermissions(PermissionKey);
                CREATE INDEX IF NOT EXISTS idx_usersessions_userid ON UserSessions(UserID);
                CREATE INDEX IF NOT EXISTS idx_usersessions_token ON UserSessions(SessionToken);
                CREATE INDEX IF NOT EXISTS idx_usersessions_isactive ON UserSessions(IsActive);
                CREATE INDEX IF NOT EXISTS idx_auditlog_userid ON AuditLog(UserID);
                CREATE INDEX IF NOT EXISTS idx_auditlog_createddate ON AuditLog(CreatedDate);
                CREATE INDEX IF NOT EXISTS idx_auditlog_actiontype ON AuditLog(ActionType);
                CREATE INDEX IF NOT EXISTS idx_accountcategories_code ON AccountCategories(CategoryCode);
                CREATE INDEX IF NOT EXISTS idx_accountcategories_parent ON AccountCategories(ParentCategoryID);
                CREATE INDEX IF NOT EXISTS idx_chartofaccounts_code ON ChartOfAccounts(AccountCode);
                CREATE INDEX IF NOT EXISTS idx_chartofaccounts_category ON ChartOfAccounts(CategoryID);
                CREATE INDEX IF NOT EXISTS idx_chartofaccounts_parent ON ChartOfAccounts(ParentAccountID);
                CREATE INDEX IF NOT EXISTS idx_chartofaccounts_type ON ChartOfAccounts(AccountType);
                CREATE INDEX IF NOT EXISTS idx_chartofaccounts_isactive ON ChartOfAccounts(IsActive);
                CREATE INDEX IF NOT EXISTS idx_treasury_code ON Treasury(TreasuryCode);
                CREATE INDEX IF NOT EXISTS idx_treasury_isactive ON Treasury(IsActive);
                CREATE INDEX IF NOT EXISTS idx_treasurytransactions_treasury ON TreasuryTransactions(TreasuryID);
                CREATE INDEX IF NOT EXISTS idx_treasurytransactions_date ON TreasuryTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_bankaccounts_code ON BankAccounts(BankCode);
                CREATE INDEX IF NOT EXISTS idx_bankaccounts_accountnumber ON BankAccounts(AccountNumber);
                CREATE INDEX IF NOT EXISTS idx_bankaccounts_isactive ON BankAccounts(IsActive);
                CREATE INDEX IF NOT EXISTS idx_banktransactions_bankaccount ON BankTransactions(BankAccountID);
                CREATE INDEX IF NOT EXISTS idx_banktransactions_date ON BankTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_number ON ReceiptVouchers(VoucherNumber);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_date ON ReceiptVouchers(VoucherDate);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_customer ON ReceiptVouchers(CustomerID);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_supplier ON ReceiptVouchers(SupplierID);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_isposted ON ReceiptVouchers(IsPosted);
                CREATE INDEX IF NOT EXISTS idx_paymentvouchers_number ON PaymentVouchers(VoucherNumber);
                CREATE INDEX IF NOT EXISTS idx_paymentvouchers_date ON PaymentVouchers(VoucherDate);
                CREATE INDEX IF NOT EXISTS idx_paymentvouchers_customer ON PaymentVouchers(CustomerID);
                CREATE INDEX IF NOT EXISTS idx_paymentvouchers_supplier ON PaymentVouchers(SupplierID);
                CREATE INDEX IF NOT EXISTS idx_paymentvouchers_isposted ON PaymentVouchers(IsPosted);
                CREATE INDEX IF NOT EXISTS idx_voucherdetails_voucher ON VoucherDetails(VoucherID, VoucherType);
                CREATE INDEX IF NOT EXISTS idx_voucherdetails_account ON VoucherDetails(AccountID);
                CREATE INDEX IF NOT EXISTS idx_checks_number ON Checks(CheckNumber);
                CREATE INDEX IF NOT EXISTS idx_checks_date ON Checks(CheckDate);
                CREATE INDEX IF NOT EXISTS idx_checks_status ON Checks(Status);
                CREATE INDEX IF NOT EXISTS idx_checks_duedate ON Checks(DueDate);
                CREATE INDEX IF NOT EXISTS idx_checks_bank ON Checks(BankName);
                CREATE INDEX IF NOT EXISTS idx_checktransactions_check ON CheckTransactions(CheckID);
                CREATE INDEX IF NOT EXISTS idx_checktransactions_date ON CheckTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_customers_code ON Customers(CustomerCode);
                CREATE INDEX IF NOT EXISTS idx_customers_name ON Customers(CustomerNameAr);
                CREATE INDEX IF NOT EXISTS idx_customers_isactive ON Customers(IsActive);
                CREATE INDEX IF NOT EXISTS idx_customers_account ON Customers(AccountID);
                CREATE INDEX IF NOT EXISTS idx_suppliers_code ON Suppliers(SupplierCode);
                CREATE INDEX IF NOT EXISTS idx_suppliers_name ON Suppliers(SupplierNameAr);
                CREATE INDEX IF NOT EXISTS idx_suppliers_isactive ON Suppliers(IsActive);
                CREATE INDEX IF NOT EXISTS idx_suppliers_account ON Suppliers(AccountID);
                CREATE INDEX IF NOT EXISTS idx_customertransactions_customer ON CustomerTransactions(CustomerID);
                CREATE INDEX IF NOT EXISTS idx_customertransactions_date ON CustomerTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_customertransactions_type ON CustomerTransactions(TransactionType);
                CREATE INDEX IF NOT EXISTS idx_suppliertransactions_supplier ON SupplierTransactions(SupplierID);
                CREATE INDEX IF NOT EXISTS idx_suppliertransactions_date ON SupplierTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_suppliertransactions_type ON SupplierTransactions(TransactionType);
                CREATE INDEX IF NOT EXISTS idx_stores_code ON Stores(StoreCode);
                CREATE INDEX IF NOT EXISTS idx_stores_name ON Stores(StoreNameAr);
                CREATE INDEX IF NOT EXISTS idx_stores_isactive ON Stores(IsActive);
                CREATE INDEX IF NOT EXISTS idx_products_code ON Products(ProductCode);
                CREATE INDEX IF NOT EXISTS idx_products_name ON Products(ProductNameAr);
                CREATE INDEX IF NOT EXISTS idx_products_barcode ON Products(Barcode);
                CREATE INDEX IF NOT EXISTS idx_products_category ON Products(CategoryID);
                CREATE INDEX IF NOT EXISTS idx_products_isactive ON Products(IsActive);
                CREATE INDEX IF NOT EXISTS idx_productcategories_code ON ProductCategories(CategoryCode);
                CREATE INDEX IF NOT EXISTS idx_productcategories_parent ON ProductCategories(ParentCategoryID);
                CREATE INDEX IF NOT EXISTS idx_storeinventory_store ON StoreInventory(StoreID);
                CREATE INDEX IF NOT EXISTS idx_storeinventory_product ON StoreInventory(ProductID);
                CREATE INDEX IF NOT EXISTS idx_storeinventory_batch ON StoreInventory(BatchNumber);
                CREATE INDEX IF NOT EXISTS idx_storeinventory_expiry ON StoreInventory(ExpiryDate);
                CREATE INDEX IF NOT EXISTS idx_inventorytransactions_store ON InventoryTransactions(StoreID);
                CREATE INDEX IF NOT EXISTS idx_inventorytransactions_product ON InventoryTransactions(ProductID);
                CREATE INDEX IF NOT EXISTS idx_inventorytransactions_date ON InventoryTransactions(TransactionDate);
                CREATE INDEX IF NOT EXISTS idx_inventorytransactions_type ON InventoryTransactions(TransactionType);
                CREATE INDEX IF NOT EXISTS idx_inventorytransactions_reference ON InventoryTransactions(ReferenceType, ReferenceID);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_number ON SalesInvoices(InvoiceNumber);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_date ON SalesInvoices(InvoiceDate);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_customer ON SalesInvoices(CustomerID);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_store ON SalesInvoices(StoreID);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_type ON SalesInvoices(InvoiceType);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_status ON SalesInvoices(PaymentStatus);
                CREATE INDEX IF NOT EXISTS idx_salesinvoices_isposted ON SalesInvoices(IsPosted);
                CREATE INDEX IF NOT EXISTS idx_salesinvoiceitems_invoice ON SalesInvoiceItems(InvoiceID);
                CREATE INDEX IF NOT EXISTS idx_salesinvoiceitems_product ON SalesInvoiceItems(ProductID);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_number ON PurchaseInvoices(InvoiceNumber);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_date ON PurchaseInvoices(InvoiceDate);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_supplier ON PurchaseInvoices(SupplierID);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_store ON PurchaseInvoices(StoreID);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_type ON PurchaseInvoices(InvoiceType);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_status ON PurchaseInvoices(PaymentStatus);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoices_isposted ON PurchaseInvoices(IsPosted);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoiceitems_invoice ON PurchaseInvoiceItems(InvoiceID);
                CREATE INDEX IF NOT EXISTS idx_purchaseinvoiceitems_product ON PurchaseInvoiceItems(ProductID);
                CREATE INDEX IF NOT EXISTS idx_customerbalancesummary_customer ON CustomerBalanceSummary(CustomerID);
                CREATE INDEX IF NOT EXISTS idx_customerbalancesummary_date ON CustomerBalanceSummary(BalanceDate);
                CREATE INDEX IF NOT EXISTS idx_supplierbalancesummary_supplier ON SupplierBalanceSummary(SupplierID);
                CREATE INDEX IF NOT EXISTS idx_supplierbalancesummary_date ON SupplierBalanceSummary(BalanceDate);
                CREATE INDEX IF NOT EXISTS idx_accountbalancesummary_account ON AccountBalanceSummary(AccountID);
                CREATE INDEX IF NOT EXISTS idx_accountbalancesummary_date ON AccountBalanceSummary(BalanceDate);
                CREATE INDEX IF NOT EXISTS idx_trialbalance_period ON TrialBalance(PeriodID);
                CREATE INDEX IF NOT EXISTS idx_trialbalance_account ON TrialBalance(AccountID);
                CREATE INDEX IF NOT EXISTS idx_journalentries_number ON JournalEntries(EntryNumber);
                CREATE INDEX IF NOT EXISTS idx_journalentries_date ON JournalEntries(EntryDate);
                CREATE INDEX IF NOT EXISTS idx_journalentries_isposted ON JournalEntries(IsPosted);
                CREATE INDEX IF NOT EXISTS idx_journalentries_reference ON JournalEntries(ReferenceType, ReferenceID);
                CREATE INDEX IF NOT EXISTS idx_journalentrydetails_entry ON JournalEntryDetails(EntryID);
                CREATE INDEX IF NOT EXISTS idx_journalentrydetails_account ON JournalEntryDetails(AccountID);
                CREATE INDEX IF NOT EXISTS idx_financialperiods_code ON FinancialPeriods(PeriodCode);
                CREATE INDEX IF NOT EXISTS idx_financialperiods_date ON FinancialPeriods(StartDate, EndDate);
                CREATE INDEX IF NOT EXISTS idx_financialperiods_isclosed ON FinancialPeriods(IsClosed);
                CREATE INDEX IF NOT EXISTS idx_license_licensekey ON SystemLicense(LicenseKey);
                CREATE INDEX IF NOT EXISTS idx_license_isactive ON SystemLicense(IsActive);
                CREATE INDEX IF NOT EXISTS idx_license_licensetype ON SystemLicense(LicenseType);
                CREATE INDEX IF NOT EXISTS idx_licenselog_licenseid ON LicenseAuditLog(LicenseID);
                CREATE INDEX IF NOT EXISTS idx_licenselog_createddate ON LicenseAuditLog(CreatedDate);
                CREATE INDEX IF NOT EXISTS idx_graceperiod_enddate ON GracePeriod(GracePeriodEndDate);
                CREATE INDEX IF NOT EXISTS idx_graceperiod_isused ON GracePeriod(IsUsed);
                CREATE INDEX IF NOT EXISTS idx_systemsettings_key ON SystemSettings(SettingKey);
            ";

            using (var cmd = new SQLiteCommand(createIndexes, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 15. المحفزات (Triggers)

        private void CreateAllTriggers(SQLiteConnection connection)
        {
            string triggerUpdateCustomerBalance = @"
        CREATE TRIGGER IF NOT EXISTS trg_update_customer_balance
        AFTER INSERT ON CustomerTransactions
        BEGIN
            UPDATE Customers 
            SET CurrentBalance = (
                SELECT BalanceAfter 
                FROM CustomerTransactions 
                WHERE TransactionID = NEW.TransactionID
            ),
            ModifiedDate = CURRENT_TIMESTAMP
            WHERE CustomerID = NEW.CustomerID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateCustomerBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerUpdateSupplierBalance = @"
        CREATE TRIGGER IF NOT EXISTS trg_update_supplier_balance
        AFTER INSERT ON SupplierTransactions
        BEGIN
            UPDATE Suppliers 
            SET CurrentBalance = (
                SELECT BalanceAfter 
                FROM SupplierTransactions 
                WHERE TransactionID = NEW.TransactionID
            ),
            ModifiedDate = CURRENT_TIMESTAMP
            WHERE SupplierID = NEW.SupplierID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateSupplierBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerUpdateStoreInventory = @"
        CREATE TRIGGER IF NOT EXISTS trg_update_store_inventory
        AFTER INSERT ON InventoryTransactions
        BEGIN
            INSERT OR REPLACE INTO StoreInventory (
                StoreID, ProductID, BatchNumber, Quantity, 
                AvailableQuantity, CostPrice, LastUpdated
            )
            SELECT 
                NEW.StoreID, NEW.ProductID, NEW.BatchNumber,
                NEW.QuantityAfter,
                NEW.QuantityAfter,
                COALESCE((SELECT CostPrice FROM StoreInventory WHERE StoreID = NEW.StoreID AND ProductID = NEW.ProductID), NEW.UnitPrice),
                CURRENT_TIMESTAMP
            WHERE EXISTS (SELECT 1 FROM Products WHERE ProductID = NEW.ProductID);
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateStoreInventory, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerJournalFromReceipt = @"
        DROP TRIGGER IF EXISTS trig_journal_from_receipt;
        CREATE TRIGGER IF NOT EXISTS trig_journal_from_receipt
        AFTER INSERT ON ReceiptVouchers
        WHEN NEW.IsPosted = 1
        BEGIN
            INSERT OR IGNORE INTO JournalEntries (
                EntryNumber, EntryDate, ReferenceType, ReferenceID, 
                ReferenceNumber, Description, IsPosted, PostedDate, PostedBy
            )
            VALUES (
                'REC-' || NEW.VoucherNumber, NEW.VoucherDate, 'RECEIPT_VOUCHER',
                NEW.VoucherID, NEW.VoucherNumber, NEW.Description, 1, 
                CURRENT_TIMESTAMP, NEW.PostedBy
            );
            
            INSERT OR IGNORE INTO JournalEntryDetails (EntryID, AccountID, DebitAmount, CreditAmount)
            SELECT 
                (SELECT EntryID FROM JournalEntries WHERE ReferenceType = 'RECEIPT_VOUCHER' AND ReferenceID = NEW.VoucherID), 
                CASE 
                    WHEN NEW.CustomerID IS NOT NULL THEN (SELECT AccountID FROM Customers WHERE CustomerID = NEW.CustomerID)
                    ELSE (SELECT AccountID FROM Treasury WHERE TreasuryID = NEW.TreasuryID)
                END,
                NEW.Amount, 0
            WHERE (NEW.CustomerID IS NOT NULL OR NEW.TreasuryID IS NOT NULL);
            
            INSERT OR IGNORE INTO JournalEntryDetails (EntryID, AccountID, DebitAmount, CreditAmount)
            SELECT 
                (SELECT EntryID FROM JournalEntries WHERE ReferenceType = 'RECEIPT_VOUCHER' AND ReferenceID = NEW.VoucherID), 
                CASE 
                    WHEN NEW.TreasuryID IS NOT NULL THEN (SELECT AccountID FROM Treasury WHERE TreasuryID = NEW.TreasuryID)
                    WHEN NEW.BankAccountID IS NOT NULL THEN (SELECT AccountID FROM BankAccounts WHERE BankAccountID = NEW.BankAccountID)
                END,
                0, NEW.Amount
            WHERE (NEW.TreasuryID IS NOT NULL OR NEW.BankAccountID IS NOT NULL);
        END;";

            using (var cmd = new SQLiteCommand(triggerJournalFromReceipt, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerJournalFromPayment = @"
        DROP TRIGGER IF EXISTS trig_journal_from_payment;
        CREATE TRIGGER IF NOT EXISTS trig_journal_from_payment
        AFTER INSERT ON PaymentVouchers
        WHEN NEW.IsPosted = 1
        BEGIN
            INSERT OR IGNORE INTO JournalEntries (
                EntryNumber, EntryDate, ReferenceType, ReferenceID, 
                ReferenceNumber, Description, IsPosted, PostedDate, PostedBy
            )
            VALUES (
                'PAY-' || NEW.VoucherNumber, NEW.VoucherDate, 'PAYMENT_VOUCHER',
                NEW.VoucherID, NEW.VoucherNumber, NEW.Description, 1, 
                CURRENT_TIMESTAMP, NEW.PostedBy
            );
            
            INSERT OR IGNORE INTO JournalEntryDetails (EntryID, AccountID, DebitAmount, CreditAmount)
            SELECT 
                (SELECT EntryID FROM JournalEntries WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = NEW.VoucherID), 
                CASE 
                    WHEN NEW.TreasuryID IS NOT NULL THEN (SELECT AccountID FROM Treasury WHERE TreasuryID = NEW.TreasuryID)
                    WHEN NEW.BankAccountID IS NOT NULL THEN (SELECT AccountID FROM BankAccounts WHERE BankAccountID = NEW.BankAccountID)
                END,
                NEW.Amount, 0
            WHERE (NEW.TreasuryID IS NOT NULL OR NEW.BankAccountID IS NOT NULL);
            
            INSERT OR IGNORE INTO JournalEntryDetails (EntryID, AccountID, DebitAmount, CreditAmount)
            SELECT 
                (SELECT EntryID FROM JournalEntries WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = NEW.VoucherID), 
                CASE 
                    WHEN NEW.CustomerID IS NOT NULL THEN (SELECT AccountID FROM Customers WHERE CustomerID = NEW.CustomerID)
                    ELSE (SELECT AccountID FROM Suppliers WHERE SupplierID = NEW.SupplierID)
                END,
                0, NEW.Amount
            WHERE (NEW.CustomerID IS NOT NULL OR NEW.SupplierID IS NOT NULL);
        END;";

            using (var cmd = new SQLiteCommand(triggerJournalFromPayment, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerCheckStatusChange = @"
        CREATE TRIGGER IF NOT EXISTS trig_check_status_change
        AFTER UPDATE OF Status ON Checks
        BEGIN
            INSERT INTO CheckTransactions (
                CheckID, FromStatus, ToStatus, Description, PerformedBy
            )
            VALUES (
                NEW.CheckID, OLD.Status, NEW.Status, 
                'Status changed from ' || OLD.Status || ' to ' || NEW.Status,
                NEW.ModifiedBy
            );
        END;";

            using (var cmd = new SQLiteCommand(triggerCheckStatusChange, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerUpdateAccountBalance = @"
        CREATE TRIGGER IF NOT EXISTS trig_update_account_balance
        AFTER INSERT ON JournalEntryDetails
        BEGIN
            UPDATE ChartOfAccounts 
            SET CurrentBalance = CurrentBalance + NEW.DebitAmount - NEW.CreditAmount,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE AccountID = NEW.AccountID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateAccountBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerUpdateTreasuryBalance = @"
        CREATE TRIGGER IF NOT EXISTS trig_update_treasury_balance
        AFTER INSERT ON TreasuryTransactions
        BEGIN
            UPDATE Treasury 
            SET CurrentBalance = NEW.BalanceAfter,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE TreasuryID = NEW.TreasuryID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateTreasuryBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerUpdateBankBalance = @"
        CREATE TRIGGER IF NOT EXISTS trig_update_bank_balance
        AFTER INSERT ON BankTransactions
        BEGIN
            UPDATE BankAccounts 
            SET CurrentBalance = NEW.BalanceAfter,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE BankAccountID = NEW.BankAccountID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateBankBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerCustomerTransactionFromSales = @"
        CREATE TRIGGER IF NOT EXISTS trig_customer_transaction_from_sales
        AFTER INSERT ON SalesInvoices
        WHEN NEW.InvoiceType = 'Sales'
        BEGIN
            INSERT INTO CustomerTransactions (
                CustomerID, TransactionDate, TransactionType,
                DebitAmount, CreditAmount, BalanceAfter,
                ReferenceType, ReferenceID, ReferenceNumber, Description
            )
            SELECT 
                NEW.CustomerID, NEW.InvoiceDate, 'Invoice',
                NEW.TotalAmount, 0, 
                COALESCE((SELECT CurrentBalance FROM Customers WHERE CustomerID = NEW.CustomerID), 0) + NEW.TotalAmount,
                'SALES_INVOICE', NEW.InvoiceID, NEW.InvoiceNumber, NEW.Notes
            WHERE NEW.CustomerID IS NOT NULL;
        END;";

            using (var cmd = new SQLiteCommand(triggerCustomerTransactionFromSales, connection))
            {
                cmd.ExecuteNonQuery();
            }

            string triggerSupplierTransactionFromPurchase = @"
        CREATE TRIGGER IF NOT EXISTS trig_supplier_transaction_from_purchase
        AFTER INSERT ON PurchaseInvoices
        WHEN NEW.InvoiceType = 'Purchase'
        BEGIN
            INSERT INTO SupplierTransactions (
                SupplierID, TransactionDate, TransactionType,
                DebitAmount, CreditAmount, BalanceAfter,
                ReferenceType, ReferenceID, ReferenceNumber, Description
            )
            SELECT 
                NEW.SupplierID, NEW.InvoiceDate, 'Purchase',
                0, NEW.TotalAmount,
                COALESCE((SELECT CurrentBalance FROM Suppliers WHERE SupplierID = NEW.SupplierID), 0) - NEW.TotalAmount,
                'PURCHASE_INVOICE', NEW.InvoiceID, NEW.InvoiceNumber, NEW.Notes
            WHERE NEW.SupplierID IS NOT NULL;
        END;";

            using (var cmd = new SQLiteCommand(triggerSupplierTransactionFromPurchase, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 15.0 سجل التعديلات (Audit Trail)

        /// <summary>
        /// إنشاء محفزات قاعدة البيانات التي تسجّل تلقائياً في جدول AuditLog الموجود مسبقاً
        /// في هيكل قاعدة البيانات (تم إنشاؤه سابقاً ضمن جداول المستخدمين لكنه لم يكن مُستخدَماً
        /// فعلياً من قبل - نفس نمط اكتُشف من قبل مع جداول البنك والشيكات في هذا المشروع).
        ///
        /// أعمدة الجدول الموجود فعلياً: LogID, UserID, ActionType, TableName, RecordID,
        /// OldValue, NewValue, IPAddress, UserAgent, CreatedDate.
        ///
        /// القرار المعماري: بدلاً من إضافة كود تسجيل يدوي في كل دالة حفظ/تعديل/حذف عبر عشرات
        /// الملفات (خطر كبير على الاستقرار في مشروع بهذا الحجم بدون بيئة اختبار فعلية)، تم
        /// الاعتماد بالكامل على Triggers قاعدة البيانات - وهي الأسلوب المستخدم بالفعل في هذا
        /// المشروع لحالات مشابهة (تحديث الأرصدة، القيود المحاسبية...). هذا يضمن أن أي تعديل من
        /// أي مكان في البرنامج سيُسجَّل تلقائياً بدون خطر على الكود الموجود حالياً.
        ///
        /// نطاق التسجيل: تم التركيز على الأحداث الأكثر حساسية أمنياً ومالياً فقط:
        /// - تغييرات صلاحيات وحسابات المستخدمين (الأخطر أمنياً)
        /// - تعديل رصيد أول المدة أو حد الائتمان لعميل/مورد يدوياً (مؤشر تلاعب محتمل)
        /// - حذف حركات مالية من كشوف الحسابات
        /// - حذف أو إلغاء فواتير بيع/شراء وإلغاء الشيكات
        /// </summary>
        private void CreateAuditTrail(SQLiteConnection connection)
        {
            string createIndexSql = @"
                CREATE INDEX IF NOT EXISTS idx_auditlog_createddate2 ON AuditLog(CreatedDate DESC);";

            using (var cmd = new SQLiteCommand(createIndexSql, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ---------- تغييرات حساسة على حسابات المستخدمين ----------

            string trigUserRoleChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_user_role_change
                AFTER UPDATE OF UserRole ON Users
                WHEN NEW.UserRole <> OLD.UserRole
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, OldValue, NewValue)
                    VALUES (NULL, 'RoleChanged', 'Users', NEW.UserID,
                        OLD.UserRole,
                        'تغيير صلاحية المستخدم ''' || NEW.Username || ''' من ''' || OLD.UserRole || ''' إلى ''' || NEW.UserRole || '''');
                END;";

            string trigUserActiveChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_user_active_change
                AFTER UPDATE OF IsActive ON Users
                WHEN NEW.IsActive <> OLD.IsActive
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'ActiveStatusChanged', 'Users', NEW.UserID,
                        CASE WHEN NEW.IsActive = 0
                             THEN 'تم تعطيل حساب المستخدم ''' || NEW.Username || ''''
                             ELSE 'تم تفعيل حساب المستخدم ''' || NEW.Username || ''''
                        END);
                END;";

            string trigUserPasswordChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_user_password_change
                AFTER UPDATE OF PasswordHash ON Users
                WHEN NEW.PasswordHash <> OLD.PasswordHash
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'PasswordChanged', 'Users', NEW.UserID,
                        'تم تغيير كلمة مرور المستخدم ''' || NEW.Username || '''');
                END;";

            // ---------- تعديل يدوي لرصيد أول المدة أو حد الائتمان (عملاء/موردين) ----------

            string trigCustomerOpeningBalanceChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_customer_opening_balance
                AFTER UPDATE OF OpeningBalance ON Customers
                WHEN NEW.OpeningBalance <> OLD.OpeningBalance
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, OldValue, NewValue)
                    VALUES (NULL, 'OpeningBalanceChanged', 'Customers', NEW.CustomerID,
                        CAST(OLD.OpeningBalance AS TEXT),
                        'تعديل رصيد أول المدة للعميل ''' || NEW.CustomerNameAr || ''' من ' || OLD.OpeningBalance || ' إلى ' || NEW.OpeningBalance);
                END;";

            string trigCustomerCreditLimitChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_customer_credit_limit
                AFTER UPDATE OF CreditLimit ON Customers
                WHEN NEW.CreditLimit <> OLD.CreditLimit
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, OldValue, NewValue)
                    VALUES (NULL, 'CreditLimitChanged', 'Customers', NEW.CustomerID,
                        CAST(OLD.CreditLimit AS TEXT),
                        'تعديل حد الائتمان للعميل ''' || NEW.CustomerNameAr || ''' من ' || OLD.CreditLimit || ' إلى ' || NEW.CreditLimit);
                END;";

            string trigSupplierOpeningBalanceChange = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_supplier_opening_balance
                AFTER UPDATE OF OpeningBalance ON Suppliers
                WHEN NEW.OpeningBalance <> OLD.OpeningBalance
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, OldValue, NewValue)
                    VALUES (NULL, 'OpeningBalanceChanged', 'Suppliers', NEW.SupplierID,
                        CAST(OLD.OpeningBalance AS TEXT),
                        'تعديل رصيد أول المدة للمورد ''' || NEW.SupplierNameAr || ''' من ' || OLD.OpeningBalance || ' إلى ' || NEW.OpeningBalance);
                END;";

            // ---------- حذف حركات مالية من كشوف الحسابات ----------

            string trigCustomerTransactionDelete = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_customer_transaction_delete
                AFTER DELETE ON CustomerTransactions
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Deleted', 'CustomerTransactions', OLD.TransactionID,
                        'حذف حركة عميل: نوع ''' || OLD.TransactionType || ''' بمبلغ ' || COALESCE(OLD.CreditAmount, OLD.DebitAmount, 0) ||
                        ' (رقم مرجع: ' || COALESCE(OLD.ReferenceNumber, 'غير محدد') || ')');
                END;";

            string trigSupplierTransactionDelete = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_supplier_transaction_delete
                AFTER DELETE ON SupplierTransactions
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Deleted', 'SupplierTransactions', OLD.TransactionID,
                        'حذف حركة مورد: نوع ''' || OLD.TransactionType || ''' بمبلغ ' || COALESCE(OLD.CreditAmount, OLD.DebitAmount, 0) ||
                        ' (رقم مرجع: ' || COALESCE(OLD.ReferenceNumber, 'غير محدد') || ')');
                END;";

            // ---------- حذف أو إلغاء فواتير ----------

            string trigSalesInvoiceDelete = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_sales_invoice_delete
                AFTER DELETE ON SalesInvoices
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Deleted', 'SalesInvoices', OLD.InvoiceID,
                        'حذف فاتورة بيع رقم ''' || OLD.InvoiceNumber || ''' بقيمة ' || OLD.TotalAmount);
                END;";

            string trigSalesInvoiceVoid = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_sales_invoice_void
                AFTER UPDATE OF IsVoid ON SalesInvoices
                WHEN NEW.IsVoid = 1 AND OLD.IsVoid = 0
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Voided', 'SalesInvoices', NEW.InvoiceID,
                        'إلغاء فاتورة بيع رقم ''' || NEW.InvoiceNumber || ''' بقيمة ' || NEW.TotalAmount);
                END;";

            string trigPurchaseInvoiceDelete = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_purchase_invoice_delete
                AFTER DELETE ON PurchaseInvoices
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Deleted', 'PurchaseInvoices', OLD.InvoiceID,
                        'حذف فاتورة مشتريات رقم ''' || OLD.InvoiceNumber || ''' بقيمة ' || OLD.TotalAmount);
                END;";

            string trigPurchaseInvoiceVoid = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_purchase_invoice_void
                AFTER UPDATE OF IsVoid ON PurchaseInvoices
                WHEN NEW.IsVoid = 1 AND OLD.IsVoid = 0
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NULL, 'Voided', 'PurchaseInvoices', NEW.InvoiceID,
                        'إلغاء فاتورة مشتريات رقم ''' || NEW.InvoiceNumber || ''' بقيمة ' || NEW.TotalAmount);
                END;";

            // ---------- إلغاء الشيكات ----------

            string trigCheckVoid = @"
                CREATE TRIGGER IF NOT EXISTS trig_audit_check_void
                AFTER UPDATE OF IsVoid ON Checks
                WHEN NEW.IsVoid = 1 AND OLD.IsVoid = 0
                BEGIN
                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                    VALUES (NEW.VoidBy, 'Voided', 'Checks', NEW.CheckID,
                        'إلغاء شيك رقم ''' || NEW.CheckNumber || ''' بقيمة ' || NEW.Amount);
                END;";

            var allAuditTriggers = new[]
            {
                trigUserRoleChange, trigUserActiveChange, trigUserPasswordChange,
                trigCustomerOpeningBalanceChange, trigCustomerCreditLimitChange, trigSupplierOpeningBalanceChange,
                trigCustomerTransactionDelete, trigSupplierTransactionDelete,
                trigSalesInvoiceDelete, trigSalesInvoiceVoid, trigPurchaseInvoiceDelete, trigPurchaseInvoiceVoid,
                trigCheckVoid
            };

            foreach (string triggerSql in allAuditTriggers)
            {
                using (var cmd = new SQLiteCommand(triggerSql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

        #region 15.1 إصلاح باج مضاعفة رصيد العملاء (Duplicate Sales Invoice Balance Repair)

        /// <summary>
        /// نتيجة عملية فحص/إصلاح مضاعفة رصيد العملاء
        /// </summary>
        public class BalanceRepairResult
        {
            public bool WasNeeded { get; set; }
            public int DuplicateRowsRemoved { get; set; }
            public int CustomersRecalculated { get; set; }
            public List<string> AffectedCustomerNames { get; set; } = new List<string>();
        }

        /// <summary>
        /// إصلاح لمرة واحدة لباج قديم كان يتسبب في مضاعفة رصيد العميل عند إنشاء فاتورة بيع:
        /// كان الكود يضيف حركة "Invoice" في جدول CustomerTransactions يدوياً بعد أن يكون قد سبق
        /// وأضافها الـ Trigger الخاص بقاعدة البيانات (trig_customer_transaction_from_sales) تلقائياً،
        /// فيتضاعف رصيد العميل الفعلي (CurrentBalance) في كل فاتورة بيع جديدة.
        /// تم إصلاح سبب المشكلة في كود إنشاء الفاتورة (InvoiceService.cs)، وهذه الدالة تعالج فقط
        /// البيانات القديمة المتأثرة بالباج قبل الإصلاح. تعمل مرة واحدة فقط بأمان تام (محمية بعلامة
        /// في جدول SystemSettings)، ولا تؤثر على المشتريات/الموردين لأنهم لم يتأثروا بهذا الباج أصلاً.
        /// </summary>
        public BalanceRepairResult RepairDuplicateSalesInvoiceCustomerBalancesIfNeeded()
        {
            var result = new BalanceRepairResult();

            try
            {
                using (var connection = new SQLiteConnection(GetConnectionString()))
                {
                    connection.Open();

                    // تحقق هل تم تنفيذ هذا الإصلاح من قبل على هذه القاعدة
                    using (var checkCmd = new SQLiteCommand(
                        "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'DuplicateSalesInvoiceBalanceRepair_v1'", connection))
                    {
                        var existing = checkCmd.ExecuteScalar();
                        if (existing != null && existing.ToString() == "Done")
                        {
                            result.WasNeeded = false;
                            return result;
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1) تحديد الحركات المكررة الناتجة عن الباج القديم:
                            //    ⚠️ اكتشفنا (بفحص نسخة احتياطية حقيقية من قاعدة البيانات) حقيقتين مهمتين:
                            //    أ) الحركة اللي بيضيفها الـ Trigger تلقائياً (trig_customer_transaction_from_sales) دايماً
                            //       CreatedBy = NULL حتى لو الفاتورة سليمة ومفيهاش أي تكرار - فمعيار "CreatedBy IS NULL"
                            //       لوحده مش كافي لتحديد التكرار؛ لازم نتأكد إن الفاتورة فعلاً بقالها أكتر من حركة "Invoice"
                            //       واحدة لنفس العميل ونفس رقم الفاتورة (تكرار حقيقي) قبل ما نحذف أي حاجة.
                            //    ب) في كل حالات التكرار الحقيقي، النسخة اللي بيضيفها الـ Trigger (CreatedBy = NULL) هي
                            //       دايماً الصحيحة (المبلغ مسجل في DebitAmount زي ما المفروض لحركة "Invoice")، بينما
                            //       النسخة التانية اللي كان الكود القديم بيضيفها يدوياً (CreatedBy IS NOT NULL) كانت
                            //       فيها باج تاني: المبلغ كان بيتسجل غلط في CreditAmount بدل DebitAmount (فتظهر الحركة
                            //       في كشف الحساب وكأنها "تحصيل" مش "بيع"). فالمعيار الصحيح للإبقاء/الحذف هو أي الصفين
                            //       فيه توزيع Debit/Credit سليم لحركة "Invoice" (DebitAmount > 0) - مش مين منسوب لمستخدم.
                            var duplicateTransactionIds = new List<long>();
                            var affectedCustomerIds = new HashSet<int>();

                            string findGroupsSql = @"
                                SELECT TransactionID, CustomerID, ReferenceNumber, DebitAmount, CreditAmount
                                FROM CustomerTransactions
                                WHERE ReferenceType = 'SALES_INVOICE'
                                AND TransactionType = 'Invoice'
                                ORDER BY CustomerID, ReferenceNumber, TransactionID";

                            var groups = new Dictionary<(int CustomerId, string RefNumber),
                                List<(long TxId, decimal Debit, decimal Credit)>>();

                            using (var findCmd = new SQLiteCommand(findGroupsSql, connection, transaction))
                            using (var reader = findCmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long txId = reader.GetInt64(0);
                                    int custId = Convert.ToInt32(reader["CustomerID"]);
                                    string refNumber = reader["ReferenceNumber"] == DBNull.Value ? "" : reader["ReferenceNumber"].ToString();
                                    decimal debit = reader["DebitAmount"] != DBNull.Value ? Convert.ToDecimal(reader["DebitAmount"]) : 0;
                                    decimal credit = reader["CreditAmount"] != DBNull.Value ? Convert.ToDecimal(reader["CreditAmount"]) : 0;

                                    var key = (custId, refNumber);
                                    if (!groups.TryGetValue(key, out var list))
                                    {
                                        list = new List<(long, decimal, decimal)>();
                                        groups[key] = list;
                                    }
                                    list.Add((txId, debit, credit));
                                }
                            }

                            // ✅ لا نلمس إلا المجموعات اللي فعلاً فيها أكتر من حركة "Invoice" واحدة لنفس الفاتورة/العميل
                            // (تكرار حقيقي)، وفيها بالظبط نسخة واحدة سليمة التوزيع (DebitAmount > 0 وCreditAmount = 0)
                            // والباقي كله بالنمط الخاطئ المعروف (DebitAmount = 0 وCreditAmount > 0). أي حالة غير متوقعة
                            // (نمط مختلف عن ده) بنتجاهلها تماماً ولا نحذف منها أي شيء، حفاظاً على سلامة البيانات.
                            foreach (var kvp in groups)
                            {
                                var rows = kvp.Value;
                                if (rows.Count <= 1) continue; // مفيش تكرار - سيب الحركة زي ما هي

                                var correctRows = rows.Where(r => r.Debit > 0 && r.Credit == 0).ToList();
                                var swappedRows = rows.Where(r => r.Debit == 0 && r.Credit > 0).ToList();

                                if (correctRows.Count != 1 || correctRows.Count + swappedRows.Count != rows.Count)
                                    continue; // نمط غير متوقع - تجاهل هذه المجموعة لمراجعة يدوية

                                foreach (var row in swappedRows)
                                {
                                    duplicateTransactionIds.Add(row.TxId);
                                    affectedCustomerIds.Add(kvp.Key.CustomerId);
                                }
                            }

                            result.DuplicateRowsRemoved = duplicateTransactionIds.Count;

                            // ✅ هذا الحذف إصلاح داخلي تلقائي لباج قديم (ليس حذفاً من مستخدم)،
                            // فنعطّل مؤقتاً تريجر تسجيل حذف حركات العملاء في سجل التدقيق أثناء
                            // تنفيذه حتى لا يظهر للمستخدم كأنه حذف حقيقي لحركات ويسبب له لبساً،
                            // مع إعادة تفعيل التريجر فوراً بعد الانتهاء ليستمر عمله بشكل طبيعي
                            // مع أي حذف حقيقي يقوم به المستخدم لاحقاً.
                            using (var dropTrigCmd = new SQLiteCommand(
                                "DROP TRIGGER IF EXISTS trig_audit_customer_transaction_delete", connection, transaction))
                            {
                                dropTrigCmd.ExecuteNonQuery();
                            }

                            // 2) حذف الحركات المكررة (نُبقي فقط على الحركة اليدوية الأصلية التي تحمل وصفاً ومستخدماً صحيحين)
                            if (duplicateTransactionIds.Count > 0)
                            {
                                foreach (var txId in duplicateTransactionIds)
                                {
                                    using (var delCmd = new SQLiteCommand(
                                        "DELETE FROM CustomerTransactions WHERE TransactionID = @id", connection, transaction))
                                    {
                                        delCmd.Parameters.AddWithValue("@id", txId);
                                        delCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // ✅ إعادة إنشاء التريجر كما هو تماماً ليعمل بشكل طبيعي بعد انتهاء الإصلاح
                            using (var recreateTrigCmd = new SQLiteCommand(@"
                                CREATE TRIGGER IF NOT EXISTS trig_audit_customer_transaction_delete
                                AFTER DELETE ON CustomerTransactions
                                BEGIN
                                    INSERT INTO AuditLog (UserID, ActionType, TableName, RecordID, NewValue)
                                    VALUES (NULL, 'Deleted', 'CustomerTransactions', OLD.TransactionID,
                                        'حذف حركة عميل: نوع ''' || OLD.TransactionType || ''' بمبلغ ' || COALESCE(OLD.CreditAmount, OLD.DebitAmount, 0) ||
                                        ' (رقم مرجع: ' || COALESCE(OLD.ReferenceNumber, 'غير محدد') || ')');
                                END;", connection, transaction))
                            {
                                recreateTrigCmd.ExecuteNonQuery();
                            }

                            // 3) إعادة احتساب الرصيد الجاري (BalanceAfter) والرصيد الحالي (CurrentBalance)
                            //    لكل عميل تأثر بالمشكلة، بدءاً من رصيد أول المدة وبالترتيب الزمني الصحيح
                            foreach (var customerId in affectedCustomerIds)
                            {
                                decimal openingBalance = 0;
                                string customerName = "";

                                using (var custCmd = new SQLiteCommand(
                                    "SELECT COALESCE(OpeningBalance, 0), CustomerNameAr FROM Customers WHERE CustomerID = @id", connection, transaction))
                                {
                                    custCmd.Parameters.AddWithValue("@id", customerId);
                                    using (var custReader = custCmd.ExecuteReader())
                                    {
                                        if (custReader.Read())
                                        {
                                            openingBalance = Convert.ToDecimal(custReader[0]);
                                            customerName = custReader[1]?.ToString() ?? $"عميل #{customerId}";
                                        }
                                    }
                                }

                                decimal runningBalance = openingBalance;

                                string listSql = @"
                                    SELECT TransactionID, TransactionType, DebitAmount, CreditAmount
                                    FROM CustomerTransactions
                                    WHERE CustomerID = @customerId
                                    ORDER BY TransactionDate ASC, TransactionID ASC";

                                var rowsToUpdate = new List<(long Id, decimal NewBalance)>();

                                using (var listCmd = new SQLiteCommand(listSql, connection, transaction))
                                {
                                    listCmd.Parameters.AddWithValue("@customerId", customerId);
                                    using (var listReader = listCmd.ExecuteReader())
                                    {
                                        while (listReader.Read())
                                        {
                                            long txId = listReader.GetInt64(0);
                                            string txType = listReader["TransactionType"]?.ToString() ?? "";
                                            decimal debit = listReader["DebitAmount"] != DBNull.Value ? Convert.ToDecimal(listReader["DebitAmount"]) : 0;
                                            decimal credit = listReader["CreditAmount"] != DBNull.Value ? Convert.ToDecimal(listReader["CreditAmount"]) : 0;

                                            if (txType == "Invoice")
                                            {
                                                decimal amount = debit != 0 ? debit : credit;
                                                runningBalance += amount;
                                            }
                                            else if (txType == "Receipt")
                                            {
                                                decimal amount = credit != 0 ? credit : debit;
                                                runningBalance -= amount;
                                            }
                                            else
                                            {
                                                runningBalance += debit;
                                                runningBalance -= credit;
                                            }

                                            rowsToUpdate.Add((txId, runningBalance));
                                        }
                                    }
                                }

                                foreach (var row in rowsToUpdate)
                                {
                                    using (var updCmd = new SQLiteCommand(
                                        "UPDATE CustomerTransactions SET BalanceAfter = @bal WHERE TransactionID = @id", connection, transaction))
                                    {
                                        updCmd.Parameters.AddWithValue("@bal", row.NewBalance);
                                        updCmd.Parameters.AddWithValue("@id", row.Id);
                                        updCmd.ExecuteNonQuery();
                                    }
                                }

                                using (var updBalCmd = new SQLiteCommand(
                                    "UPDATE Customers SET CurrentBalance = @bal, ModifiedDate = @modDate WHERE CustomerID = @id", connection, transaction))
                                {
                                    updBalCmd.Parameters.AddWithValue("@bal", runningBalance);
                                    updBalCmd.Parameters.AddWithValue("@modDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                    updBalCmd.Parameters.AddWithValue("@id", customerId);
                                    updBalCmd.ExecuteNonQuery();
                                }

                                result.AffectedCustomerNames.Add(customerName);
                            }

                            result.CustomersRecalculated = affectedCustomerIds.Count;
                            result.WasNeeded = duplicateTransactionIds.Count > 0;

                            // 4) تسجيل أن الإصلاح تم تنفيذه حتى لا يتكرر تشغيله مرة أخرى مطلقاً
                            using (var markCmd = new SQLiteCommand(@"
                                INSERT INTO SystemSettings (SettingKey, SettingValue, Description, UpdatedDate)
                                VALUES ('DuplicateSalesInvoiceBalanceRepair_v1', 'Done', 'إصلاح باج مضاعفة رصيد العملاء عند فواتير البيع - نُفذ تلقائياً', @date)
                                ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = 'Done', UpdatedDate = @date", connection, transaction))
                            {
                                markCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                markCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ RepairDuplicateSalesInvoiceCustomerBalancesIfNeeded", ex, "DatabaseService");
                System.Diagnostics.Debug.WriteLine($"RepairDuplicateSalesInvoiceCustomerBalancesIfNeeded Error: {ex.Message}");
                throw;
            }

            return result;
        }

        #endregion

        #region 16. تحديث هيكل الجداول (Database Schema Updates)

        private void UpdateDatabaseSchema(SQLiteConnection connection)
        {
            try
            {
                // ✅ إضافة عمود OriginalInvoiceID لجدول SalesInvoices (لازم لربط فاتورة مرتجع البيع
                // بالفاتورة الأصلية اللي تم الإرجاع منها)
                string checkSalesReturnColumn = "PRAGMA table_info(SalesInvoices)";
                bool hasOriginalSalesInvoiceId = false;

                using (var cmd = new SQLiteCommand(checkSalesReturnColumn, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "OriginalInvoiceID")
                        {
                            hasOriginalSalesInvoiceId = true;
                            break;
                        }
                    }
                }

                if (!hasOriginalSalesInvoiceId)
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE SalesInvoices ADD COLUMN OriginalInvoiceID INTEGER", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                // ✅ نفس الإضافة لجدول PurchaseInvoices (لمرتجع المشتريات)
                string checkPurchaseReturnColumn = "PRAGMA table_info(PurchaseInvoices)";
                bool hasOriginalPurchaseInvoiceId = false;

                using (var cmd = new SQLiteCommand(checkPurchaseReturnColumn, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "OriginalInvoiceID")
                        {
                            hasOriginalPurchaseInvoiceId = true;
                            break;
                        }
                    }
                }

                if (!hasOriginalPurchaseInvoiceId)
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE PurchaseInvoices ADD COLUMN OriginalInvoiceID INTEGER", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                string checkBankColumns = "PRAGMA table_info(BankAccounts)";
                bool hasBankName = false;

                using (var cmd = new SQLiteCommand(checkBankColumns, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "BankName")
                        {
                            hasBankName = true;
                            break;
                        }
                    }
                }

                if (!hasBankName)
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE BankAccounts ADD COLUMN BankName TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("UPDATE BankAccounts SET BankName = BankCode WHERE BankName IS NULL OR BankName = ''", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                string checkCustomerColumns = "PRAGMA table_info(Customers)";
                bool hasCustomerName = false;

                using (var cmd = new SQLiteCommand(checkCustomerColumns, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "CustomerName")
                        {
                            hasCustomerName = true;
                            break;
                        }
                    }
                }

                if (!hasCustomerName)
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Customers ADD COLUMN CustomerName TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("UPDATE Customers SET CustomerName = CustomerCode WHERE CustomerName IS NULL OR CustomerName = ''", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                string checkSupplierColumns = "PRAGMA table_info(Suppliers)";
                bool hasSupplierName = false;

                using (var cmd = new SQLiteCommand(checkSupplierColumns, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        if (columnName == "SupplierName")
                        {
                            hasSupplierName = true;
                            break;
                        }
                    }
                }

                if (!hasSupplierName)
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Suppliers ADD COLUMN SupplierName TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("UPDATE Suppliers SET SupplierName = SupplierCode WHERE SupplierName IS NULL OR SupplierName = ''", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating database schema: {ex.Message}");
            }
        }

        private void UpdateChecksTableSchema(SQLiteConnection connection)
        {
            try
            {
                string checkColumns = "PRAGMA table_info(Checks)";
                List<string> existingColumns = new List<string>();

                using (var cmd = new SQLiteCommand(checkColumns, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string columnName = reader["name"].ToString();
                        existingColumns.Add(columnName);
                    }
                }

                if (!existingColumns.Contains("CustomerID"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN CustomerID INTEGER", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN SupplierID INTEGER", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN Description TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("AccountNumber"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN AccountNumber TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("PayeeName"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN PayeeName TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("PayerName"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN PayerName TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("DepositDate"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN DepositDate DATETIME", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("ClearanceDate"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN ClearanceDate DATETIME", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                if (!existingColumns.Contains("BounceReason"))
                {
                    using (var cmd = new SQLiteCommand("ALTER TABLE Checks ADD COLUMN BounceReason TEXT", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating Checks table schema: {ex.Message}");
            }
        }

        #endregion

        #region 17. البيانات الأساسية (Basic Data)

        private void InitializeBasicData(SQLiteConnection connection)
        {
            try
            {
                string checkAccountsQuery = "SELECT COUNT(*) FROM ChartOfAccounts";
                using (var cmd = new SQLiteCommand(checkAccountsQuery, connection))
                {
                    long accountCount = (long)cmd.ExecuteScalar();
                    if (accountCount == 0)
                    {
                        CreateDefaultChartOfAccounts(connection);
                    }
                }

                string checkTreasuryQuery = "SELECT COUNT(*) FROM Treasury";
                using (var cmd = new SQLiteCommand(checkTreasuryQuery, connection))
                {
                    long treasuryCount = (long)cmd.ExecuteScalar();
                    if (treasuryCount == 0)
                    {
                        CreateDefaultTreasury(connection);
                    }
                }

                string checkStoreQuery = "SELECT COUNT(*) FROM Stores WHERE StoreCode = 'MAIN'";
                using (var cmd = new SQLiteCommand(checkStoreQuery, connection))
                {
                    long storeCount = (long)cmd.ExecuteScalar();
                    if (storeCount == 0)
                    {
                        CreateDefaultStore(connection);
                    }
                }

                string checkPeriodsQuery = "SELECT COUNT(*) FROM FinancialPeriods";
                using (var cmd = new SQLiteCommand(checkPeriodsQuery, connection))
                {
                    long periodCount = (long)cmd.ExecuteScalar();
                    if (periodCount == 0)
                    {
                        CreateDefaultFinancialPeriods(connection);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing basic data: {ex.Message}");
            }
        }

        private void CreateDefaultChartOfAccounts(SQLiteConnection connection)
        {
            string insertCategories = @"
                INSERT OR IGNORE INTO AccountCategories (CategoryCode, CategoryNameAr, CategoryNameEn, CategoryType, SortOrder) VALUES
                ('1', 'الأصول', 'Assets', 'Assets', 1),
                ('2', 'الخصوم', 'Liabilities', 'Liabilities', 2),
                ('3', 'حقوق الملكية', 'Equity', 'Equity', 3),
                ('4', 'الإيرادات', 'Revenue', 'Revenue', 4),
                ('5', 'المصروفات', 'Expenses', 'Expenses', 5);
                
                INSERT OR IGNORE INTO ChartOfAccounts (AccountCode, AccountNameAr, AccountNameEn, CategoryID, AccountType, IsDebit, SortOrder) VALUES
                ('1000', 'الصندوق', 'Cash', 1, 'Main', 1, 1),
                ('1100', 'البنوك', 'Banks', 1, 'Main', 1, 2),
                ('1200', 'المخزون', 'Inventory', 1, 'Main', 1, 3),
                ('1300', 'العملاء', 'Customers', 1, 'Main', 1, 4),
                ('1400', 'الموردين', 'Suppliers', 2, 'Main', 0, 5),
                ('1500', 'رأس المال', 'Capital', 3, 'Main', 0, 6),
                ('1600', 'المبيعات', 'Sales Revenue', 4, 'Main', 0, 7),
                ('1700', 'تكلفة المبيعات', 'Cost of Sales', 5, 'Main', 1, 8),
                ('1800', 'المصروفات التشغيلية', 'Operating Expenses', 5, 'Main', 1, 9);";

            using (var cmd = new SQLiteCommand(insertCategories, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateDefaultTreasury(SQLiteConnection connection)
        {
            string insertTreasury = @"
                INSERT OR IGNORE INTO Treasury (TreasuryCode, TreasuryNameAr, TreasuryNameEn, AccountID, IsActive)
                VALUES ('CASH', 'الصندوق الرئيسي', 'Main Cash', (SELECT AccountID FROM ChartOfAccounts WHERE AccountCode = '1000'), 1);";

            using (var cmd = new SQLiteCommand(insertTreasury, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateDefaultStore(SQLiteConnection connection)
        {
            string insertStore = @"
                INSERT OR IGNORE INTO Stores (StoreCode, StoreNameAr, StoreNameEn, StoreType, IsActive)
                VALUES ('MAIN', 'المخزن الرئيسي', 'Main Store', 'Main', 1);";

            using (var cmd = new SQLiteCommand(insertStore, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void CreateDefaultFinancialPeriods(SQLiteConnection connection)
        {
            DateTime currentYear = new DateTime(DateTime.Now.Year, 1, 1);
            string insertPeriods = $@"
                INSERT OR IGNORE INTO FinancialPeriods (PeriodCode, PeriodNameAr, PeriodNameEn, StartDate, EndDate, PeriodType, IsActive)
                VALUES 
                ('{DateTime.Now.Year}', 'السنة المالية {DateTime.Now.Year}', 'Fiscal Year {DateTime.Now.Year}', '{currentYear:yyyy-MM-dd}', '{currentYear.AddYears(1).AddDays(-1):yyyy-MM-dd}', 'Year', 1);";

            using (var cmd = new SQLiteCommand(insertPeriods, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region 18. التحقق من الأرصدة (Validate and Repair Balances)

        private void ValidateAndRepairAllBalances(SQLiteConnection connection)
        {
            try
            {
                string validateCustomers = @"
                    UPDATE Customers SET CurrentBalance = COALESCE((
                        SELECT BalanceAfter FROM CustomerTransactions 
                        WHERE CustomerID = Customers.CustomerID 
                        ORDER BY TransactionID DESC LIMIT 1
                    ), OpeningBalance);";

                using (var cmd = new SQLiteCommand(validateCustomers, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                string validateSuppliers = @"
                    UPDATE Suppliers SET CurrentBalance = COALESCE((
                        SELECT BalanceAfter FROM SupplierTransactions 
                        WHERE SupplierID = Suppliers.SupplierID 
                        ORDER BY TransactionID DESC LIMIT 1
                    ), OpeningBalance);";

                using (var cmd = new SQLiteCommand(validateSuppliers, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                string validateAccounts = @"
                    UPDATE ChartOfAccounts SET CurrentBalance = COALESCE((
                        SELECT SUM(DebitAmount) - SUM(CreditAmount) FROM JournalEntryDetails 
                        WHERE AccountID = ChartOfAccounts.AccountID
                    ), OpeningBalance);";

                using (var cmd = new SQLiteCommand(validateAccounts, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating balances: {ex.Message}");
            }
        }

        #endregion

        #region 18.5 استرجاع حركات الفواتير المفقودة من كشوف الحسابات (Backfill Missing Invoice Transactions)

        // خلفية المشكلة: تسجيل حركة "فاتورة" في CustomerTransactions/SupplierTransactions كان يعتمد
        // تاريخيًا على تريجرات AFTER INSERT (trig_customer_transaction_from_sales /
        // trig_supplier_transaction_from_purchase). أي فاتورة اتسجّلت في SalesInvoices/PurchaseInvoices
        // قبل ما التريجر ده يبقى موجود فعليًا في قاعدة البيانات (نسخة قديمة من البرنامج، أو قبل إضافة
        // منطق التسجيل اليدوي المرافق له في InvoiceService) بقت من غير أي حركة مقابلة في كشف الحساب -
        // فمابتظهرش في الكشف، ورصيد العميل/المورد الحالي (اللي بيتحسب من آخر حركة مسجَّلة في
        // ValidateAndRepairAllBalances فوق) بيبقى ناقص المبلغ ده كمان.
        //
        // الدالة دي بتشتغل تلقائيًا مع كل تشغيل للبرنامج (زي ValidateAndRepairAllBalances تمامًا)،
        // مفيهاش أي واجهة مستخدم أو زرار: بتدور على أي فاتورة نشطة (IsVoid = 0) مالهاش حركة مقابلة،
        // تضيفها، وبعدين تعيد حساب الرصيد التراكمي بالتسلسل الزمني الصحيح لكل العملاء/الموردين
        // عشان الرصيد الحالي المحفوظ يفضل مطابق تمامًا لمجموع الحركات الحقيقية. لو مفيش أي فاتورة
        // ناقصة، الدالة مابتعملش أي تعديل خالص (عملية آمنة تتكرر كل مرة من غير أي أثر جانبي).

        private void BackfillMissingInvoiceTransactions(SQLiteConnection connection)
        {
            try
            {
                BackfillMissingCustomerInvoiceTransactions(connection);
                BackfillMissingSupplierPurchaseTransactions(connection);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BackfillMissingInvoiceTransactions Error: {ex.Message}");
            }
        }

        private void BackfillMissingCustomerInvoiceTransactions(SQLiteConnection connection)
        {
            string insertMissing = @"
                INSERT INTO CustomerTransactions (
                    CustomerID, TransactionDate, TransactionType,
                    DebitAmount, CreditAmount, BalanceAfter,
                    ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                )
                SELECT
                    si.CustomerID, si.InvoiceDate, 'Invoice',
                    si.TotalAmount, 0, 0,
                    'SALES_INVOICE', si.InvoiceID, si.InvoiceNumber, si.Notes, si.CreatedBy, CURRENT_TIMESTAMP
                FROM SalesInvoices si
                WHERE si.InvoiceType = 'Sales'
                  AND si.CustomerID IS NOT NULL
                  AND si.IsVoid = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM CustomerTransactions ct
                      WHERE ct.ReferenceType = 'SALES_INVOICE'
                        AND ct.ReferenceID = si.InvoiceID
                        AND ct.TransactionType = 'Invoice'
                  );";

            int insertedCount;
            using (var cmd = new SQLiteCommand(insertMissing, connection))
            {
                insertedCount = cmd.ExecuteNonQuery();
            }

            if (insertedCount == 0) return;

            System.Diagnostics.Debug.WriteLine($"🔧 تم استرجاع {insertedCount} حركة فاتورة بيع كانت مفقودة من كشوف حسابات العملاء");
            RecalculateAllCustomerRunningBalances(connection);
        }

        private void BackfillMissingSupplierPurchaseTransactions(SQLiteConnection connection)
        {
            string insertMissing = @"
                INSERT INTO SupplierTransactions (
                    SupplierID, TransactionDate, TransactionType,
                    DebitAmount, CreditAmount, BalanceAfter,
                    ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                )
                SELECT
                    pi.SupplierID, pi.InvoiceDate, 'Purchase',
                    0, pi.TotalAmount, 0,
                    'PURCHASE_INVOICE', pi.InvoiceID, pi.InvoiceNumber, pi.Notes, pi.CreatedBy, CURRENT_TIMESTAMP
                FROM PurchaseInvoices pi
                WHERE pi.InvoiceType = 'Purchase'
                  AND pi.SupplierID IS NOT NULL
                  AND pi.IsVoid = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM SupplierTransactions st
                      WHERE st.ReferenceType = 'PURCHASE_INVOICE'
                        AND st.ReferenceID = pi.InvoiceID
                        AND st.TransactionType = 'Purchase'
                  );";

            int insertedCount;
            using (var cmd = new SQLiteCommand(insertMissing, connection))
            {
                insertedCount = cmd.ExecuteNonQuery();
            }

            if (insertedCount == 0) return;

            System.Diagnostics.Debug.WriteLine($"🔧 تم استرجاع {insertedCount} حركة فاتورة مشتريات كانت مفقودة من كشوف حسابات الموردين");
            RecalculateAllSupplierRunningBalances(connection);
        }

        // إعادة حساب BalanceAfter بالتسلسل الزمني الصحيح (نفس صيغة الحساب المستخدمة فعليًا
        // في شاشة كشف حساب العميل: فاتورة = رصيد + مدين، تحصيل = رصيد - دائن، أي نوع تاني = مدين
        // يزوّد ودائن يقلّل) بدءًا من رصيد أول المدة، لكل عميل عنده أي حركة على الإطلاق.
        // صفوف مساعدة بسيطة (كلاسات عادية بدل ValueTuple) عشان نضمن التوافق مع أي إصدار
        // C# مُستخدَم في بناء المشروع من غير أي اعتماد إضافي.
        private class PartyOpeningBalance
        {
            public int PartyId;
            public decimal OpeningBalance;
        }

        private class LedgerRow
        {
            public int TransactionId;
            public string TransactionType;
            public decimal Debit;
            public decimal Credit;
        }

        private void RecalculateAllCustomerRunningBalances(SQLiteConnection connection)
        {
            var customers = new List<PartyOpeningBalance>();
            using (var cmd = new SQLiteCommand("SELECT CustomerID, COALESCE(OpeningBalance, 0) FROM Customers", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    customers.Add(new PartyOpeningBalance { PartyId = reader.GetInt32(0), OpeningBalance = reader.GetDecimal(1) });
                }
            }

            foreach (var customer in customers)
            {
                var rows = new List<LedgerRow>();
                using (var cmd = new SQLiteCommand(@"
                    SELECT TransactionID, TransactionType, DebitAmount, CreditAmount
                    FROM CustomerTransactions
                    WHERE CustomerID = @customerId
                    ORDER BY TransactionDate ASC, TransactionID ASC", connection))
                {
                    cmd.Parameters.AddWithValue("@customerId", customer.PartyId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new LedgerRow
                            {
                                TransactionId = reader.GetInt32(0),
                                TransactionType = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Debit = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                                Credit = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
                            });
                        }
                    }
                }

                if (rows.Count == 0) continue;

                decimal runningBalance = customer.OpeningBalance;
                using (var updateCmd = new SQLiteCommand(
                    "UPDATE CustomerTransactions SET BalanceAfter = @balance WHERE TransactionID = @id", connection))
                {
                    updateCmd.Parameters.Add("@balance", System.Data.DbType.Decimal);
                    updateCmd.Parameters.Add("@id", System.Data.DbType.Int32);

                    foreach (var row in rows)
                    {
                        if (row.TransactionType == "Invoice")
                        {
                            decimal amount = row.Debit != 0 ? row.Debit : row.Credit;
                            runningBalance += amount;
                        }
                        else if (row.TransactionType == "Receipt")
                        {
                            decimal amount = row.Credit != 0 ? row.Credit : row.Debit;
                            runningBalance -= amount;
                        }
                        else
                        {
                            runningBalance += row.Debit;
                            runningBalance -= row.Credit;
                        }

                        updateCmd.Parameters["@balance"].Value = runningBalance;
                        updateCmd.Parameters["@id"].Value = row.TransactionId;
                        updateCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // نفس فكرة RecalculateAllCustomerRunningBalances بالظبط لكن بصيغة الحساب الخاصة بالموردين
        // (فاتورة مشتريات = رصيد + دائن، سند صرف = رصيد - مدين، أي نوع تاني = دائن يزوّد ومدين يقلّل).
        private void RecalculateAllSupplierRunningBalances(SQLiteConnection connection)
        {
            var suppliers = new List<PartyOpeningBalance>();
            using (var cmd = new SQLiteCommand("SELECT SupplierID, COALESCE(OpeningBalance, 0) FROM Suppliers", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    suppliers.Add(new PartyOpeningBalance { PartyId = reader.GetInt32(0), OpeningBalance = reader.GetDecimal(1) });
                }
            }

            foreach (var supplier in suppliers)
            {
                var rows = new List<LedgerRow>();
                using (var cmd = new SQLiteCommand(@"
                    SELECT TransactionID, TransactionType, DebitAmount, CreditAmount
                    FROM SupplierTransactions
                    WHERE SupplierID = @supplierId
                    ORDER BY TransactionDate ASC, TransactionID ASC", connection))
                {
                    cmd.Parameters.AddWithValue("@supplierId", supplier.PartyId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new LedgerRow
                            {
                                TransactionId = reader.GetInt32(0),
                                TransactionType = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Debit = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                                Credit = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
                            });
                        }
                    }
                }

                if (rows.Count == 0) continue;

                decimal runningBalance = supplier.OpeningBalance;
                using (var updateCmd = new SQLiteCommand(
                    "UPDATE SupplierTransactions SET BalanceAfter = @balance WHERE TransactionID = @id", connection))
                {
                    updateCmd.Parameters.Add("@balance", System.Data.DbType.Decimal);
                    updateCmd.Parameters.Add("@id", System.Data.DbType.Int32);

                    foreach (var row in rows)
                    {
                        if (row.TransactionType == "Purchase")
                        {
                            decimal amount = row.Credit != 0 ? row.Credit : row.Debit;
                            runningBalance += amount;
                        }
                        else if (row.TransactionType == "Payment")
                        {
                            decimal amount = row.Debit != 0 ? row.Debit : row.Credit;
                            runningBalance -= amount;
                        }
                        else
                        {
                            runningBalance -= row.Debit;
                            runningBalance += row.Credit;
                        }

                        updateCmd.Parameters["@balance"].Value = runningBalance;
                        updateCmd.Parameters["@id"].Value = row.TransactionId;
                        updateCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        #endregion

        // ==================== دوال المستخدمين الأساسية ====================

        public bool ValidateUser(string username, string password)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT PasswordHash FROM Users WHERE Username = @user AND IsActive = 1";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@user", username);
                    object result = cmd.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                        return false;

                    string storedHash = result.ToString();
                    return PasswordHelper.VerifyPassword(password, storedHash);
                }
            }
        }

        public bool HasAnyUser()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        public bool CreateFirstUser(string username, string password)
        {
            string hash = PasswordHelper.HashPassword(password);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO Users (Username, PasswordHash, FullName, UserRole, IsActive, CreatedDate, LastLoginDate)
                    VALUES (@user, @hash, @user, 'مدير النظام', 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@hash", hash);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ==================== الدوال غير المتزامنة (Async) ====================

        public async Task<List<string>> GetActiveUsernamesAsync()
        {
            var usernames = new List<string>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT Username FROM Users WHERE IsActive = 1 ORDER BY Username";

                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        usernames.Add(reader.GetString(0));
                    }
                }
            }

            return usernames;
        }

        public async Task<UserData> ValidateUserAsync(string username, string password)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT UserID, Username, PasswordHash, FullName, UserRole, 
                           COALESCE(Email, '') as Email, 
                           COALESCE(Phone, '') as Phone
                    FROM Users 
                    WHERE Username = @user AND IsActive = 1";

                UserData user = null;
                string storedHash = null;

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@user", username);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            storedHash = reader.GetString(2);

                            user = new UserData
                            {
                                UserID = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                PasswordHash = storedHash,
                                FullName = reader.GetString(3),
                                UserRole = reader.GetString(4),
                                Email = reader.GetString(5),
                                Phone = reader.GetString(6)
                            };
                        }
                    }
                }

                if (user == null)
                    return null;

                // ✅ التحقق الآمن من كلمة المرور (يدعم الصيغة الجديدة PBKDF2 والصيغة القديمة SHA256 تلقائياً)
                if (!PasswordHelper.VerifyPassword(password, storedHash))
                    return null;

                // ✅ ترقية تلقائية وصامتة: لو كانت كلمة المرور المخزَّنة بالصيغة القديمة غير الآمنة،
                // نعيد تشفيرها الآن بالمعيار الجديد الآمن (PBKDF2 + Salt) فور نجاح تسجيل الدخول،
                // بدون أي إزعاج أو تدخل مطلوب من المستخدم.
                if (PasswordHelper.IsLegacyHash(storedHash))
                {
                    try
                    {
                        string upgradedHash = PasswordHelper.HashPassword(password);
                        string upgradeSql = "UPDATE Users SET PasswordHash = @hash WHERE UserID = @id";

                        using (var upgradeCmd = new SQLiteCommand(upgradeSql, connection))
                        {
                            upgradeCmd.Parameters.AddWithValue("@hash", upgradedHash);
                            upgradeCmd.Parameters.AddWithValue("@id", user.UserID);
                            await upgradeCmd.ExecuteNonQueryAsync();
                        }

                        user.PasswordHash = upgradedHash;
                        System.Diagnostics.Debug.WriteLine($"🔒 تم ترقية تشفير كلمة مرور المستخدم '{username}' تلقائياً إلى معيار أكثر أماناً (PBKDF2)");
                    }
                    catch (Exception upgradeEx)
                    {
                        // فشل الترقية لا يجب أن يمنع تسجيل الدخول - المستخدم بالفعل تحقق بنجاح
                        System.Diagnostics.Debug.WriteLine($"⚠️ تعذّرت ترقية تشفير كلمة مرور المستخدم '{username}': {upgradeEx.Message}");
                    }
                }

                return user;
            }
        }

        public async Task<bool> UpdateLastLoginAsync(int userId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "UPDATE Users SET LastLoginDate = CURRENT_TIMESTAMP WHERE UserID = @userId";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<bool> HasAnyUserAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Users WHERE IsActive = 1";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Users WHERE Username = @username";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public async Task<bool> CreateUserAsync(string username, string password, string fullName, string email, string phone, string userRole)
        {
            if (await UserExistsAsync(username))
                return false;

            string hash = PasswordHelper.HashPassword(password);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

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
            }
        }

        public async Task<bool> CreateFirstUserAsync(string username, string password, string fullName)
        {
            string hash = PasswordHelper.HashPassword(password);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    INSERT INTO Users (Username, PasswordHash, FullName, UserRole, IsActive, CreatedDate, LastLoginDate)
                    VALUES (@user, @hash, @fullName, 'مدير النظام', 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.Parameters.AddWithValue("@fullName", fullName);

                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<UserData> GetUserByUsernameAsync(string username)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT UserID, Username, PasswordHash, FullName, UserRole, 
                           COALESCE(Email, '') as Email, 
                           COALESCE(Phone, '') as Phone
                    FROM Users 
                    WHERE Username = @username AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@username", username);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new UserData
                            {
                                UserID = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                PasswordHash = reader.GetString(2),
                                FullName = reader.GetString(3),
                                UserRole = reader.GetString(4),
                                Email = reader.GetString(5),
                                Phone = reader.GetString(6)
                            };
                        }
                    }
                }
            }

            return null;
        }

        // ==================== دوال التصنيفات (Categories) ====================

        public async Task<List<CategoryItem>> GetCategoriesAsync()
        {
            var categories = new List<CategoryItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT CategoryID, CategoryCode, CategoryNameAr, CategoryNameEn FROM ProductCategories WHERE IsActive = 1 ORDER BY CategoryNameAr";

                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        categories.Add(new CategoryItem
                        {
                            CategoryID = reader.GetInt32(0),
                            CategoryCode = reader.GetString(1),
                            CategoryNameAr = reader.GetString(2),
                            CategoryNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }

            return categories;
        }

        // ==================== دوال المنتجات (Products) ====================

        public async Task<List<ProductItem>> GetProductsAsync(string searchText = "")
        {
            var products = new List<ProductItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT 
                p.ProductID, 
                p.ProductCode, 
                p.ProductNameAr, 
                p.ProductNameEn, 
                p.CategoryID, 
                p.Unit,
                p.Barcode,
                p.CostPrice, 
                p.SalePrice,
                p.WholesalePrice,
                p.MinimumQuantity,
                p.MaximumQuantity,
                p.ReorderLevel,
                p.IsActive,
                pc.CategoryNameAr as CategoryName,
                -- ✅ الأعمدة الجديدة لنظام الوحدات الثلاثة
                COALESCE(p.Unit1, '') as Unit1,
                COALESCE(p.Unit2, '') as Unit2,
                COALESCE(p.Unit3, '') as Unit3,
                COALESCE(p.Unit1Factor, 1) as Unit1Factor,
                COALESCE(p.Unit2Factor, 1) as Unit2Factor,
                COALESCE(p.Price1, 0) as Price1,
                COALESCE(p.Price2, 0) as Price2,
                COALESCE(p.Price3, 0) as Price3,
                COALESCE(p.ReorderUnit, '') as ReorderUnit,
                COALESCE(p.ReorderLevelInBaseUnit, 0) as ReorderLevelInBaseUnit,
                COALESCE(p.QuantityInBaseUnit, 0) as QuantityInBaseUnit,
                COALESCE(si.Quantity, 0) as CurrentQuantity
            FROM Products p
            LEFT JOIN ProductCategories pc ON p.CategoryID = pc.CategoryID
            LEFT JOIN StoreInventory si ON p.ProductID = si.ProductID AND si.StoreID = (SELECT StoreID FROM Stores WHERE StoreCode = 'MAIN' AND IsActive = 1 LIMIT 1)
            WHERE p.IsActive = 1";

                if (!string.IsNullOrEmpty(searchText))
                {
                    sql += " AND (p.ProductCode LIKE @searchText OR p.ProductNameAr LIKE @searchText OR p.ProductNameEn LIKE @searchText OR p.Barcode LIKE @searchText)";
                }

                sql += " ORDER BY p.ProductCode";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        cmd.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var product = new ProductItem
                            {
                                ProductID = reader.GetInt32(0),
                                ProductCode = reader.GetString(1),
                                ProductNameAr = reader.GetString(2),
                                ProductNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                CategoryID = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                Unit = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Barcode = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                CostPrice = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                SalePrice = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                WholesalePrice = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                                MinimumQuantity = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                MaximumQuantity = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                                ReorderLevel = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                IsActive = reader.GetInt32(13) == 1,
                                CategoryName = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                // ✅ الأعمدة الجديدة
                                Unit1 = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                Unit2 = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                Unit3 = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                Unit1Factor = reader.IsDBNull(18) ? 1 : reader.GetInt32(18),
                                Unit2Factor = reader.IsDBNull(19) ? 1 : reader.GetInt32(19),
                                Price1 = reader.IsDBNull(20) ? 0 : reader.GetDecimal(20),
                                Price2 = reader.IsDBNull(21) ? 0 : reader.GetDecimal(21),
                                Price3 = reader.IsDBNull(22) ? 0 : reader.GetDecimal(22),
                                ReorderUnit = reader.IsDBNull(23) ? "" : reader.GetString(23),
                                ReorderLevelInBaseUnit = reader.IsDBNull(24) ? 0 : reader.GetInt32(24),
                                QuantityInBaseUnit = reader.IsDBNull(25) ? 0 : reader.GetInt64(25),
                                CurrentQuantity = reader.IsDBNull(26) ? 0 : reader.GetDecimal(26)
                            };

                            products.Add(product);
                        }
                    }
                }
            }

            return products;
        }

        public async Task<ProductItem> GetProductByIdAsync(int productId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT 
                ProductID, 
                ProductCode, 
                ProductNameAr, 
                ProductNameEn, 
                CategoryID, 
                Unit,
                Barcode,
                CostPrice, 
                SalePrice, 
                WholesalePrice, 
                MinimumQuantity, 
                MaximumQuantity, 
                ReorderLevel,
                -- ✅ الأعمدة الجديدة لنظام الوحدات الثلاثة
                COALESCE(Unit1, '') as Unit1,
                COALESCE(Unit2, '') as Unit2,
                COALESCE(Unit3, '') as Unit3,
                COALESCE(Unit1Factor, 1) as Unit1Factor,
                COALESCE(Unit2Factor, 1) as Unit2Factor,
                COALESCE(Price1, 0) as Price1,
                COALESCE(Price2, 0) as Price2,
                COALESCE(Price3, 0) as Price3,
                COALESCE(ReorderUnit, '') as ReorderUnit,
                COALESCE(ReorderLevelInBaseUnit, 0) as ReorderLevelInBaseUnit,
                COALESCE(QuantityInBaseUnit, 0) as QuantityInBaseUnit
            FROM Products 
            WHERE ProductID = @id AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", productId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ProductItem
                            {
                                // الأعمدة الأساسية (0-12)
                                ProductID = reader.GetInt32(0),
                                ProductCode = reader.GetString(1),
                                ProductNameAr = reader.GetString(2),
                                ProductNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                CategoryID = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                Unit = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Barcode = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                CostPrice = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                SalePrice = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                WholesalePrice = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                                MinimumQuantity = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                MaximumQuantity = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                                ReorderLevel = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),

                                // ✅ الأعمدة الجديدة (13-23)
                                Unit1 = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                Unit2 = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                Unit3 = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                Unit1Factor = reader.IsDBNull(16) ? 1 : reader.GetInt32(16),
                                Unit2Factor = reader.IsDBNull(17) ? 1 : reader.GetInt32(17),
                                Price1 = reader.IsDBNull(18) ? 0 : reader.GetDecimal(18),
                                Price2 = reader.IsDBNull(19) ? 0 : reader.GetDecimal(19),
                                Price3 = reader.IsDBNull(20) ? 0 : reader.GetDecimal(20),
                                ReorderUnit = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                ReorderLevelInBaseUnit = reader.IsDBNull(22) ? 0 : reader.GetInt32(22),
                                QuantityInBaseUnit = reader.IsDBNull(23) ? 0 : reader.GetInt64(23)
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<bool> AddProductAsync(ProductItem product)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                INSERT INTO Products (
                    ProductCode, ProductNameAr, ProductNameEn, CategoryID, Unit,
                    Barcode, CostPrice, SalePrice, WholesalePrice,
                    MinimumQuantity, MaximumQuantity, ReorderLevel,
                    IsActive, CreatedDate, CreatedBy,
                    -- ✅ الأعمدة الجديدة
                    Unit1, Unit2, Unit3,
                    Unit1Factor, Unit2Factor,
                    Price1, Price2, Price3,
                    ReorderUnit, ReorderLevelInBaseUnit,
                    QuantityInBaseUnit
                ) VALUES (
                    @ProductCode, @ProductNameAr, @ProductNameEn, @CategoryID, @Unit,
                    @Barcode, @CostPrice, @SalePrice, @WholesalePrice,
                    @MinimumQuantity, @MaximumQuantity, @ReorderLevel,
                    1, CURRENT_TIMESTAMP, @CreatedBy,
                    @Unit1, @Unit2, @Unit3,
                    @Unit1Factor, @Unit2Factor,
                    @Price1, @Price2, @Price3,
                    @ReorderUnit, @ReorderLevelInBaseUnit,
                    @QuantityInBaseUnit
                )";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@ProductCode", product.ProductCode);
                        cmd.Parameters.AddWithValue("@ProductNameAr", product.ProductNameAr);
                        cmd.Parameters.AddWithValue("@ProductNameEn", product.ProductNameEn ?? "");
                        cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID > 0 ? product.CategoryID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Unit", product.Unit ?? "");
                        cmd.Parameters.AddWithValue("@Barcode", product.Barcode ?? "");
                        cmd.Parameters.AddWithValue("@CostPrice", product.CostPrice);
                        cmd.Parameters.AddWithValue("@SalePrice", product.SalePrice);
                        cmd.Parameters.AddWithValue("@WholesalePrice", product.WholesalePrice);
                        cmd.Parameters.AddWithValue("@MinimumQuantity", product.MinimumQuantity);
                        cmd.Parameters.AddWithValue("@MaximumQuantity", product.MaximumQuantity);
                        cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
                        cmd.Parameters.AddWithValue("@CreatedBy", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                        // ✅ الأعمدة الجديدة
                        cmd.Parameters.AddWithValue("@Unit1", product.Unit1 ?? "");
                        cmd.Parameters.AddWithValue("@Unit2", product.Unit2 ?? "");
                        cmd.Parameters.AddWithValue("@Unit3", product.Unit3 ?? "");
                        cmd.Parameters.AddWithValue("@Unit1Factor", product.Unit1Factor > 0 ? product.Unit1Factor : 1);
                        cmd.Parameters.AddWithValue("@Unit2Factor", product.Unit2Factor > 0 ? product.Unit2Factor : 1);
                        cmd.Parameters.AddWithValue("@Price1", product.Price1);
                        cmd.Parameters.AddWithValue("@Price2", product.Price2);
                        cmd.Parameters.AddWithValue("@Price3", product.Price3);
                        cmd.Parameters.AddWithValue("@ReorderUnit", product.ReorderUnit ?? "");
                        cmd.Parameters.AddWithValue("@ReorderLevelInBaseUnit", product.ReorderLevelInBaseUnit);
                        cmd.Parameters.AddWithValue("@QuantityInBaseUnit", product.QuantityInBaseUnit);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddProductAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateProductAsync(ProductItem product)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE Products SET 
                    ProductCode = @ProductCode,
                    ProductNameAr = @ProductNameAr,
                    ProductNameEn = @ProductNameEn,
                    CategoryID = @CategoryID,
                    Unit = @Unit,
                    Barcode = @Barcode,
                    CostPrice = @CostPrice,
                    SalePrice = @SalePrice,
                    WholesalePrice = @WholesalePrice,
                    MinimumQuantity = @MinimumQuantity,
                    MaximumQuantity = @MaximumQuantity,
                    ReorderLevel = @ReorderLevel,
                    ModifiedDate = CURRENT_TIMESTAMP,
                    -- ✅ الأعمدة الجديدة
                    Unit1 = @Unit1,
                    Unit2 = @Unit2,
                    Unit3 = @Unit3,
                    Unit1Factor = @Unit1Factor,
                    Unit2Factor = @Unit2Factor,
                    Price1 = @Price1,
                    Price2 = @Price2,
                    Price3 = @Price3,
                    ReorderUnit = @ReorderUnit,
                    ReorderLevelInBaseUnit = @ReorderLevelInBaseUnit,
                    QuantityInBaseUnit = @QuantityInBaseUnit
                WHERE ProductID = @ProductID";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@ProductCode", product.ProductCode);
                        cmd.Parameters.AddWithValue("@ProductNameAr", product.ProductNameAr);
                        cmd.Parameters.AddWithValue("@ProductNameEn", product.ProductNameEn ?? "");
                        cmd.Parameters.AddWithValue("@CategoryID", product.CategoryID > 0 ? product.CategoryID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Unit", product.Unit ?? "");
                        cmd.Parameters.AddWithValue("@Barcode", product.Barcode ?? "");
                        cmd.Parameters.AddWithValue("@CostPrice", product.CostPrice);
                        cmd.Parameters.AddWithValue("@SalePrice", product.SalePrice);
                        cmd.Parameters.AddWithValue("@WholesalePrice", product.WholesalePrice);
                        cmd.Parameters.AddWithValue("@MinimumQuantity", product.MinimumQuantity);
                        cmd.Parameters.AddWithValue("@MaximumQuantity", product.MaximumQuantity);
                        cmd.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
                        cmd.Parameters.AddWithValue("@ProductID", product.ProductID);

                        // ✅ الأعمدة الجديدة
                        cmd.Parameters.AddWithValue("@Unit1", product.Unit1 ?? "");
                        cmd.Parameters.AddWithValue("@Unit2", product.Unit2 ?? "");
                        cmd.Parameters.AddWithValue("@Unit3", product.Unit3 ?? "");
                        cmd.Parameters.AddWithValue("@Unit1Factor", product.Unit1Factor > 0 ? product.Unit1Factor : 1);
                        cmd.Parameters.AddWithValue("@Unit2Factor", product.Unit2Factor > 0 ? product.Unit2Factor : 1);
                        cmd.Parameters.AddWithValue("@Price1", product.Price1);
                        cmd.Parameters.AddWithValue("@Price2", product.Price2);
                        cmd.Parameters.AddWithValue("@Price3", product.Price3);
                        cmd.Parameters.AddWithValue("@ReorderUnit", product.ReorderUnit ?? "");
                        cmd.Parameters.AddWithValue("@ReorderLevelInBaseUnit", product.ReorderLevelInBaseUnit);
                        cmd.Parameters.AddWithValue("@QuantityInBaseUnit", product.QuantityInBaseUnit);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateProductAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "UPDATE Products SET IsActive = 0 WHERE ProductID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", productId);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting product: {ex.Message}");
                return false;
            }
        }

        // ==================== دوال العملاء (Customers) ====================

        public async Task<List<CustomerItem>> GetCustomersAsync(string searchText = "")
        {
            var customers = new List<CustomerItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT 
                        CustomerID, CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                        AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                        Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                        ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate
                    FROM Customers 
                    WHERE IsActive = 1";

                if (!string.IsNullOrEmpty(searchText))
                {
                    sql += " AND (CustomerCode LIKE @search OR CustomerNameAr LIKE @search OR CustomerName LIKE @search OR Phone LIKE @search OR Mobile LIKE @search)";
                }

                sql += " ORDER BY CustomerCode";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var customer = new CustomerItem
                            {
                                CustomerID = reader.GetInt32(0),
                                CustomerCode = reader.GetString(1),
                                CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                CustomerNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                CustomerNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                IsActive = reader.GetInt32(20) == 1,
                                Notes = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                CreatedDate = reader.IsDBNull(22) ? DateTime.Now : reader.GetDateTime(22)
                            };

                            if (customer.AccountID > 0)
                            {
                                string accountSql = "SELECT AccountNameAr FROM ChartOfAccounts WHERE AccountID = @accountId";
                                using (var accountCmd = new SQLiteCommand(accountSql, connection))
                                {
                                    accountCmd.Parameters.AddWithValue("@accountId", customer.AccountID);
                                    object accountName = await accountCmd.ExecuteScalarAsync();
                                    if (accountName != null)
                                    {
                                        customer.AccountName = accountName.ToString();
                                    }
                                }
                            }

                            customers.Add(customer);
                        }
                    }
                }
            }

            return customers;
        }

        public async Task<CustomerItem> GetCustomerByIdAsync(int customerId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT 
                        CustomerID, CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                        AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                        Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                        ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate
                    FROM Customers 
                    WHERE CustomerID = @id AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", customerId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var customer = new CustomerItem
                            {
                                CustomerID = reader.GetInt32(0),
                                CustomerCode = reader.GetString(1),
                                CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                CustomerNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                CustomerNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                IsActive = reader.GetInt32(20) == 1,
                                Notes = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                CreatedDate = reader.IsDBNull(22) ? DateTime.Now : reader.GetDateTime(22)
                            };

                            if (customer.AccountID > 0)
                            {
                                string accountSql = "SELECT AccountNameAr FROM ChartOfAccounts WHERE AccountID = @accountId";
                                using (var accountCmd = new SQLiteCommand(accountSql, connection))
                                {
                                    accountCmd.Parameters.AddWithValue("@accountId", customer.AccountID);
                                    object accountName = await accountCmd.ExecuteScalarAsync();
                                    if (accountName != null)
                                    {
                                        customer.AccountName = accountName.ToString();
                                    }
                                }
                            }

                            return customer;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<string> DebugCustomerBalanceAsync(int customerId)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice' بينما هذه الحركات
                    // تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync)، فكان
                    // "TotalSales" هنا يظهر صفر دائمًا مهما كان عدد فواتير العميل.
                    string sql = @"
                SELECT 
                    c.CustomerID,
                    c.CustomerCode,
                    c.OpeningBalance,
                    c.CurrentBalance as BalanceInCustomersTable,
                    COALESCE((
                        SELECT SUM(DebitAmount) FROM CustomerTransactions 
                        WHERE CustomerID = c.CustomerID AND TransactionType = 'Invoice'
                    ), 0) as TotalSales,
                    COALESCE((
                        SELECT SUM(CreditAmount) FROM CustomerTransactions 
                        WHERE CustomerID = c.CustomerID AND TransactionType = 'Receipt'
                    ), 0) as TotalReceipts,
                    c.OpeningBalance + 
                        COALESCE((SELECT SUM(DebitAmount) FROM CustomerTransactions WHERE CustomerID = c.CustomerID AND TransactionType = 'Invoice'), 0) -
                        COALESCE((SELECT SUM(CreditAmount) FROM CustomerTransactions WHERE CustomerID = c.CustomerID AND TransactionType = 'Receipt'), 0) as CalculatedBalance
                FROM Customers c
                WHERE c.CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return $@"
╔══════════════════════════════════════════════════════════════════╗
║                    تشخيص رصيد العميل                            ║
╠══════════════════════════════════════════════════════════════════╣
║ CustomerID:              {reader.GetInt32(0),-35} ║
║ CustomerCode:            {reader.GetString(1),-35} ║
║ OpeningBalance:          {reader.GetDecimal(2),-35:N2} ║
║ Balance in Customers Table: {reader.GetDecimal(3),-35:N2} ║
║ Total Sales (Invoice Debit): {reader.GetDecimal(4),-35:N2} ║
║ Total Receipts (Receipt Credit): {reader.GetDecimal(5),-35:N2} ║
║ Calculated Balance:      {reader.GetDecimal(6),-35:N2} ║
╠══════════════════════════════════════════════════════════════════╣
║ ملاحظة: الرصيد = OpeningBalance + المبيعات - التحصيلات          ║
╚══════════════════════════════════════════════════════════════════╝";
                            }
                        }
                    }
                }
                return "❌ لم يتم العثور على العميل";
            }
            catch (Exception ex)
            {
                return $"❌ خطأ في التشخيص: {ex.Message}";
            }
        }
        public async Task<bool> CustomerCodeExistsAsync(string customerCode, int excludeCustomerId = 0)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Customers WHERE CustomerCode = @code AND IsActive = 1 AND CustomerID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@code", customerCode);
                    cmd.Parameters.AddWithValue("@excludeId", excludeCustomerId);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// تحديث رصيد عميل محدد في جدول العملاء
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice' بدل DebitAmount (راجع
        /// AddCustomerInvoiceTransactionAsync)، فكانت تُصفِّر مساهمة فواتير العميل من رصيده.
        /// </summary>
        public async Task<bool> UpdateCustomerBalanceInTableAsync(int customerId)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE Customers 
                SET CurrentBalance = COALESCE((
                    SELECT OpeningBalance + 
                        COALESCE(SUM(
                            CASE 
                                WHEN TransactionType = 'Invoice' THEN DebitAmount
                                WHEN TransactionType = 'Receipt' THEN -CreditAmount
                                WHEN TransactionType = 'Refund' THEN -CreditAmount
                                ELSE 0
                            END
                        ), 0)
                    FROM CustomerTransactions 
                    WHERE CustomerTransactions.CustomerID = Customers.CustomerID
                ), OpeningBalance)
                WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCustomerBalanceInTableAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// إعادة حساب جميع أرصدة العملاء
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice' بدل DebitAmount (راجع
        /// AddCustomerInvoiceTransactionAsync)، فكانت تُصفِّر مساهمة فواتير كل العملاء من أرصدتهم.
        /// </summary>
        public async Task<int> UpdateAllCustomersBalancesAsync()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE Customers 
                SET CurrentBalance = COALESCE((
                    SELECT OpeningBalance + 
                        COALESCE(SUM(
                            CASE 
                                WHEN TransactionType = 'Invoice' THEN DebitAmount
                                WHEN TransactionType = 'Receipt' THEN -CreditAmount
                                WHEN TransactionType = 'Refund' THEN -CreditAmount
                                ELSE 0
                            END
                        ), 0)
                    FROM CustomerTransactions 
                    WHERE CustomerTransactions.CustomerID = Customers.CustomerID
                ), OpeningBalance)";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        return await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAllCustomersBalancesAsync Error: {ex.Message}");
                return 0;
            }
        }

        public async Task<string> GenerateUniqueCustomerCodeAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(CustomerCode, 5) AS INTEGER)) FROM Customers WHERE CustomerCode LIKE 'CUS-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"CUS-{nextNumber:D6}";
                }
            }
        }

        public async Task<bool> AddCustomerAsync(CustomerItem customer)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO Customers (
                            CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                            AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                            Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                            ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate, CreatedBy
                        ) VALUES (
                            @code, @name, @nameAr, @nameEn, @accountId, @openingBalance, @currentBalance,
                            @creditLimit, @paymentTerms, @phone, @mobile, @fax, @email, @website,
                            @address, @taxNumber, @commercialRegister, @contactPerson, @contactPersonPhone,
                            1, @notes, CURRENT_TIMESTAMP, @userId
                        )";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", customer.CustomerCode);
                        cmd.Parameters.AddWithValue("@name", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameAr", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", customer.CustomerNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", customer.AccountID > 0 ? customer.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@openingBalance", customer.OpeningBalance);
                        cmd.Parameters.AddWithValue("@currentBalance", customer.OpeningBalance);
                        cmd.Parameters.AddWithValue("@creditLimit", customer.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", customer.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", customer.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", customer.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", customer.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", customer.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", customer.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", customer.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", customer.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", customer.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", customer.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", customer.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", customer.Notes ?? "");
                        cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding customer: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateCustomerAsync(CustomerItem customer)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        UPDATE Customers SET 
                            CustomerCode = @code,
                            CustomerName = @name,
                            CustomerNameAr = @nameAr,
                            CustomerNameEn = @nameEn,
                            AccountID = @accountId,
                            CreditLimit = @creditLimit,
                            PaymentTerms = @paymentTerms,
                            Phone = @phone,
                            Mobile = @mobile,
                            Fax = @fax,
                            Email = @email,
                            Website = @website,
                            Address = @address,
                            TaxNumber = @taxNumber,
                            CommercialRegister = @commercialRegister,
                            ContactPerson = @contactPerson,
                            ContactPersonPhone = @contactPersonPhone,
                            Notes = @notes,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE CustomerID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", customer.CustomerCode);
                        cmd.Parameters.AddWithValue("@name", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameAr", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", customer.CustomerNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", customer.AccountID > 0 ? customer.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@creditLimit", customer.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", customer.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", customer.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", customer.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", customer.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", customer.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", customer.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", customer.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", customer.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", customer.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", customer.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", customer.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", customer.Notes ?? "");
                        cmd.Parameters.AddWithValue("@id", customer.CustomerID);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating customer: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCustomerAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "UPDATE Customers SET IsActive = 0 WHERE CustomerID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", customerId);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting customer: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetTotalCustomersCountAsync(bool activeOnly = true)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = activeOnly ? "SELECT COUNT(*) FROM Customers WHERE IsActive = 1" : "SELECT COUNT(*) FROM Customers";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        public async Task<int> GetActiveCustomersCountAsync()
        {
            return await GetTotalCustomersCountAsync(true);
        }

        // ==================== دوال الموردين (Suppliers) ====================

        public async Task<List<SupplierItem>> GetSuppliersAsync(string searchText = "")
        {
            var suppliers = new List<SupplierItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT 
                        SupplierID, SupplierCode, SupplierName, SupplierNameAr, SupplierNameEn,
                        AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                        Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                        ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate
                    FROM Suppliers 
                    WHERE IsActive = 1";

                if (!string.IsNullOrEmpty(searchText))
                {
                    sql += " AND (SupplierCode LIKE @search OR SupplierNameAr LIKE @search OR SupplierName LIKE @search OR Phone LIKE @search OR Mobile LIKE @search)";
                }

                sql += " ORDER BY SupplierCode";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var supplier = new SupplierItem
                            {
                                SupplierID = reader.GetInt32(0),
                                SupplierCode = reader.GetString(1),
                                SupplierName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                SupplierNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                SupplierNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                IsActive = reader.GetInt32(20) == 1,
                                Notes = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                CreatedDate = reader.IsDBNull(22) ? DateTime.Now : reader.GetDateTime(22)
                            };

                            if (supplier.AccountID > 0)
                            {
                                string accountSql = "SELECT AccountNameAr FROM ChartOfAccounts WHERE AccountID = @accountId";
                                using (var accountCmd = new SQLiteCommand(accountSql, connection))
                                {
                                    accountCmd.Parameters.AddWithValue("@accountId", supplier.AccountID);
                                    object accountName = await accountCmd.ExecuteScalarAsync();
                                    if (accountName != null)
                                    {
                                        supplier.AccountName = accountName.ToString();
                                    }
                                }
                            }

                            suppliers.Add(supplier);
                        }
                    }
                }
            }

            return suppliers;
        }

        public async Task<SupplierItem> GetSupplierByIdAsync(int supplierId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT 
                        SupplierID, SupplierCode, SupplierName, SupplierNameAr, SupplierNameEn,
                        AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                        Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                        ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate
                    FROM Suppliers 
                    WHERE SupplierID = @id AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", supplierId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var supplier = new SupplierItem
                            {
                                SupplierID = reader.GetInt32(0),
                                SupplierCode = reader.GetString(1),
                                SupplierName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                SupplierNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                SupplierNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                IsActive = reader.GetInt32(20) == 1,
                                Notes = reader.IsDBNull(21) ? "" : reader.GetString(21),
                                CreatedDate = reader.IsDBNull(22) ? DateTime.Now : reader.GetDateTime(22)
                            };

                            if (supplier.AccountID > 0)
                            {
                                string accountSql = "SELECT AccountNameAr FROM ChartOfAccounts WHERE AccountID = @accountId";
                                using (var accountCmd = new SQLiteCommand(accountSql, connection))
                                {
                                    accountCmd.Parameters.AddWithValue("@accountId", supplier.AccountID);
                                    object accountName = await accountCmd.ExecuteScalarAsync();
                                    if (accountName != null)
                                    {
                                        supplier.AccountName = accountName.ToString();
                                    }
                                }
                            }

                            return supplier;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<bool> SupplierCodeExistsAsync(string supplierCode, int excludeSupplierId = 0)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Suppliers WHERE SupplierCode = @code AND IsActive = 1 AND SupplierID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@code", supplierCode);
                    cmd.Parameters.AddWithValue("@excludeId", excludeSupplierId);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        public async Task<string> GenerateUniqueSupplierCodeAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(SupplierCode, 5) AS INTEGER)) FROM Suppliers WHERE SupplierCode LIKE 'SUP-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"SUP-{nextNumber:D6}";
                }
            }
        }

        public async Task<bool> AddSupplierAsync(SupplierItem supplier)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO Suppliers (
                            SupplierCode, SupplierName, SupplierNameAr, SupplierNameEn,
                            AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                            Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                            ContactPerson, ContactPersonPhone, IsActive, Notes, CreatedDate, CreatedBy
                        ) VALUES (
                            @code, @name, @nameAr, @nameEn, @accountId, @openingBalance, @currentBalance,
                            @creditLimit, @paymentTerms, @phone, @mobile, @fax, @email, @website,
                            @address, @taxNumber, @commercialRegister, @contactPerson, @contactPersonPhone,
                            1, @notes, CURRENT_TIMESTAMP, @userId
                        )";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", supplier.SupplierCode);
                        cmd.Parameters.AddWithValue("@name", supplier.SupplierNameAr);
                        cmd.Parameters.AddWithValue("@nameAr", supplier.SupplierNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", supplier.SupplierNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", supplier.AccountID > 0 ? supplier.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@openingBalance", supplier.OpeningBalance);
                        cmd.Parameters.AddWithValue("@currentBalance", supplier.OpeningBalance);
                        cmd.Parameters.AddWithValue("@creditLimit", supplier.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", supplier.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", supplier.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", supplier.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", supplier.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", supplier.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", supplier.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", supplier.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", supplier.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", supplier.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", supplier.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", supplier.Notes ?? "");
                        cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding supplier: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateSupplierAsync(SupplierItem supplier)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        UPDATE Suppliers SET 
                            SupplierCode = @code,
                            SupplierName = @name,
                            SupplierNameAr = @nameAr,
                            SupplierNameEn = @nameEn,
                            AccountID = @accountId,
                            CreditLimit = @creditLimit,
                            PaymentTerms = @paymentTerms,
                            Phone = @phone,
                            Mobile = @mobile,
                            Fax = @fax,
                            Email = @email,
                            Website = @website,
                            Address = @address,
                            TaxNumber = @taxNumber,
                            CommercialRegister = @commercialRegister,
                            ContactPerson = @contactPerson,
                            ContactPersonPhone = @contactPersonPhone,
                            Notes = @notes,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE SupplierID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", supplier.SupplierCode);
                        cmd.Parameters.AddWithValue("@name", supplier.SupplierNameAr);
                        cmd.Parameters.AddWithValue("@nameAr", supplier.SupplierNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", supplier.SupplierNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", supplier.AccountID > 0 ? supplier.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@creditLimit", supplier.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", supplier.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", supplier.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", supplier.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", supplier.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", supplier.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", supplier.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", supplier.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", supplier.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", supplier.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", supplier.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", supplier.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", supplier.Notes ?? "");
                        cmd.Parameters.AddWithValue("@id", supplier.SupplierID);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating supplier: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteSupplierAsync(int supplierId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "UPDATE Suppliers SET IsActive = 0 WHERE SupplierID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", supplierId);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting supplier: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetTotalSuppliersCountAsync(bool activeOnly = true)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = activeOnly ? "SELECT COUNT(*) FROM Suppliers WHERE IsActive = 1" : "SELECT COUNT(*) FROM Suppliers";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        public async Task<int> GetActiveSuppliersCountAsync()
        {
            return await GetTotalSuppliersCountAsync(true);
        }

        // ==================== دوال فواتير المشتريات (Purchase Invoices) ====================

        public async Task<int> GetMaxPurchaseInvoiceNumberAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(InvoiceNumber, 5) AS INTEGER)) FROM PurchaseInvoices WHERE InvoiceNumber LIKE 'PIN-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    return (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                }
            }
        }

        public async Task<string> GeneratePurchaseInvoiceNumberAsync()
        {
            int maxNumber = await GetMaxPurchaseInvoiceNumberAsync();
            int nextNumber = maxNumber + 1;
            return $"PIN-{nextNumber:D6}";
        }

        public async Task<bool> PurchaseInvoiceNumberExistsAsync(string invoiceNumber)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM PurchaseInvoices WHERE InvoiceNumber = @invoiceNumber";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// حفظ فاتورة مشتريات (نقدية أو أجل)
        /// </summary>
        /// <summary>
        /// حفظ فاتورة مشتريات (نقدية أو أجل) - معدلة لحل مشكلة مضاعفة الكمية
        /// </summary>


        /// <summary>
        /// إعادة حساب جميع أرصدة الموردين من SupplierTransactions (حل جذري)
        /// </summary>
        public async Task RecalculateAllSuppliersBalancesAsync()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string getAllSuppliersSql = "SELECT SupplierID, OpeningBalance FROM Suppliers WHERE IsActive = 1";
                    var suppliersList = new List<(int Id, decimal OpeningBalance)>();

                    using (var cmd = new SQLiteCommand(getAllSuppliersSql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            suppliersList.Add((
                                Id: reader.GetInt32(0),
                                OpeningBalance: reader.IsDBNull(1) ? 0 : reader.GetDecimal(1)
                            ));
                        }
                    }

                    foreach (var supplier in suppliersList)
                    {
                        // حساب صافي الحركات (الإضافات - الخصومات)
                        string getNetTransactionsSql = @"
                    SELECT COALESCE(SUM(CreditAmount), 0) - COALESCE(SUM(DebitAmount), 0)
                    FROM SupplierTransactions 
                    WHERE SupplierID = @supplierId";

                        decimal netTransactions = 0;
                        using (var cmd = new SQLiteCommand(getNetTransactionsSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@supplierId", supplier.Id);
                            object result = await cmd.ExecuteScalarAsync();
                            netTransactions = result != null ? Convert.ToDecimal(result) : 0;
                        }

                        // الرصيد النهائي = الرصيد الافتتاحي + صافي الحركات
                        decimal finalBalance = supplier.OpeningBalance + netTransactions;

                        string updateSql = @"
                    UPDATE Suppliers 
                    SET CurrentBalance = @newBalance,
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE SupplierID = @supplierId";

                        using (var cmd = new SQLiteCommand(updateSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", finalBalance);
                            cmd.Parameters.AddWithValue("@supplierId", supplier.Id);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        System.Diagnostics.Debug.WriteLine($"💰 المورد {supplier.Id}: رصيد افتتاحي={supplier.OpeningBalance}, صافي حركات={netTransactions}, الرصيد النهائي={finalBalance}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ تم إعادة حساب جميع أرصدة الموردين بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في إعادة حساب أرصدة الموردين: {ex.Message}");
            }
        }

        public async Task<string> GetSettingValueAsync(string settingKey)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT SettingValue FROM SystemSettings WHERE SettingKey = @key";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@key", settingKey);
                    object result = await cmd.ExecuteScalarAsync();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }

        public async Task<bool> UpdateSettingValueAsync(string settingKey, string settingValue, int updatedBy = 0)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = @"
                        UPDATE SystemSettings 
                        SET SettingValue = @value, 
                            UpdatedDate = CURRENT_TIMESTAMP,
                            UpdatedBy = @updatedBy
                        WHERE SettingKey = @key";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@value", settingValue);
                        cmd.Parameters.AddWithValue("@key", settingKey);
                        cmd.Parameters.AddWithValue("@updatedBy", updatedBy > 0 ? updatedBy : (object)DBNull.Value);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating setting: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetAllSettingsAsync()
        {
            var settings = new Dictionary<string, string>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT SettingKey, SettingValue FROM SystemSettings";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        settings[reader.GetString(0)] = reader.GetString(1);
                    }
                }
            }
            return settings;
        }

        public async Task<string> GetCurrencySymbolAsync()
        {
            string symbol = await GetSettingValueAsync("CurrencySymbol");
            return string.IsNullOrEmpty(symbol) ? "ر.س" : symbol;
        }

        public async Task<int> GetDecimalPlacesAsync()
        {
            string value = await GetSettingValueAsync("DecimalPlaces");
            if (int.TryParse(value, out int decimalPlaces))
            {
                return decimalPlaces;
            }
            return 2;
        }

        // ==================== دوال سندات القبض (Receipt Vouchers) ====================

        /// <summary>
        /// إنشاء رقم سند قبض جديد
        /// </summary>
        public async Task<string> GenerateReceiptVoucherNumberAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(VoucherNumber, 5) AS INTEGER)) FROM ReceiptVouchers WHERE VoucherNumber LIKE 'REC-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"REC-{nextNumber:D6}";
                }
            }
        }

        /// <summary>
        /// الحصول على قائمة الخزائن النشطة
        /// </summary>
        public async Task<List<TreasuryBasicItem>> GetActiveTreasuriesAsync()
        {
            var treasuries = new List<TreasuryBasicItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT TreasuryID, TreasuryCode, TreasuryNameAr, CurrentBalance FROM Treasury WHERE IsActive = 1 ORDER BY TreasuryNameAr";

                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        treasuries.Add(new TreasuryBasicItem
                        {
                            Id = reader.GetInt32(0),
                            Code = reader.GetString(1),
                            Name = reader.GetString(2),
                            CurrentBalance = reader.GetDecimal(3)
                        });
                    }
                }
            }

            return treasuries;
        }

        /// <summary>
        /// الحصول على الخزينة الافتراضية (أول خزينة نشطة) أو إنشاء خزينة افتراضية إذا لم توجد
        /// </summary>
        public async Task<int> GetDefaultTreasuryIdAsync()
        {
            var treasuries = await GetActiveTreasuriesAsync();

            if (treasuries.Count > 0)
            {
                return treasuries.First().Id;
            }

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string getCashAccountSql = "SELECT AccountID FROM ChartOfAccounts WHERE AccountCode = '1000' LIMIT 1";
                int cashAccountId = 0;
                using (var cmd = new SQLiteCommand(getCashAccountSql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        cashAccountId = Convert.ToInt32(result);
                    }
                }

                string insertSql = @"
                    INSERT INTO Treasury (TreasuryCode, TreasuryNameAr, TreasuryNameEn, AccountID, CurrentBalance, IsActive, CreatedDate)
                    VALUES ('CASH', 'الصندوق الرئيسي', 'Main Cash', @accountId, 0, 1, CURRENT_TIMESTAMP);
                    SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(insertSql, connection))
                {
                    cmd.Parameters.AddWithValue("@accountId", cashAccountId > 0 ? cashAccountId : (object)DBNull.Value);
                    int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return newId;
                }
            }
        }

        /// <summary>
        /// الحصول على رصيد الخزينة الحالي
        /// </summary>
        public async Task<decimal> GetTreasuryBalanceAsync(int treasuryId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT CurrentBalance FROM Treasury WHERE TreasuryID = @id";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", treasuryId);
                    object result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToDecimal(result) : 0;
                }
            }
        }

        /// <summary>
        /// الحصول على فواتير العميل الغير مدفوعة بالكامل
        /// </summary>
        public async Task<List<CustomerInvoiceItem>> GetCustomerInvoicesAsync(int customerId)
        {
            var invoices = new List<CustomerInvoiceItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = @"
                    SELECT InvoiceID, InvoiceNumber, RemainingAmount, TotalAmount
                    FROM SalesInvoices 
                    WHERE CustomerID = @customerId 
                    AND PaymentStatus != 'Paid'
                    AND IsVoid = 0
                    ORDER BY InvoiceDate DESC";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            invoices.Add(new CustomerInvoiceItem
                            {
                                InvoiceID = reader.GetInt32(0),
                                InvoiceNumber = reader.GetString(1),
                                RemainingAmount = reader.GetDecimal(2),
                                TotalAmount = reader.GetDecimal(3)
                            });
                        }
                    }
                }
            }

            return invoices;
        }

        /// <summary>
        /// الحصول على تحصيلات العملاء مع بيانات الخزينة المرتبطة
        /// </summary>
        public async Task<List<CustomerPaymentWithTreasuryItem>> GetCustomerPaymentsWithTreasuryAsync(string filter = "All", int customerId = 0)
        {
            var payments = new List<CustomerPaymentWithTreasuryItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string dateFilter = "";
                if (filter == "Today")
                    dateFilter = "AND DATE(rv.VoucherDate) = DATE('now')";
                else if (filter == "ThisWeek")
                    dateFilter = "AND DATE(rv.VoucherDate) >= DATE('now', '-7 days')";
                else if (filter == "ThisMonth")
                    dateFilter = "AND strftime('%Y-%m', rv.VoucherDate) = strftime('%Y-%m', 'now')";

                string customerFilter = customerId > 0 ? "AND rv.CustomerID = @customerId" : "";

                string sql = $@"
                    SELECT 
                        rv.VoucherID, 
                        rv.VoucherNumber,
                        rv.VoucherDate, 
                        rv.CustomerID, 
                        c.CustomerNameAr,
                        c.CustomerCode,
                        rv.Amount, 
                        rv.PaymentMethod, 
                        rv.CheckNumber, 
                        rv.Description,
                        si.InvoiceNumber,
                        rv.TreasuryID,
                        t.TreasuryNameAr as TreasuryName
                    FROM ReceiptVouchers rv
                    LEFT JOIN Customers c ON rv.CustomerID = c.CustomerID
                    LEFT JOIN SalesInvoices si ON rv.ReferenceNumber = si.InvoiceNumber
                    LEFT JOIN Treasury t ON rv.TreasuryID = t.TreasuryID
                    WHERE rv.CustomerID IS NOT NULL {dateFilter} {customerFilter}
                    ORDER BY rv.VoucherDate DESC
                    LIMIT 100";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (customerId > 0)
                        cmd.Parameters.AddWithValue("@customerId", customerId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            payments.Add(new CustomerPaymentWithTreasuryItem
                            {
                                PaymentID = reader.GetInt32(0),
                                VoucherNumber = reader.GetString(1),
                                PaymentDate = reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                                CustomerID = reader.GetInt32(3),
                                CustomerName = reader.GetString(4),
                                CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Amount = reader.GetDecimal(6),
                                PaymentMethod = GetPaymentMethodArabic(reader.GetString(7)),
                                CheckNumber = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                InvoiceNumber = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                TreasuryID = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                                TreasuryName = reader.IsDBNull(12) ? "" : reader.GetString(12)
                            });
                        }
                    }
                }
            }

            return payments;
        }

        /// <summary>
        /// حفظ سند قبض مع ربطه بالخزينة وتحديث جميع الأرصدة
        /// </summary>
        public async Task<bool> SaveCustomerPaymentWithTreasuryAsync(
     string voucherNumber,
     DateTime voucherDate,
     int customerId,
     decimal amount,
     string paymentMethod,
     string checkNumber,
     string description,
     int? invoiceId,
     string invoiceNumber,
     int treasuryId,
     int createdBy)
        {
            if (string.IsNullOrWhiteSpace(voucherNumber))
            {
                System.Diagnostics.Debug.WriteLine("رقم سند القبض لا يمكن أن يكون فارغاً");
                return false;
            }

            if (customerId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("معرف العميل غير صالح");
                return false;
            }

            if (amount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("المبلغ يجب أن يكون أكبر من صفر");
                return false;
            }

            if (treasuryId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("معرف الخزينة غير صالح");
                return false;
            }

            if (createdBy <= 0)
            {
                createdBy = 1;
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // تعطيل الـ Triggers مؤقتاً
                        string disableTriggers = "PRAGMA recursive_triggers = OFF;";
                        using (var cmd = new SQLiteCommand(disableTriggers, connection, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        int voucherId = 0;

                        string receiptSql = @"
                    INSERT INTO ReceiptVouchers (
                        VoucherNumber, VoucherDate, CustomerID, Amount, PaymentMethod,
                        CheckNumber, Description, ReferenceNumber, TreasuryID, 
                        IsPosted, CreatedBy, CreatedDate
                    ) VALUES (
                        @voucherNumber, @voucherDate, @customerId, @amount, @paymentMethod,
                        @checkNumber, @description, @referenceNumber, @treasuryId,
                        0, @createdBy, @createdDate
                    );
                    SELECT last_insert_rowid();";

                        using (var cmd = new SQLiteCommand(receiptSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@voucherDate", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                            cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                            cmd.Parameters.AddWithValue("@description", description ?? "");
                            cmd.Parameters.AddWithValue("@referenceNumber", invoiceNumber ?? "");
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                            object result = await cmd.ExecuteScalarAsync();
                            voucherId = Convert.ToInt32(result);
                        }

                        if (voucherId == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("فشل في إدخال سند القبض");
                            transaction.Rollback();
                            return false;
                        }

                        // الحصول على AccountID للعميل
                        int customerAccountId = 0;
                        string getCustomerAccountSql = @"
                    SELECT COALESCE(AccountID, 0) FROM Customers WHERE CustomerID = @customerId";
                        using (var cmd = new SQLiteCommand(getCustomerAccountSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            object result = await cmd.ExecuteScalarAsync();
                            customerAccountId = result != null ? Convert.ToInt32(result) : 0;
                        }

                        // الحصول على AccountID للخزينة
                        int treasuryAccountId = 0;
                        string getTreasuryAccountSql = @"
                    SELECT COALESCE(AccountID, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                        using (var cmd = new SQLiteCommand(getTreasuryAccountSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            object result = await cmd.ExecuteScalarAsync();
                            treasuryAccountId = result != null ? Convert.ToInt32(result) : 0;
                        }

                        System.Diagnostics.Debug.WriteLine($"Customer AccountID: {customerAccountId}, Treasury AccountID: {treasuryAccountId}");

                        // إذا لم يكن هناك AccountID للعميل، استخدم حساب العملاء الافتراضي
                        if (customerAccountId == 0)
                        {
                            string getDefaultCustomerAccountSql = @"
                        SELECT AccountID FROM ChartOfAccounts WHERE AccountCode = '1300' LIMIT 1";
                            using (var cmd = new SQLiteCommand(getDefaultCustomerAccountSql, connection, transaction))
                            {
                                object result = await cmd.ExecuteScalarAsync();
                                customerAccountId = result != null ? Convert.ToInt32(result) : 0;
                            }
                            System.Diagnostics.Debug.WriteLine($"Using default Customer AccountID: {customerAccountId}");
                        }

                        // إذا لم يكن هناك AccountID للخزينة، استخدم حساب الصندوق الافتراضي
                        if (treasuryAccountId == 0)
                        {
                            string getDefaultTreasuryAccountSql = @"
                        SELECT AccountID FROM ChartOfAccounts WHERE AccountCode = '1000' LIMIT 1";
                            using (var cmd = new SQLiteCommand(getDefaultTreasuryAccountSql, connection, transaction))
                            {
                                object result = await cmd.ExecuteScalarAsync();
                                treasuryAccountId = result != null ? Convert.ToInt32(result) : 0;
                            }
                            System.Diagnostics.Debug.WriteLine($"Using default Treasury AccountID: {treasuryAccountId}");
                        }

                        // تحديث رصيد العميل
                        string updateCustomerSql = @"
                    UPDATE Customers 
                    SET CurrentBalance = CurrentBalance - @amount,
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE CustomerID = @customerId";

                        using (var cmd = new SQLiteCommand(updateCustomerSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        decimal newCustomerBalance = 0;
                        string getCustomerBalanceSql = "SELECT CurrentBalance FROM Customers WHERE CustomerID = @customerId";
                        using (var cmd = new SQLiteCommand(getCustomerBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            object result = await cmd.ExecuteScalarAsync();
                            newCustomerBalance = result != null ? Convert.ToDecimal(result) : 0;
                        }

                        // إضافة حركة العميل
                        string customerTransactionSql = @"
                    INSERT INTO CustomerTransactions (
                        CustomerID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @customerId, @date, 'Receipt',
                        0, @amount, @balanceAfter,
                        'RECEIPT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, @createdDate
                    )";

                        using (var cmd = new SQLiteCommand(customerTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@balanceAfter", newCustomerBalance);
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@description", description ?? "");
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ============================================================
                        // حساب الرصيد الحالي للخزينة من صافي جميع الحركات (وليس آخر رصيد)
                        // ============================================================
                        decimal currentTreasuryBalance = 0;
                        string getCurrentBalanceSql = @"
                    SELECT COALESCE(SUM(
                        CASE 
                            WHEN TransactionType = 'Receipt' THEN Amount
                            WHEN TransactionType = 'Payment' THEN -Amount
                            ELSE 0
                        END
                    ), 0) as CurrentBalance
                    FROM TreasuryTransactions 
                    WHERE TreasuryID = @treasuryId";

                        using (var cmd = new SQLiteCommand(getCurrentBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            object result = await cmd.ExecuteScalarAsync();
                            currentTreasuryBalance = result != null ? Convert.ToDecimal(result) : 0;
                        }

                        // حساب الرصيد الجديد (إضافة المبلغ للإيداع)
                        decimal newTreasuryBalance = currentTreasuryBalance + amount;

                        System.Diagnostics.Debug.WriteLine($"💰 رصيد الخزينة الحالي (من صافي الحركات): {currentTreasuryBalance}");
                        System.Diagnostics.Debug.WriteLine($"💰 إضافة حركة إيداع: +{amount}, الرصيد الجديد: {newTreasuryBalance}");

                        // إضافة حركة الخزينة
                        string treasuryTransactionSql = @"
                    INSERT INTO TreasuryTransactions (
                        TreasuryID, TransactionDate, TransactionType,
                        Amount, BalanceAfter, Description,
                        ReferenceType, ReferenceID, ReferenceNumber, CreatedBy, CreatedDate
                    ) VALUES (
                        @treasuryId, @date, 'Receipt',
                        @amount, @balanceAfter, @description,
                        'RECEIPT_VOUCHER', @voucherId, @voucherNumber, @createdBy, @createdDate
                    )";

                        using (var cmd = new SQLiteCommand(treasuryTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@balanceAfter", newTreasuryBalance);
                            cmd.Parameters.AddWithValue("@description", $"تحصيل من العميل - {description ?? ""}");
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // تحديث رصيد الخزينة في جدول Treasury
                        string updateTreasurySql = @"
                    UPDATE Treasury 
                    SET CurrentBalance = @newBalance,
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE TreasuryID = @treasuryId";

                        using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // إنشاء القيد المحاسبي يدوياً بدلاً من الـ Trigger
                        if (customerAccountId > 0 && treasuryAccountId > 0)
                        {
                            // الحصول على رقم القيد التالي
                            string getMaxEntryNumberSql = "SELECT MAX(CAST(SUBSTR(EntryNumber, 5) AS INTEGER)) FROM JournalEntries WHERE EntryNumber LIKE 'JRN-%'";
                            int maxEntryNumber = 0;
                            using (var cmd = new SQLiteCommand(getMaxEntryNumberSql, connection, transaction))
                            {
                                object result = await cmd.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    maxEntryNumber = Convert.ToInt32(result);
                                }
                            }
                            string entryNumber = $"JRN-{(maxEntryNumber + 1):D6}";

                            // إدراج القيد المحاسبي
                            string journalEntrySql = @"
                        INSERT INTO JournalEntries (
                            EntryNumber, EntryDate, ReferenceType, ReferenceID, ReferenceNumber, 
                            Description, IsPosted, PostedDate, PostedBy, CreatedBy, CreatedDate
                        ) VALUES (
                            @entryNumber, @entryDate, 'RECEIPT_VOUCHER', @voucherId, @voucherNumber,
                            @description, 1, CURRENT_TIMESTAMP, @createdBy, @createdBy, CURRENT_TIMESTAMP
                        );
                        SELECT last_insert_rowid();";

                            int entryId = 0;
                            using (var cmd = new SQLiteCommand(journalEntrySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@entryNumber", entryNumber);
                                cmd.Parameters.AddWithValue("@entryDate", voucherDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@description", $"سند قبض رقم {voucherNumber} من العميل");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                object result = await cmd.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    entryId = Convert.ToInt32(result);
                                }
                            }

                            if (entryId > 0)
                            {
                                // إضافة تفاصيل القيد: مدين حساب الخزينة / دائن حساب العميل
                                string journalDetailsSql = @"
                            INSERT INTO JournalEntryDetails (
                                EntryID, AccountID, DebitAmount, CreditAmount, Description, CreatedDate
                            ) VALUES 
                            (@entryId, @treasuryAccountId, @amount, 0, 'من الخزينة', CURRENT_TIMESTAMP),
                            (@entryId, @customerAccountId, 0, @amount, 'إلى العميل', CURRENT_TIMESTAMP);";

                                using (var cmd = new SQLiteCommand(journalDetailsSql, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@entryId", entryId);
                                    cmd.Parameters.AddWithValue("@treasuryAccountId", treasuryAccountId);
                                    cmd.Parameters.AddWithValue("@customerAccountId", customerAccountId);
                                    cmd.Parameters.AddWithValue("@amount", amount);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                // تحديث رصيد الحسابات
                                string updateAccountSql = @"
                            UPDATE ChartOfAccounts 
                            SET CurrentBalance = CurrentBalance + 
                                CASE 
                                    WHEN AccountID = @treasuryAccountId THEN @amount
                                    WHEN AccountID = @customerAccountId THEN -@amount
                                    ELSE 0
                                END,
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE AccountID IN (@treasuryAccountId, @customerAccountId)";

                                using (var cmd = new SQLiteCommand(updateAccountSql, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@treasuryAccountId", treasuryAccountId);
                                    cmd.Parameters.AddWithValue("@customerAccountId", customerAccountId);
                                    cmd.Parameters.AddWithValue("@amount", amount);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        if (invoiceId.HasValue && invoiceId.Value > 0)
                        {
                            string updateInvoiceSql = @"
                        UPDATE SalesInvoices 
                        SET PaidAmount = PaidAmount + @amount,
                            RemainingAmount = RemainingAmount - @amount,
                            PaymentStatus = CASE 
                                WHEN RemainingAmount - @amount <= 0 THEN 'Paid'
                                ELSE 'Partial'
                            END,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE InvoiceID = @invoiceId";

                            using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@invoiceId", invoiceId.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // تحديث سند القبض ليتم ترحيله
                        string updateReceiptSql = @"
                    UPDATE ReceiptVouchers 
                    SET IsPosted = 1, PostedDate = CURRENT_TIMESTAMP, PostedBy = @createdBy
                    WHERE VoucherID = @voucherId";

                        using (var cmd = new SQLiteCommand(updateReceiptSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // إضافة سجل في AuditLog
                        string auditLogSql = @"
                    INSERT INTO AuditLog (
                        UserID, ActionType, TableName, RecordID, NewValue, CreatedDate
                    ) VALUES (
                        @userId, 'INSERT', 'ReceiptVouchers', @recordId, @newValue, CURRENT_TIMESTAMP
                    )";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", createdBy);
                            cmd.Parameters.AddWithValue("@recordId", voucherId);
                            cmd.Parameters.AddWithValue("@newValue", $"سند قبض رقم {voucherNumber} بقيمة {amount} من العميل");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // إعادة تفعيل الـ Triggers
                        string enableTriggers = "PRAGMA recursive_triggers = ON;";
                        using (var cmd = new SQLiteCommand(enableTriggers, connection, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"تم حفظ سند القبض {voucherNumber} بنجاح");
                        return true;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في قاعدة البيانات: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"رمز الخطأ: {ex.ErrorCode}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ غير متوقع: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"نوع الخطأ: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"تفاصيل الخطأ: {ex.StackTrace}");
                return false;
            }
        }


        /// <summary>
        /// إنشاء جداول نظام الأقساط
        /// </summary>
        private void CreateInstallmentTables(SQLiteConnection connection)
        {
            // جدول الأقساط الرئيسي
            string createInstallmentsTable = @"
        CREATE TABLE IF NOT EXISTS Installments (
            InstallmentID INTEGER PRIMARY KEY AUTOINCREMENT,
            InvoiceID INTEGER NOT NULL,
            CustomerID INTEGER NOT NULL,
            InstallmentNumber INTEGER NOT NULL,
            InstallmentAmount DECIMAL(18,2) NOT NULL,
            DueDate DATE NOT NULL,
            DueDays INTEGER NOT NULL,
            Status TEXT CHECK(Status IN ('Pending', 'Paid', 'Overdue', 'Partial')) DEFAULT 'Pending',
            PaidAmount DECIMAL(18,2) DEFAULT 0,
            RemainingAmount DECIMAL(18,2) DEFAULT 0,
            PaidDate DATE,
            PaymentMethod TEXT,
            ReceiptVoucherID INTEGER,
            Notes TEXT,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            CreatedBy INTEGER,
            ModifiedDate DATETIME,
            ModifiedBy INTEGER,
            FOREIGN KEY(InvoiceID) REFERENCES SalesInvoices(InvoiceID) ON DELETE CASCADE,
            FOREIGN KEY(CustomerID) REFERENCES Customers(CustomerID),
            FOREIGN KEY(ReceiptVoucherID) REFERENCES ReceiptVouchers(VoucherID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
            FOREIGN KEY(ModifiedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createInstallmentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // جدول سجل تحصيل الأقساط
            string createInstallmentPaymentsTable = @"
        CREATE TABLE IF NOT EXISTS InstallmentPayments (
            PaymentID INTEGER PRIMARY KEY AUTOINCREMENT,
            InstallmentID INTEGER NOT NULL,
            PaymentDate DATE NOT NULL,
            Amount DECIMAL(18,2) NOT NULL,
            PaymentMethod TEXT CHECK(PaymentMethod IN ('Cash', 'Bank', 'Check', 'Transfer')) NOT NULL,
            ReceiptVoucherID INTEGER,
            TreasuryID INTEGER,
            BankAccountID INTEGER,
            CheckNumber TEXT,
            CheckDate DATE,
            BankName TEXT,
            Notes TEXT,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            CreatedBy INTEGER,
            FOREIGN KEY(InstallmentID) REFERENCES Installments(InstallmentID) ON DELETE CASCADE,
            FOREIGN KEY(ReceiptVoucherID) REFERENCES ReceiptVouchers(VoucherID),
            FOREIGN KEY(TreasuryID) REFERENCES Treasury(TreasuryID),
            FOREIGN KEY(BankAccountID) REFERENCES BankAccounts(BankAccountID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createInstallmentPaymentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // إنشاء الفهارس
            string createIndexes = @"
        CREATE INDEX IF NOT EXISTS idx_installments_invoice ON Installments(InvoiceID);
        CREATE INDEX IF NOT EXISTS idx_installments_customer ON Installments(CustomerID);
        CREATE INDEX IF NOT EXISTS idx_installments_status ON Installments(Status);
        CREATE INDEX IF NOT EXISTS idx_installments_duedate ON Installments(DueDate);
        CREATE INDEX IF NOT EXISTS idx_installments_paid ON Installments(PaidDate);
        CREATE INDEX IF NOT EXISTS idx_installmentpayments_installment ON InstallmentPayments(InstallmentID);
        CREATE INDEX IF NOT EXISTS idx_installmentpayments_date ON InstallmentPayments(PaymentDate);
    ";

            using (var cmd = new SQLiteCommand(createIndexes, connection))
            {
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جداول الأقساط بنجاح");
        }


        /// <summary>
        /// إنشاء جداول التسويات الجردية
        /// </summary>
        /// <summary>
        /// إنشاء جداول التسويات الجردية مع دعم نظام الوحدات الثلاثة
        /// </summary>
        private void CreateInventoryAdjustmentTables(SQLiteConnection connection)
        {
            // ==================== جدول التسويات الجردية (الرأس) ====================
            string createAdjustmentsTable = @"
        CREATE TABLE IF NOT EXISTS InventoryAdjustments (
            AdjustmentID INTEGER PRIMARY KEY AUTOINCREMENT,
            AdjustmentNumber TEXT UNIQUE NOT NULL,
            WarehouseID INTEGER NOT NULL,
            Reason TEXT NOT NULL,
            Notes TEXT,
            AdjustmentDate DATE NOT NULL,
            CreatedBy INTEGER NOT NULL,
            CreatedDate DATETIME NOT NULL,
            ApprovedBy INTEGER,
            ApprovedDate DATETIME,
            Status TEXT NOT NULL DEFAULT 'Pending',
            TotalCost DECIMAL(18,2) NOT NULL DEFAULT 0,
            FOREIGN KEY(WarehouseID) REFERENCES Stores(StoreID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
            FOREIGN KEY(ApprovedBy) REFERENCES Users(UserID)
        );
    ";

            using (var cmd = new SQLiteCommand(createAdjustmentsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ==================== ✅ جدول تفاصيل التسويات الجردية (الأصناف) مع دعم الوحدات ====================
            string createDetailsTable = @"
        CREATE TABLE IF NOT EXISTS InventoryAdjustmentDetails (
            DetailID INTEGER PRIMARY KEY AUTOINCREMENT,
            AdjustmentID INTEGER NOT NULL,
            ProductID INTEGER NOT NULL,
            SystemQuantity DECIMAL(18,2) NOT NULL,
            ActualQuantity DECIMAL(18,2) NOT NULL,
            DifferenceQuantity DECIMAL(18,2) NOT NULL,
            AdjustmentType TEXT NOT NULL,
            UnitCost DECIMAL(18,2) NOT NULL,
            TotalCost DECIMAL(18,2) NOT NULL,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            -- ✅ الأعمدة الجديدة لدعم نظام الوحدات الثلاثة
            UnitName TEXT DEFAULT 'وحدة',
            UnitTag TEXT DEFAULT 'Unit3',
            QuantityInBaseUnit DECIMAL(18,2) DEFAULT 0,
            FOREIGN KEY(AdjustmentID) REFERENCES InventoryAdjustments(AdjustmentID) ON DELETE CASCADE,
            FOREIGN KEY(ProductID) REFERENCES Products(ProductID)
        );
    ";

            using (var cmd = new SQLiteCommand(createDetailsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // ==================== الفهارس لتحسين الأداء ====================
            string createIndexes = @"
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustments_status ON InventoryAdjustments(Status);
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustments_date ON InventoryAdjustments(AdjustmentDate);
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustments_warehouse ON InventoryAdjustments(WarehouseID);
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustments_number ON InventoryAdjustments(AdjustmentNumber);
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustmentdetails_adjustment ON InventoryAdjustmentDetails(AdjustmentID);
        CREATE INDEX IF NOT EXISTS idx_inventoryadjustmentdetails_product ON InventoryAdjustmentDetails(ProductID);
    ";

            using (var cmd = new SQLiteCommand(createIndexes, connection))
            {
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جدول التسويات الجردية وجدول التفاصيل مع دعم نظام الوحدات بنجاح");
        }
        /// <summary>
        /// إعادة حساب رصيد الخزينة بناءً على صافي جميع الحركات وتحديثه
        /// </summary>
        public async Task<decimal> RecalculateTreasuryBalanceFromTransactionsAsync(int treasuryId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string calculateBalanceSql = @"
                SELECT COALESCE(SUM(
                    CASE 
                        WHEN TransactionType = 'Receipt' THEN Amount
                        WHEN TransactionType = 'Payment' THEN -Amount
                        ELSE 0
                    END
                ), 0) as CalculatedBalance
                FROM TreasuryTransactions 
                WHERE TreasuryID = @treasuryId";

                    decimal calculatedBalance = 0;
                    using (var cmd = new SQLiteCommand(calculateBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        object result = await cmd.ExecuteScalarAsync();
                        calculatedBalance = result != null ? Convert.ToDecimal(result) : 0;
                    }

                    string updateSql = "UPDATE Treasury SET CurrentBalance = @newBalance, ModifiedDate = CURRENT_TIMESTAMP WHERE TreasuryID = @treasuryId";
                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@newBalance", calculatedBalance);
                        cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"💰 إعادة حساب رصيد الخزينة ID {treasuryId}: الرصيد الجديد = {calculatedBalance}");
                    return calculatedBalance;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateTreasuryBalanceFromTransactionsAsync Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// إعادة حساب جميع أرصدة الخزائن بناءً على صافي الحركات
        /// </summary>
        public async Task RecalculateAllTreasuryBalancesFromTransactionsAsync()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string getTreasuriesSql = "SELECT TreasuryID FROM Treasury WHERE IsActive = 1";
                    var treasuryIds = new List<int>();

                    using (var cmd = new SQLiteCommand(getTreasuriesSql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            treasuryIds.Add(reader.GetInt32(0));
                        }
                    }

                    foreach (int treasuryId in treasuryIds)
                    {
                        await RecalculateTreasuryBalanceFromTransactionsAsync(treasuryId);
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم إعادة حساب أرصدة {treasuryIds.Count} خزينة بناءً على صافي الحركات");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateAllTreasuryBalancesFromTransactionsAsync Error: {ex.Message}");
            }
        }
        /// <summary>
        /// حذف سند قبض مع استعادة الأرصدة
        /// </summary>
        public async Task<bool> DeleteCustomerPaymentWithTreasuryAsync(int paymentId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        int customerId = 0;
                        decimal amount = 0;
                        int treasuryId = 0;
                        string voucherNumber = "";

                        string selectSql = @"
                            SELECT CustomerID, Amount, TreasuryID, VoucherNumber 
                            FROM ReceiptVouchers 
                            WHERE VoucherID = @id";

                        using (var cmd = new SQLiteCommand(selectSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", paymentId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    customerId = reader.GetInt32(0);
                                    amount = reader.GetDecimal(1);
                                    treasuryId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                    voucherNumber = reader.GetString(3);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("لم يتم العثور على سند القبض");
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        if (customerId > 0)
                        {
                            string updateCustomerSql = "UPDATE Customers SET CurrentBalance = CurrentBalance + @amount WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(updateCustomerSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@id", customerId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            string deleteCustomerTransactionSql = @"
                                DELETE FROM CustomerTransactions 
                                WHERE ReferenceType = 'RECEIPT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteCustomerTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", paymentId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        if (treasuryId > 0)
                        {
                            string updateTreasurySql = "UPDATE Treasury SET CurrentBalance = CurrentBalance - @amount WHERE TreasuryID = @id";
                            using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@id", treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            string deleteTreasuryTransactionSql = @"
                                DELETE FROM TreasuryTransactions 
                                WHERE ReferenceType = 'RECEIPT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteTreasuryTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", paymentId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        string deleteReceiptSql = "DELETE FROM ReceiptVouchers WHERE VoucherID = @id";
                        using (var cmd = new SQLiteCommand(deleteReceiptSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", paymentId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string auditLogSql = @"
                            INSERT INTO AuditLog (
                                UserID, ActionType, TableName, RecordID, OldValue, CreatedDate
                            ) VALUES (
                                @userId, 'DELETE', 'ReceiptVouchers', @recordId, @oldValue, CURRENT_TIMESTAMP
                            )";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            cmd.Parameters.AddWithValue("@recordId", paymentId);
                            cmd.Parameters.AddWithValue("@oldValue", $"حذف سند قبض رقم {voucherNumber} بقيمة {amount}");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"تم حذف سند القبض {voucherNumber} بنجاح");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في حذف السند: {ex.Message}");
                return false;
            }
        }

        // ==================== دوال سندات الصرف / المدفوعات (Payment Vouchers) ====================

        /// <summary>
        /// إنشاء رقم سند صرف جديد
        /// </summary>
        public async Task<string> GeneratePaymentVoucherNumberAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(VoucherNumber, 5) AS INTEGER)) FROM PaymentVouchers WHERE VoucherNumber LIKE 'PAY-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"PAY-{nextNumber:D6}";
                }
            }
        }

        /// <summary>
        /// الحصول على جميع سندات الصرف مع بيانات الخزينة والطرف المرتبطة
        /// </summary>
        public async Task<List<PaymentVoucherWithDetails>> GetPaymentVouchersWithDetailsAsync(string filter = "All", int partyId = 0, bool isSupplier = true)
        {
            var payments = new List<PaymentVoucherWithDetails>();

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string dateFilter = "";
                    if (filter == "Today")
                        dateFilter = "AND DATE(pv.VoucherDate) = DATE('now')";
                    else if (filter == "ThisWeek")
                        dateFilter = "AND DATE(pv.VoucherDate) >= DATE('now', '-7 days')";
                    else if (filter == "ThisMonth")
                        dateFilter = "AND strftime('%Y-%m', pv.VoucherDate) = strftime('%Y-%m', 'now')";

                    string partyFilter = "";
                    if (partyId > 0)
                    {
                        if (isSupplier)
                            partyFilter = "AND pv.SupplierID = @partyId";
                        else
                            partyFilter = "AND pv.CustomerID = @partyId";
                    }

                    string sql = $@"
                        SELECT 
                            pv.VoucherID, 
                            pv.VoucherNumber,
                            pv.VoucherDate, 
                            pv.CustomerID, 
                            pv.SupplierID,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName,
                            COALESCE(s.SupplierNameAr, s.SupplierName, '') as SupplierName,
                            COALESCE(c.CustomerCode, '') as CustomerCode,
                            COALESCE(s.SupplierCode, '') as SupplierCode,
                            pv.Amount, 
                            pv.PaymentMethod, 
                            COALESCE(pv.CheckNumber, '') as CheckNumber, 
                            COALESCE(pv.Description, '') as Description,
                            pv.TreasuryID,
                            COALESCE(t.TreasuryNameAr, t.TreasuryNameEn, '') as TreasuryName
                        FROM PaymentVouchers pv
                        LEFT JOIN Customers c ON pv.CustomerID = c.CustomerID AND c.IsActive = 1
                        LEFT JOIN Suppliers s ON pv.SupplierID = s.SupplierID AND s.IsActive = 1
                        LEFT JOIN Treasury t ON pv.TreasuryID = t.TreasuryID AND t.IsActive = 1
                        WHERE pv.IsPosted = 1 {dateFilter} {partyFilter}
                        ORDER BY pv.VoucherDate DESC
                        LIMIT 500";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (partyId > 0)
                            cmd.Parameters.AddWithValue("@partyId", partyId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int voucherId = reader.GetInt32(0);
                                string voucherNumber = reader.GetString(1);
                                DateTime voucherDateTime = reader.GetDateTime(2);
                                string voucherDate = voucherDateTime.ToString("yyyy-MM-dd");

                                int customerId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                                int supplierId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

                                string customerName = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                string supplierName = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                string customerCode = reader.IsDBNull(7) ? "" : reader.GetString(7);
                                string supplierCode = reader.IsDBNull(8) ? "" : reader.GetString(8);

                                decimal amount = reader.GetDecimal(9);
                                string paymentMethod = reader.GetString(10);
                                string checkNumber = reader.IsDBNull(11) ? "" : reader.GetString(11);
                                string description = reader.IsDBNull(12) ? "" : reader.GetString(12);
                                int treasuryId = reader.IsDBNull(13) ? 0 : reader.GetInt32(13);
                                string treasuryName = reader.IsDBNull(14) ? "" : reader.GetString(14);

                                string partyName = "";
                                string partyCode = "";

                                if (supplierId > 0)
                                {
                                    partyName = supplierName;
                                    partyCode = supplierCode;
                                    if (string.IsNullOrEmpty(partyName) && !string.IsNullOrEmpty(partyCode))
                                    {
                                        partyName = partyCode;
                                    }
                                    else if (!string.IsNullOrEmpty(partyCode) && !string.IsNullOrEmpty(partyName))
                                    {
                                        partyName = $"{partyCode} - {partyName}";
                                    }
                                }
                                else if (customerId > 0)
                                {
                                    partyName = customerName;
                                    partyCode = customerCode;
                                    if (string.IsNullOrEmpty(partyName) && !string.IsNullOrEmpty(partyCode))
                                    {
                                        partyName = partyCode;
                                    }
                                    else if (!string.IsNullOrEmpty(partyCode) && !string.IsNullOrEmpty(partyName))
                                    {
                                        partyName = $"{partyCode} - {partyName}";
                                    }
                                }

                                if (string.IsNullOrEmpty(partyName))
                                {
                                    partyName = "غير محدد";
                                }

                                string paymentMethodArabic = GetPaymentMethodArabic(paymentMethod);

                                var payment = new PaymentVoucherWithDetails
                                {
                                    VoucherID = voucherId,
                                    VoucherNumber = voucherNumber,
                                    VoucherDate = voucherDate,
                                    CustomerID = customerId,
                                    SupplierID = supplierId,
                                    PartyName = partyName,
                                    PartyCode = partyCode,
                                    Amount = amount,
                                    PaymentMethod = paymentMethodArabic,
                                    CheckNumber = checkNumber,
                                    Description = description,
                                    TreasuryID = treasuryId,
                                    TreasuryName = treasuryName
                                };

                                payments.Add(payment);
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم استرجاع {payments.Count} سند صرف من قاعدة البيانات");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في GetPaymentVouchersWithDetailsAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return payments;
        }

        /// <summary>
        /// حفظ سند صرف مع تحديث جميع الأرصدة (للمورد أو العميل)
        /// </summary>
        /// <summary>
        /// حفظ سند صرف مع تحديث جميع الأرصدة (للمورد أو العميل) - مع التحقق من رصيد الخزينة
        /// </summary>
        /// <summary>
        /// حفظ سند صرف مع تحديث جميع الأرصدة (للمورد أو العميل)
        /// </summary>
        /// <summary>
        /// حفظ سند صرف مع تحديث جميع الأرصدة (للمورد أو العميل)
        /// </summary>
        /// <summary>
        /// حفظ سند صرف مع تحديث جميع الأرصدة وظهوره في كشف حساب المورد/العميل
        /// </summary>
        public async Task<bool> SavePaymentVoucherAsync(
    string voucherNumber,
    DateTime voucherDate,
    int supplierId,
    int customerId,
    decimal amount,
    string paymentMethod,
    string checkNumber,
    string description,
    int treasuryId,
    int createdBy,
    int? invoiceId = null)
        {
            // ============================================================
            // التحقق الأولي من صحة البيانات
            // ============================================================
            if (string.IsNullOrWhiteSpace(voucherNumber))
            {
                System.Diagnostics.Debug.WriteLine("رقم سند الصرف لا يمكن أن يكون فارغاً");
                return false;
            }

            if (supplierId <= 0 && customerId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("يجب تحديد مورد أو عميل");
                return false;
            }

            if (amount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("المبلغ يجب أن يكون أكبر من صفر");
                return false;
            }

            if (treasuryId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("معرف الخزينة غير صالح");
                return false;
            }

            if (createdBy <= 0)
            {
                createdBy = 1;
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // ============================================================
                        // 1. التحقق من رصيد الخزينة
                        // ============================================================
                        decimal currentTreasuryBalance = 0;
                        string getTreasuryBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                        using (var cmd = new SQLiteCommand(getTreasuryBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            object result = await cmd.ExecuteScalarAsync();
                            currentTreasuryBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                        }

                        if (currentTreasuryBalance < amount)
                        {
                            MessageBox.Show($"عذراً، رصيد الخزينة غير كافٍ!\n\nالرصيد الحالي: {currentTreasuryBalance:N2}\nالمبلغ المطلوب: {amount:N2}",
                                            "رصيد غير كافٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            transaction.Rollback();
                            return false;
                        }

                        // ============================================================
                        // 2. إضافة سند الصرف
                        // ============================================================
                        int voucherId = 0;

                        string paymentSql = @"
            INSERT INTO PaymentVouchers (
                VoucherNumber, VoucherDate, CustomerID, SupplierID, Amount, PaymentMethod,
                CheckNumber, Description, TreasuryID, IsPosted, CreatedBy, CreatedDate
            ) VALUES (
                @voucherNumber, @voucherDate, @customerId, @supplierId, @amount, @paymentMethod,
                @checkNumber, @description, @treasuryId, 1, @createdBy, CURRENT_TIMESTAMP
            );
            SELECT last_insert_rowid();";

                        using (var cmd = new SQLiteCommand(paymentSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@voucherDate", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@customerId", customerId > 0 ? customerId : 0);
                            cmd.Parameters.AddWithValue("@supplierId", supplierId > 0 ? supplierId : 0);
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod ?? "Cash");
                            cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                            cmd.Parameters.AddWithValue("@description", description ?? "");
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);

                            object result = await cmd.ExecuteScalarAsync();
                            voucherId = (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
                        }

                        if (voucherId == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("فشل في إدخال سند الصرف");
                            transaction.Rollback();
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم إدخال سند الصرف (ID: {voucherId}) - رقم: {voucherNumber}");

                        // ============================================================
                        // 3. تحديث رصيد الخزينة (نقصان)
                        // ============================================================
                        decimal newTreasuryBalance = currentTreasuryBalance - amount;

                        string updateTreasurySql = @"
            UPDATE Treasury 
            SET CurrentBalance = @newBalance,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE TreasuryID = @treasuryId";

                        using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد الخزينة: {currentTreasuryBalance} → {newTreasuryBalance}");

                        // ============================================================
                        // 4. إضافة حركة خزينة
                        // ============================================================
                        string treasuryTransactionSql = @"
            INSERT INTO TreasuryTransactions (
                TreasuryID, TransactionDate, TransactionType,
                Amount, BalanceAfter, Description,
                ReferenceType, ReferenceID, ReferenceNumber, CreatedBy, CreatedDate
            ) VALUES (
                @treasuryId, @date, 'Payment',
                @amount, @balanceAfter, @description,
                'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @createdBy, CURRENT_TIMESTAMP
            )";

                        using (var cmd = new SQLiteCommand(treasuryTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@balanceAfter", newTreasuryBalance);
                            cmd.Parameters.AddWithValue("@description", $"سند صرف - {description ?? ""}");
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        System.Diagnostics.Debug.WriteLine($"💰 إضافة حركة خزينة جديدة: -{amount}");

                        // ============================================================
                        // 5. معالجة المورد (إذا كان الدفع لمورد)
                        // ============================================================
                        if (supplierId > 0)
                        {
                            // الحصول على الرصيد الحالي للمورد من جدول SupplierTransactions
                            decimal currentSupplierBalance = 0;
                            string getSupplierBalanceSql = @"
                        SELECT COALESCE((
                            SELECT OpeningBalance - COALESCE(SUM(CreditAmount), 0)
                            FROM SupplierTransactions 
                            WHERE SupplierID = @supplierId 
                            AND TransactionType = 'Payment'
                        ), OpeningBalance)
                        FROM Suppliers WHERE SupplierID = @supplierId";

                            using (var cmd = new SQLiteCommand(getSupplierBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentSupplierBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            // ✅ حساب الرصيد بعد الدفع (يقلل الرصيد)
                            decimal newSupplierBalance = currentSupplierBalance - amount;

                            // ✅ تحديث رصيد المورد في جدول Suppliers
                            string updateSupplierSql = @"
                UPDATE Suppliers 
                SET CurrentBalance = @newBalance,
                    ModifiedDate = CURRENT_TIMESTAMP
                WHERE SupplierID = @supplierId";

                            using (var cmd = new SQLiteCommand(updateSupplierSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newSupplierBalance);
                                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ✅ إضافة حركة صرف للمورد باستخدام DebitAmount (لتقليل الرصيد)
                            string supplierTransactionSql = @"
                INSERT INTO SupplierTransactions (
                    SupplierID, TransactionDate, TransactionType,
                    DebitAmount, CreditAmount, BalanceAfter,
                    ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                ) VALUES (
                    @supplierId, @date, 'Payment',
                    @amount, 0, @balanceAfter,  -- ✅ DebitAmount = المبلغ, CreditAmount = 0
                    'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                )";

                            using (var cmd = new SQLiteCommand(supplierTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                                cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", newSupplierBalance);
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@description", description ?? "");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد المورد: {currentSupplierBalance} → {newSupplierBalance}");
                            System.Diagnostics.Debug.WriteLine($"📌 إضافة حركة دفع للمورد في SupplierTransactions (مدين: {amount})");
                        }

                        // ============================================================
                        // 6. معالجة العميل (إذا كان الدفع لعميل)
                        // ============================================================
                        else if (customerId > 0)
                        {
                            // الحصول على الرصيد الحالي للعميل
                            decimal currentCustomerBalance = 0;
                            string getCustomerBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @customerId";
                            using (var cmd = new SQLiteCommand(getCustomerBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentCustomerBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            // تحديث رصيد العميل (يزيد الرصيد لأننا دفعنا له)
                            decimal newCustomerBalance = currentCustomerBalance + amount;

                            string updateCustomerSql = @"
                UPDATE Customers 
                SET CurrentBalance = @newBalance,
                    ModifiedDate = CURRENT_TIMESTAMP
                WHERE CustomerID = @customerId";

                            using (var cmd = new SQLiteCommand(updateCustomerSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // إضافة حركة للعميل في CustomerTransactions
                            string customerTransactionSql = @"
                INSERT INTO CustomerTransactions (
                    CustomerID, TransactionDate, TransactionType,
                    DebitAmount, CreditAmount, BalanceAfter,
                    ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                ) VALUES (
                    @customerId, @date, 'Payment',
                    0, @amount, @balanceAfter,
                    'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                )";

                            using (var cmd = new SQLiteCommand(customerTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@description", description ?? "");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد العميل: {currentCustomerBalance} → {newCustomerBalance}");
                            System.Diagnostics.Debug.WriteLine($"📌 إضافة حركة دفع للعميل في CustomerTransactions (دائن: {amount})");
                        }

                        // ============================================================
                        // 7. تحديث الفاتورة المرتبطة (إذا وجدت)
                        // ============================================================
                        if (invoiceId.HasValue && invoiceId.Value > 0)
                        {
                            string updateInvoiceSql = @"
                UPDATE PurchaseInvoices 
                SET PaidAmount = COALESCE(PaidAmount, 0) + @amount,
                    RemainingAmount = CASE 
                        WHEN COALESCE(RemainingAmount, 0) - @amount < 0 THEN 0
                        ELSE COALESCE(RemainingAmount, 0) - @amount
                    END,
                    PaymentStatus = CASE 
                        WHEN COALESCE(RemainingAmount, 0) - @amount <= 0 THEN 'Paid'
                        ELSE 'Partial'
                    END,
                    ModifiedDate = CURRENT_TIMESTAMP
                WHERE InvoiceID = @invoiceId";

                            using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@invoiceId", invoiceId.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"📋 تحديث الفاتورة ID: {invoiceId.Value} - إضافة دفعة بقيمة {amount}");
                        }

                        // ============================================================
                        // 8. سجل التدقيق
                        // ============================================================
                        string auditLogSql = @"
            INSERT INTO AuditLog (
                UserID, ActionType, TableName, RecordID, NewValue, CreatedDate
            ) VALUES (
                @userId, 'INSERT', 'PaymentVouchers', @recordId, @newValue, CURRENT_TIMESTAMP
            );";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", createdBy);
                            cmd.Parameters.AddWithValue("@recordId", voucherId);
                            cmd.Parameters.AddWithValue("@newValue", $"سند صرف رقم {voucherNumber} بقيمة {amount}");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم حفظ سند الصرف {voucherNumber} بنجاح");
                        return true;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في قاعدة البيانات: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"رمز الخطأ: {ex.ErrorCode}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ غير متوقع: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"نوع الخطأ: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"تفاصيل الخطأ: {ex.StackTrace}");
                return false;
            }
        }
        /// <summary>
        /// حذف سند صرف مع استعادة الأرصدة
        /// </summary>
        /// <summary>
        /// حذف سند صرف مع استعادة الأرصدة (عكس تأثير سند الصرف)
        /// </summary>
        /// <summary>
        /// حذف سند صرف مع استعادة الأرصدة (عكس تأثير سند الصرف)
        /// </summary>
        public async Task<bool> DeletePaymentVoucherAsync(int paymentId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        int customerId = 0;
                        int supplierId = 0;
                        decimal amount = 0;
                        int treasuryId = 0;
                        string voucherNumber = "";

                        string selectSql = @"
                    SELECT COALESCE(CustomerID, 0) as CustomerID, 
                           COALESCE(SupplierID, 0) as SupplierID, 
                           Amount, 
                           COALESCE(TreasuryID, 0) as TreasuryID, 
                           VoucherNumber 
                    FROM PaymentVouchers 
                    WHERE VoucherID = @id";

                        using (var cmd = new SQLiteCommand(selectSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", paymentId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    customerId = reader.GetInt32(0);
                                    supplierId = reader.GetInt32(1);
                                    amount = reader.GetDecimal(2);
                                    treasuryId = reader.GetInt32(3);
                                    voucherNumber = reader.GetString(4);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("⚠️ لم يتم العثور على سند الصرف");
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"🗑️ حذف سند الصرف: ID={paymentId}, رقم={voucherNumber}, المبلغ={amount}");

                        // ============================================================
                        // 1. استعادة رصيد الخزينة (عكس تأثير السند)
                        // ============================================================
                        if (treasuryId > 0)
                        {
                            string updateTreasurySql = "UPDATE Treasury SET CurrentBalance = COALESCE(CurrentBalance, 0) + @amount WHERE TreasuryID = @id";
                            using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@id", treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد الخزينة: +{amount}");

                            string deleteTreasuryTransactionSql = @"
                        DELETE FROM TreasuryTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteTreasuryTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", paymentId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"🗑️ تم حذف حركة الخزينة المرتبطة");
                        }

                        // ============================================================
                        // 2. ✅ استعادة رصيد المورد (عكس تأثير السند)
                        // ============================================================
                        if (supplierId > 0)
                        {
                            // الحصول على الرصيد الحالي للمورد
                            decimal currentSupplierBalance = 0;
                            string getSupplierBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Suppliers WHERE SupplierID = @supplierId";
                            using (var cmd = new SQLiteCommand(getSupplierBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentSupplierBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            // ✅ عند حذف سند صرف للمورد: نضيف الرصيد مرة أخرى (عكس عملية الطرح عند الحفظ)
                            // لأن الدفع كان يقلل الرصيد (DebitAmount)، فالحذف يزيد الرصيد
                            decimal newSupplierBalance = currentSupplierBalance + amount;

                            string updateSupplierSql = "UPDATE Suppliers SET CurrentBalance = @newBalance WHERE SupplierID = @id";
                            using (var cmd = new SQLiteCommand(updateSupplierSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newSupplierBalance);
                                cmd.Parameters.AddWithValue("@id", supplierId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد المورد: {currentSupplierBalance} → {newSupplierBalance} (+{amount})");

                            // حذف حركة المورد المرتبطة
                            string deleteSupplierTransactionSql = @"
                        DELETE FROM SupplierTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteSupplierTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", paymentId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"🗑️ تم حذف حركة المورد المرتبطة من SupplierTransactions");
                        }

                        // ============================================================
                        // 3. استعادة رصيد العميل (عكس تأثير السند)
                        // ============================================================
                        else if (customerId > 0)
                        {
                            decimal currentCustomerBalance = 0;
                            string getCustomerBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @customerId";
                            using (var cmd = new SQLiteCommand(getCustomerBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentCustomerBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newCustomerBalance = currentCustomerBalance - amount;

                            string updateCustomerSql = "UPDATE Customers SET CurrentBalance = @newBalance WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(updateCustomerSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@id", customerId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد العميل: {currentCustomerBalance} → {newCustomerBalance} (-{amount})");

                            string deleteCustomerTransactionSql = @"
                        DELETE FROM CustomerTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteCustomerTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", paymentId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"🗑️ تم حذف حركة العميل المرتبطة من CustomerTransactions");
                        }

                        // ============================================================
                        // 4. حذف سند الصرف نفسه
                        // ============================================================
                        string deletePaymentSql = "DELETE FROM PaymentVouchers WHERE VoucherID = @id";
                        using (var cmd = new SQLiteCommand(deletePaymentSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", paymentId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        System.Diagnostics.Debug.WriteLine($"🗑️ تم حذف سند الصرف {voucherNumber}");

                        // ============================================================
                        // 5. تسجيل عملية الحذف في سجل التدقيق
                        // ============================================================
                        string auditLogSql = @"
                    INSERT INTO AuditLog (
                        UserID, ActionType, TableName, RecordID, OldValue, CreatedDate
                    ) VALUES (
                        @userId, 'DELETE', 'PaymentVouchers', @recordId, @oldValue, CURRENT_TIMESTAMP
                    )";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            cmd.Parameters.AddWithValue("@recordId", paymentId);
                            cmd.Parameters.AddWithValue("@oldValue", $"حذف سند صرف رقم {voucherNumber} بقيمة {amount}");
                            await cmd.ExecuteNonQueryAsync();
                        }
                        System.Diagnostics.Debug.WriteLine($"📝 تم تسجيل عملية الحذف في سجل التدقيق");

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم حذف سند الصرف {voucherNumber} بنجاح واستعادة جميع الأرصدة");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في حذف السند: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }


        public async Task<bool> UpdatePaymentVoucherAsync(
    int voucherId,
    string voucherNumber,
    DateTime voucherDate,
    int supplierId,
    int customerId,
    decimal amount,
    string paymentMethod,
    string checkNumber,
    string description,
    int treasuryId,
    int modifiedBy,
    int? invoiceId = null)
        {
            // ============================================================
            // التحقق الأولي من صحة البيانات
            // ============================================================
            if (voucherId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("معرف سند الصرف غير صالح");
                return false;
            }

            if (string.IsNullOrWhiteSpace(voucherNumber))
            {
                System.Diagnostics.Debug.WriteLine("رقم سند الصرف لا يمكن أن يكون فارغاً");
                return false;
            }

            if (supplierId <= 0 && customerId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("يجب تحديد مورد أو عميل");
                return false;
            }

            if (amount <= 0)
            {
                System.Diagnostics.Debug.WriteLine("المبلغ يجب أن يكون أكبر من صفر");
                return false;
            }

            if (treasuryId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("معرف الخزينة غير صالح");
                return false;
            }

            if (modifiedBy <= 0)
            {
                modifiedBy = 1;
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // ============================================================
                        // 1. جلب بيانات السند القديم
                        // ============================================================
                        int oldSupplierId = 0;
                        int oldCustomerId = 0;
                        decimal oldAmount = 0;
                        int oldTreasuryId = 0;
                        string oldVoucherNumber = "";

                        string selectOldSql = @"
                    SELECT COALESCE(SupplierID, 0) as SupplierID, 
                           COALESCE(CustomerID, 0) as CustomerID, 
                           Amount, 
                           COALESCE(TreasuryID, 0) as TreasuryID, 
                           VoucherNumber 
                    FROM PaymentVouchers 
                    WHERE VoucherID = @voucherId";

                        using (var cmd = new SQLiteCommand(selectOldSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    oldSupplierId = reader.GetInt32(0);
                                    oldCustomerId = reader.GetInt32(1);
                                    oldAmount = reader.GetDecimal(2);
                                    oldTreasuryId = reader.GetInt32(3);
                                    oldVoucherNumber = reader.GetString(4);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("⚠️ لم يتم العثور على سند الصرف");
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"🔄 تحديث سند الصرف: ID={voucherId}, رقم={oldVoucherNumber}");
                        System.Diagnostics.Debug.WriteLine($"   القديم: المبلغ={oldAmount}, المورد={oldSupplierId}, العميل={oldCustomerId}");
                        System.Diagnostics.Debug.WriteLine($"   الجديد: المبلغ={amount}, المورد={supplierId}, العميل={customerId}");

                        // ============================================================
                        // 2. استعادة الأرصدة القديمة (عكس تأثير السند القديم)
                        // ============================================================

                        // 2.1 استعادة رصيد الخزينة القديم
                        if (oldTreasuryId > 0)
                        {
                            string restoreTreasurySql = "UPDATE Treasury SET CurrentBalance = COALESCE(CurrentBalance, 0) + @amount WHERE TreasuryID = @id";
                            using (var cmd = new SQLiteCommand(restoreTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", oldAmount);
                                cmd.Parameters.AddWithValue("@id", oldTreasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد الخزينة القديم: +{oldAmount}");

                            // حذف حركة الخزينة القديمة
                            string deleteTreasurySql = @"
                        DELETE FROM TreasuryTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // 2.2 استعادة رصيد المورد القديم
                        if (oldSupplierId > 0)
                        {
                            decimal oldSupplierBalance = 0;
                            string getBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Suppliers WHERE SupplierID = @id";
                            using (var cmd = new SQLiteCommand(getBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", oldSupplierId);
                                object result = await cmd.ExecuteScalarAsync();
                                oldSupplierBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newSupplierBalance = oldSupplierBalance + oldAmount;
                            string updateSql = "UPDATE Suppliers SET CurrentBalance = @newBalance WHERE SupplierID = @id";
                            using (var cmd = new SQLiteCommand(updateSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newSupplierBalance);
                                cmd.Parameters.AddWithValue("@id", oldSupplierId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد المورد القديم: {oldSupplierBalance} → {newSupplierBalance} (+{oldAmount})");

                            // حذف حركة المورد القديمة
                            string deleteSupplierSql = @"
                        DELETE FROM SupplierTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteSupplierSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // 2.3 استعادة رصيد العميل القديم
                        else if (oldCustomerId > 0)
                        {
                            decimal oldCustomerBalance = 0;
                            string getBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(getBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", oldCustomerId);
                                object result = await cmd.ExecuteScalarAsync();
                                oldCustomerBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newCustomerBalance = oldCustomerBalance - oldAmount;
                            string updateSql = "UPDATE Customers SET CurrentBalance = @newBalance WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(updateSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@id", oldCustomerId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 استعادة رصيد العميل القديم: {oldCustomerBalance} → {newCustomerBalance} (-{oldAmount})");

                            // حذف حركة العميل القديمة
                            string deleteCustomerSql = @"
                        DELETE FROM CustomerTransactions 
                        WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";
                            using (var cmd = new SQLiteCommand(deleteCustomerSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // ============================================================
                        // 3. تطبيق الأرصدة الجديدة (تأثير السند الجديد)
                        // ============================================================

                        // 3.1 التحقق من رصيد الخزينة الجديد
                        decimal newTreasuryBalance = 0;
                        string getNewTreasurySql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @id";
                        using (var cmd = new SQLiteCommand(getNewTreasurySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", treasuryId);
                            object result = await cmd.ExecuteScalarAsync();
                            newTreasuryBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                        }

                        if (newTreasuryBalance < amount)
                        {
                            MessageBox.Show($"عذراً، رصيد الخزينة غير كافٍ!\n\nالرصيد الحالي: {newTreasuryBalance:N2}\nالمبلغ المطلوب: {amount:N2}",
                                            "رصيد غير كافٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            transaction.Rollback();
                            return false;
                        }

                        // 3.2 تحديث رصيد الخزينة الجديد
                        decimal afterTreasuryBalance = newTreasuryBalance - amount;
                        string updateTreasurySql = "UPDATE Treasury SET CurrentBalance = @newBalance WHERE TreasuryID = @id";
                        using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", afterTreasuryBalance);
                            cmd.Parameters.AddWithValue("@id", treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد الخزينة الجديد: {newTreasuryBalance} → {afterTreasuryBalance} (-{amount})");

                        // 3.3 إضافة حركة خزينة جديدة
                        string newTreasuryTransactionSql = @"
                    INSERT INTO TreasuryTransactions (
                        TreasuryID, TransactionDate, TransactionType,
                        Amount, BalanceAfter, Description,
                        ReferenceType, ReferenceID, ReferenceNumber, CreatedBy, CreatedDate
                    ) VALUES (
                        @treasuryId, @date, 'Payment',
                        @amount, @balanceAfter, @description,
                        'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @createdBy, CURRENT_TIMESTAMP
                    )";

                        using (var cmd = new SQLiteCommand(newTreasuryTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@balanceAfter", afterTreasuryBalance);
                            cmd.Parameters.AddWithValue("@description", $"سند صرف - {description ?? ""}");
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@createdBy", modifiedBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 3.4 تحديث بيانات المورد الجديد
                        if (supplierId > 0)
                        {
                            decimal currentSupplierBalance = 0;
                            string getBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Suppliers WHERE SupplierID = @id";
                            using (var cmd = new SQLiteCommand(getBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", supplierId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentSupplierBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            decimal afterSupplierBalance = currentSupplierBalance - amount;
                            string updateSql = "UPDATE Suppliers SET CurrentBalance = @newBalance WHERE SupplierID = @id";
                            using (var cmd = new SQLiteCommand(updateSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", afterSupplierBalance);
                                cmd.Parameters.AddWithValue("@id", supplierId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد المورد الجديد: {currentSupplierBalance} → {afterSupplierBalance} (-{amount})");

                            // إضافة حركة مورد جديدة (DebitAmount)
                            string newSupplierTransactionSql = @"
                        INSERT INTO SupplierTransactions (
                            SupplierID, TransactionDate, TransactionType,
                            DebitAmount, CreditAmount, BalanceAfter,
                            ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                        ) VALUES (
                            @supplierId, @date, 'Payment',
                            @amount, 0, @balanceAfter,
                            'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                        )";

                            using (var cmd = new SQLiteCommand(newSupplierTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                                cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", afterSupplierBalance);
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@description", description ?? "");
                                cmd.Parameters.AddWithValue("@createdBy", modifiedBy);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"📌 إضافة حركة دفع جديدة للمورد (DebitAmount: {amount})");
                        }

                        // 3.5 تحديث بيانات العميل الجديد
                        else if (customerId > 0)
                        {
                            decimal currentCustomerBalance = 0;
                            string getBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(getBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", customerId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentCustomerBalance = (result != null && result != DBNull.Value) ? Convert.ToDecimal(result) : 0;
                            }

                            decimal afterCustomerBalance = currentCustomerBalance + amount;
                            string updateSql = "UPDATE Customers SET CurrentBalance = @newBalance WHERE CustomerID = @id";
                            using (var cmd = new SQLiteCommand(updateSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", afterCustomerBalance);
                                cmd.Parameters.AddWithValue("@id", customerId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد العميل الجديد: {currentCustomerBalance} → {afterCustomerBalance} (+{amount})");

                            // إضافة حركة عميل جديدة
                            string newCustomerTransactionSql = @"
                        INSERT INTO CustomerTransactions (
                            CustomerID, TransactionDate, TransactionType,
                            DebitAmount, CreditAmount, BalanceAfter,
                            ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                        ) VALUES (
                            @customerId, @date, 'Payment',
                            0, @amount, @balanceAfter,
                            'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                        )";

                            using (var cmd = new SQLiteCommand(newCustomerTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                cmd.Parameters.AddWithValue("@date", voucherDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", afterCustomerBalance);
                                cmd.Parameters.AddWithValue("@voucherId", voucherId);
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@description", description ?? "");
                                cmd.Parameters.AddWithValue("@createdBy", modifiedBy);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"📌 إضافة حركة دفع جديدة للعميل (CreditAmount: {amount})");
                        }

                        // ============================================================
                        // 4. تحديث بيانات سند الصرف نفسه
                        // ============================================================
                        string updateVoucherSql = @"
                    UPDATE PaymentVouchers SET 
                        VoucherNumber = @voucherNumber,
                        VoucherDate = @voucherDate,
                        SupplierID = @supplierId,
                        CustomerID = @customerId,
                        Amount = @amount,
                        PaymentMethod = @paymentMethod,
                        CheckNumber = @checkNumber,
                        Description = @description,
                        TreasuryID = @treasuryId,
                        ModifiedBy = @modifiedBy,
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE VoucherID = @voucherId";

                        using (var cmd = new SQLiteCommand(updateVoucherSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                            cmd.Parameters.AddWithValue("@voucherDate", voucherDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@supplierId", supplierId > 0 ? supplierId : 0);
                            cmd.Parameters.AddWithValue("@customerId", customerId > 0 ? customerId : 0);
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod ?? "Cash");
                            cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                            cmd.Parameters.AddWithValue("@description", description ?? "");
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@modifiedBy", modifiedBy);
                            cmd.Parameters.AddWithValue("@voucherId", voucherId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ============================================================
                        // 5. تحديث الفاتورة المرتبطة (إذا وجدت)
                        // ============================================================
                        if (invoiceId.HasValue && invoiceId.Value > 0)
                        {
                            string updateInvoiceSql = @"
                        UPDATE PurchaseInvoices 
                        SET PaidAmount = COALESCE(PaidAmount, 0) + @amount,
                            RemainingAmount = CASE 
                                WHEN COALESCE(RemainingAmount, 0) - @amount < 0 THEN 0
                                ELSE COALESCE(RemainingAmount, 0) - @amount
                            END,
                            PaymentStatus = CASE 
                                WHEN COALESCE(RemainingAmount, 0) - @amount <= 0 THEN 'Paid'
                                ELSE 'Partial'
                            END,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE InvoiceID = @invoiceId";

                            using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@invoiceId", invoiceId.Value);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"📋 تحديث الفاتورة ID: {invoiceId.Value}");
                        }

                        // ============================================================
                        // 6. سجل التدقيق
                        // ============================================================
                        string auditLogSql = @"
                    INSERT INTO AuditLog (
                        UserID, ActionType, TableName, RecordID, OldValue, NewValue, CreatedDate
                    ) VALUES (
                        @userId, 'UPDATE', 'PaymentVouchers', @recordId, @oldValue, @newValue, CURRENT_TIMESTAMP
                    )";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", modifiedBy);
                            cmd.Parameters.AddWithValue("@recordId", voucherId);
                            cmd.Parameters.AddWithValue("@oldValue", $"المبلغ القديم: {oldAmount}, المورد القديم: {oldSupplierId}");
                            cmd.Parameters.AddWithValue("@newValue", $"المبلغ الجديد: {amount}, المورد الجديد: {supplierId}");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث سند الصرف {voucherNumber} بنجاح");
                        return true;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في قاعدة البيانات: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"رمز الخطأ: {ex.ErrorCode}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ غير متوقع: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"نوع الخطأ: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"تفاصيل الخطأ: {ex.StackTrace}");
                return false;
            }
        }
        // ==================== دوال مساعدة عامة ====================
        // ملاحظة: تم نقل منطق تشفير كلمات المرور بالكامل إلى RasidAccountingSystem.Helpers.PasswordHelper
        // (معيار PBKDF2 مع Salt)، بدلاً من الدالة القديمة ComputeSha256Hash غير الآمنة.

        /// <summary>
        /// الحصول على اتصال بقاعدة البيانات
        /// </summary>
        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// اختبار الاتصال بقاعدة البيانات
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// تحويل طريقة السداد من الإنجليزية إلى العربية
        /// </summary>
        private string GetPaymentMethodArabic(string method)
        {
            switch (method?.ToLower())
            {
                case "cash": return "نقدي";
                case "check": return "شيك";
                case "transfer": return "تحويل بنكي";
                case "card": return "بطاقة ائتمان";
                case "bank": return "تحويل بنكي";
                default: return method ?? "";
            }
        }

        /// <summary>
        /// تحويل طريقة السداد من العربية إلى الإنجليزية
        /// </summary>
        private string GetPaymentMethodEnglish(string arabicMethod)
        {
            switch (arabicMethod)
            {
                case "نقدي": return "Cash";
                case "شيك": return "Check";
                case "تحويل بنكي": return "Transfer";
                case "بطاقة ائتمان": return "Card";
                default: return "Cash";
            }
        }

        // ==================== دوال فواتير البيع (Sales Invoices) - المطلوبة لـ SalesInvoiceView ====================

        /// <summary>
        /// الحصول على أكبر رقم فاتورة بيع
        /// </summary>
        public async Task<int> GetMaxSalesInvoiceNumberAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(InvoiceNumber, 5) AS INTEGER)) FROM SalesInvoices WHERE InvoiceNumber LIKE 'SIN-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    return (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                }
            }
        }

        /// <summary>
        /// إنشاء رقم فاتورة بيع جديد
        /// </summary>
        public async Task<string> GenerateSalesInvoiceNumberAsync()
        {
            int maxNumber = await GetMaxSalesInvoiceNumberAsync();
            int nextNumber = maxNumber + 1;
            return $"SIN-{nextNumber:D6}";
        }

        /// <summary>
        /// التحقق من وجود رقم فاتورة بيع
        /// </summary>
        public async Task<bool> SalesInvoiceNumberExistsAsync(string invoiceNumber)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM SalesInvoices WHERE InvoiceNumber = @invoiceNumber";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// حفظ فاتورة بيع فقط (بدون سند قبض - لطريقة الدفع أجل)
        /// </summary>
        /// <summary>
        /// حفظ فاتورة بيع فقط (بدون سند قبض - لطريقة الدفع أجل)
        /// </summary>
        /// <summary>
        /// حفظ فاتورة بيع آجلة (بدون سند قبض)
        /// </summary>

        /// <summary>
        /// حفظ فاتورة بيع مع سند قبض (للطرق: نقدي، تحويل بنكي، شيك)
        /// </summary>

        /// <summary>
        /// التحقق من صحة قاعدة البيانات وإصلاح التناقضات
        /// </summary>
        public async Task<bool> ValidateAndRepairDatabaseAsync()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 1. التحقق من وجود فواتير بدون حركات مورد
                    string checkOrphanInvoices = @"
                SELECT COUNT(*) FROM PurchaseInvoices pi
                LEFT JOIN SupplierTransactions st ON pi.InvoiceNumber = st.ReferenceNumber 
                WHERE st.TransactionID IS NULL";

                    using (var cmd = new SQLiteCommand(checkOrphanInvoices, connection))
                    {
                        long orphanCount = (long)await cmd.ExecuteScalarAsync();
                        if (orphanCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ يوجد {orphanCount} فاتورة بدون حركات مورد");
                        }
                    }

                    // 2. التحقق من وجود حركات مورد بدون فواتير
                    string checkOrphanTransactions = @"
                SELECT COUNT(*) FROM SupplierTransactions st
                LEFT JOIN PurchaseInvoices pi ON st.ReferenceNumber = pi.InvoiceNumber 
                WHERE pi.InvoiceID IS NULL AND st.ReferenceType = 'PURCHASE_INVOICE'";

                    using (var cmd = new SQLiteCommand(checkOrphanTransactions, connection))
                    {
                        long orphanCount = (long)await cmd.ExecuteScalarAsync();
                        if (orphanCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ يوجد {orphanCount} حركة مورد بدون فاتورة");
                        }
                    }

                    // 3. التحقق من وجود حركات دفع بدون سندات صرف
                    string checkPaymentTransactions = @"
                SELECT COUNT(*) FROM SupplierTransactions st
                WHERE st.TransactionType = 'Payment' 
                AND NOT EXISTS (SELECT 1 FROM PaymentVouchers pv WHERE pv.VoucherNumber = st.ReferenceNumber)";

                    using (var cmd = new SQLiteCommand(checkPaymentTransactions, connection))
                    {
                        long orphanCount = (long)await cmd.ExecuteScalarAsync();
                        if (orphanCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ يوجد {orphanCount} حركة دفع بدون سند صرف");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في فحص قاعدة البيانات: {ex.Message}");
                return false;
            }
        }
        #region 20. دوال إدارة العهد (Custody Management)

        /// <summary>
        /// إنشاء جداول العهد في قاعدة البيانات
        /// </summary>
        /// <summary>
        /// إنشاء جداول العهد في قاعدة البيانات
        /// </summary>
        private void CreateCustodyTables(SQLiteConnection connection)
        {
            // جدول الموظفين (أصحاب العهد)
            string createEmployeesTable = @"
        CREATE TABLE IF NOT EXISTS Employees (
            EmployeeID INTEGER PRIMARY KEY AUTOINCREMENT,
            EmployeeCode TEXT UNIQUE NOT NULL,
            EmployeeNameAr TEXT NOT NULL,
            EmployeeNameEn TEXT,
            Department TEXT,
            Position TEXT,
            Phone TEXT,
            Email TEXT,
            IsActive INTEGER DEFAULT 1,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ModifiedDate DATETIME,
            CreatedBy INTEGER,
            Notes TEXT
        );";

            using (var cmd = new SQLiteCommand(createEmployeesTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // جدول العهود الرئيسي
            string createCustodyTable = @"
        CREATE TABLE IF NOT EXISTS Custody (
            CustodyID INTEGER PRIMARY KEY AUTOINCREMENT,
            CustodyNumber TEXT UNIQUE NOT NULL,
            CustodyDate DATE NOT NULL,
            EmployeeID INTEGER NOT NULL,
            Amount DECIMAL(18,2) NOT NULL,
            Currency TEXT DEFAULT 'SAR',
            CustodyType TEXT CHECK(CustodyType IN ('Cash', 'Asset', 'Mixed')) DEFAULT 'Cash',
            Status TEXT CHECK(Status IN ('Active', 'PartiallySettled', 'Settled', 'Cancelled')) DEFAULT 'Active',
            ExpectedReturnDate DATE,
            ActualReturnDate DATE,
            SettledAmount DECIMAL(18,2) DEFAULT 0,
            RemainingAmount DECIMAL(18,2) DEFAULT 0,
            Description TEXT,
            TreasuryID INTEGER,
            CreatedBy INTEGER,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            ApprovedBy INTEGER,
            ApprovedDate DATETIME,
            RejectionReason TEXT,
            IsActive INTEGER DEFAULT 1,
            Notes TEXT,
            FOREIGN KEY(EmployeeID) REFERENCES Employees(EmployeeID),
            FOREIGN KEY(TreasuryID) REFERENCES Treasury(TreasuryID),
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID),
            FOREIGN KEY(ApprovedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createCustodyTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // جدول عمليات العهدة
            string createCustodyTransactionsTable = @"
        CREATE TABLE IF NOT EXISTS CustodyTransactions (
            TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
            CustodyID INTEGER NOT NULL,
            TransactionDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            TransactionType TEXT CHECK(TransactionType IN ('Issue', 'Return', 'Settlement', 'Adjustment')),
            Amount DECIMAL(18,2) NOT NULL,
            BalanceBefore DECIMAL(18,2) NOT NULL,
            BalanceAfter DECIMAL(18,2) NOT NULL,
            ExpenseType TEXT,
            Description TEXT,
            ReceiptNumber TEXT,
            ReferenceNumber TEXT,
            CreatedBy INTEGER,
            CreatedDate DATETIME DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY(CustodyID) REFERENCES Custody(CustodyID) ON DELETE CASCADE,
            FOREIGN KEY(CreatedBy) REFERENCES Users(UserID)
        );";

            using (var cmd = new SQLiteCommand(createCustodyTransactionsTable, connection))
            {
                cmd.ExecuteNonQuery();
            }

            // إضافة فهارس
            string createIndexes = @"
        CREATE INDEX IF NOT EXISTS idx_custody_number ON Custody(CustodyNumber);
        CREATE INDEX IF NOT EXISTS idx_custody_employee ON Custody(EmployeeID);
        CREATE INDEX IF NOT EXISTS idx_custody_status ON Custody(Status);
        CREATE INDEX IF NOT EXISTS idx_custody_date ON Custody(CustodyDate);
        CREATE INDEX IF NOT EXISTS idx_custodytransactions_custody ON CustodyTransactions(CustodyID);
        CREATE INDEX IF NOT EXISTS idx_custodytransactions_date ON CustodyTransactions(TransactionDate);
        CREATE INDEX IF NOT EXISTS idx_employees_code ON Employees(EmployeeCode);
        CREATE INDEX IF NOT EXISTS idx_employees_name ON Employees(EmployeeNameAr);
        CREATE INDEX IF NOT EXISTS idx_employees_department ON Employees(Department);
    ";

            using (var cmd = new SQLiteCommand(createIndexes, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// إنشاء البيانات الأساسية للعهد
        /// </summary>
        /// <summary>
        /// إنشاء البيانات الأساسية للعهد
        /// </summary>
        private void InitializeCustodyBasicData(SQLiteConnection connection)
        {
            // تم إزالة الموظفين الثابتين
            // سيتم إضافة الموظفين من خلال صفحة إدارة الموظفين
            System.Diagnostics.Debug.WriteLine("✓ تم إنشاء جداول العهد - الموظفين سيتم إضافتهم يدوياً");
        }

        /// <summary>
        /// إنشاء محفزات (Triggers) للعهد
        /// </summary>
        private void CreateCustodyTriggers(SQLiteConnection connection)
        {
            // تحديث الرصيد المتبقي تلقائياً عند إضافة عمليات
            string triggerUpdateCustodyBalance = @"
        CREATE TRIGGER IF NOT EXISTS trg_update_custody_balance
        AFTER INSERT ON CustodyTransactions
        BEGIN
            UPDATE Custody 
            SET RemainingAmount = NEW.BalanceAfter,
                SettledAmount = (SELECT Amount FROM Custody WHERE CustodyID = NEW.CustodyID) - NEW.BalanceAfter,
                Status = CASE 
                    WHEN NEW.BalanceAfter = 0 THEN 'Settled'
                    WHEN NEW.BalanceAfter < (SELECT Amount FROM Custody WHERE CustodyID = NEW.CustodyID) THEN 'PartiallySettled'
                    ELSE 'Active'
                END
            WHERE CustodyID = NEW.CustodyID;
        END;";

            using (var cmd = new SQLiteCommand(triggerUpdateCustodyBalance, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// إنشاء رقم عهدة جديد
        /// </summary>
        public async Task<string> GenerateCustodyNumberAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(CustodyNumber, 5) AS INTEGER)) FROM Custody WHERE CustodyNumber LIKE 'CUS-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"CUS-{nextNumber:D6}";
                }
            }
        }

        /// <summary>
        /// توليد كود موظف فريد تلقائياً
        /// </summary>
        public async Task<string> GenerateUniqueEmployeeCodeAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(EmployeeCode, 5) AS INTEGER)) FROM Employees WHERE EmployeeCode LIKE 'EMP-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"EMP-{nextNumber:D6}";
                }
            }
        }
        /// <summary>
        /// الحصول على قائمة الموظفين النشطين
        /// </summary>
        public async Task<List<EmployeeItem>> GetEmployeesAsync(string searchText = "")
        {
            var employees = new List<EmployeeItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT EmployeeID, EmployeeCode, EmployeeNameAr, EmployeeNameEn, Department, Position, Phone, Email, IsActive FROM Employees WHERE IsActive = 1";

                if (!string.IsNullOrEmpty(searchText))
                {
                    sql += " AND (EmployeeCode LIKE @search OR EmployeeNameAr LIKE @search OR Department LIKE @search)";
                }

                sql += " ORDER BY EmployeeNameAr";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            employees.Add(new EmployeeItem
                            {
                                EmployeeID = reader.GetInt32(0),
                                EmployeeCode = reader.GetString(1),
                                EmployeeNameAr = reader.GetString(2),
                                EmployeeNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Department = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Position = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Email = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                IsActive = reader.GetInt32(8) == 1
                            });
                        }
                    }
                }
            }

            return employees;
        }


        /// <summary>
        /// الحصول على بيانات موظف بواسطة المعرف
        /// </summary>
        public async Task<EmployeeItem> GetEmployeeByIdAsync(int employeeId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT EmployeeID, EmployeeCode, EmployeeNameAr, EmployeeNameEn, 
                   Department, Position, Phone, Email, IsActive
            FROM Employees 
            WHERE EmployeeID = @id AND IsActive = 1";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", employeeId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new EmployeeItem
                            {
                                EmployeeID = reader.GetInt32(0),
                                EmployeeCode = reader.GetString(1),
                                EmployeeNameAr = reader.GetString(2),
                                EmployeeNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Department = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Position = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Email = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                IsActive = reader.GetInt32(8) == 1
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// الحصول على عدد الموظفين
        /// </summary>
        public async Task<int> GetEmployeesCountAsync(bool activeOnly = true)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = activeOnly ? "SELECT COUNT(*) FROM Employees WHERE IsActive = 1" : "SELECT COUNT(*) FROM Employees";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
        }

        /// <summary>
        /// التحقق من وجود كود موظف مكرر
        /// </summary>
        public async Task<bool> IsEmployeeCodeExistsAsync(string employeeCode, int excludeEmployeeId = 0)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Employees WHERE EmployeeCode = @code AND IsActive = 1 AND EmployeeID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@code", employeeCode);
                    cmd.Parameters.AddWithValue("@excludeId", excludeEmployeeId);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        /// <summary>
        /// تحديث بيانات موظف
        /// </summary>
        public async Task<bool> UpdateEmployeeAsync(EmployeeItem employee)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE Employees SET 
                    EmployeeCode = @code,
                    EmployeeNameAr = @nameAr,
                    EmployeeNameEn = @nameEn,
                    Department = @department,
                    Position = @position,
                    Phone = @phone,
                    Email = @email,
                    ModifiedDate = CURRENT_TIMESTAMP
                WHERE EmployeeID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", employee.EmployeeCode);
                        cmd.Parameters.AddWithValue("@nameAr", employee.EmployeeNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", employee.EmployeeNameEn ?? "");
                        cmd.Parameters.AddWithValue("@department", employee.Department ?? "");
                        cmd.Parameters.AddWithValue("@position", employee.Position ?? "");
                        cmd.Parameters.AddWithValue("@phone", employee.Phone ?? "");
                        cmd.Parameters.AddWithValue("@email", employee.Email ?? "");
                        cmd.Parameters.AddWithValue("@id", employee.EmployeeID);

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating employee: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// حذف موظف (تعطيله فقط)
        /// </summary>
        public async Task<bool> DeleteEmployeeAsync(int employeeId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // التحقق من وجود عهود مرتبطة بهذا الموظف
                    string checkCustodySql = "SELECT COUNT(*) FROM Custody WHERE EmployeeID = @id AND IsActive = 1 AND Status != 'Settled'";
                    using (var checkCmd = new SQLiteCommand(checkCustodySql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@id", employeeId);
                        long custodyCount = (long)await checkCmd.ExecuteScalarAsync();

                        if (custodyCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"لا يمكن حذف الموظف لأنه مرتبط بـ {custodyCount} عهدة نشطة");
                            return false;
                        }
                    }

                    string sql = "UPDATE Employees SET IsActive = 0, ModifiedDate = CURRENT_TIMESTAMP WHERE EmployeeID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", employeeId);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting employee: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// الحصول على قائمة العهود النشطة
        /// </summary>
        public async Task<List<CustodyItem>> GetCustodyListAsync(string filter = "All", int employeeId = 0)
        {
            var custodyList = new List<CustodyItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string statusFilter = "";
                if (filter == "Active")
                    statusFilter = "AND c.Status = 'Active'";
                else if (filter == "Settled")
                    statusFilter = "AND c.Status IN ('Settled', 'PartiallySettled')";
                else if (filter == "Cancelled")
                    statusFilter = "AND c.Status = 'Cancelled'";

                string employeeFilter = employeeId > 0 ? "AND c.EmployeeID = @employeeId" : "";

                string sql = $@"
            SELECT 
                c.CustodyID, c.CustodyNumber, c.CustodyDate, c.EmployeeID,
                e.EmployeeNameAr, e.EmployeeCode,
                c.Amount, c.Currency, c.CustodyType, c.Status,
                c.ExpectedReturnDate, c.ActualReturnDate,
                c.SettledAmount, c.RemainingAmount, c.Description,
                c.TreasuryID, t.TreasuryNameAr as TreasuryName,
                c.CreatedDate, c.IsActive
            FROM Custody c
            LEFT JOIN Employees e ON c.EmployeeID = e.EmployeeID
            LEFT JOIN Treasury t ON c.TreasuryID = t.TreasuryID
            WHERE c.IsActive = 1 {statusFilter} {employeeFilter}
            ORDER BY c.CustodyDate DESC";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    if (employeeId > 0)
                        cmd.Parameters.AddWithValue("@employeeId", employeeId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            custodyList.Add(new CustodyItem
                            {
                                CustodyID = reader.GetInt32(0),
                                CustodyNumber = reader.GetString(1),
                                CustodyDate = reader.GetDateTime(2),
                                EmployeeID = reader.GetInt32(3),
                                EmployeeName = reader.GetString(4),
                                EmployeeCode = reader.GetString(5),
                                Amount = reader.GetDecimal(6),
                                Currency = reader.IsDBNull(7) ? "SAR" : reader.GetString(7),
                                CustodyType = reader.IsDBNull(8) ? "Cash" : reader.GetString(8),
                                Status = reader.IsDBNull(9) ? "Active" : reader.GetString(9),
                                ExpectedReturnDate = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10),
                                ActualReturnDate = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                                SettledAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                RemainingAmount = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                Description = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                TreasuryID = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                                TreasuryName = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                CreatedDate = reader.GetDateTime(17),
                                IsActive = reader.GetInt32(18) == 1
                            });
                        }
                    }
                }
            }

            return custodyList;
        }

        /// <summary>
        /// الحصول على عمليات عهدة محددة
        /// </summary>
        public async Task<List<CustodyTransactionItem>> GetCustodyTransactionsAsync(int custodyId)
        {
            var transactions = new List<CustodyTransactionItem>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT 
                TransactionID, TransactionDate, TransactionType,
                Amount, BalanceBefore, BalanceAfter, ExpenseType,
                Description, ReceiptNumber, ReferenceNumber
            FROM CustodyTransactions 
            WHERE CustodyID = @custodyId
            ORDER BY TransactionDate DESC";

                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@custodyId", custodyId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            transactions.Add(new CustodyTransactionItem
                            {
                                TransactionID = reader.GetInt32(0),
                                TransactionDate = reader.GetDateTime(1),
                                TransactionType = reader.GetString(2),
                                Amount = reader.GetDecimal(3),
                                BalanceBefore = reader.GetDecimal(4),
                                BalanceAfter = reader.GetDecimal(5),
                                ExpenseType = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Description = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                ReceiptNumber = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                ReferenceNumber = reader.IsDBNull(9) ? "" : reader.GetString(9)
                            });
                        }
                    }
                }
            }

            return transactions;
        }

        /// <summary>
        /// إنشاء عهدة جديدة
        /// </summary>
        /// <summary>
        /// إنشاء عهدة جديدة - مع التحقق من رصيد الخزينة
        /// </summary>
        public async Task<bool> CreateCustodyAsync(CustodyItem custody, int createdBy)
        {
            // ============================================================
            // التحقق الأولي من صحة البيانات
            // ============================================================
            if (custody == null)
            {
                MessageBox.Show("بيانات العهدة غير صالحة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (custody.EmployeeID <= 0)
            {
                MessageBox.Show("الرجاء اختيار الموظف", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (custody.Amount <= 0)
            {
                MessageBox.Show("المبلغ يجب أن يكون أكبر من صفر", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (createdBy <= 0)
            {
                createdBy = 1;
            }

            // ============================================================
            // التحقق من رصيد الخزينة (لأن العهدة تخصم من الخزينة)
            // ============================================================
            if (custody.TreasuryID > 0)
            {
                using (var checkConn = new SQLiteConnection(_connectionString))
                {
                    await checkConn.OpenAsync();
                    string checkSql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @id";
                    using (var cmd = new SQLiteCommand(checkSql, checkConn))
                    {
                        cmd.Parameters.AddWithValue("@id", custody.TreasuryID);
                        decimal currentBalance = (decimal?)await cmd.ExecuteScalarAsync() ?? 0;

                        if (currentBalance < custody.Amount)
                        {
                            MessageBox.Show($"عذراً، رصيد الخزينة غير كافٍ!\n\nالرصيد الحالي: {currentBalance:N2}\nالمبلغ المطلوب للعهدة: {custody.Amount:N2}\n\nالرجاء إيداع المبلغ في الخزينة أولاً.",
                                            "رصيد غير كافٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ رصيد الخزينة كافٍ: {currentBalance:N2} >= {custody.Amount:N2}");
                    }
                }
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // ============================================================
                        // التحقق مرة أخرى من رصيد الخزينة داخل المعاملة
                        // ============================================================
                        if (custody.TreasuryID > 0)
                        {
                            decimal currentTreasuryBalance = 0;
                            string getTreasuryBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                            using (var cmd = new SQLiteCommand(getTreasuryBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", custody.TreasuryID);
                                object result = await cmd.ExecuteScalarAsync();
                                currentTreasuryBalance = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            if (currentTreasuryBalance < custody.Amount)
                            {
                                MessageBox.Show($"عذراً، رصيد الخزينة غير كافٍ!\n\nالرصيد الحالي: {currentTreasuryBalance:N2}\nالمبلغ المطلوب للعهدة: {custody.Amount:N2}",
                                                "رصيد غير كافٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
                                transaction.Rollback();
                                return false;
                            }
                        }

                        string custodyNumber = await GenerateCustodyNumberAsync();

                        string sql = @"
                    INSERT INTO Custody (
                        CustodyNumber, CustodyDate, EmployeeID, Amount, Currency,
                        CustodyType, Status, ExpectedReturnDate, Description,
                        TreasuryID, CreatedBy, CreatedDate, IsActive,
                        SettledAmount, RemainingAmount
                    ) VALUES (
                        @number, @date, @employeeId, @amount, @currency,
                        @type, 'Active', @expectedDate, @description,
                        @treasuryId, @createdBy, CURRENT_TIMESTAMP, 1,
                        0, @amount
                    );
                    SELECT last_insert_rowid();";

                        int custodyId = 0;
                        using (var cmd = new SQLiteCommand(sql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@number", custodyNumber);
                            cmd.Parameters.AddWithValue("@date", custody.CustodyDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@employeeId", custody.EmployeeID);
                            cmd.Parameters.AddWithValue("@amount", custody.Amount);
                            cmd.Parameters.AddWithValue("@currency", custody.Currency ?? "SAR");
                            cmd.Parameters.AddWithValue("@type", custody.CustodyType ?? "Cash");
                            cmd.Parameters.AddWithValue("@expectedDate", custody.ExpectedReturnDate.HasValue ? custody.ExpectedReturnDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@description", custody.Description ?? "");
                            cmd.Parameters.AddWithValue("@treasuryId", custody.TreasuryID > 0 ? custody.TreasuryID : (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);

                            object result = await cmd.ExecuteScalarAsync();
                            custodyId = Convert.ToInt32(result);
                        }

                        if (custodyId == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("❌ فشل في إدخال العهدة");
                            transaction.Rollback();
                            return false;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم إدخال العهدة (ID: {custodyId}) - رقم: {custodyNumber}");

                        // ============================================================
                        // إضافة حركة إنشاء العهدة
                        // ============================================================
                        string transactionSql = @"
                    INSERT INTO CustodyTransactions (
                        CustodyID, TransactionDate, TransactionType,
                        Amount, BalanceBefore, BalanceAfter,
                        Description, CreatedBy
                    ) VALUES (
                        @custodyId, @date, 'Issue',
                        @amount, 0, @amount,
                        @description, @createdBy
                    )";

                        using (var cmd = new SQLiteCommand(transactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@custodyId", custodyId);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@amount", custody.Amount);
                            cmd.Parameters.AddWithValue("@description", $"إنشاء عهدة جديدة - {custody.Description}");
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم إضافة حركة إنشاء العهدة");

                        // ============================================================
                        // إذا كانت الخزينة محددة، قم بخصم المبلغ من الخزينة
                        // ============================================================
                        if (custody.TreasuryID > 0)
                        {
                            // الحصول على الرصيد الحالي للخزينة
                            decimal currentTreasuryBalance = 0;
                            string getCurrentBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                            using (var cmd = new SQLiteCommand(getCurrentBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", custody.TreasuryID);
                                object result = await cmd.ExecuteScalarAsync();
                                currentTreasuryBalance = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newTreasuryBalance = currentTreasuryBalance - custody.Amount;

                            // تحديث رصيد الخزينة
                            string updateTreasurySql = @"
                        UPDATE Treasury 
                        SET CurrentBalance = @newBalance,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE TreasuryID = @treasuryId";

                            using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                                cmd.Parameters.AddWithValue("@treasuryId", custody.TreasuryID);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"💰 تحديث رصيد الخزينة: {currentTreasuryBalance} → {newTreasuryBalance}");

                            // إضافة حركة خزينة
                            string treasuryTransactionSql = @"
                        INSERT INTO TreasuryTransactions (
                            TreasuryID, TransactionDate, TransactionType,
                            Amount, BalanceAfter, Description,
                            ReferenceType, ReferenceID, ReferenceNumber, CreatedBy
                        ) VALUES (
                            @treasuryId, @date, 'Payment',
                            @amount, @balanceAfter, @description,
                            'CUSTODY', @custodyId, @custodyNumber, @createdBy
                        )";

                            using (var cmd = new SQLiteCommand(treasuryTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", custody.TreasuryID);
                                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@amount", custody.Amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", newTreasuryBalance);
                                cmd.Parameters.AddWithValue("@description", $"صرف عهدة للموظف - {custody.EmployeeName}");
                                cmd.Parameters.AddWithValue("@custodyId", custodyId);
                                cmd.Parameters.AddWithValue("@custodyNumber", custodyNumber);
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"💰 تم خصم {custody.Amount} من الخزينة للعهدة {custodyNumber}");
                        }

                        // ============================================================
                        // سجل التدقيق
                        // ============================================================
                        string auditLogSql = @"
                    INSERT INTO AuditLog (
                        UserID, ActionType, TableName, RecordID, NewValue, CreatedDate
                    ) VALUES (
                        @userId, 'INSERT', 'Custody', @recordId, @newValue, CURRENT_TIMESTAMP
                    );";

                        using (var cmd = new SQLiteCommand(auditLogSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", createdBy);
                            cmd.Parameters.AddWithValue("@recordId", custodyId);
                            cmd.Parameters.AddWithValue("@newValue", $"إنشاء عهدة رقم {custodyNumber} بقيمة {custody.Amount:N2} للموظف {custody.EmployeeName}");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        MessageBox.Show($"✅ تم إنشاء العهدة {custodyNumber} بنجاح\n\nالمبلغ: {custody.Amount:N2}\nالموظف: {custody.EmployeeName}",
                                        "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
                        System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء العهدة {custodyNumber} بنجاح");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في إنشاء العهدة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في إنشاء العهدة: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// إضافة عملية صرف من العهدة
        /// </summary>
        public async Task<bool> AddCustodyExpenseAsync(int custodyId, decimal amount, string expenseType, string description, string receiptNumber, int createdBy)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        decimal currentBalance = 0;
                        int employeeId = 0;
                        decimal totalAmount = 0;
                        string custodyNumber = "";

                        string getCustodySql = "SELECT RemainingAmount, EmployeeID, Amount, CustodyNumber FROM Custody WHERE CustodyID = @id AND IsActive = 1";
                        using (var cmd = new SQLiteCommand(getCustodySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", custodyId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    currentBalance = reader.GetDecimal(0);
                                    employeeId = reader.GetInt32(1);
                                    totalAmount = reader.GetDecimal(2);
                                    custodyNumber = reader.GetString(3);
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        if (amount > currentBalance)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        decimal newBalance = currentBalance - amount;

                        // إضافة حركة الصرف
                        string transactionSql = @"
                    INSERT INTO CustodyTransactions (
                        CustodyID, TransactionDate, TransactionType,
                        Amount, BalanceBefore, BalanceAfter,
                        ExpenseType, Description, ReceiptNumber, CreatedBy
                    ) VALUES (
                        @custodyId, @date, 'Return',
                        @amount, @balanceBefore, @balanceAfter,
                        @expenseType, @description, @receiptNumber, @createdBy
                    )";

                        using (var cmd = new SQLiteCommand(transactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@custodyId", custodyId);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@balanceBefore", currentBalance);
                            cmd.Parameters.AddWithValue("@balanceAfter", newBalance);
                            cmd.Parameters.AddWithValue("@expenseType", expenseType ?? "");
                            cmd.Parameters.AddWithValue("@description", description ?? "");
                            cmd.Parameters.AddWithValue("@receiptNumber", receiptNumber ?? "");
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم إضافة صرف بقيمة {amount} للعهدة {custodyNumber}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في إضافة صرف للعهدة: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تسوية عهدة بالكامل أو جزئياً
        /// </summary>
        public async Task<bool> SettleCustodyAsync(int custodyId, decimal settledAmount, string description, int createdBy)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        decimal remainingAmount = 0;
                        string custodyNumber = "";

                        string getCustodySql = "SELECT RemainingAmount, CustodyNumber FROM Custody WHERE CustodyID = @id";
                        using (var cmd = new SQLiteCommand(getCustodySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", custodyId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    remainingAmount = reader.GetDecimal(0);
                                    custodyNumber = reader.GetString(1);
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        decimal settlementAmount = settledAmount > 0 ? settledAmount : remainingAmount;
                        if (settlementAmount > remainingAmount)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        decimal newBalance = remainingAmount - settlementAmount;

                        // إضافة حركة التسوية
                        string transactionSql = @"
                    INSERT INTO CustodyTransactions (
                        CustodyID, TransactionDate, TransactionType,
                        Amount, BalanceBefore, BalanceAfter,
                        Description, CreatedBy
                    ) VALUES (
                        @custodyId, @date, 'Settlement',
                        @amount, @balanceBefore, @balanceAfter,
                        @description, @createdBy
                    )";

                        using (var cmd = new SQLiteCommand(transactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@custodyId", custodyId);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@amount", settlementAmount);
                            cmd.Parameters.AddWithValue("@balanceBefore", remainingAmount);
                            cmd.Parameters.AddWithValue("@balanceAfter", newBalance);
                            cmd.Parameters.AddWithValue("@description", description ?? "تسوية عهدة");
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // تحديث العهدة (سيتم عن طريق الـ Trigger، ولكن نحدث هنا للتأكيد)
                        string updateCustodySql = @"
                    UPDATE Custody 
                    SET ActualReturnDate = CASE WHEN @newBalance = 0 THEN CURRENT_DATE ELSE ActualReturnDate END
                    WHERE CustodyID = @id";

                        using (var cmd = new SQLiteCommand(updateCustodySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", newBalance);
                            cmd.Parameters.AddWithValue("@id", custodyId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تسوية العهدة {custodyNumber} بقيمة {settlementAmount}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تسوية العهدة: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// إلغاء عهدة
        /// </summary>
        public async Task<bool> CancelCustodyAsync(int custodyId, string reason, int cancelledBy)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        decimal remainingAmount = 0;
                        int treasuryId = 0;
                        string custodyNumber = "";

                        string getCustodySql = "SELECT RemainingAmount, TreasuryID, CustodyNumber FROM Custody WHERE CustodyID = @id";
                        using (var cmd = new SQLiteCommand(getCustodySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", custodyId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    remainingAmount = reader.GetDecimal(0);
                                    treasuryId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                    custodyNumber = reader.GetString(2);
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }

                        // إضافة حركة إلغاء
                        string transactionSql = @"
                    INSERT INTO CustodyTransactions (
                        CustodyID, TransactionDate, TransactionType,
                        Amount, BalanceBefore, BalanceAfter,
                        Description, CreatedBy
                    ) VALUES (
                        @custodyId, @date, 'Adjustment',
                        @amount, @balanceBefore, 0,
                        @description, @createdBy
                    )";

                        using (var cmd = new SQLiteCommand(transactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@custodyId", custodyId);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@amount", remainingAmount);
                            cmd.Parameters.AddWithValue("@balanceBefore", remainingAmount);
                            cmd.Parameters.AddWithValue("@description", $"إلغاء عهدة - {reason}");
                            cmd.Parameters.AddWithValue("@createdBy", cancelledBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // تحديث العهدة
                        string updateCustodySql = @"
                    UPDATE Custody 
                    SET Status = 'Cancelled',
                        IsActive = 0,
                        RemainingAmount = 0,
                        Notes = COALESCE(Notes || '\n', '') || @reason
                    WHERE CustodyID = @id";

                        using (var cmd = new SQLiteCommand(updateCustodySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@reason", $"تم الإلغاء: {reason}");
                            cmd.Parameters.AddWithValue("@id", custodyId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // إعادة المبلغ المتبقي للخزينة
                        if (treasuryId > 0 && remainingAmount > 0)
                        {
                            string updateTreasurySql = @"
                        UPDATE Treasury 
                        SET CurrentBalance = CurrentBalance + @amount,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE TreasuryID = @treasuryId";

                            using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@amount", remainingAmount);
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم إلغاء العهدة {custodyNumber}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في إلغاء العهدة: {ex.Message}");
                return false;
            }
        }



        #endregion
        // ==================== نهاية كلاس DatabaseService ====================

        // ==================== دوال الأرصدة الافتتاحية للمخزون (Opening Stock) ====================

        /// <summary>
        /// الحصول على ID المخزن الرئيسي
        /// </summary>
        public async Task<int> GetMainStoreIdAsync()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT StoreID FROM Stores WHERE StoreCode = 'MAIN' AND IsActive = 1 LIMIT 1";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }


        /// <summary>
        /// الحصول على الكمية الحالية لمنتج في المخزن الرئيسي
        /// </summary>
        /// <summary>
        /// الحصول على الكمية الحالية لمنتج في المخزن الرئيسي
        /// </summary>
        public async Task<decimal> GetCurrentStockAsync(int productId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                int storeId = await GetMainStoreIdAsync();
                if (storeId == 0) return 0;

                string sql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    object result = await cmd.ExecuteScalarAsync();
                    decimal currentQty = result != null ? Convert.ToDecimal(result) : 0;
                    System.Diagnostics.Debug.WriteLine($"📦 GetCurrentStockAsync: المنتج {productId} => {currentQty} (بالوحدة الأساسية)");
                    return currentQty;
                }
            }
        }


        /// <summary>
        /// إضافة رصيد افتتاحي لمنتج في مخزن محدد
        /// </summary>
        /// <param name="productId">معرف المنتج</param>
        /// <param name="storeId">معرف المخزن</param>
        /// <param name="quantity">الكمية المراد إضافتها</param>
        /// <param name="costPrice">سعر الشراء (اختياري)</param>
        public async Task<bool> AddOpeningStockAsync(int productId, int storeId, decimal quantity, decimal costPrice = 0)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // التحقق من وجود المخزن
                        string checkStoreSql = "SELECT COUNT(*) FROM Stores WHERE StoreID = @storeId AND IsActive = 1";
                        using (var cmd = new SQLiteCommand(checkStoreSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            long storeCount = (long)await cmd.ExecuteScalarAsync();
                            if (storeCount == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"المخزن ID {storeId} غير موجود أو غير نشط");
                                return false;
                            }
                        }

                        // التحقق من وجود المنتج
                        string checkProductSql = "SELECT COUNT(*) FROM Products WHERE ProductID = @id AND IsActive = 1";
                        using (var cmd = new SQLiteCommand(checkProductSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", productId);
                            long count = (long)await cmd.ExecuteScalarAsync();
                            if (count == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"المنتج ID {productId} غير موجود");
                                return false;
                            }
                        }

                        // الحصول على الرصيد الحالي قبل الإضافة (بالوحدة الأساسية)
                        decimal currentQuantity = 0;
                        string getCurrentSql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
                        using (var cmd = new SQLiteCommand(getCurrentSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", productId);
                            object result = await cmd.ExecuteScalarAsync();
                            currentQuantity = result != null ? Convert.ToDecimal(result) : 0;
                        }

                        decimal newQuantity = currentQuantity + quantity;
                        System.Diagnostics.Debug.WriteLine($"📦 المنتج {productId} في المخزن {storeId}: الرصيد السابق = {currentQuantity}, الإضافة = {quantity}, الرصيد الجديد = {newQuantity} (بالوحدة الأساسية)");

                        // تحديث المخزون (الكمية مخزنة بالوحدة الأساسية)
                        string sql = @"
                    INSERT INTO StoreInventory (StoreID, ProductID, Quantity, AvailableQuantity, CostPrice, LastUpdated)
                    VALUES (@storeId, @productId, @quantity, @quantity, @costPrice, CURRENT_TIMESTAMP)
                    ON CONFLICT(StoreID, ProductID) DO UPDATE SET
                        Quantity = Quantity + @quantity,
                        AvailableQuantity = AvailableQuantity + @quantity,
                        CostPrice = CASE WHEN @costPrice > 0 THEN @costPrice ELSE CostPrice END,
                        LastUpdated = CURRENT_TIMESTAMP";

                        using (var cmd = new SQLiteCommand(sql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", productId);
                            cmd.Parameters.AddWithValue("@quantity", quantity);
                            cmd.Parameters.AddWithValue("@costPrice", costPrice);
                            int result = await cmd.ExecuteNonQueryAsync();

                            if (result == 0)
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // ✅ تحديث الكمية بالوحدة الأساسية في جدول المنتجات
                        string updateProductQuantitySql = @"
                    UPDATE Products 
                    SET QuantityInBaseUnit = QuantityInBaseUnit + @quantity
                    WHERE ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantity);
                            cmd.Parameters.AddWithValue("@productId", productId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // التحقق من الرصيد بعد التحديث
                        decimal afterQuantity = 0;
                        using (var cmd = new SQLiteCommand(getCurrentSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", productId);
                            object result = await cmd.ExecuteScalarAsync();
                            afterQuantity = result != null ? Convert.ToDecimal(result) : 0;
                        }
                        System.Diagnostics.Debug.WriteLine($"✅ بعد التحديث: الرصيد = {afterQuantity} (بالوحدة الأساسية)");

                        // تسجيل حركة المخزون
                        string transactionSql = @"
                    INSERT INTO InventoryTransactions (
                        StoreID, ProductID, TransactionDate, TransactionType,
                        Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                        Description, CreatedBy
                    ) VALUES (
                        @storeId, @productId, @date, 'Opening',
                        @quantity, @oldQuantity, @newQuantity, @unitPrice, @totalAmount,
                        @description, @userId
                    )";

                        using (var transCmd = new SQLiteCommand(transactionSql, connection, transaction))
                        {
                            transCmd.Parameters.AddWithValue("@storeId", storeId);
                            transCmd.Parameters.AddWithValue("@productId", productId);
                            transCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                            transCmd.Parameters.AddWithValue("@quantity", quantity);
                            transCmd.Parameters.AddWithValue("@oldQuantity", currentQuantity);
                            transCmd.Parameters.AddWithValue("@newQuantity", afterQuantity);
                            transCmd.Parameters.AddWithValue("@unitPrice", costPrice);
                            transCmd.Parameters.AddWithValue("@totalAmount", quantity * costPrice);
                            transCmd.Parameters.AddWithValue("@description", "رصيد افتتاحي للمخزون (بالوحدة الأساسية)");
                            transCmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            await transCmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم إضافة {quantity} وحدة (أساسية) للمنتج {productId} في المخزن {storeId} بنجاح");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إضافة الرصيد الافتتاحي: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// إضافة رصيد افتتاحي لمنتج في المخزن الرئيسي (للتوافق مع الكود القديم)
        /// </summary>
        public async Task<bool> AddOpeningStockAsync(int productId, decimal quantity, decimal costPrice = 0)
        {
            int mainStoreId = await GetMainStoreIdAsync();
            if (mainStoreId == 0)
            {
                System.Diagnostics.Debug.WriteLine("لا يوجد مخزن رئيسي");
                return false;
            }
            return await AddOpeningStockAsync(productId, mainStoreId, quantity, costPrice);
        }


        /// <summary>
        /// الحصول على الكمية الحالية لمنتج في مخزن محدد
        /// </summary>
        public async Task<decimal> GetCurrentStockInStoreAsync(int productId, int storeId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    object result = await cmd.ExecuteScalarAsync();
                    decimal stock = result != null ? Convert.ToDecimal(result) : 0;
                    System.Diagnostics.Debug.WriteLine($"📦 GetCurrentStockInStoreAsync: المنتج {productId} في المخزن {storeId} => {stock} (بالوحدة الأساسية)");
                    return stock;
                }
            }
        }


        public async Task<ManagedConnection> GetManagedConnectionAsync(CancellationToken cancellationToken = default)
        {
            return await _connectionManager.GetConnectionAsync(cancellationToken);
        }

        public void CloseAllConnections()
        {
            _connectionManager?.CloseAllConnections();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connectionManager?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}