using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RasidAccountingSystem.Views
{
    public partial class PaymentVouchersView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private ObservableCollection<PaymentVoucherItem> _vouchersList;
        private bool _isInitialized = false;
        private bool _isSaving = false;
        private bool _isDeleting = false;
        private bool _isSupplierMode = true;

        // متغيرات AutoComplete للأطراف (موردين/عملاء)
        private List<PartySimple> AllPartiesList = new List<PartySimple>();
        private PartySimple SelectedPartyObj = null;
        private bool _isSelectingPartyFromList = false;
        private bool _isClearing = false;
        private bool _isNavigatingWithArrows = false;

        // متغيرات AutoComplete للفواتير (للموردين فقط)
        private List<PurchaseInvoiceSimple> AllInvoicesList = new List<PurchaseInvoiceSimple>();
        private PurchaseInvoiceSimple SelectedInvoiceObj = null;
        private bool _isSelectingInvoiceFromList = false;
        private bool _isNavigatingWithArrowsForInvoices = false;

        // متغيرات ComboBox لطرق السداد
        private List<PaymentMethodItem> AllPaymentMethodsList = new List<PaymentMethodItem>();
        private PaymentMethodItem SelectedPaymentMethodObj = null;
        private bool _isSelectingPaymentMethodFromList = false;

        // متغيرات ComboBox للخزائن
        private List<TreasurySimple> AllTreasuriesList = new List<TreasurySimple>();
        private TreasurySimple SelectedTreasuryObj = null;
        private bool _isSelectingTreasuryFromList = false;

        #endregion

        #region الكلاسات المساعدة (Helper Classes)

        public class PartySimple
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public bool IsSupplier { get; set; }
            public override string ToString() => Name;
        }

        public class TreasurySimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class PurchaseInvoiceSimple
        {
            public int Id { get; set; }
            public string DisplayText { get; set; }
            public decimal RemainingAmount { get; set; }
            public string InvoiceNumber { get; set; }
            public override string ToString() => DisplayText;
        }

        public class PaymentMethodItem
        {
            public string Value { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }

        // كلاسات للتعديل
        public class PaymentVoucherEditData
        {
            public int VoucherID { get; set; }
            public string VoucherNumber { get; set; }
            public DateTime VoucherDate { get; set; }
            public int SupplierID { get; set; }
            public int CustomerID { get; set; }
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; }
            public string CheckNumber { get; set; }
            public string Description { get; set; }
            public int TreasuryID { get; set; }
        }

        public class PartyEditItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class TreasuryEditItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        #endregion

        #region المنشئ

        public PaymentVouchersView()
        {
            InitializeComponent();
            this.Loaded += PaymentVouchersView_Loaded;
        }

        private async void PaymentVouchersView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _dbService = new DatabaseService();
                if (_dbService == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("فشل في تهيئة خدمة قاعدة البيانات", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                _vouchersList = new ObservableCollection<PaymentVoucherItem>();
                dgVouchers.ItemsSource = _vouchersList;

                CurrencyHelper.ConnectionString = _dbService.GetConnectionString();
                CurrencyHelper.LoadCurrencySettings();

                await LoadVoucherNumberAsync();
                await LoadAllPartiesForAutoCompleteAsync();
                await LoadAllTreasuriesForAutoCompleteAsync();
                await LoadAllInvoicesForAutoCompleteAsync();
                await LoadPaymentMethodsAsync();
                await LoadVouchersAsync();

                await Dispatcher.InvokeAsync(() =>
                {
                    cboPaymentMethod.ItemsSource = AllPaymentMethodsList;
                    cboTreasury.ItemsSource = AllTreasuriesList;
                    if (AllPaymentMethodsList.Count > 0) cboPaymentMethod.SelectedIndex = 0;
                    if (AllTreasuriesList.Count > 0) cboTreasury.SelectedIndex = 0;
                });

                AttachEvents();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في تحميل الصفحة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                System.Diagnostics.Debug.WriteLine($"Error in Loaded: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void AttachEvents()
        {
            try
            {
                if (txtAmount != null) txtAmount.TextChanged += TxtAmount_TextChanged;
                if (btnSave != null) btnSave.Click += BtnSave_Click;
                if (btnClear != null) btnClear.Click += BtnClear_Click;

                if (rbAll != null) rbAll.Checked += Filter_Checked;
                if (rbToday != null) rbToday.Checked += Filter_Checked;
                if (rbThisWeek != null) rbThisWeek.Checked += Filter_Checked;
                if (rbThisMonth != null) rbThisMonth.Checked += Filter_Checked;

                if (cboPartyType != null) cboPartyType.SelectionChanged += CboPartyType_SelectionChanged;
                if (cboPaymentMethod != null) cboPaymentMethod.SelectionChanged += CboPaymentMethod_SelectionChanged;
                if (cboTreasury != null) cboTreasury.SelectionChanged += CboTreasury_SelectionChanged;

                if (txtPartySearch != null)
                {
                    txtPartySearch.TextChanged += TxtPartySearch_TextChanged;
                    txtPartySearch.GotFocus += TxtPartySearch_GotFocus;
                    txtPartySearch.LostFocus += TxtPartySearch_LostFocus;
                    txtPartySearch.KeyDown += TxtPartySearch_KeyDown;
                    txtPartySearch.PreviewKeyDown += TxtPartySearch_PreviewKeyDown;
                }

                if (lstParties != null)
                {
                    lstParties.MouseUp += LstParties_MouseUp;
                    lstParties.KeyDown += LstParties_KeyDown;
                    lstParties.PreviewKeyDown += LstParties_PreviewKeyDown;
                }

                if (txtInvoiceSearch != null)
                {
                    txtInvoiceSearch.TextChanged += TxtInvoiceSearch_TextChanged;
                    txtInvoiceSearch.GotFocus += TxtInvoiceSearch_GotFocus;
                    txtInvoiceSearch.LostFocus += TxtInvoiceSearch_LostFocus;
                    txtInvoiceSearch.KeyDown += TxtInvoiceSearch_KeyDown;
                    txtInvoiceSearch.PreviewKeyDown += TxtInvoiceSearch_PreviewKeyDown;
                }

                if (lstInvoices != null)
                {
                    lstInvoices.MouseUp += LstInvoices_MouseUp;
                    lstInvoices.KeyDown += LstInvoices_KeyDown;
                    lstInvoices.PreviewKeyDown += LstInvoices_PreviewKeyDown;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error attaching events: {ex.Message}");
            }
        }

        #endregion

        #region إدارة التمرير وإغلاق القوائم المنسدلة

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 || e.HorizontalChange != 0)
            {
                CloseAllPopups();
            }
        }

        private void CloseAllPopups()
        {
            if (popupParties != null && popupParties.IsOpen) popupParties.IsOpen = false;
            if (popupInvoices != null && popupInvoices.IsOpen) popupInvoices.IsOpen = false;
        }

        #endregion

        #region دوال حساب الرصيد الفعلي وتحديثه

        private async Task<decimal> GetSupplierActualBalanceAsync(int supplierId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            COALESCE((SELECT OpeningBalance FROM Suppliers WHERE SupplierID = @supplierId), 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM SupplierTransactions 
                                WHERE SupplierID = @supplierId 
                                AND TransactionType = 'Payment'
                            ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"💰 الرصيد الفعلي للمورد {supplierId}: {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSupplierActualBalanceAsync Error: {ex.Message}");
                return 0;
            }
        }

        // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice' بدل DebitAmount (راجع
        // AddCustomerInvoiceTransactionAsync)، فكانت مساهمة فواتير العميل تساوي صفر دائمًا.
        private async Task<decimal> GetCustomerActualBalanceAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            COALESCE((SELECT OpeningBalance FROM Customers WHERE CustomerID = @customerId), 0) +
                            COALESCE((
                                SELECT SUM(DebitAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId 
                                AND TransactionType = 'Invoice'
                            ), 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId 
                                AND TransactionType = 'Receipt'
                            ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"الرصيد الفعلي للعميل {customerId}: {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerActualBalanceAsync Error: {ex.Message}");
                return 0;
            }
        }

        private async Task<decimal> GetPartyActualBalanceAsync(PartySimple party)
        {
            if (party == null) return 0;

            if (party.IsSupplier)
            {
                return await GetSupplierActualBalanceAsync(party.Id);
            }
            else
            {
                return await GetCustomerActualBalanceAsync(party.Id);
            }
        }

        private async Task UpdateSupplierBalanceInTableAsync(int supplierId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    // ✅ إصلاح باج خطير: المعادلة القديمة كانت (أ) تتجاهل فواتير الشراء
                    // (TransactionType = 'Purchase') تماماً من الحساب، (ب) تقرأ عمود CreditAmount
                    // لحركات الدفع بينما هذه الحركات فعلياً تُخزَّن في DebitAmount (راجع
                    // AddSupplierPaymentTransactionAsync)، فكانت SUM(CreditAmount) لحركات الدفع
                    // تساوي صفر دائماً. النتيجة: كل سند صرف يُسجَّل لمورد كان "يُصفِّر" رصيده
                    // الفعلي عائداً لرصيد أول المدة فقط، ماحياً كل فواتير الشراء والمدفوعات
                    // السابقة من الرصيد المعروض على الفور. المعادلة الصحيحة الموحَّدة مع باقي
                    // أنحاء البرنامج: الرصيد = أول المدة + مشتريات (Purchase) - مدفوعات (Payment) - مرتجعات (Refund)
                    string updateSql = @"
                        UPDATE Suppliers 
                        SET CurrentBalance = 
                            COALESCE(OpeningBalance, 0) + COALESCE((
                                SELECT SUM(
                                    CASE 
                                        WHEN TransactionType = 'Purchase' THEN CreditAmount
                                        WHEN TransactionType = 'Payment' THEN -DebitAmount
                                        WHEN TransactionType = 'Refund' THEN -DebitAmount
                                        ELSE (CreditAmount - DebitAmount)
                                    END
                                )
                                FROM SupplierTransactions 
                                WHERE SupplierID = @supplierId
                            ), 0),
                        ModifiedDate = CURRENT_TIMESTAMP
                        WHERE SupplierID = @supplierId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد المورد {supplierId} في جدول Suppliers، الصفوف المتأثرة: {rowsAffected}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSupplierBalanceInTableAsync Error: {ex.Message}");
                Logger.LogError($"فشل تحديث رصيد المورد #{supplierId} بعد سند صرف", ex, "PaymentVouchersView");
            }
        }

        private async Task UpdateCustomerBalanceInTableAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه
                    // الحركات تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync
                    // وتريجر trig_customer_transaction_from_sales) - نفس الباج اللي اتصلح بالفعل
                    // لصيغة الموردين المطابقة فوق (UpdateSupplierBalanceInTableAsync) في نفس الملف،
                    // لكن اتفات إصلاحه هنا. النتيجة: كل سند صرف مرتبط بعميل (مرتجع فلوس له مثلًا)
                    // كان "يُصفِّر" مساهمة كل فواتيره من رصيده الحالي المعروض فورًا.
                    string updateSql = @"
                        UPDATE Customers 
                        SET CurrentBalance = 
                            COALESCE(OpeningBalance, 0) + COALESCE((
                                SELECT SUM(
                                    CASE 
                                        WHEN TransactionType = 'Invoice' THEN DebitAmount
                                        WHEN TransactionType = 'Receipt' THEN -CreditAmount
                                        WHEN TransactionType = 'Refund' THEN -CreditAmount
                                        ELSE (DebitAmount - CreditAmount)
                                    END
                                )
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId
                            ), 0),
                        ModifiedDate = CURRENT_TIMESTAMP
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"تم تحديث رصيد العميل {customerId} في جدول Customers، الصفوف المتأثرة: {rowsAffected}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCustomerBalanceInTableAsync Error: {ex.Message}");
                Logger.LogError($"فشل تحديث رصيد العميل #{customerId} بعد سند صرف", ex, "PaymentVouchersView");
            }
        }

        private async Task UpdatePartyBalanceInTableAsync(PartySimple party)
        {
            if (party == null) return;

            if (party.IsSupplier)
            {
                await UpdateSupplierBalanceInTableAsync(party.Id);
            }
            else
            {
                await UpdateCustomerBalanceInTableAsync(party.Id);
            }
        }

        #endregion

        #region دوال تحميل البيانات الأساسية

        private async Task LoadVoucherNumberAsync()
        {
            try
            {
                if (_dbService == null)
                {
                    await Dispatcher.InvokeAsync(() => txtVoucherNumber.Text = $"PAY-{DateTime.Now:yyyyMMddHHmmss}");
                    return;
                }

                string voucherNumber = await _dbService.GeneratePaymentVoucherNumberAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    txtVoucherNumber.Text = voucherNumber;
                    dpVoucherDate.SelectedDate = DateTime.Now;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading voucher number: {ex.Message}");
                await Dispatcher.InvokeAsync(() => txtVoucherNumber.Text = $"PAY-{DateTime.Now:yyyyMMddHHmmss}");
            }
        }

        private async Task<Dictionary<int, decimal>> GetAllSuppliersActualBalancesAsync()
        {
            var result = new Dictionary<int, decimal>();
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            s.SupplierID,
                            COALESCE(s.OpeningBalance, 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM SupplierTransactions 
                                WHERE SupplierID = s.SupplierID 
                                AND TransactionType = 'Payment'
                            ), 0) as ActualBalance
                        FROM Suppliers s";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int supplierId = Convert.ToInt32(reader["SupplierID"]);
                            decimal balance = reader["ActualBalance"] != DBNull.Value ? Convert.ToDecimal(reader["ActualBalance"]) : 0;
                            result[supplierId] = balance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllSuppliersActualBalancesAsync Error: {ex.Message}");
            }
            return result;
        }

        private async Task<Dictionary<int, decimal>> GetAllCustomersActualBalancesAsync()
        {
            var result = new Dictionary<int, decimal>();
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            c.CustomerID,
                            COALESCE(c.OpeningBalance, 0) +
                            COALESCE((
                                SELECT SUM(DebitAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = c.CustomerID 
                                AND TransactionType = 'Invoice'
                            ), 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = c.CustomerID 
                                AND TransactionType = 'Receipt'
                            ), 0) as ActualBalance
                        FROM Customers c";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int customerId = Convert.ToInt32(reader["CustomerID"]);
                            decimal balance = reader["ActualBalance"] != DBNull.Value ? Convert.ToDecimal(reader["ActualBalance"]) : 0;
                            result[customerId] = balance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllCustomersActualBalancesAsync Error: {ex.Message}");
            }
            return result;
        }

        private async Task LoadAllPartiesForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;
                AllPartiesList.Clear();

                var suppliers = await _dbService.GetSuppliersAsync();
                if (suppliers != null)
                {
                    var supplierBalances = await GetAllSuppliersActualBalancesAsync();
                    foreach (var supplier in suppliers)
                    {
                        if (supplier != null)
                        {
                            decimal actualBalance = supplierBalances.TryGetValue(supplier.SupplierID, out var bal) ? bal : 0;
                            string supplierName = !string.IsNullOrEmpty(supplier.SupplierNameAr) ? supplier.SupplierNameAr : supplier.SupplierName;

                            string displayText;
                            if (actualBalance > 0)
                            {
                                displayText = $"{supplierName} (علينا: {CurrencyHelper.FormatAmount(actualBalance)})";
                            }
                            else if (actualBalance < 0)
                            {
                                decimal absoluteBalance = Math.Abs(actualBalance);
                                displayText = $"{supplierName} (لنا: {CurrencyHelper.FormatAmount(absoluteBalance)})";
                            }
                            else
                            {
                                displayText = $"{supplierName} (متزن)";
                            }

                            AllPartiesList.Add(new PartySimple
                            {
                                Id = supplier.SupplierID,
                                Code = supplier.SupplierCode,
                                Name = displayText,
                                CurrentBalance = actualBalance,
                                IsSupplier = true
                            });
                        }
                    }
                }

                var customers = await _dbService.GetCustomersAsync();
                if (customers != null)
                {
                    var customerBalances = await GetAllCustomersActualBalancesAsync();
                    foreach (var customer in customers)
                    {
                        if (customer != null)
                        {
                            decimal actualBalance = customerBalances.TryGetValue(customer.CustomerID, out var bal) ? bal : 0;
                            string customerName = !string.IsNullOrEmpty(customer.CustomerNameAr) ? customer.CustomerNameAr : customer.CustomerName;

                            string displayText;
                            if (actualBalance > 0)
                            {
                                displayText = $"{customerName} (عليه: {CurrencyHelper.FormatAmount(actualBalance)})";
                            }
                            else if (actualBalance < 0)
                            {
                                decimal absoluteBalance = Math.Abs(actualBalance);
                                displayText = $"{customerName} (له: {CurrencyHelper.FormatAmount(absoluteBalance)})";
                            }
                            else
                            {
                                displayText = $"{customerName} (متزن)";
                            }

                            AllPartiesList.Add(new PartySimple
                            {
                                Id = customer.CustomerID,
                                Code = customer.CustomerCode,
                                Name = displayText,
                                CurrentBalance = actualBalance,
                                IsSupplier = false
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllPartiesList.Count} طرف بأرصدتهم الصحيحة");
                UpdatePartyListByType();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllPartiesForAutoCompleteAsync Error: {ex.Message}");
            }
        }

        private async Task LoadAllTreasuriesForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;
                AllTreasuriesList.Clear();

                var treasuries = await _dbService.GetActiveTreasuriesAsync();
                if (treasuries != null)
                {
                    foreach (var treasury in treasuries)
                    {
                        if (treasury != null)
                        {
                            AllTreasuriesList.Add(new TreasurySimple
                            {
                                Id = treasury.Id,
                                Code = treasury.Code,
                                Name = $"{treasury.Name} (رصيد: {CurrencyHelper.FormatAmount(treasury.CurrentBalance)})",
                                CurrentBalance = treasury.CurrentBalance
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllTreasuriesList.Count} خزينة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllTreasuriesForAutoCompleteAsync Error: {ex.Message}");
            }
        }

        private async Task LoadAllInvoicesForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;
                AllInvoicesList.Clear();

                AllInvoicesList.Add(new PurchaseInvoiceSimple
                {
                    Id = 0,
                    DisplayText = "-- بدون فاتورة --",
                    RemainingAmount = 0,
                    InvoiceNumber = ""
                });

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllInvoicesList.Count} فاتورة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAllInvoicesForAutoCompleteAsync Error: {ex.Message}");
            }
        }

        private async Task LoadSupplierInvoicesForAutoCompleteAsync(int supplierId)
        {
            try
            {
                if (_dbService == null) return;
                AllInvoicesList.Clear();

                AllInvoicesList.Add(new PurchaseInvoiceSimple
                {
                    Id = 0,
                    DisplayText = "-- بدون فاتورة --",
                    RemainingAmount = 0,
                    InvoiceNumber = ""
                });

                using (var connection = _dbService.GetConnection())
                {
                    await connection.OpenAsync();
                    string sql = @"
                        SELECT InvoiceID, InvoiceNumber, RemainingAmount
                        FROM PurchaseInvoices 
                        WHERE SupplierID = @supplierId 
                        AND PaymentStatus != 'Paid'
                        AND IsVoid = 0
                        ORDER BY InvoiceDate DESC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                AllInvoicesList.Add(new PurchaseInvoiceSimple
                                {
                                    Id = reader.GetInt32(0),
                                    InvoiceNumber = reader.GetString(1),
                                    RemainingAmount = reader.GetDecimal(2),
                                    DisplayText = $"{reader.GetString(1)} - المتبقي: {CurrencyHelper.FormatAmount(reader.GetDecimal(2))}"
                                });
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllInvoicesList.Count} فاتورة للمورد {supplierId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSupplierInvoicesForAutoCompleteAsync Error: {ex.Message}");
            }
        }

        private async Task LoadPaymentMethodsAsync()
        {
            try
            {
                AllPaymentMethodsList.Clear();
                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Cash", DisplayName = "💰 نقدي" });
                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Check", DisplayName = "🏦 شيك" });
                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Transfer", DisplayName = "💳 تحويل بنكي" });
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPaymentMethodsAsync Error: {ex.Message}");
            }
        }

        private async Task LoadVouchersAsync()
        {
            try
            {
                if (_dbService == null)
                {
                    System.Diagnostics.Debug.WriteLine("_dbService is null in LoadVouchersAsync");
                    UpdateEmptyUI();
                    return;
                }

                string filter = "All";
                await Dispatcher.InvokeAsync(() =>
                {
                    if (rbToday?.IsChecked == true) filter = "Today";
                    else if (rbThisWeek?.IsChecked == true) filter = "ThisWeek";
                    else if (rbThisMonth?.IsChecked == true) filter = "ThisMonth";
                });

                await Dispatcher.InvokeAsync(() => _vouchersList?.Clear());

                var payments = await _dbService.GetPaymentVouchersWithDetailsAsync(filter);

                decimal totalAmount = 0;
                if (payments != null && _vouchersList != null)
                {
                    foreach (var payment in payments)
                    {
                        if (payment != null)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                _vouchersList.Add(new PaymentVoucherItem
                                {
                                    PaymentID = payment.VoucherID,
                                    VoucherNumber = payment.VoucherNumber,
                                    PaymentDate = payment.VoucherDate,
                                    PartyName = payment.PartyName,
                                    Amount = payment.Amount,
                                    PaymentMethodDisplay = payment.PaymentMethod,
                                    TreasuryName = payment.TreasuryName,
                                    CheckNumber = payment.CheckNumber,
                                    Description = payment.Description
                                });
                            });
                            totalAmount += payment.Amount;
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (lblTotalAmount != null) lblTotalAmount.Text = CurrencyHelper.FormatAmount(totalAmount);
                    if (lblVouchersCount != null) lblVouchersCount.Text = _vouchersList?.Count.ToString() ?? "0";
                    if (dgVouchers != null) dgVouchers.Visibility = _vouchersList?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    if (lblNoData != null) lblNoData.Visibility = _vouchersList?.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                });

                System.Diagnostics.Debug.WriteLine($"تم تحميل {_vouchersList?.Count ?? 0} سند صرف");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadVouchersAsync Error: {ex.Message}");
                UpdateEmptyUI();
            }
        }

        private void UpdateEmptyUI()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (lblTotalAmount != null) lblTotalAmount.Text = "0";
                if (lblVouchersCount != null) lblVouchersCount.Text = "0";
                if (dgVouchers != null) dgVouchers.Visibility = Visibility.Collapsed;
                if (lblNoData != null) lblNoData.Visibility = Visibility.Visible;
            });
        }

        #endregion

        #region دوال AutoComplete للطرف

        private void UpdatePartyListByType()
        {
            var filtered = AllPartiesList.Where(p => p.IsSupplier == _isSupplierMode).ToList();
            if (lstParties != null) lstParties.ItemsSource = filtered;
        }

        private void CboPartyType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            _isSupplierMode = cboPartyType.SelectedIndex == 0;
            if (lblParty != null) lblParty.Text = _isSupplierMode ? "المورد" : "العميل";
            if (txtPartyPlaceholder != null) txtPartyPlaceholder.Text = _isSupplierMode ? "ابحث باسم المورد أو الكود..." : "ابحث باسم العميل أو الكود...";

            txtPartySearch.Text = "";
            SelectedPartyObj = null;
            if (borderPartyBalance != null) borderPartyBalance.Visibility = Visibility.Collapsed;
            if (borderBalanceAfter != null) borderBalanceAfter.Visibility = Visibility.Collapsed;
            if (pnlInvoice != null) pnlInvoice.Visibility = Visibility.Collapsed;

            UpdatePartyListByType();
        }

        private void TxtPartySearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtPartyPlaceholder != null) txtPartyPlaceholder.Visibility = Visibility.Collapsed;
            if (borderPartySearch != null)
            {
                borderPartySearch.BorderBrush = (Brush)FindResource("PaymentPrimaryColor");
                borderPartySearch.BorderThickness = new Thickness(2);
            }
        }

        private void TxtPartySearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isNavigatingWithArrows)
            {
                if (string.IsNullOrWhiteSpace(txtPartySearch.Text) && txtPartyPlaceholder != null)
                    txtPartyPlaceholder.Visibility = Visibility.Visible;

                if (borderPartySearch != null)
                {
                    borderPartySearch.BorderBrush = (Brush)FindResource("BorderLightColor");
                    borderPartySearch.BorderThickness = new Thickness(1);
                }

                Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    if (popupParties != null && lstParties != null && !lstParties.IsMouseOver)
                        popupParties.IsOpen = false;
                }));
            }
        }

        private void TxtPartySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingPartyFromList) return;

            string searchText = txtPartySearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupParties != null) popupParties.IsOpen = false;
                if (!_isClearing)
                {
                    SelectedPartyObj = null;
                    if (borderPartyBalance != null) borderPartyBalance.Visibility = Visibility.Collapsed;
                    if (borderBalanceAfter != null) borderBalanceAfter.Visibility = Visibility.Collapsed;
                    if (lblPartyBalance != null) lblPartyBalance.Text = CurrencyHelper.FormatAmount(0);
                }
                return;
            }

            var filtered = AllPartiesList
                .Where(p => p.IsSupplier == _isSupplierMode && p.Name?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (lstParties != null) lstParties.ItemsSource = filtered;
            if (popupParties != null) popupParties.IsOpen = filtered.Count > 0;
        }

        private void LstParties_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstParties.SelectedItem is PartySimple selectedParty) SelectParty(selectedParty);
        }

        private void LstParties_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstParties.SelectedItem is PartySimple selectedParty)
            {
                SelectParty(selectedParty);
                if (popupParties != null) popupParties.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && popupParties != null)
            {
                popupParties.IsOpen = false;
                txtPartySearch.Focus();
                e.Handled = true;
            }
        }

        private void LstParties_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up && lstParties.SelectedIndex == 0)
            {
                txtPartySearch.Focus();
                e.Handled = true;
            }
        }

        private void TxtPartySearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popupParties != null && popupParties.IsOpen && lstParties?.Items.Count > 0)
            {
                lstParties.Focus();
                if (lstParties.SelectedIndex == -1) lstParties.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && popupParties?.IsOpen == true && lstParties?.SelectedItem is PartySimple p)
            {
                SelectParty(p);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && popupParties != null)
            {
                popupParties.IsOpen = false;
                e.Handled = true;
            }
        }

        private void TxtPartySearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down) e.Handled = true;
        }

        private async void SelectParty(PartySimple party)
        {
            if (party == null) return;

            _isSelectingPartyFromList = true;
            SelectedPartyObj = party;
            txtPartySearch.Text = party.Name;
            if (popupParties != null) popupParties.IsOpen = false;

            decimal actualBalance = await GetPartyActualBalanceAsync(party);
            party.CurrentBalance = actualBalance;

            if (borderPartyBalance != null) borderPartyBalance.Visibility = Visibility.Visible;
            if (borderBalanceAfter != null) borderBalanceAfter.Visibility = Visibility.Visible;
            if (lblPartyBalance != null) lblPartyBalance.Text = CurrencyHelper.FormatAmount(actualBalance);

            if (lblPartyBalanceStatus != null && borderBalanceStatus != null)
            {
                if (actualBalance > 0)
                {
                    lblPartyBalanceStatus.Text = party.IsSupplier ? "دائن (للمورد)" : "مدين (عليه)";
                    borderBalanceStatus.Background = party.IsSupplier ? (Brush)FindResource("SuccessColor") : (Brush)FindResource("DangerColor");
                }
                else if (actualBalance < 0)
                {
                    lblPartyBalanceStatus.Text = party.IsSupplier ? "مدين (للمورد)" : "دائن (له)";
                    borderBalanceStatus.Background = party.IsSupplier ? (Brush)FindResource("DangerColor") : (Brush)FindResource("SuccessColor");
                }
                else
                {
                    lblPartyBalanceStatus.Text = "متزن";
                    borderBalanceStatus.Background = (Brush)FindResource("InfoColor");
                }
            }

            UpdateBalanceAfter();

            if (party.IsSupplier)
            {
                await LoadSupplierInvoicesForAutoCompleteAsync(party.Id);
                if (pnlInvoice != null) pnlInvoice.Visibility = Visibility.Visible;
                txtInvoiceSearch.Text = "";
                if (txtInvoicePlaceholder != null) txtInvoicePlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                if (pnlInvoice != null) pnlInvoice.Visibility = Visibility.Collapsed;
                SelectedInvoiceObj = null;
            }

            _isSelectingPartyFromList = false;
            System.Diagnostics.Debug.WriteLine($"تم اختيار الطرف: {party.Name} (ID: {party.Id}, الرصيد الفعلي: {actualBalance})");
        }

        #endregion

        #region دوال AutoComplete للفواتير

        private void TxtInvoiceSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtInvoicePlaceholder != null) txtInvoicePlaceholder.Visibility = Visibility.Collapsed;
            if (borderInvoiceSearch != null)
            {
                borderInvoiceSearch.BorderBrush = (Brush)FindResource("PaymentPrimaryColor");
                borderInvoiceSearch.BorderThickness = new Thickness(2);
            }
        }

        private void TxtInvoiceSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isNavigatingWithArrowsForInvoices)
            {
                if (string.IsNullOrWhiteSpace(txtInvoiceSearch.Text) && txtInvoicePlaceholder != null)
                    txtInvoicePlaceholder.Visibility = Visibility.Visible;

                if (borderInvoiceSearch != null)
                {
                    borderInvoiceSearch.BorderBrush = (Brush)FindResource("BorderLightColor");
                    borderInvoiceSearch.BorderThickness = new Thickness(1);
                }

                Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    if (popupInvoices != null && lstInvoices != null && !lstInvoices.IsMouseOver)
                        popupInvoices.IsOpen = false;
                }));
            }
        }

        private void TxtInvoiceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingInvoiceFromList) return;

            string searchText = txtInvoiceSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                if (popupInvoices != null) popupInvoices.IsOpen = false;
                if (!_isClearing) SelectedInvoiceObj = null;
                UpdateBalanceAfter();
                return;
            }

            var filtered = AllInvoicesList.Where(i => i.DisplayText?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (lstInvoices != null) lstInvoices.ItemsSource = filtered;
            if (popupInvoices != null) popupInvoices.IsOpen = filtered.Count > 0;
        }

        private void LstInvoices_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstInvoices.SelectedItem is PurchaseInvoiceSimple inv) SelectInvoice(inv);
        }

        private void LstInvoices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstInvoices.SelectedItem is PurchaseInvoiceSimple inv)
            {
                SelectInvoice(inv);
                if (popupInvoices != null) popupInvoices.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && popupInvoices != null)
            {
                popupInvoices.IsOpen = false;
                txtInvoiceSearch.Focus();
                e.Handled = true;
            }
        }

        private void LstInvoices_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up && lstInvoices.SelectedIndex == 0)
            {
                txtInvoiceSearch.Focus();
                e.Handled = true;
            }
        }

        private void TxtInvoiceSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popupInvoices != null && popupInvoices.IsOpen && lstInvoices?.Items.Count > 0)
            {
                lstInvoices.Focus();
                if (lstInvoices.SelectedIndex == -1) lstInvoices.SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && popupInvoices?.IsOpen == true && lstInvoices?.SelectedItem is PurchaseInvoiceSimple inv)
            {
                SelectInvoice(inv);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && popupInvoices != null)
            {
                popupInvoices.IsOpen = false;
                e.Handled = true;
            }
        }

        private void TxtInvoiceSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down) e.Handled = true;
        }

        private void SelectInvoice(PurchaseInvoiceSimple invoice)
        {
            if (invoice == null) return;

            _isSelectingInvoiceFromList = true;
            SelectedInvoiceObj = invoice;
            txtInvoiceSearch.Text = invoice.DisplayText;
            if (popupInvoices != null) popupInvoices.IsOpen = false;
            UpdateBalanceAfter();
            _isSelectingInvoiceFromList = false;
        }

        #endregion

        #region دوال ComboBox لطريقة السداد

        private void CboPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPaymentMethod.SelectedItem is PaymentMethodItem method)
            {
                SelectedPaymentMethodObj = method;
                if (pnlCheckNumber != null)
                {
                    pnlCheckNumber.Visibility = method.Value == "Check" ? Visibility.Visible : Visibility.Collapsed;
                    if (method.Value != "Check" && txtCheckNumber != null) txtCheckNumber.Text = "";
                }
            }
        }

        #endregion

        #region دوال ComboBox للخزينة

        private void CboTreasury_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboTreasury.SelectedItem is TreasurySimple treasury)
            {
                SelectedTreasuryObj = treasury;
            }
        }

        #endregion

        #region الأحداث الأخرى

        private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBalanceAfter();
        }

        private void UpdateBalanceAfter()
        {
            try
            {
                if (SelectedPartyObj != null && txtAmount != null && decimal.TryParse(txtAmount.Text, out decimal amount) && lblBalanceAfter != null)
                {
                    decimal balanceAfter;
                    if (SelectedInvoiceObj != null && SelectedInvoiceObj.Id > 0)
                    {
                        balanceAfter = SelectedInvoiceObj.RemainingAmount - amount;
                    }
                    else
                    {
                        if (SelectedPartyObj.IsSupplier)
                        {
                            balanceAfter = SelectedPartyObj.CurrentBalance - amount;
                        }
                        else
                        {
                            balanceAfter = SelectedPartyObj.CurrentBalance + amount;
                        }
                    }

                    Dispatcher.InvokeAsync(() =>
                    {
                        lblBalanceAfter.Text = CurrencyHelper.FormatAmount(balanceAfter);
                        lblBalanceAfter.Foreground = balanceAfter < 0 ? (Brush)FindResource("WarningColor") : (Brush)FindResource("SuccessColor");
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateBalanceAfter Error: {ex.Message}");
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving) return;
            _isSaving = true;

            try
            {
                if (_dbService == null)
                {
                    await ShowMessage("خدمة قاعدة البيانات غير متاحة", "خطأ");
                    return;
                }
                if (SelectedPartyObj == null) { await ShowMessage("الرجاء اختيار طرف", "تنبيه"); return; }
                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0) { await ShowMessage("المبلغ غير صحيح", "تنبيه"); txtAmount.Focus(); return; }
                if (SelectedTreasuryObj == null) { await ShowMessage("الرجاء اختيار الخزينة", "تنبيه"); return; }
                if (SelectedPaymentMethodObj == null) { await ShowMessage("الرجاء اختيار طريقة السداد", "تنبيه"); return; }

                if (SelectedPartyObj.IsSupplier && amount > SelectedPartyObj.CurrentBalance)
                {
                    await ShowMessage($"رصيد المورد غير كافٍ!\nالرصيد الحالي: {CurrencyHelper.FormatAmount(SelectedPartyObj.CurrentBalance)}\nالمبلغ المطلوب: {CurrencyHelper.FormatAmount(amount)}", "رصيد غير كافٍ");
                    txtAmount.Focus();
                    return;
                }

                int currentUserId = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1;
                int? invoiceId = (SelectedInvoiceObj != null && SelectedInvoiceObj.Id > 0 && SelectedPartyObj.IsSupplier) ? SelectedInvoiceObj.Id : (int?)null;

                bool success = await _dbService.SavePaymentVoucherAsync(
                    txtVoucherNumber.Text,
                    dpVoucherDate.SelectedDate ?? DateTime.Now,
                    SelectedPartyObj.IsSupplier ? SelectedPartyObj.Id : 0,
                    SelectedPartyObj.IsSupplier ? 0 : SelectedPartyObj.Id,
                    amount,
                    SelectedPaymentMethodObj.Value,
                    txtCheckNumber.Text,
                    txtDescription.Text,
                    SelectedTreasuryObj.Id,
                    currentUserId,
                    invoiceId
                );

                if (success)
                {
                    await UpdatePartyBalanceInTableAsync(SelectedPartyObj);
                    await ShowMessage($"تم حفظ سند الصرف بنجاح\nالمبلغ: {CurrencyHelper.FormatAmount(amount)}", "تم");
                    ClearForm();
                    await GenerateVoucherNumberAsync();
                    await LoadAllPartiesForAutoCompleteAsync();
                    await LoadAllTreasuriesForAutoCompleteAsync();
                    await LoadVouchersAsync();
                }
                else
                {
                    await ShowMessage("حدث خطأ في حفظ سند الصرف", "خطأ");
                }
            }
            catch (Exception ex)
            {
                await ShowMessage($"خطأ: {ex.Message}", "خطأ");
                System.Diagnostics.Debug.WriteLine($"SaveVoucherAsync Error: {ex.Message}");
            }
            finally { _isSaving = false; }
        }

        private async Task GenerateVoucherNumberAsync()
        {
            try
            {
                string voucherNumber = await _dbService.GeneratePaymentVoucherNumberAsync();
                await Dispatcher.InvokeAsync(() => txtVoucherNumber.Text = voucherNumber);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => txtVoucherNumber.Text = $"PAY-{DateTime.Now:yyyyMMddHHmmss}");
            }
        }

        private async Task ShowMessage(string message, string title)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK,
                    title == "خطأ" ? MessageBoxImage.Error : MessageBoxImage.Information);
            });
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            _isClearing = true;
            Dispatcher.InvokeAsync(() =>
            {
                if (txtAmount != null) txtAmount.Text = "";
                if (txtDescription != null) txtDescription.Text = "";
                if (txtCheckNumber != null) txtCheckNumber.Text = "";
                if (txtPartySearch != null) txtPartySearch.Text = "";
                if (txtInvoiceSearch != null) txtInvoiceSearch.Text = "";
                if (cboPaymentMethod != null) cboPaymentMethod.SelectedIndex = 0;
                if (cboTreasury != null && AllTreasuriesList.Count > 0) cboTreasury.SelectedIndex = 0;
                if (dpVoucherDate != null) dpVoucherDate.SelectedDate = DateTime.Now;
                if (pnlCheckNumber != null) pnlCheckNumber.Visibility = Visibility.Collapsed;
                if (borderPartyBalance != null) borderPartyBalance.Visibility = Visibility.Collapsed;
                if (borderBalanceAfter != null) borderBalanceAfter.Visibility = Visibility.Collapsed;
                if (pnlInvoice != null) pnlInvoice.Visibility = Visibility.Collapsed;
                if (txtPartyPlaceholder != null) txtPartyPlaceholder.Visibility = Visibility.Visible;
                if (txtInvoicePlaceholder != null) txtInvoicePlaceholder.Visibility = Visibility.Visible;
            });
            SelectedPartyObj = null;
            SelectedInvoiceObj = null;
            _isClearing = false;
        }

        private async void Filter_Checked(object sender, RoutedEventArgs e)
        {
            await LoadVouchersAsync();
        }

        private async void DeleteVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeleting) return;

            if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int paymentId))
            {
                var voucher = _vouchersList?.FirstOrDefault(v => v.PaymentID == paymentId);
                if (voucher == null) return;

                if (MessageBox.Show($"هل أنت متأكد من حذف سند الصرف رقم {voucher.VoucherNumber}؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                _isDeleting = true;
                try
                {
                    if (await _dbService.DeletePaymentVoucherAsync(paymentId))
                    {
                        await ShowMessage("تم حذف سند الصرف بنجاح", "تم");
                        await LoadAllPartiesForAutoCompleteAsync();
                        await LoadAllTreasuriesForAutoCompleteAsync();
                        await LoadVouchersAsync();
                    }
                    else
                    {
                        await ShowMessage("حدث خطأ في حذف سند الصرف", "خطأ");
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessage($"خطأ: {ex.Message}", "خطأ");
                }
                finally { _isDeleting = false; }
            }
        }

        #endregion

        #region دوال طباعة سند الصرف

        private async void PrintVoucher_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null || btn.Tag == null) return;

                if (!int.TryParse(btn.Tag.ToString(), out int paymentId)) return;

                var voucher = _vouchersList?.FirstOrDefault(v => v.PaymentID == paymentId);
                if (voucher == null)
                {
                    await ShowMessage("لم يتم العثور على بيانات السند", "تنبيه");
                    return;
                }

                var voucherDetails = await GetPaymentVoucherDetailsAsync(paymentId);
                if (voucherDetails == null)
                {
                    await ShowMessage("حدث خطأ في جلب بيانات السند", "خطأ");
                    return;
                }

                FixedDocument fixedDocument = CreatePaymentVoucherPrintDocument(voucherDetails);
                if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                {
                    await ShowPrintPreviewDialogAsync(fixedDocument, voucherDetails.VoucherNumber);
                }
                else
                {
                    MessageBox.Show("حدث خطأ في إنشاء مستند الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrintVoucher_Click Error: {ex.Message}");
                await ShowMessage($"خطأ في الطباعة: {ex.Message}", "خطأ");
            }
        }

        private async Task ShowPrintPreviewDialogAsync(FixedDocument fixedDocument, string voucherNumber)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Window previewWindow = new Window
                {
                    Title = $"معاينة قبل الطباعة - سند صرف {voucherNumber}",
                    Width = 850,
                    Height = 950,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.CanResize,
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    FlowDirection = FlowDirection.RightToLeft
                };

                DocumentViewer documentViewer = new DocumentViewer
                {
                    Document = fixedDocument
                };

                StackPanel toolbar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    FlowDirection = FlowDirection.RightToLeft
                };

                Button printButton = new Button
                {
                    Content = "🖨️ طباعة",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    Foreground = Brushes.White,
                    Cursor = Cursors.Hand
                };
                printButton.Click += (s, ev) =>
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        printDialog.PrintDocument(fixedDocument.DocumentPaginator, $"سند_صرف_{voucherNumber}");
                        MessageBox.Show("تم إرسال المستند إلى الطابعة بنجاح", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };

                Button closeButton = new Button
                {
                    Content = "✖ إغلاق",
                    Width = 85,
                    Height = 35,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    Cursor = Cursors.Hand
                };
                closeButton.Click += (s, ev) => previewWindow.Close();

                toolbar.Children.Add(printButton);
                toolbar.Children.Add(closeButton);

                DockPanel mainPanel = new DockPanel();
                DockPanel.SetDock(toolbar, Dock.Top);
                mainPanel.Children.Add(toolbar);
                mainPanel.Children.Add(documentViewer);

                previewWindow.Content = mainPanel;
                previewWindow.Owner = Window.GetWindow(this);
                previewWindow.ShowDialog();
            });
        }

        private async Task<PaymentVoucherPrintData> GetPaymentVoucherDetailsAsync(int paymentId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            pv.VoucherNumber,
                            pv.VoucherDate,
                            pv.Amount,
                            pv.PaymentMethod,
                            pv.CheckNumber,
                            pv.Description,
                            pv.CustomerID,
                            pv.SupplierID,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName,
                            COALESCE(c.CustomerCode, '') as CustomerCode,
                            COALESCE(s.SupplierNameAr, s.SupplierName, '') as SupplierName,
                            COALESCE(s.SupplierCode, '') as SupplierCode,
                            t.TreasuryNameAr as TreasuryName,
                            t.TreasuryCode as TreasuryCode,
                            u.FullName as CreatedByName
                        FROM PaymentVouchers pv
                        LEFT JOIN Customers c ON pv.CustomerID = c.CustomerID
                        LEFT JOIN Suppliers s ON pv.SupplierID = s.SupplierID
                        LEFT JOIN Treasury t ON pv.TreasuryID = t.TreasuryID
                        LEFT JOIN Users u ON pv.CreatedBy = u.UserID
                        WHERE pv.VoucherID = @paymentId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@paymentId", paymentId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new PaymentVoucherPrintData
                                {
                                    VoucherNumber = reader["VoucherNumber"]?.ToString() ?? "",
                                    VoucherDate = reader["VoucherDate"] != DBNull.Value ? Convert.ToDateTime(reader["VoucherDate"]) : DateTime.Now,
                                    Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                                    PaymentMethod = GetPaymentMethodArabic(reader["PaymentMethod"]?.ToString() ?? "Cash"),
                                    CheckNumber = reader["CheckNumber"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    CustomerID = reader["CustomerID"] != DBNull.Value ? Convert.ToInt32(reader["CustomerID"]) : 0,
                                    SupplierID = reader["SupplierID"] != DBNull.Value ? Convert.ToInt32(reader["SupplierID"]) : 0,
                                    CustomerName = reader["CustomerName"]?.ToString() ?? "",
                                    CustomerCode = reader["CustomerCode"]?.ToString() ?? "",
                                    SupplierName = reader["SupplierName"]?.ToString() ?? "",
                                    SupplierCode = reader["SupplierCode"]?.ToString() ?? "",
                                    TreasuryName = reader["TreasuryName"]?.ToString() ?? "",
                                    TreasuryCode = reader["TreasuryCode"]?.ToString() ?? "",
                                    CreatedByName = reader["CreatedByName"]?.ToString() ?? "نظام"
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPaymentVoucherDetailsAsync Error: {ex.Message}");
            }

            return null;
        }

        private string GetPaymentMethodArabic(string method)
        {
            switch (method?.ToLower())
            {
                case "cash": return "نقدي";
                case "check": return "شيك";
                case "transfer": return "تحويل بنكي";
                case "bank": return "تحويل بنكي";
                default: return method ?? "";
            }
        }

        private FixedDocument CreatePaymentVoucherPrintDocument(PaymentVoucherPrintData data)
        {
            try
            {
                string companyName = GetCompanyName();
                BitmapImage companyLogo = GetCompanyLogo();

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(680, 900);

                PageContent pageContent = new PageContent();
                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = 680;
                fixedPage.Height = 900;
                fixedPage.Background = Brushes.White;
                fixedPage.FlowDirection = FlowDirection.RightToLeft;
                fixedPage.Language = XmlLanguage.GetLanguage("ar-SA");

                StackPanel printPanel = new StackPanel();
                printPanel.Width = 620;
                printPanel.Margin = new Thickness(30, 30, 30, 30);
                printPanel.Background = Brushes.White;
                printPanel.FlowDirection = FlowDirection.RightToLeft;

                Grid headerGrid = CreateVoucherHeader(companyName, companyLogo);
                printPanel.Children.Add(headerGrid);

                Border separator = new Border
                {
                    Height = 2,
                    Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    Margin = new Thickness(0, 10, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                printPanel.Children.Add(separator);

                TextBlock titleBlock = new TextBlock
                {
                    Text = "سند صرف",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                printPanel.Children.Add(titleBlock);

                Grid infoGrid = CreateVoucherInfoGrid(data);
                printPanel.Children.Add(infoGrid);

                Border innerSeparator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Margin = new Thickness(0, 15, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                printPanel.Children.Add(innerSeparator);

                Grid amountGrid = CreateAmountGrid(data);
                printPanel.Children.Add(amountGrid);

                if (!string.IsNullOrEmpty(data.Description))
                {
                    Border descBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 15, 0, 15),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        BorderThickness = new Thickness(1)
                    };

                    StackPanel descPanel = new StackPanel
                    {
                        FlowDirection = FlowDirection.RightToLeft
                    };

                    descPanel.Children.Add(new TextBlock
                    {
                        Text = "البيان",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        FontFamily = new FontFamily("Cairo"),
                        Margin = new Thickness(0, 0, 0, 5),
                        HorizontalAlignment = HorizontalAlignment.Right
                    });

                    descPanel.Children.Add(new TextBlock
                    {
                        Text = data.Description,
                        FontSize = 12,
                        FontFamily = new FontFamily("Cairo"),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Right
                    });

                    descBorder.Child = descPanel;
                    printPanel.Children.Add(descBorder);
                }

                Grid signatureGrid = CreateSignatureGrid();
                printPanel.Children.Add(signatureGrid);

                TextBlock footerBlock = new TextBlock
                {
                    Text = $"تم الإنشاء بواسطة: {data.CreatedByName} | تاريخ الطباعة: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                printPanel.Children.Add(footerBlock);

                fixedPage.Children.Add(printPanel);
                pageContent.Child = fixedPage;
                fixedDocument.Pages.Add(pageContent);

                return fixedDocument;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreatePaymentVoucherPrintDocument Error: {ex.Message}");
                return null;
            }
        }

        private Grid CreateVoucherHeader(string companyName, BitmapImage logo)
        {
            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 10);
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (logo != null)
            {
                Border logoBorder = new Border
                {
                    Width = 60,
                    Height = 60,
                    CornerRadius = new CornerRadius(30),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(0, 0, 15, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image logoImage = new Image
                {
                    Source = logo,
                    Width = 50,
                    Height = 50,
                    Stretch = Stretch.UniformToFill
                };

                EllipseGeometry clipGeometry = new EllipseGeometry(new Point(25, 25), 25, 25);
                logoImage.Clip = clipGeometry;
                logoBorder.Child = logoImage;

                Grid.SetColumn(logoBorder, 0);
                headerGrid.Children.Add(logoBorder);
            }

            StackPanel companyInfoPanel = new StackPanel();
            companyInfoPanel.VerticalAlignment = VerticalAlignment.Center;
            companyInfoPanel.HorizontalAlignment = HorizontalAlignment.Center;

            TextBlock companyNameBlock = new TextBlock
            {
                Text = companyName,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            companyInfoPanel.Children.Add(companyNameBlock);

            TextBlock voucherTypeBlock = new TextBlock
            {
                Text = "سند صرف نقدي",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            companyInfoPanel.Children.Add(voucherTypeBlock);

            Grid.SetColumn(companyInfoPanel, 1);
            headerGrid.Children.Add(companyInfoPanel);

            return headerGrid;
        }

        private Grid CreateVoucherInfoGrid(PaymentVoucherPrintData data)
        {
            Grid infoGrid = new Grid();
            infoGrid.Margin = new Thickness(0, 0, 0, 15);
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.Margin = new Thickness(0, 0, 0, 10);

            AddInfoRow(row1, 0, "رقم السند:", data.VoucherNumber);
            AddInfoRow(row1, 1, "التاريخ:", data.VoucherDate.ToString("yyyy-MM-dd"));
            Grid.SetRow(row1, 0);
            infoGrid.Children.Add(row1);

            Grid row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.Margin = new Thickness(0, 0, 0, 10);

            string partyType = data.SupplierID > 0 ? "المورد:" : "العميل:";
            string partyName = data.SupplierID > 0 ? $"{data.SupplierCode} - {data.SupplierName}" : $"{data.CustomerCode} - {data.CustomerName}";
            AddInfoRow(row2, 0, partyType, partyName);
            AddInfoRow(row2, 1, "طريقة السداد:", data.PaymentMethod);
            Grid.SetRow(row2, 1);
            infoGrid.Children.Add(row2);

            Grid row3 = new Grid();
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.Margin = new Thickness(0, 0, 0, 10);

            AddInfoRow(row3, 0, "الخزينة:", $"{data.TreasuryCode} - {data.TreasuryName}");
            string checkInfo = string.IsNullOrEmpty(data.CheckNumber) ? "---" : data.CheckNumber;
            AddInfoRow(row3, 1, "رقم الشيك:", checkInfo);
            Grid.SetRow(row3, 2);
            infoGrid.Children.Add(row3);

            return infoGrid;
        }

        private void AddInfoRow(Grid grid, int column, string label, string value)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            Grid innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                FontFamily = new FontFamily("Cairo"),
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(labelBlock, 0);
            innerGrid.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                FontFamily = new FontFamily("Cairo"),
                TextAlignment = TextAlignment.Left
            };
            Grid.SetColumn(valueBlock, 1);
            innerGrid.Children.Add(valueBlock);

            cardBorder.Child = innerGrid;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        private Grid CreateAmountGrid(PaymentVoucherPrintData data)
        {
            Grid amountGrid = new Grid();
            amountGrid.Margin = new Thickness(0, 10, 0, 10);
            amountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            amountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border amountCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                BorderThickness = new Thickness(1.5)
            };

            StackPanel amountStack = new StackPanel();
            amountStack.HorizontalAlignment = HorizontalAlignment.Center;

            amountStack.Children.Add(new TextBlock
            {
                Text = "المبلغ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            amountStack.Children.Add(new TextBlock
            {
                Text = CurrencyHelper.FormatAmount(data.Amount),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            amountStack.Children.Add(new TextBlock
            {
                Text = "ريال سعودي فقط",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });

            amountCard.Child = amountStack;
            Grid.SetColumn(amountCard, 0);
            amountGrid.Children.Add(amountCard);

            Border emptyCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            Grid.SetColumn(emptyCard, 1);
            amountGrid.Children.Add(emptyCard);

            return amountGrid;
        }

        private Grid CreateSignatureGrid()
        {
            Grid signatureGrid = new Grid();
            signatureGrid.Margin = new Thickness(0, 20, 0, 0);
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.FlowDirection = FlowDirection.RightToLeft;

            AddSignatureCard(signatureGrid, 0, "المستلم", "الاسم والتوقيع");
            AddSignatureCard(signatureGrid, 1, "الصرف", "الاسم والتوقيع");
            AddSignatureCard(signatureGrid, 2, "المدير المالي", "الاسم والتوقيع");

            return signatureGrid;
        }

        private void AddSignatureCard(Grid grid, int column, string title, string subtitle)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 15, 12, 15),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            StackPanel cardStack = new StackPanel();
            cardStack.HorizontalAlignment = HorizontalAlignment.Center;
            cardStack.FlowDirection = FlowDirection.RightToLeft;

            cardStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            Border signatureLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Width = 120,
                Margin = new Thickness(0, 15, 0, 5)
            };
            cardStack.Children.Add(signatureLine);

            cardStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            cardBorder.Child = cardStack;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        private string GetCompanyName()
        {
            try
            {
                using (var connection = _dbService.GetConnection())
                {
                    connection.Open();
                    string sql = "SELECT CompanyName FROM CompanySettings LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = cmd.ExecuteScalar();
                        return result != null ? result.ToString() : "النظام المحاسبي";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCompanyName Error: {ex.Message}");
                return "النظام المحاسبي";
            }
        }

        private BitmapImage GetCompanyLogo()
        {
            try
            {
                using (var connection = _dbService.GetConnection())
                {
                    connection.Open();
                    string sql = "SELECT LogoPath FROM CompanySettings LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            string logoPath = result.ToString();
                            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                bitmap.DecodePixelWidth = 100;
                                bitmap.DecodePixelHeight = 100;
                                bitmap.EndInit();
                                bitmap.Freeze();
                                return bitmap;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCompanyLogo Error: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region دوال التعديل (Edit Methods)

        /// <summary>
        /// فتح نافذة تعديل سند الصرف
        /// </summary>
        private async void EditVoucher_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int voucherId))
                {
                    var voucher = _vouchersList?.FirstOrDefault(v => v.PaymentID == voucherId);
                    if (voucher == null) return;

                    var voucherDetails = await GetPaymentVoucherDetailsForEditAsync(voucherId);
                    if (voucherDetails == null)
                    {
                        await ShowMessage("حدث خطأ في جلب بيانات السند", "خطأ");
                        return;
                    }

                    await OpenEditVoucherDialogAsync(voucherDetails);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditVoucher_Click Error: {ex.Message}");
                await ShowMessage($"خطأ: {ex.Message}", "خطأ");
            }
        }

        /// <summary>
        /// جلب بيانات سند الصرف للتعديل
        /// </summary>
        private async Task<PaymentVoucherEditData> GetPaymentVoucherDetailsForEditAsync(int voucherId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            VoucherID,
                            VoucherNumber,
                            VoucherDate,
                            SupplierID,
                            CustomerID,
                            Amount,
                            PaymentMethod,
                            CheckNumber,
                            Description,
                            TreasuryID
                        FROM PaymentVouchers 
                        WHERE VoucherID = @voucherId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@voucherId", voucherId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new PaymentVoucherEditData
                                {
                                    VoucherID = reader.GetInt32(0),
                                    VoucherNumber = reader.GetString(1),
                                    VoucherDate = reader.GetDateTime(2),
                                    SupplierID = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                    CustomerID = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                    Amount = reader.GetDecimal(5),
                                    PaymentMethod = reader.GetString(6),
                                    CheckNumber = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    Description = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    TreasuryID = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPaymentVoucherDetailsForEditAsync Error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// فتح نافذة تعديل سند الصرف
        /// </summary>
        private async Task OpenEditVoucherDialogAsync(PaymentVoucherEditData voucherData)
        {
            try
            {
                Window editWindow = new Window
                {
                    Title = $"تعديل سند صرف - {voucherData.VoucherNumber}",
                    Width = 700,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    FlowDirection = FlowDirection.RightToLeft
                };

                Grid mainGrid = new Grid();
                mainGrid.Margin = new Thickness(20);

                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // رقم السند
                StackPanel voucherNumberPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                voucherNumberPanel.Children.Add(new TextBlock
                {
                    Text = "رقم السند:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                TextBox txtVoucherNumber = new TextBox
                {
                    Text = voucherData.VoucherNumber,
                    IsReadOnly = true,
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    Height = 32,
                    FontSize = 13
                };
                voucherNumberPanel.Children.Add(txtVoucherNumber);
                Grid.SetRow(voucherNumberPanel, 0);
                mainGrid.Children.Add(voucherNumberPanel);

                // التاريخ
                StackPanel datePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                datePanel.Children.Add(new TextBlock
                {
                    Text = "التاريخ:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                DatePicker dpVoucherDate = new DatePicker
                {
                    SelectedDate = voucherData.VoucherDate,
                    Height = 32,
                    FontSize = 13
                };
                datePanel.Children.Add(dpVoucherDate);
                Grid.SetRow(datePanel, 1);
                mainGrid.Children.Add(datePanel);

                // الطرف
                StackPanel partyPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                partyPanel.Children.Add(new TextBlock
                {
                    Text = "الطرف:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });

                ComboBox cboPartyType = new ComboBox
                {
                    Height = 32,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                cboPartyType.Items.Add("مورد");
                cboPartyType.Items.Add("عميل");
                cboPartyType.SelectedIndex = voucherData.SupplierID > 0 ? 0 : 1;
                partyPanel.Children.Add(cboPartyType);

                ComboBox cboParty = new ComboBox
                {
                    Height = 32,
                    FontSize = 13,
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id"
                };
                await LoadPartiesForEditAsync(cboParty, voucherData.SupplierID > 0,
                    voucherData.SupplierID > 0 ? voucherData.SupplierID : voucherData.CustomerID);
                partyPanel.Children.Add(cboParty);
                Grid.SetRow(partyPanel, 2);
                mainGrid.Children.Add(partyPanel);

                // طريقة الدفع
                StackPanel paymentPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                paymentPanel.Children.Add(new TextBlock
                {
                    Text = "طريقة الدفع:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                ComboBox cboPaymentMethod = new ComboBox
                {
                    Height = 32,
                    FontSize = 13
                };
                cboPaymentMethod.Items.Add("نقدي");
                cboPaymentMethod.Items.Add("شيك");
                cboPaymentMethod.Items.Add("تحويل بنكي");
                cboPaymentMethod.SelectedIndex = GetPaymentMethodIndex(voucherData.PaymentMethod);
                paymentPanel.Children.Add(cboPaymentMethod);
                Grid.SetRow(paymentPanel, 3);
                mainGrid.Children.Add(paymentPanel);

                // الخزينة
                StackPanel treasuryPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                treasuryPanel.Children.Add(new TextBlock
                {
                    Text = "الخزينة:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                ComboBox cboTreasury = new ComboBox
                {
                    Height = 32,
                    FontSize = 13,
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id"
                };
                await LoadTreasuriesForEditAsync(cboTreasury, voucherData.TreasuryID);
                treasuryPanel.Children.Add(cboTreasury);
                Grid.SetRow(treasuryPanel, 4);
                mainGrid.Children.Add(treasuryPanel);

                // المبلغ
                StackPanel amountPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                amountPanel.Children.Add(new TextBlock
                {
                    Text = "المبلغ:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                TextBox txtAmount = new TextBox
                {
                    Text = voucherData.Amount.ToString("N2"),
                    Height = 32,
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center
                };
                amountPanel.Children.Add(txtAmount);
                Grid.SetRow(amountPanel, 5);
                mainGrid.Children.Add(amountPanel);

                // رقم الشيك
                StackPanel checkPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                checkPanel.Children.Add(new TextBlock
                {
                    Text = "رقم الشيك:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                TextBox txtCheckNumber = new TextBox
                {
                    Text = voucherData.CheckNumber,
                    Height = 32,
                    FontSize = 13
                };
                checkPanel.Children.Add(txtCheckNumber);
                checkPanel.Visibility = voucherData.PaymentMethod == "Check" ? Visibility.Visible : Visibility.Collapsed;
                Grid.SetRow(checkPanel, 6);
                mainGrid.Children.Add(checkPanel);

                // الوصف
                StackPanel descPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                descPanel.Children.Add(new TextBlock
                {
                    Text = "الوصف:",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                TextBox txtDescription = new TextBox
                {
                    Text = voucherData.Description,
                    Height = 60,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                descPanel.Children.Add(txtDescription);
                Grid.SetRow(descPanel, 7);
                mainGrid.Children.Add(descPanel);

                // الأزرار
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Button btnSave = new Button
                {
                    Content = "💾 حفظ التعديلات",
                    Width = 140,
                    Height = 38,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    Foreground = Brushes.White,
                    Cursor = Cursors.Hand
                };

                Button btnCancel = new Button
                {
                    Content = "✖ إلغاء",
                    Width = 100,
                    Height = 38,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    Cursor = Cursors.Hand
                };

                buttonPanel.Children.Add(btnSave);
                buttonPanel.Children.Add(btnCancel);
                Grid.SetRow(buttonPanel, 9);
                mainGrid.Children.Add(buttonPanel);

                editWindow.Content = mainGrid;
                editWindow.Owner = Window.GetWindow(this);

                // أحداث النافذة
                cboPaymentMethod.SelectionChanged += (s, ev) =>
                {
                    checkPanel.Visibility = cboPaymentMethod.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
                };

                cboPartyType.SelectionChanged += async (s, ev) =>
                {
                    bool isSupplier = cboPartyType.SelectedIndex == 0;
                    await LoadPartiesForEditAsync(cboParty, isSupplier, 0);
                };

                btnSave.Click += async (s, ev) =>
                {
                    try
                    {
                        if (cboParty.SelectedItem == null)
                        {
                            await ShowMessage("الرجاء اختيار طرف", "تنبيه");
                            return;
                        }

                        if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                        {
                            await ShowMessage("المبلغ غير صحيح", "تنبيه");
                            txtAmount.Focus();
                            return;
                        }

                        if (cboTreasury.SelectedItem == null)
                        {
                            await ShowMessage("الرجاء اختيار الخزينة", "تنبيه");
                            return;
                        }

                        int selectedPartyId = 0;
                        if (cboParty.SelectedItem is PartyEditItem partyItem)
                        {
                            selectedPartyId = partyItem.Id;
                        }

                        int supplierId = cboPartyType.SelectedIndex == 0 ? selectedPartyId : 0;
                        int customerId = cboPartyType.SelectedIndex == 1 ? selectedPartyId : 0;
                        string paymentMethod = cboPaymentMethod.SelectedIndex == 0 ? "Cash" :
                                               cboPaymentMethod.SelectedIndex == 1 ? "Check" : "Transfer";

                        int treasuryId = 0;
                        if (cboTreasury.SelectedItem is TreasuryEditItem treasuryItem)
                        {
                            treasuryId = treasuryItem.Id;
                        }

                        // ✅ استدعاء دالة التعديل
                        bool success = await _dbService.UpdatePaymentVoucherAsync(
                            voucherData.VoucherID,
                            txtVoucherNumber.Text,
                            dpVoucherDate.SelectedDate ?? DateTime.Now,
                            supplierId,
                            customerId,
                            amount,
                            paymentMethod,
                            txtCheckNumber.Text,
                            txtDescription.Text,
                            treasuryId,
                            LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1,
                            null
                        );

                        if (success)
                        {
                            await ShowMessage("تم تحديث سند الصرف بنجاح", "تم");
                            editWindow.Close();
                            await LoadAllPartiesForAutoCompleteAsync();
                            await LoadAllTreasuriesForAutoCompleteAsync();
                            await LoadVouchersAsync();
                        }
                        else
                        {
                            await ShowMessage("حدث خطأ في تحديث سند الصرف", "خطأ");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SaveEdit Error: {ex.Message}");
                        await ShowMessage($"خطأ: {ex.Message}", "خطأ");
                    }
                };

                btnCancel.Click += (s, ev) => editWindow.Close();

                editWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenEditVoucherDialogAsync Error: {ex.Message}");
                await ShowMessage($"خطأ: {ex.Message}", "خطأ");
            }
        }

        /// <summary>
        /// تحميل الأطراف للتعديل
        /// </summary>
        private async Task LoadPartiesForEditAsync(ComboBox cbo, bool isSupplier, int selectedId)
        {
            try
            {
                cbo.Items.Clear();
                var parties = new List<PartyEditItem>();

                if (isSupplier)
                {
                    var suppliers = await _dbService.GetSuppliersAsync();
                    foreach (var supplier in suppliers)
                    {
                        string name = !string.IsNullOrEmpty(supplier.SupplierNameAr) ? supplier.SupplierNameAr : supplier.SupplierName;
                        parties.Add(new PartyEditItem
                        {
                            Id = supplier.SupplierID,
                            Code = supplier.SupplierCode,
                            Name = $"{name}"
                        });
                    }
                }
                else
                {
                    var customers = await _dbService.GetCustomersAsync();
                    foreach (var customer in customers)
                    {
                        string name = !string.IsNullOrEmpty(customer.CustomerNameAr) ? customer.CustomerNameAr : customer.CustomerName;
                        parties.Add(new PartyEditItem
                        {
                            Id = customer.CustomerID,
                            Code = customer.CustomerCode,
                            Name = $"{name}"
                        });
                    }
                }

                cbo.ItemsSource = parties;
                if (selectedId > 0)
                {
                    cbo.SelectedValue = selectedId;
                }
                else if (parties.Count > 0)
                {
                    cbo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadPartiesForEditAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// تحميل الخزائن للتعديل
        /// </summary>
        private async Task LoadTreasuriesForEditAsync(ComboBox cbo, int selectedId)
        {
            try
            {
                cbo.Items.Clear();
                var treasuries = await _dbService.GetActiveTreasuriesAsync();
                var items = new List<TreasuryEditItem>();

                foreach (var treasury in treasuries)
                {
                    items.Add(new TreasuryEditItem
                    {
                        Id = treasury.Id,
                        Code = treasury.Code,
                        Name = treasury.Name
                    });
                }

                cbo.ItemsSource = items;
                if (selectedId > 0)
                {
                    cbo.SelectedValue = selectedId;
                }
                else if (items.Count > 0)
                {
                    cbo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuriesForEditAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// الحصول على مؤشر طريقة الدفع
        /// </summary>
        private int GetPaymentMethodIndex(string paymentMethod)
        {
            switch (paymentMethod?.ToLower())
            {
                case "cash": return 0;
                case "check": return 1;
                case "transfer": return 2;
                default: return 0;
            }
        }

        #endregion
    }

    #region كلاس بيانات سند الصرف

    public class PaymentVoucherItem
    {
        public int PaymentID { get; set; }
        public string VoucherNumber { get; set; }
        public string PaymentDate { get; set; }
        public string PartyName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethodDisplay { get; set; }
        public string TreasuryName { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
    }

    #endregion

    #region كلاس بيانات طباعة سند الصرف

    public class PaymentVoucherPrintData
    {
        public string VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
        public int CustomerID { get; set; }
        public int SupplierID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCode { get; set; }
        public string SupplierName { get; set; }
        public string SupplierCode { get; set; }
        public string TreasuryName { get; set; }
        public string TreasuryCode { get; set; }
        public string CreatedByName { get; set; }
    }

    #endregion
}