using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;
using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RasidAccountingSystem.Views
{
    public partial class CustomerStatementView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private DataTable _statementData;
        private bool _isLoading = false;
        private bool _isSelectingCustomerFromList = false;
        private bool _isNavigatingWithArrows = false;
        private bool _isCustomerSelected = false;
        private List<TransactionItem> _currentTransactionItems = new List<TransactionItem>();

        // معلومات العميل المختار
        private int _selectedCustomerId = 0;
        private string _selectedCustomerName = "";
        private string _selectedCustomerCode = "";
        private decimal _customerOpeningBalance = 0;
        private decimal _customerCurrentBalance = 0;

        // كلاس مساعد لعرض العملاء في AutoComplete
        private List<CustomerSearchItem> AllCustomersList = new List<CustomerSearchItem>();
        private CustomerSearchItem SelectedCustomerObj = null;

        // ✅ (خطوة 4): نفس منطق حقل العميل الموجود في فاتورة المبيعات — تتبّع النتائج المفلترة
        // والعنصر المظلَّل بالكيبورد جوه نفس التكست بوكس، من غير ما الفوكس يقفز للِست
        private List<CustomerSearchItem> _currentFilteredCustomers = new List<CustomerSearchItem>();
        private int _currentCustomerSelectedIndex = -1;

        #endregion

        #region الكلاسات المساعدة

        public class CustomerSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class TransactionItem
        {
            public string TransactionDate { get; set; }
            public string DocumentType { get; set; }
            public string DocumentNumber { get; set; }
            public string Description { get; set; }
            public decimal DebitAmount { get; set; }
            public decimal CreditAmount { get; set; }
            public decimal BalanceAfter { get; set; }
            public Brush BalanceColor { get; set; }
        }

        #endregion

        #region المنشئ

        public CustomerStatementView()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeEvents();
            InitializeDefaultDates();
            this.Loaded += async (sender, e) => await OnViewLoadedAsync();
        }

        private void InitializeDatabase()
        {
            try
            {
                _dbService = new DatabaseService();
                if (_dbService != null)
                {
                    _statementData = new DataTable();
                    System.Diagnostics.Debug.WriteLine("✅ تم تهيئة خدمة قاعدة البيانات بنجاح");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ فشل في تهيئة خدمة قاعدة البيانات");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تهيئة قاعدة البيانات: {ex.Message}");
            }
        }

        private void InitializeEvents()
        {
            try
            {
                if (btnShowStatement != null)
                {
                    btnShowStatement.Click += async (s, e) => await LoadStatementAsync();
                }

                if (btnRefresh != null)
                {
                    btnRefresh.Click += async (s, e) => await RefreshDataAsync();
                }

                if (btnExportExcel != null)
                {
                    btnExportExcel.Click += ExportToExcel;
                }

                if (btnPrint != null)
                {
                    btnPrint.Click += PrintReport;
                }

                if (dpFromDate != null)
                {
                    dpFromDate.SelectedDateChanged += async (s, e) => await OnFilterChangedAsync();
                }

                if (dpToDate != null)
                {
                    dpToDate.SelectedDateChanged += async (s, e) => await OnFilterChangedAsync();
                }

                if (cboTransactionType != null)
                {
                    cboTransactionType.SelectionChanged += async (s, e) => await OnFilterChangedAsync();
                }

                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة الأحداث بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تهيئة الأحداث: {ex.Message}");
            }
        }

        // ✅ (خطوة 4): بداية افتراضية "متحركة" لفلتر التاريخ = اليوم ناقص 5 سنين، بدل تاريخ ثابت
        // زي 2000/1/1. كده الفلتر الافتراضي فعليًا بيغطي أي حركة معقولة العمر (زي بيانات مُستعادة
        // من نسخة احتياطية) من غير ما "ينسى" حاجة، ومن غير ما يحتاج تحديث يدوي كل ما السنين تعدّي.
        private static DateTime GetDefaultStatementFromDate() => DateTime.Now.AddYears(-3).Date;

        private void InitializeDefaultDates()
        {
            try
            {
                if (dpFromDate != null)
                {
                    dpFromDate.SelectedDate = GetDefaultStatementFromDate();
                }

                if (dpToDate != null)
                {
                    dpToDate.SelectedDate = DateTime.Now;
                }

                if (cboTransactionType != null)
                {
                    cboTransactionType.SelectedIndex = 0;
                }

                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة التواريخ الافتراضية بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تهيئة التواريخ الافتراضية: {ex.Message}");
            }
        }

        #endregion

        #region دوال التحميل الأساسية

        private async Task OnViewLoadedAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await LoadAllCustomersAsync();
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل الصفحة: {ex.Message}");
                await ShowErrorMessageAsync($"خطأ في تحميل البيانات: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            if (!_isCustomerSelected || _selectedCustomerId == 0)
            {
                await ShowMessageAsync("الرجاء اختيار عميل أولاً", "تنبيه");
                return;
            }

            await LoadAllCustomersAsync();
            await LoadStatementAsync();
            await ShowMessageAsync("تم تحديث البيانات بنجاح", "تحديث");
        }

        private async Task OnFilterChangedAsync()
        {
            if (_isCustomerSelected && _selectedCustomerId > 0)
            {
                await LoadStatementAsync();
            }
        }

        private async Task LoadAllCustomersAsync()
        {
            try
            {
                if (_dbService == null) return;

                AllCustomersList.Clear();

                // ✅ استعلام واحد مجمّع يحسب رصيد كل عميل داخل نفس الاستعلام (subquery لكل عمود)
                // بدل استدعاء GetCustomerActualBalanceForStatementAsync لكل عميل على حدة داخل الـ loop
                // (كانت بتفتح اتصال SQLite جديد وتشغّل استعلام كامل لكل عميل، وده اللي كان بيسبب
                // تجمّد الواجهة كلما زاد عدد العملاء).
                // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه الحركات
                // تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync) - فكانت
                // مساهمة كل فواتير أي عميل في رصيده المعروض بجانب اسمه في قائمة اختيار العملاء
                // تساوي صفر دائمًا.
                using (var connection = _dbService.GetConnection())
                {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT
                    c.CustomerID,
                    c.CustomerCode,
                    c.CustomerNameAr,
                    c.CustomerName,
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
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount)
                        FROM CustomerTransactions
                        WHERE CustomerID = c.CustomerID
                        AND TransactionType = 'Refund'
                    ), 0) as ActualBalance
                FROM Customers c
                WHERE c.IsActive = 1
                ORDER BY c.CustomerNameAr";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int customerId = reader.GetInt32(0);
                            string customerCode = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string customerNameAr = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string customerNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            decimal actualBalance = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4));

                            string customerName = !string.IsNullOrEmpty(customerNameAr) ? customerNameAr : customerNameEn;

                            AllCustomersList.Add(new CustomerSearchItem
                            {
                                Id = customerId,
                                Code = customerCode,
                                Name = customerName,
                                CurrentBalance = actualBalance
                            });
                        }
                    }
                }

                if (lstCustomers != null)
                {
                    lstCustomers.ItemsSource = AllCustomersList;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {AllCustomersList.Count} عميل مع أرصدتهم الصحيحة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل العملاء: {ex.Message}");
            }
        }

        /// <summary>
        /// جلب الرصيد الفعلي للعميل من CustomerTransactions مع رصيد أول المدة
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه الحركات تُخزَّن
        /// فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync) - فكانت مساهمة كل
        /// فواتير العميل في هذا الرصيد تساوي صفر دائمًا.
        /// المعادلة الصحيحة: الرصيد = OpeningBalance + SUM(DebitAmount للفواتير) - SUM(CreditAmount للتحصيلات) - SUM(CreditAmount لمرتجعات البيع)
        /// </summary>
        private async Task<decimal> GetCustomerActualBalanceForStatementAsync(int customerId)
        {
            try
            {
                using (var connection = _dbService.GetConnection())
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
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount) 
                        FROM CustomerTransactions 
                        WHERE CustomerID = @customerId 
                        AND TransactionType = 'Refund'
                    ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"الرصيد الفعلي للعميل {customerId} (لكشف الحساب): {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerActualBalanceForStatementAsync Error: {ex.Message}");
                return 0;
            }
        }
        #endregion

        #region دوال AutoComplete للعملاء

        // ✅ (خطوة 4): نفس سلوك حقل العميل في فاتورة المبيعات بالضبط — التنقل بالأسهم بيتم جوه
        // التكست بوكس نفسه (من غير ما الفوكس يتحول لليست)، والـ Popup فاضل مفتوح لحد ما تختار.

        private void TxtCustomerSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtCustomerPlaceholder != null) txtCustomerPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void TxtCustomerSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerSearch.Text) && txtCustomerPlaceholder != null)
                txtCustomerPlaceholder.Visibility = Visibility.Visible;

            Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (popupCustomers != null && lstCustomers != null && !lstCustomers.IsMouseOver && !lstCustomers.IsKeyboardFocusWithin)
                {
                    popupCustomers.IsOpen = false;
                }
            }));
        }

        private void TxtCustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingCustomerFromList) return;

            string searchText = txtCustomerSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupCustomers != null) popupCustomers.IsOpen = false;
                _currentCustomerSelectedIndex = -1;
                return;
            }

            _currentFilteredCustomers = AllCustomersList
                .Where(c => c.Name != null && c.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (lstCustomers != null)
            {
                lstCustomers.ItemsSource = _currentFilteredCustomers;
                lstCustomers.SelectedIndex = -1;
            }
            _currentCustomerSelectedIndex = -1;

            if (popupCustomers != null)
                popupCustomers.IsOpen = _currentFilteredCustomers.Count > 0;
        }

        private void TxtCustomerSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;

                    if (popupCustomers != null && !popupCustomers.IsOpen && _currentFilteredCustomers.Count > 0)
                        popupCustomers.IsOpen = true;

                    if (lstCustomers != null && lstCustomers.Items.Count > 0 && _currentCustomerSelectedIndex < lstCustomers.Items.Count - 1)
                    {
                        _currentCustomerSelectedIndex++;
                        lstCustomers.SelectedIndex = _currentCustomerSelectedIndex;
                        lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;

                    if (lstCustomers != null && lstCustomers.Items.Count > 0 && _currentCustomerSelectedIndex > 0)
                    {
                        _currentCustomerSelectedIndex--;
                        lstCustomers.SelectedIndex = _currentCustomerSelectedIndex;
                        lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                    }
                    else if (_currentCustomerSelectedIndex == 0 && lstCustomers != null)
                    {
                        _currentCustomerSelectedIndex = -1;
                        lstCustomers.SelectedIndex = -1;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (_currentCustomerSelectedIndex >= 0 && lstCustomers != null && _currentCustomerSelectedIndex < lstCustomers.Items.Count)
                    {
                        e.Handled = true;
                        if (lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
                            SelectCustomer(selectedCustomer);
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (popupCustomers != null) popupCustomers.IsOpen = false;
                    _currentCustomerSelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtCustomerSearch_PreviewKeyDown Error: {ex.Message}");
            }
        }

        private void LstCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCustomers != null && lstCustomers.SelectedItem is CustomerSearchItem)
                _currentCustomerSelectedIndex = lstCustomers.SelectedIndex;
        }

        private void LstCustomers_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
                SelectCustomer(selectedCustomer);
        }

        private void LstCustomers_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
                    SelectCustomer(selectedCustomer);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (popupCustomers != null) popupCustomers.IsOpen = false;
                txtCustomerSearch?.Focus();
            }
        }

        private async void SelectCustomer(CustomerSearchItem customer)
        {
            if (customer == null) return;

            _isSelectingCustomerFromList = true;
            SelectedCustomerObj = customer;
            txtCustomerSearch.Text = customer.Name;
            if (popupCustomers != null) popupCustomers.IsOpen = false;
            _currentCustomerSelectedIndex = -1;

            _selectedCustomerId = customer.Id;
            _selectedCustomerName = customer.Name;
            _selectedCustomerCode = customer.Code;
            _customerCurrentBalance = customer.CurrentBalance;
            _isCustomerSelected = true;

            await LoadCustomerInfoAsync();
            await LoadStatementAsync();

            _isSelectingCustomerFromList = false;
            System.Diagnostics.Debug.WriteLine($"✅ تم اختيار العميل: {customer.Name} (ID: {customer.Id})");
        }

        private async Task LoadCustomerInfoAsync()
        {
            try
            {
                if (_selectedCustomerId == 0) return;

                using (var connection = _dbService.GetConnection())
                {
                    await connection.OpenAsync();

                    string openingBalanceSql = @"
                        SELECT COALESCE(OpeningBalance, 0) 
                        FROM Customers 
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(openingBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", _selectedCustomerId);
                        object result = await cmd.ExecuteScalarAsync();
                        _customerOpeningBalance = result != null ? Convert.ToDecimal(result) : 0;
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (borderCustomerInfo != null)
                    {
                        borderCustomerInfo.Visibility = Visibility.Visible;
                    }

                    if (lblCustomerName != null)
                    {
                        lblCustomerName.Text = _selectedCustomerName;
                    }

                    if (lblCustomerCode != null)
                    {
                        lblCustomerCode.Text = _selectedCustomerCode;
                    }

                    if (lblOpeningBalance != null)
                    {
                        lblOpeningBalance.Text = CurrencyHelper.FormatAmount(_customerOpeningBalance);
                    }

                    // ✅ رمز العملة بقى بيتقرأ من إعدادات النظام (CurrencyHelper) بدل ما يكون
                    // مكتوب "ر.س" ثابت في الشاشة، فلو المستخدم غيّر العملة من الإعدادات هتتغير هنا كمان
                    if (lblClosingBalanceCurrency != null)
                    {
                        lblClosingBalanceCurrency.Text = CurrencyHelper.GetCurrencySymbol();
                    }

                    if (lblNetChangeCurrency != null)
                    {
                        lblNetChangeCurrency.Text = CurrencyHelper.GetCurrencySymbol();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل معلومات العميل: {ex.Message}");
            }
        }

        #endregion

        #region دالة تحميل كشف الحساب

        private async Task LoadStatementAsync()
        {
            if (!_isCustomerSelected || _selectedCustomerId == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ لم يتم اختيار عميل بعد، يتم تجاهل تحميل البيانات");
                return;
            }

            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                DateTime fromDate = dpFromDate?.SelectedDate ?? GetDefaultStatementFromDate();
                DateTime toDate = dpToDate?.SelectedDate ?? DateTime.Now;
                string transactionTypeFilter = "";

                if (cboTransactionType?.SelectedItem is ComboBoxItem selectedItem)
                {
                    string type = selectedItem.Content?.ToString() ?? "الكل";
                    if (type == "مبيعات")
                        transactionTypeFilter = "Invoice";
                    else if (type == "تحصيلات")
                        transactionTypeFilter = "Receipt";
                }

                await LoadCustomerTransactionsAsync(_selectedCustomerId, fromDate, toDate, transactionTypeFilter);
                UpdateStatementUI();

                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل كشف الحساب: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadCustomerTransactionsAsync(int customerId, DateTime fromDate, DateTime toDate, string transactionTypeFilter)
        {
            try
            {
                using (var connection = _dbService.GetConnection())
                {
                    await connection.OpenAsync();

                    if (_statementData != null)
                    {
                        _statementData.Clear();
                    }
                    else
                    {
                        _statementData = new DataTable();
                    }

                    // ✅ إصلاح (خطوة 3): كل صف هنا دلوقتي فريد وممثِّل لحركة حقيقية واحدة فعلًا
                    // (بعد إصلاح مصدر البيانات في InvoiceService وتنظيف السجلات القديمة)، فبقينا
                    // مش محتاجين "تفريد" الصفوف بناءً على تشابه رقم المستند + النوع - المنطق ده كان هش
                    // وممكن يحذف حركتين حقيقيتين مختلفتين بالغلط لو اتفق تشابه المفتاح (مثلاً سندي قبض
                    // بنفس رقم المرجع). TransactionID هو المفتاح الوحيد المضمون إنه فريد فعليًا.
                    string sql = @"
                        SELECT 
                            TransactionID,
                            TransactionDate,
                            TransactionType,
                            ReferenceNumber as DocumentNumber,
                            Description,
                            DebitAmount,
                            CreditAmount,
                            BalanceAfter
                        FROM CustomerTransactions 
                        WHERE CustomerID = @customerId 
                        AND TransactionDate BETWEEN @fromDate AND @toDate";

                    if (!string.IsNullOrEmpty(transactionTypeFilter))
                    {
                        sql += " AND TransactionType = @transactionType";
                    }

                    sql += " ORDER BY TransactionDate ASC, TransactionID ASC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@fromDate", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@toDate", toDate.ToString("yyyy-MM-dd"));

                        if (!string.IsNullOrEmpty(transactionTypeFilter))
                        {
                            cmd.Parameters.AddWithValue("@transactionType", transactionTypeFilter);
                        }

                        using (var adapter = new SQLiteDataAdapter(cmd))
                        {
                            adapter.Fill(_statementData);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_statementData?.Rows?.Count ?? 0} حركة للعميل");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل حركات العميل: {ex.Message}");
                if (_statementData == null)
                {
                    _statementData = new DataTable();
                }
            }
        }

        private void UpdateStatementUI()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (dgTransactions == null) return;

                    decimal runningBalance = _customerOpeningBalance;
                    decimal totalDebit = 0;
                    decimal totalCredit = 0;

                    _currentTransactionItems.Clear();
                    var transactionItems = new List<TransactionItem>();

                    if (_statementData != null && _statementData.Rows.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"📊 معالجة {_statementData.Rows.Count} حركة لعرضها في الجدول");

                        foreach (DataRow row in _statementData.Rows)
                        {
                            string transactionDate = row["TransactionDate"] != DBNull.Value ? Convert.ToDateTime(row["TransactionDate"]).ToString("yyyy-MM-dd") : "";
                            string transactionTypeDB = row["TransactionType"]?.ToString() ?? "";
                            string documentNumber = row["DocumentNumber"]?.ToString() ?? "";
                            string description = row["Description"]?.ToString() ?? "";

                            decimal debitAmount = 0;
                            decimal creditAmount = 0;
                            decimal balanceAfter = 0;

                            if (transactionTypeDB == "Invoice")
                            {
                                debitAmount = row["DebitAmount"] != DBNull.Value ? Convert.ToDecimal(row["DebitAmount"]) : 0;
                                if (debitAmount == 0)
                                {
                                    debitAmount = row["CreditAmount"] != DBNull.Value ? Convert.ToDecimal(row["CreditAmount"]) : 0;
                                }
                                totalDebit += debitAmount;
                                runningBalance += debitAmount;
                                balanceAfter = runningBalance;
                                System.Diagnostics.Debug.WriteLine($"   - حركة مبيعات: {documentNumber}, المبلغ: {debitAmount}");
                            }
                            else if (transactionTypeDB == "Receipt")
                            {
                                creditAmount = row["CreditAmount"] != DBNull.Value ? Convert.ToDecimal(row["CreditAmount"]) : 0;
                                if (creditAmount == 0)
                                {
                                    creditAmount = row["DebitAmount"] != DBNull.Value ? Convert.ToDecimal(row["DebitAmount"]) : 0;
                                }
                                totalCredit += creditAmount;
                                runningBalance -= creditAmount;
                                balanceAfter = runningBalance;
                                System.Diagnostics.Debug.WriteLine($"   - حركة تحصيل: {documentNumber}, المبلغ: {creditAmount}");
                            }
                            else
                            {
                                debitAmount = row["DebitAmount"] != DBNull.Value ? Convert.ToDecimal(row["DebitAmount"]) : 0;
                                creditAmount = row["CreditAmount"] != DBNull.Value ? Convert.ToDecimal(row["CreditAmount"]) : 0;

                                if (debitAmount > 0)
                                {
                                    runningBalance += debitAmount;
                                    totalDebit += debitAmount;
                                }
                                if (creditAmount > 0)
                                {
                                    runningBalance -= creditAmount;
                                    totalCredit += creditAmount;
                                }
                                balanceAfter = runningBalance;
                            }

                            string documentType = "";
                            if (transactionTypeDB == "Invoice")
                                documentType = "فاتورة بيع";
                            else if (transactionTypeDB == "Receipt")
                                documentType = "سند قبض";
                            else
                                documentType = transactionTypeDB;

                            Brush balanceColor = runningBalance >= 0
                                ? (Brush)FindResource("SuccessColor")
                                : (Brush)FindResource("DangerColor");

                            var item = new TransactionItem
                            {
                                TransactionDate = transactionDate,
                                DocumentType = documentType,
                                DocumentNumber = documentNumber,
                                Description = description,
                                DebitAmount = debitAmount,
                                CreditAmount = creditAmount,
                                BalanceAfter = Math.Abs(balanceAfter),
                                BalanceColor = balanceColor
                            };

                            transactionItems.Add(item);
                            _currentTransactionItems.Add(item);
                        }

                        dgTransactions.ItemsSource = transactionItems;

                        if (lblRecordsCount != null)
                        {
                            lblRecordsCount.Text = $"عدد السجلات: {transactionItems.Count}";
                        }

                        if (lblTotalDebit != null)
                        {
                            lblTotalDebit.Text = totalDebit.ToString("N2");
                        }

                        if (lblTotalCredit != null)
                        {
                            lblTotalCredit.Text = totalCredit.ToString("N2");
                        }

                        if (lblClosingBalance != null)
                        {
                            lblClosingBalance.Text = runningBalance.ToString("N2");
                        }

                        if (lblTotalSales != null)
                        {
                            lblTotalSales.Text = totalDebit.ToString("N2");
                        }

                        if (lblTotalReceipts != null)
                        {
                            lblTotalReceipts.Text = totalCredit.ToString("N2");
                        }

                        if (dgTransactions != null)
                        {
                            dgTransactions.Visibility = Visibility.Visible;
                        }

                        if (lblNoData != null)
                        {
                            lblNoData.Visibility = Visibility.Collapsed;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث الواجهة بـ {transactionItems.Count} حركة");
                        System.Diagnostics.Debug.WriteLine($"📋 تفاصيل الحركات في الذاكرة:");
                        for (int i = 0; i < _currentTransactionItems.Count; i++)
                        {
                            var item = _currentTransactionItems[i];
                            System.Diagnostics.Debug.WriteLine($"   {i + 1}. {item.DocumentType} - {item.DocumentNumber} - تاريخ: {item.TransactionDate} - دائن: {item.CreditAmount} - مدين: {item.DebitAmount}");
                        }
                    }
                    else
                    {
                        dgTransactions.ItemsSource = null;
                        _currentTransactionItems.Clear();

                        if (lblRecordsCount != null)
                        {
                            lblRecordsCount.Text = "عدد السجلات: 0";
                        }

                        if (lblTotalDebit != null)
                        {
                            lblTotalDebit.Text = "0.00";
                        }

                        if (lblTotalCredit != null)
                        {
                            lblTotalCredit.Text = "0.00";
                        }

                        if (lblClosingBalance != null)
                        {
                            lblClosingBalance.Text = _customerOpeningBalance.ToString("N2");
                        }

                        if (lblTotalSales != null)
                        {
                            lblTotalSales.Text = "0.00";
                        }

                        if (lblTotalReceipts != null)
                        {
                            lblTotalReceipts.Text = "0.00";
                        }

                        if (dgTransactions != null)
                        {
                            dgTransactions.Visibility = Visibility.Collapsed;
                        }

                        if (lblNoData != null)
                        {
                            lblNoData.Visibility = Visibility.Visible;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحديث واجهة كشف الحساب: {ex.Message}");
                }
            });
        }

        #endregion

        #region دوال استرجاع معلومات الشركة للطباعة

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
                System.Diagnostics.Debug.WriteLine($"خطأ في استرجاع اسم الشركة: {ex.Message}");
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

                string defaultLogoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Rasid.png");
                if (System.IO.File.Exists(defaultLogoPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(defaultLogoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bitmap.DecodePixelWidth = 100;
                    bitmap.DecodePixelHeight = 100;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل الشعار: {ex.Message}");
            }
            return null;
        }

        private Grid CreateHeaderWithLogo(string companyName, BitmapImage logo)
        {
            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 15);
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (logo != null)
            {
                Border logoBorder = new Border
                {
                    Width = 80,
                    Height = 80,
                    CornerRadius = new CornerRadius(40),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(0, 0, 15, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image logoImage = new Image
                {
                    Source = logo,
                    Width = 70,
                    Height = 70,
                    Stretch = Stretch.UniformToFill
                };

                EllipseGeometry clipGeometry = new EllipseGeometry(new Point(35, 35), 35, 35);
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
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            companyInfoPanel.Children.Add(companyNameBlock);

            TextBlock reportTitleBlock = new TextBlock
            {
                Text = "كشف حساب العميل",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            companyInfoPanel.Children.Add(reportTitleBlock);

            Grid.SetColumn(companyInfoPanel, 1);
            headerGrid.Children.Add(companyInfoPanel);

            return headerGrid;
        }

        private void AddInfoCard(Grid grid, int column, string label, string value)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            StackPanel cardStack = new StackPanel();

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                Margin = new Thickness(0, 0, 0, 3)
            };
            cardStack.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                FontFamily = new FontFamily("Cairo")
            };
            cardStack.Children.Add(valueBlock);

            cardBorder.Child = cardStack;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        private void AddGridCell(Grid grid, int column, int row, string text, HorizontalAlignment alignment = HorizontalAlignment.Center, Brush foreground = null, FontWeight? fontWeight = null)
        {
            Border cellBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(8, 6, 8, 6)
            };

            TextBlock cellText = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = alignment,
                TextWrapping = TextWrapping.Wrap
            };

            if (foreground != null)
            {
                cellText.Foreground = foreground;
            }

            if (fontWeight.HasValue)
            {
                cellText.FontWeight = fontWeight.Value;
            }

            cellBorder.Child = cellText;
            Grid.SetColumn(cellBorder, column);
            Grid.SetRow(cellBorder, row);
            grid.Children.Add(cellBorder);
        }

        private void AddSummaryCard(Grid grid, int column, string label, decimal value, Brush color)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            StackPanel cardStack = new StackPanel();
            cardStack.HorizontalAlignment = HorizontalAlignment.Center;

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cardStack.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value.ToString("N2"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            cardStack.Children.Add(valueBlock);

            TextBlock currencyBlock = new TextBlock
            {
                Text = CurrencyHelper.GetCurrencySymbol(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            cardStack.Children.Add(currencyBlock);

            cardBorder.Child = cardStack;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        #endregion

        #region دوال الطباعة مع المعاينة (معدلة لدعم الصفحات المتعددة)

        private void PrintReport(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isCustomerSelected || _selectedCustomerId == 0)
                {
                    MessageBox.Show("الرجاء اختيار عميل أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_currentTransactionItems == null || _currentTransactionItems.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🖨️ بدء إنشاء مستند الطباعة لـ {_currentTransactionItems.Count} حركة");

                for (int i = 0; i < _currentTransactionItems.Count; i++)
                {
                    var item = _currentTransactionItems[i];
                    System.Diagnostics.Debug.WriteLine($"   حركة {i + 1}: {item.DocumentType} - {item.DocumentNumber} - المبلغ: {(item.CreditAmount > 0 ? item.CreditAmount : item.DebitAmount)}");
                }

                FixedDocument fixedDocument = CreatePrintDocument(_currentTransactionItems);

                if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء {fixedDocument.Pages.Count} صفحة بنجاح");
                    PrintPreviewWindow previewWindow = new PrintPreviewWindow(fixedDocument);
                    previewWindow.Owner = Window.GetWindow(this);
                    previewWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("حدث خطأ في إنشاء مستند الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في الطباعة: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FixedDocument CreatePrintDocument(List<TransactionItem> transactionItems)
        {
            try
            {
                string companyName = GetCompanyName();
                BitmapImage companyLogo = GetCompanyLogo();
                DateTime fromDate = dpFromDate?.SelectedDate ?? GetDefaultStatementFromDate();
                DateTime toDate = dpToDate?.SelectedDate ?? DateTime.Now;

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(794, 1123);

                int itemsPerPage = 12;
                int totalItems = transactionItems.Count;
                int pageCount = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                System.Diagnostics.Debug.WriteLine($"📄 سيتم إنشاء {pageCount} صفحة لـ {totalItems} حركة");

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var pageItems = transactionItems.Skip(pageIndex * itemsPerPage).Take(itemsPerPage).ToList();

                    PageContent pageContent = new PageContent();
                    FixedPage fixedPage = new FixedPage();
                    fixedPage.Width = 794;
                    fixedPage.Height = 1123;
                    fixedPage.Background = Brushes.White;
                    fixedPage.FlowDirection = FlowDirection.RightToLeft;

                    StackPanel printPanel = new StackPanel();
                    printPanel.Width = 714;
                    printPanel.Margin = new Thickness(40, 40, 40, 40);
                    printPanel.Background = Brushes.White;
                    printPanel.FlowDirection = FlowDirection.RightToLeft;

                    Grid headerGrid = CreateHeaderWithLogo(companyName, companyLogo);
                    printPanel.Children.Add(headerGrid);

                    Border separator = new Border
                    {
                        Height = 2,
                        Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                        Margin = new Thickness(0, 10, 0, 15),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    printPanel.Children.Add(separator);

                    Grid filterGrid = new Grid();
                    filterGrid.Margin = new Thickness(0, 0, 0, 20);
                    filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    filterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    AddInfoCard(filterGrid, 0, "اسم العميل", _selectedCustomerName);
                    AddInfoCard(filterGrid, 1, "كود العميل", _selectedCustomerCode);
                    AddInfoCard(filterGrid, 2, "الرصيد الافتتاحي", $"{_customerOpeningBalance:N2} (مدين)");
                    AddInfoCard(filterGrid, 3, "الفترة", $"{fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}");

                    printPanel.Children.Add(filterGrid);

                    Grid dataGrid = CreatePrintDataGrid(pageItems, pageIndex, itemsPerPage);
                    printPanel.Children.Add(dataGrid);

                    if (pageIndex == pageCount - 1)
                    {
                        Border summaryBorder = CreatePrintSummaryBorder(transactionItems);
                        printPanel.Children.Add(summaryBorder);
                    }

                    TextBlock pageNumberBlock = new TextBlock
                    {
                        Text = $"صفحة {pageIndex + 1} من {pageCount}",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        FontFamily = new FontFamily("Cairo"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    printPanel.Children.Add(pageNumberBlock);

                    if (pageIndex == 0)
                    {
                        TextBlock printDateBlock = new TextBlock
                        {
                            Text = $"تاريخ الطباعة: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                            FontFamily = new FontFamily("Cairo"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        printPanel.Children.Add(printDateBlock);
                    }

                    fixedPage.Children.Add(printPanel);
                    pageContent.Child = fixedPage;
                    fixedDocument.Pages.Add(pageContent);
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء {fixedDocument.Pages.Count} صفحة للطباعة بنجاح مع {totalItems} حركة");
                return fixedDocument;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في إنشاء مستند الطباعة: {ex.Message}");
                return null;
            }
        }

        private Grid CreatePrintDataGrid(List<TransactionItem> transactionItems, int pageIndex, int itemsPerPage)
        {
            Grid dataGrid = new Grid();
            dataGrid.Margin = new Thickness(0, 0, 0, 20);
            dataGrid.ShowGridLines = true;

            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });

            string[] headers = { "التاريخ", "نوع المستند", "رقم المستند", "البيان", "مدين (مبيعات)", "دائن (تحصيلات)", "الرصيد بعد" };
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(100, 200, 120)),
                    BorderThickness = new Thickness(0, 0, i < headers.Length - 1 ? 1 : 0, 0)
                };

                TextBlock headerText = new TextBlock
                {
                    Text = headers[i],
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Cairo")
                };
                headerBorder.Child = headerText;

                Grid.SetColumn(headerBorder, i);
                dataGrid.Children.Add(headerBorder);
            }

            int rowIndex = 1;

            decimal runningBalance = _customerOpeningBalance;

            int previousItemsCount = pageIndex * itemsPerPage;
            var allItems = _currentTransactionItems;

            for (int i = 0; i < previousItemsCount && i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (item.DocumentType == "فاتورة بيع")
                {
                    runningBalance += item.DebitAmount;
                }
                else if (item.DocumentType == "سند قبض")
                {
                    runningBalance -= item.CreditAmount;
                }
            }

            System.Diagnostics.Debug.WriteLine($"📄 صفحة {pageIndex + 1}: بدء الرصيد = {runningBalance}");

            foreach (var item in transactionItems)
            {
                dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border rowBorder = new Border
                {
                    Background = rowIndex % 2 == 0 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White
                };
                Grid.SetRow(rowBorder, rowIndex);
                Grid.SetColumnSpan(rowBorder, headers.Length);
                dataGrid.Children.Add(rowBorder);

                if (item.DocumentType == "فاتورة بيع")
                {
                    runningBalance += item.DebitAmount;
                }
                else if (item.DocumentType == "سند قبض")
                {
                    runningBalance -= item.CreditAmount;
                }

                AddGridCell(dataGrid, 0, rowIndex, item.TransactionDate);
                AddGridCell(dataGrid, 1, rowIndex, item.DocumentType);
                AddGridCell(dataGrid, 2, rowIndex, item.DocumentNumber);
                AddGridCell(dataGrid, 3, rowIndex, string.IsNullOrEmpty(item.Description) ? "---" : item.Description, HorizontalAlignment.Right);
                AddGridCell(dataGrid, 4, rowIndex, item.DebitAmount.ToString("N2"), HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(231, 76, 60)));
                AddGridCell(dataGrid, 5, rowIndex, item.CreditAmount.ToString("N2"), HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(39, 174, 96)));
                AddGridCell(dataGrid, 6, rowIndex, runningBalance.ToString("N2"), HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(16, 185, 129)));

                rowIndex++;
            }

            if (transactionItems.Count < itemsPerPage)
            {
                int emptyRows = itemsPerPage - transactionItems.Count;
                for (int i = 0; i < emptyRows; i++)
                {
                    dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    Border emptyRowBorder = new Border
                    {
                        Background = (rowIndex + i) % 2 == 0 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White
                    };
                    Grid.SetRow(emptyRowBorder, rowIndex + i);
                    Grid.SetColumnSpan(emptyRowBorder, headers.Length);
                    dataGrid.Children.Add(emptyRowBorder);

                    for (int j = 0; j < headers.Length; j++)
                    {
                        AddGridCell(dataGrid, j, rowIndex + i, "");
                    }
                }
            }

            return dataGrid;
        }

        private Border CreatePrintSummaryBorder(List<TransactionItem> transactionItems)
        {
            decimal totalDebit = transactionItems.Sum(x => x.DebitAmount);
            decimal totalCredit = transactionItems.Sum(x => x.CreditAmount);
            decimal closingBalance = totalDebit - totalCredit + _customerOpeningBalance;

            Border summaryBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 20, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            Grid summaryGrid = new Grid();
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddSummaryCard(summaryGrid, 0, "إجمالي المبيعات (مدين)", totalDebit, new SolidColorBrush(Color.FromRgb(231, 76, 60)));
            AddSummaryCard(summaryGrid, 1, "إجمالي التحصيلات (دائن)", totalCredit, new SolidColorBrush(Color.FromRgb(39, 174, 96)));
            AddSummaryCard(summaryGrid, 2, "الرصيد النهائي", closingBalance, new SolidColorBrush(Color.FromRgb(16, 185, 129)));

            summaryBorder.Child = summaryGrid;
            return summaryBorder;
        }

        #endregion

        #region دوال التصدير إلى Excel

        private void ExportToExcel(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isCustomerSelected || _selectedCustomerId == 0)
                {
                    MessageBox.Show("الرجاء اختيار عميل أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_currentTransactionItems == null || _currentTransactionItems.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"كشف_حساب_العميل_{_selectedCustomerCode}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("كشف حساب العميل");

                        DateTime fromDate = dpFromDate?.SelectedDate ?? GetDefaultStatementFromDate();
                        DateTime toDate = dpToDate?.SelectedDate ?? DateTime.Now;

                        worksheet.Cell(1, 1).Value = "كشف حساب العميل";
                        worksheet.Cell(1, 1).Style.Font.Bold = true;
                        worksheet.Cell(1, 1).Style.Font.FontSize = 16;

                        worksheet.Cell(2, 1).Value = $"اسم العميل: {_selectedCustomerName}";
                        worksheet.Cell(3, 1).Value = $"كود العميل: {_selectedCustomerCode}";
                        worksheet.Cell(4, 1).Value = $"الرصيد الافتتاحي: {_customerOpeningBalance:N2} (مدين)";
                        worksheet.Cell(5, 1).Value = $"الفترة: {fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}";
                        worksheet.Cell(6, 1).Value = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                        int startRow = 8;
                        string[] headers = { "التاريخ", "نوع المستند", "رقم المستند", "البيان", "مدين (مبيعات)", "دائن (تحصيلات)", "الرصيد بعد" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(startRow, i + 1).Value = headers[i];
                            worksheet.Cell(startRow, i + 1).Style.Font.Bold = true;
                            worksheet.Cell(startRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                        }

                        int dataRowStart = startRow + 1;
                        int rowIndex = dataRowStart;
                        decimal runningBalance = _customerOpeningBalance;

                        foreach (var item in _currentTransactionItems)
                        {
                            if (item.DocumentType == "فاتورة بيع")
                            {
                                runningBalance += item.DebitAmount;
                            }
                            else if (item.DocumentType == "سند قبض")
                            {
                                runningBalance -= item.CreditAmount;
                            }

                            worksheet.Cell(rowIndex, 1).Value = item.TransactionDate;
                            worksheet.Cell(rowIndex, 2).Value = item.DocumentType;
                            worksheet.Cell(rowIndex, 3).Value = item.DocumentNumber;
                            worksheet.Cell(rowIndex, 4).Value = string.IsNullOrEmpty(item.Description) ? "---" : item.Description;
                            worksheet.Cell(rowIndex, 5).Value = item.DebitAmount;
                            worksheet.Cell(rowIndex, 6).Value = item.CreditAmount;
                            worksheet.Cell(rowIndex, 7).Value = runningBalance;
                            rowIndex++;
                        }

                        int totalRow = rowIndex + 1;
                        worksheet.Cell(totalRow, 4).Value = "الإجمالي:";
                        worksheet.Cell(totalRow, 4).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 5).Value = _currentTransactionItems.Sum(x => x.DebitAmount).ToString("N2");
                        worksheet.Cell(totalRow, 5).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 6).Value = _currentTransactionItems.Sum(x => x.CreditAmount).ToString("N2");
                        worksheet.Cell(totalRow, 6).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 7).Value = runningBalance.ToString("N2");
                        worksheet.Cell(totalRow, 7).Style.Font.Bold = true;

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"تم تصدير {_currentTransactionItems.Count} سجل بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تصدير Excel: {ex.Message}");
                MessageBox.Show($"خطأ في تصدير البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال مساعدة عامة

        private async Task ShowErrorMessageAsync(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        #endregion
    }
}