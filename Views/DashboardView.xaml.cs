using LiveCharts;
using RasidAccountingSystem.Helpers;
using LiveCharts.Wpf;
using Microsoft.Win32;
using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;

namespace RasidAccountingSystem.Views
{
    public partial class DashboardView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _databaseService;
        private string _connectionString;
        private DataTable _reportData;
        private bool _isInitialized = false;
        private bool _isLoading = false;

        // متغيرات مؤشرات الأداء (KPIs)
        private decimal totalSales = 0;
        private decimal totalPurchases = 0;
        private decimal totalReceipts = 0;
        private decimal totalPayments = 0;
        private decimal netProfit = 0;
        private decimal profitMargin = 0;
        private decimal receivables = 0;
        private decimal payables = 0;

        // متغيرات المقارنة مع الفترة السابقة
        private decimal previousSales = 0;
        private decimal previousPurchases = 0;
        private decimal previousReceipts = 0;
        private decimal previousPayments = 0;
        private decimal previousProfit = 0;

        // بيانات الرسوم البيانية
        private List<decimal> monthlySalesData = new List<decimal>();
        private List<decimal> monthlyPurchasesData = new List<decimal>();
        private List<string> monthsList = new List<string>();

        // بيانات توزيع الإيرادات
        private decimal cashReceipts = 0;
        private decimal checkReceipts = 0;
        private decimal transferReceipts = 0;

        #endregion

        #region المنشئ

        public DashboardView()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeEvents();
            InitializeDefaultDates();
            ConfigureDataGridColumns();

            // ✅ رمز العملة بقى بيتقرأ من إعدادات النظام بدل ما يكون ثابت "ر.س" في التصميم
            lblCurrencyTotalAmount.Text = CurrencyHelper.GetCurrencySymbol();

            this.Loaded += async (sender, e) => await OnViewLoadedAsync();
        }

        private void InitializeDatabase()
        {
            try
            {
                _databaseService = new DatabaseService();
                if (_databaseService != null)
                {
                    _connectionString = _databaseService.GetConnectionString();
                    _reportData = new DataTable();
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
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void InitializeEvents()
        {
            try
            {
                if (FromDatePicker != null)
                {
                    FromDatePicker.SelectedDateChanged += async (s, e) => await OnDateRangeChangedAsync();
                }

                if (ToDatePicker != null)
                {
                    ToDatePicker.SelectedDateChanged += async (s, e) => await OnDateRangeChangedAsync();
                }

                if (PeriodCombo != null)
                {
                    PeriodCombo.SelectionChanged += async (s, e) => await OnPeriodChangedAsync();
                }

                if (ReportTypeCombo != null)
                {
                    ReportTypeCombo.SelectionChanged += async (s, e) => await OnReportTypeChangedAsync();
                }

                if (RefreshButton != null)
                {
                    RefreshButton.Click += async (s, e) => await RefreshDataAsync();
                }

                if (ExportExcelButton != null)
                {
                    ExportExcelButton.Click += ExportToExcel;
                }

                if (PrintButton != null)
                {
                    PrintButton.Click += PrintReport;
                }

                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة الأحداث بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تهيئة الأحداث: {ex.Message}");
            }
        }

        private void InitializeDefaultDates()
        {
            try
            {
                if (FromDatePicker != null)
                {
                    FromDatePicker.SelectedDate = DateTime.Now.AddMonths(-1);
                }

                if (ToDatePicker != null)
                {
                    ToDatePicker.SelectedDate = DateTime.Now;
                }

                if (PeriodCombo != null)
                {
                    PeriodCombo.SelectedIndex = 2; // شهر
                }

                if (ReportTypeCombo != null)
                {
                    ReportTypeCombo.SelectedIndex = 0; // الملخص المالي
                }

                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة التواريخ الافتراضية بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تهيئة التواريخ الافتراضية: {ex.Message}");
            }
        }

        private void ConfigureDataGridColumns()
        {
            try
            {
                if (DataGridTable == null) return;

                // تنظيف الأعمدة الحالية
                DataGridTable.Columns.Clear();

                // تعريف الأعمدة يدوياً لضمان ظهور البيانات بشكل صحيح
                DataGridTextColumn column1 = new DataGridTextColumn();
                column1.Header = "نوع المعاملة";
                column1.Binding = new System.Windows.Data.Binding("نوع المعاملة");
                column1.Width = new DataGridLength(110);
                column1.FontFamily = new FontFamily("Cairo");
                column1.FontSize = 12;
                DataGridTable.Columns.Add(column1);

                DataGridTextColumn column2 = new DataGridTextColumn();
                column2.Header = "رقم المعاملة";
                column2.Binding = new System.Windows.Data.Binding("رقم المعاملة");
                column2.Width = new DataGridLength(120);
                column2.FontFamily = new FontFamily("Cairo");
                column2.FontSize = 12;
                DataGridTable.Columns.Add(column2);

                DataGridTextColumn column3 = new DataGridTextColumn();
                column3.Header = "التاريخ";
                column3.Binding = new System.Windows.Data.Binding("التاريخ");
                column3.Width = new DataGridLength(100);
                column3.FontFamily = new FontFamily("Cairo");
                column3.FontSize = 12;
                DataGridTable.Columns.Add(column3);

                DataGridTextColumn column4 = new DataGridTextColumn();
                column4.Header = "الطرف";
                column4.Binding = new System.Windows.Data.Binding("الطرف");
                column4.Width = new DataGridLength(150);
                column4.FontFamily = new FontFamily("Cairo");
                column4.FontSize = 12;
                DataGridTable.Columns.Add(column4);

                DataGridTextColumn column5 = new DataGridTextColumn();
                column5.Header = "المبلغ";
                column5.Binding = new System.Windows.Data.Binding("المبلغ");
                column5.Width = new DataGridLength(120);
                column5.FontFamily = new FontFamily("Cairo");
                column5.FontSize = 12;
                column5.FontWeight = FontWeights.Bold;
                DataGridTable.Columns.Add(column5);

                DataGridTextColumn column6 = new DataGridTextColumn();
                column6.Header = "طريقة السداد";
                column6.Binding = new System.Windows.Data.Binding("طريقة السداد");
                column6.Width = new DataGridLength(110);
                column6.FontFamily = new FontFamily("Cairo");
                column6.FontSize = 12;
                DataGridTable.Columns.Add(column6);

                DataGridTextColumn column7 = new DataGridTextColumn();
                column7.Header = "الاتجاه";
                column7.Binding = new System.Windows.Data.Binding("الاتجاه");
                column7.Width = new DataGridLength(80);
                column7.FontFamily = new FontFamily("Cairo");
                column7.FontSize = 12;
                DataGridTable.Columns.Add(column7);

                DataGridTextColumn column8 = new DataGridTextColumn();
                column8.Header = "الخزينة";
                column8.Binding = new System.Windows.Data.Binding("الخزينة");
                column8.Width = new DataGridLength(120);
                column8.FontFamily = new FontFamily("Cairo");
                column8.FontSize = 12;
                DataGridTable.Columns.Add(column8);

                DataGridTextColumn column9 = new DataGridTextColumn();
                column9.Header = "البيان";
                column9.Binding = new System.Windows.Data.Binding("البيان");
                column9.Width = new DataGridLength(200);
                column9.FontFamily = new FontFamily("Cairo");
                column9.FontSize = 12;
                DataGridTable.Columns.Add(column9);

                System.Diagnostics.Debug.WriteLine("✅ تم تكوين أعمدة الجدول بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تكوين أعمدة الجدول: {ex.Message}");
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
                await LoadAllDataAsync();
                _isInitialized = true;
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

        private async Task LoadAllDataAsync()
        {
            DateTime fromDate = FromDatePicker?.SelectedDate ?? DateTime.Now.AddMonths(-1);
            DateTime toDate = ToDatePicker?.SelectedDate ?? DateTime.Now;
            string searchText = SearchBox?.Text ?? "";

            await LoadKpiDataAsync(fromDate, toDate);
            await LoadMonthlyDataAsync(fromDate, toDate);
            await LoadRevenueDistributionAsync(fromDate, toDate);
            await LoadReportDataAsync(fromDate, toDate, searchText);
            await LoadReceivablesAndPayablesAsync();

            UpdateKpiCards();
            UpdateCharts();
            UpdateRevenueChart();
            UpdateDataGrid();
        }

        private async Task OnDateRangeChangedAsync()
        {
            if (!_isInitialized) return;
            await LoadAllDataAsync();
        }

        private async Task OnPeriodChangedAsync()
        {
            if (!_isInitialized) return;
            await UpdateDateRangeFromPeriodAsync();
            await LoadAllDataAsync();
        }

        private async Task OnReportTypeChangedAsync()
        {
            if (!_isInitialized) return;
            await LoadReportDataBasedOnTypeAsync();
        }

        private async Task RefreshDataAsync()
        {
            await LoadAllDataAsync();
            await ShowMessageAsync("تم تحديث البيانات بنجاح", "تحديث");
        }

        private async Task UpdateDateRangeFromPeriodAsync()
        {
            if (PeriodCombo?.SelectedItem == null) return;

            string period = (PeriodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "شهر";
            DateTime toDate = DateTime.Now;
            DateTime fromDate = toDate;

            switch (period)
            {
                case "اليوم":
                    fromDate = toDate.Date;
                    break;
                case "أسبوع":
                    fromDate = toDate.AddDays(-7);
                    break;
                case "شهر":
                    fromDate = toDate.AddMonths(-1);
                    break;
                case "ربع سنة":
                    fromDate = toDate.AddMonths(-3);
                    break;
                case "سنة":
                    fromDate = toDate.AddYears(-1);
                    break;
                default:
                    fromDate = toDate.AddMonths(-1);
                    break;
            }

            if (FromDatePicker != null)
            {
                FromDatePicker.SelectedDate = fromDate;
            }

            if (ToDatePicker != null)
            {
                ToDatePicker.SelectedDate = toDate;
            }

            await Task.CompletedTask;
        }

        #endregion

        #region دوال تحميل بيانات مؤشرات الأداء (KPIs)

        private async Task LoadKpiDataAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // تحميل إجمالي المبيعات
                    string salesQuery = @"
                        SELECT COALESCE(SUM(TotalAmount), 0) 
                        FROM SalesInvoices 
                        WHERE InvoiceDate BETWEEN @from AND @to 
                        AND IsVoid = 0";

                    using (var cmd = new SQLiteCommand(salesQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        totalSales = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // تحميل إجمالي المشتريات
                    string purchasesQuery = @"
                        SELECT COALESCE(SUM(TotalAmount), 0) 
                        FROM PurchaseInvoices 
                        WHERE InvoiceDate BETWEEN @from AND @to 
                        AND IsVoid = 0";

                    using (var cmd = new SQLiteCommand(purchasesQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        totalPurchases = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // تحميل إجمالي المقبوضات (سندات القبض)
                    string receiptsQuery = @"
                        SELECT COALESCE(SUM(Amount), 0) 
                        FROM ReceiptVouchers 
                        WHERE VoucherDate BETWEEN @from AND @to 
                        AND IsPosted = 1";

                    using (var cmd = new SQLiteCommand(receiptsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        totalReceipts = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // تحميل إجمالي المدفوعات (سندات الصرف)
                    string paymentsQuery = @"
                        SELECT COALESCE(SUM(Amount), 0) 
                        FROM PaymentVouchers 
                        WHERE VoucherDate BETWEEN @from AND @to 
                        AND IsPosted = 1";

                    using (var cmd = new SQLiteCommand(paymentsQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        totalPayments = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // حساب صافي الربح وهامش الربح
                    netProfit = totalSales - totalPurchases;
                    profitMargin = totalSales > 0 ? (netProfit / totalSales) * 100 : 0;

                    // تحميل بيانات الفترة السابقة للمقارنة
                    TimeSpan dateDiff = toDate - fromDate;
                    DateTime previousFromDate = fromDate.AddDays(-dateDiff.TotalDays);
                    DateTime previousToDate = fromDate.AddDays(-1);

                    // المبيعات السابقة
                    using (var cmd = new SQLiteCommand(salesQuery, connection))
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@from", previousFromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", previousToDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        previousSales = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // المشتريات السابقة
                    using (var cmd = new SQLiteCommand(purchasesQuery, connection))
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@from", previousFromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", previousToDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        previousPurchases = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // المقبوضات السابقة
                    using (var cmd = new SQLiteCommand(receiptsQuery, connection))
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@from", previousFromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", previousToDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        previousReceipts = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // المدفوعات السابقة
                    using (var cmd = new SQLiteCommand(paymentsQuery, connection))
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@from", previousFromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", previousToDate.ToString("yyyy-MM-dd"));
                        var result = await cmd.ExecuteScalarAsync();
                        previousPayments = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                    }

                    // الأرباح السابقة
                    previousProfit = previousSales - previousPurchases;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل بيانات مؤشرات الأداء: المبيعات={totalSales}, المشتريات={totalPurchases}, صافي الربح={netProfit}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل بيانات مؤشرات الأداء: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// حساب الرصيد الفعلي للعميل من CustomerTransactions
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه الحركات تُخزَّن
        /// فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync)، فكانت مساهمة كل
        /// فواتير العميل في هذا الحساب تساوي صفر دائمًا. المعادلة الصحيحة: الرصيد = OpeningBalance
        /// + SUM(DebitAmount للفواتير) - SUM(CreditAmount للتحصيلات)
        /// </summary>
        private async Task<decimal> GetCustomerActualBalanceForDashboardAsync(int customerId, decimal openingBalance)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            @openingBalance + 
                            COALESCE((SELECT SUM(DebitAmount) FROM CustomerTransactions WHERE CustomerID = @customerId AND TransactionType = 'Invoice'), 0) -
                            COALESCE((SELECT SUM(CreditAmount) FROM CustomerTransactions WHERE CustomerID = @customerId AND TransactionType = 'Receipt'), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@openingBalance", openingBalance);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerActualBalanceForDashboardAsync Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// حساب الرصيد الفعلي للمورد من SupplierTransactions
        /// المعادلة الصحيحة: الرصيد = OpeningBalance + SUM(CreditAmount للمشتريات) - SUM(CreditAmount للمدفوعات)
        /// </summary>
        private async Task<decimal> GetSupplierActualBalanceForDashboardAsync(int supplierId, decimal openingBalance)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // ✅ حساب الرصيد من كلا العمودين (حل دائم بغض النظر عن مكان تخزين البيانات)
                    string sql = @"
                SELECT 
                    @openingBalance + 
                    COALESCE((SELECT SUM(CreditAmount) - SUM(DebitAmount) FROM SupplierTransactions WHERE SupplierID = @supplierId AND TransactionType = 'Purchase'), 0) -
                    COALESCE((SELECT SUM(CreditAmount) - SUM(DebitAmount) FROM SupplierTransactions WHERE SupplierID = @supplierId AND TransactionType = 'Payment'), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        cmd.Parameters.AddWithValue("@openingBalance", openingBalance);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSupplierActualBalanceForDashboardAsync Error: {ex.Message}");
                return 0;
            }
        }

        private async Task LoadReceivablesAndPayablesAsync()
        {
            try
            {
                decimal totalReceivables = 0;
                decimal totalPayables = 0;

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // ============================================================
                    // حساب الذمم المدينة (أرصدة العملاء الموجبة)
                    // ============================================================
                    string getAllCustomersSql = @"
                        SELECT CustomerID, COALESCE(OpeningBalance, 0) as OpeningBalance
                        FROM Customers 
                        WHERE IsActive = 1";

                    var customersList = new List<(int Id, decimal OpeningBalance)>();

                    using (var cmd = new SQLiteCommand(getAllCustomersSql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int customerId = reader.GetInt32(0);
                            decimal openingBalance = reader.GetDecimal(1);
                            customersList.Add((customerId, openingBalance));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"📊 جاري حساب أرصدة {customersList.Count} عميل للذمم المدينة");

                    foreach (var customer in customersList)
                    {
                        decimal actualBalance = await GetCustomerActualBalanceForDashboardAsync(customer.Id, customer.OpeningBalance);

                        if (actualBalance > 0)
                        {
                            totalReceivables += actualBalance;
                            System.Diagnostics.Debug.WriteLine($"   العميل {customer.Id}: الرصيد = {actualBalance:N2} (مدين)");
                        }
                    }

                    // ============================================================
                    // حساب الذمم الدائنة (أرصدة الموردين الموجبة)
                    // ============================================================
                    string getAllSuppliersSql = @"
                        SELECT SupplierID, COALESCE(OpeningBalance, 0) as OpeningBalance
                        FROM Suppliers 
                        WHERE IsActive = 1";

                    var suppliersList = new List<(int Id, decimal OpeningBalance)>();

                    using (var cmd = new SQLiteCommand(getAllSuppliersSql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int supplierId = reader.GetInt32(0);
                            decimal openingBalance = reader.GetDecimal(1);
                            suppliersList.Add((supplierId, openingBalance));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"📊 جاري حساب أرصدة {suppliersList.Count} مورد للذمم الدائنة");

                    foreach (var supplier in suppliersList)
                    {
                        decimal actualBalance = await GetSupplierActualBalanceForDashboardAsync(supplier.Id, supplier.OpeningBalance);

                        if (actualBalance > 0)
                        {
                            totalPayables += actualBalance;
                            System.Diagnostics.Debug.WriteLine($"   المورد {supplier.Id}: الرصيد = {actualBalance:N2} (دائن)");
                        }
                    }
                }

                receivables = totalReceivables;
                payables = totalPayables;

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل بيانات الذمم: مدينة={receivables:N2}, دائنة={payables:N2}");

                // تحديث بطاقات الذمم في الواجهة
                Dispatcher.InvokeAsync(() =>
                {
                    if (ReceivablesText != null)
                    {
                        ReceivablesText.Text = receivables.ToString("N0");
                    }

                    if (PayablesText != null)
                    {
                        PayablesText.Text = payables.ToString("N0");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل بيانات الذمم: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region دوال تحميل البيانات الشهرية للرسوم البيانية

        private async Task LoadMonthlyDataAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                monthlySalesData.Clear();
                monthlyPurchasesData.Clear();
                monthsList.Clear();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    DateTime currentDate = new DateTime(fromDate.Year, fromDate.Month, 1);
                    int totalMonths = ((toDate.Year - fromDate.Year) * 12) + (toDate.Month - fromDate.Month) + 1;

                    // تحديد عدد الأشهر (بحد أقصى 12 شهراً للعرض الواضح)
                    int maxMonths = Math.Min(totalMonths, 12);

                    for (int i = 0; i < maxMonths; i++)
                    {
                        DateTime monthStart = currentDate.AddMonths(i);
                        DateTime monthEnd = monthStart.AddMonths(1).AddDays(-1);

                        // إضافة اسم الشهر بالميلادي (مثل يناير, فبراير, مارس)
                        string monthName = monthStart.ToString("MMMM", new System.Globalization.CultureInfo("ar-EG"));
                        monthsList.Add(monthName);

                        // تحميل مبيعات الشهر
                        string salesQuery = @"
                            SELECT COALESCE(SUM(TotalAmount), 0) 
                            FROM SalesInvoices 
                            WHERE InvoiceDate BETWEEN @start AND @end 
                            AND IsVoid = 0";

                        using (var cmd = new SQLiteCommand(salesQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@start", monthStart.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@end", monthEnd.ToString("yyyy-MM-dd"));
                            var result = await cmd.ExecuteScalarAsync();
                            decimal monthlySales = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                            monthlySalesData.Add(monthlySales);
                        }

                        // تحميل مشتريات الشهر
                        string purchasesQuery = @"
                            SELECT COALESCE(SUM(TotalAmount), 0) 
                            FROM PurchaseInvoices 
                            WHERE InvoiceDate BETWEEN @start AND @end 
                            AND IsVoid = 0";

                        using (var cmd = new SQLiteCommand(purchasesQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@start", monthStart.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@end", monthEnd.ToString("yyyy-MM-dd"));
                            var result = await cmd.ExecuteScalarAsync();
                            decimal monthlyPurchases = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
                            monthlyPurchasesData.Add(monthlyPurchases);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل البيانات الشهرية: {monthsList.Count} شهر");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل البيانات الشهرية: {ex.Message}");
            }
        }

        private async Task LoadRevenueDistributionAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // تحميل توزيع الإيرادات حسب طريقة السداد من سندات القبض
                    string revenueDistributionQuery = @"
                        SELECT 
                            PaymentMethod,
                            COALESCE(SUM(Amount), 0) as TotalAmount
                        FROM ReceiptVouchers 
                        WHERE VoucherDate BETWEEN @from AND @to 
                        AND IsPosted = 1
                        GROUP BY PaymentMethod";

                    using (var cmd = new SQLiteCommand(revenueDistributionQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            cashReceipts = 0;
                            checkReceipts = 0;
                            transferReceipts = 0;

                            while (await reader.ReadAsync())
                            {
                                string paymentMethod = reader.GetString(0);
                                decimal amount = reader.GetDecimal(1);

                                switch (paymentMethod.ToLower())
                                {
                                    case "cash":
                                        cashReceipts = amount;
                                        break;
                                    case "check":
                                        checkReceipts = amount;
                                        break;
                                    case "transfer":
                                    case "bank":
                                        transferReceipts = amount;
                                        break;
                                }
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل توزيع الإيرادات: نقدي={cashReceipts}, شيكات={checkReceipts}, تحويلات={transferReceipts}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل توزيع الإيرادات: {ex.Message}");
            }
        }

        #endregion

        #region دوال تحميل بيانات التقرير (جدول المعاملات المالية)

        private async Task LoadReportDataAsync(DateTime fromDate, DateTime toDate, string searchText = "")
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    if (_reportData != null)
                    {
                        _reportData.Clear();
                    }
                    else
                    {
                        _reportData = new DataTable();
                    }

                    // استعلام شامل لجلب جميع المعاملات المالية من جميع الجداول
                    string comprehensiveQuery = @"
                        SELECT 
                            'سند قبض' as 'نوع المعاملة',
                            VoucherNumber as 'رقم المعاملة',
                            VoucherDate as 'التاريخ',
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as 'الطرف',
                            Amount as 'المبلغ',
                            CASE 
                                WHEN PaymentMethod = 'Cash' THEN 'نقدي'
                                WHEN PaymentMethod = 'Check' THEN 'شيك'
                                WHEN PaymentMethod = 'Transfer' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Bank' THEN 'تحويل بنكي'
                                ELSE PaymentMethod
                            END as 'طريقة السداد',
                            'وارد' as 'الاتجاه',
                            COALESCE(t.TreasuryNameAr, '') as 'الخزينة',
                            COALESCE(rv.Description, '') as 'البيان'
                        FROM ReceiptVouchers rv
                        LEFT JOIN Customers c ON rv.CustomerID = c.CustomerID
                        LEFT JOIN Treasury t ON rv.TreasuryID = t.TreasuryID
                        WHERE rv.VoucherDate BETWEEN @from AND @to 
                        AND rv.IsPosted = 1
                        
                        UNION ALL
                        
                        SELECT 
                            'سند صرف' as 'نوع المعاملة',
                            VoucherNumber as 'رقم المعاملة',
                            VoucherDate as 'التاريخ',
                            CASE 
                                WHEN rv.CustomerID IS NOT NULL THEN COALESCE(c.CustomerNameAr, c.CustomerName, '')
                                WHEN rv.SupplierID IS NOT NULL THEN COALESCE(s.SupplierNameAr, s.SupplierName, '')
                                ELSE ''
                            END as 'الطرف',
                            Amount as 'المبلغ',
                            CASE 
                                WHEN PaymentMethod = 'Cash' THEN 'نقدي'
                                WHEN PaymentMethod = 'Check' THEN 'شيك'
                                WHEN PaymentMethod = 'Transfer' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Bank' THEN 'تحويل بنكي'
                                ELSE PaymentMethod
                            END as 'طريقة السداد',
                            'منصرف' as 'الاتجاه',
                            COALESCE(t.TreasuryNameAr, '') as 'الخزينة',
                            COALESCE(rv.Description, '') as 'البيان'
                        FROM PaymentVouchers rv
                        LEFT JOIN Customers c ON rv.CustomerID = c.CustomerID
                        LEFT JOIN Suppliers s ON rv.SupplierID = s.SupplierID
                        LEFT JOIN Treasury t ON rv.TreasuryID = t.TreasuryID
                        WHERE rv.VoucherDate BETWEEN @from AND @to 
                        AND rv.IsPosted = 1
                        
                        UNION ALL
                        
                        SELECT 
                            'فاتورة بيع' as 'نوع المعاملة',
                            InvoiceNumber as 'رقم المعاملة',
                            InvoiceDate as 'التاريخ',
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as 'الطرف',
                            TotalAmount as 'المبلغ',
                            CASE 
                                WHEN PaymentMethod = 'Cash' THEN 'نقدي'
                                WHEN PaymentMethod = 'Check' THEN 'شيك'
                                WHEN PaymentMethod = 'Transfer' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Bank' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Credit' THEN 'آجل'
                                ELSE PaymentMethod
                            END as 'طريقة السداد',
                            'وارد' as 'الاتجاه',
                            '' as 'الخزينة',
                            COALESCE(si.Notes, '') as 'البيان'
                        FROM SalesInvoices si
                        LEFT JOIN Customers c ON si.CustomerID = c.CustomerID
                        WHERE si.InvoiceDate BETWEEN @from AND @to 
                        AND si.IsVoid = 0
                        
                        UNION ALL
                        
                        SELECT 
                            'فاتورة مشتريات' as 'نوع المعاملة',
                            InvoiceNumber as 'رقم المعاملة',
                            InvoiceDate as 'التاريخ',
                            COALESCE(s.SupplierNameAr, s.SupplierName, '') as 'الطرف',
                            TotalAmount as 'المبلغ',
                            CASE 
                                WHEN PaymentMethod = 'Cash' THEN 'نقدي'
                                WHEN PaymentMethod = 'Check' THEN 'شيك'
                                WHEN PaymentMethod = 'Transfer' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Bank' THEN 'تحويل بنكي'
                                WHEN PaymentMethod = 'Credit' THEN 'آجل'
                                ELSE PaymentMethod
                            END as 'طريقة السداد',
                            'منصرف' as 'الاتجاه',
                            '' as 'الخزينة',
                            COALESCE(pi.Notes, '') as 'البيان'
                        FROM PurchaseInvoices pi
                        LEFT JOIN Suppliers s ON pi.SupplierID = s.SupplierID
                        WHERE pi.InvoiceDate BETWEEN @from AND @to 
                        AND pi.IsVoid = 0
                        
                        ORDER BY [التاريخ] DESC
                        LIMIT 1000";

                    using (var cmd = new SQLiteCommand(comprehensiveQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));

                        using (var adapter = new SQLiteDataAdapter(cmd))
                        {
                            adapter.Fill(_reportData);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_reportData?.Rows?.Count ?? 0} سجل في تقرير المعاملات المالية");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل بيانات التقرير: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                if (_reportData == null)
                {
                    _reportData = new DataTable();
                }
            }
        }

        private async Task LoadReportDataBasedOnTypeAsync()
        {
            if (!_isInitialized) return;

            DateTime fromDate = FromDatePicker?.SelectedDate ?? DateTime.Now.AddMonths(-1);
            DateTime toDate = ToDatePicker?.SelectedDate ?? DateTime.Now;
            string reportType = (ReportTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "الملخص المالي";

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    if (_reportData != null)
                    {
                        _reportData.Clear();
                    }
                    else
                    {
                        _reportData = new DataTable();
                    }

                    string query = "";

                    switch (reportType)
                    {
                        case "تقرير المبيعات":
                            query = @"
                                SELECT 
                                    si.InvoiceNumber as 'رقم الفاتورة',
                                    si.InvoiceDate as 'التاريخ',
                                    c.CustomerNameAr as 'العميل',
                                    si.TotalAmount as 'المبلغ',
                                    si.PaymentStatus as 'حالة السداد'
                                FROM SalesInvoices si
                                LEFT JOIN Customers c ON si.CustomerID = c.CustomerID
                                WHERE si.InvoiceDate BETWEEN @from AND @to 
                                AND si.IsVoid = 0
                                ORDER BY si.InvoiceDate DESC
                                LIMIT 500";
                            break;

                        case "تقرير المشتريات":
                            query = @"
                                SELECT 
                                    pi.InvoiceNumber as 'رقم الفاتورة',
                                    pi.InvoiceDate as 'التاريخ',
                                    s.SupplierNameAr as 'المورد',
                                    pi.TotalAmount as 'المبلغ',
                                    pi.PaymentStatus as 'حالة السداد'
                                FROM PurchaseInvoices pi
                                LEFT JOIN Suppliers s ON pi.SupplierID = s.SupplierID
                                WHERE pi.InvoiceDate BETWEEN @from AND @to 
                                AND pi.IsVoid = 0
                                ORDER BY pi.InvoiceDate DESC
                                LIMIT 500";
                            break;

                        case "الملخص المالي":
                        default:
                            string searchText = SearchBox?.Text ?? "";
                            await LoadReportDataAsync(fromDate, toDate, searchText);
                            return;
                    }

                    if (!string.IsNullOrEmpty(query))
                    {
                        using (var cmd = new SQLiteCommand(query, connection))
                        {
                            cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));

                            using (var adapter = new SQLiteDataAdapter(cmd))
                            {
                                adapter.Fill(_reportData);
                            }
                        }
                    }
                }

                UpdateDataGrid();
                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_reportData?.Rows?.Count ?? 0} سجل للتقرير نوع: {reportType}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل التقرير حسب النوع: {ex.Message}");
            }
        }

        #endregion

        #region دوال تحديث واجهة المستخدم

        private void UpdateKpiCards()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // تحديث بطاقة المبيعات
                    if (SalesText != null)
                    {
                        SalesText.Text = totalSales.ToString("N0");
                    }

                    // تحديث بطاقة المشتريات
                    if (PurchasesText != null)
                    {
                        PurchasesText.Text = totalPurchases.ToString("N0");
                    }

                    // تحديث بطاقة صافي الربح
                    if (ProfitText != null)
                    {
                        ProfitText.Text = netProfit.ToString("N0");
                    }

                    // تحديث بطاقة هامش الربح
                    if (MarginText != null)
                    {
                        MarginText.Text = $"{profitMargin:F1}%";
                    }

                    // تحديث بطاقة المقبوضات
                    if (ReceiptsText != null)
                    {
                        ReceiptsText.Text = totalReceipts.ToString("N0");
                    }

                    // تحديث بطاقة المدفوعات
                    if (PaymentsText != null)
                    {
                        PaymentsText.Text = totalPayments.ToString("N0");
                    }

                    // حساب وتحديث اتجاه المبيعات
                    if (SalesTrendText != null)
                    {
                        decimal salesChange = previousSales > 0 ? ((totalSales - previousSales) / previousSales) * 100 : (totalSales > 0 ? 100 : 0);

                        if (salesChange >= 0)
                        {
                            SalesTrendText.Text = $"▲ +{salesChange:F0}%";
                            SalesTrendText.Foreground = new SolidColorBrush(Color.FromRgb(0, 179, 131));
                        }
                        else
                        {
                            SalesTrendText.Text = $"▼ {Math.Abs(salesChange):F0}%";
                            SalesTrendText.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        }
                    }

                    // حساب وتحديث اتجاه المشتريات
                    if (PurchasesTrendText != null)
                    {
                        decimal purchasesChange = previousPurchases > 0 ? ((totalPurchases - previousPurchases) / previousPurchases) * 100 : (totalPurchases > 0 ? 100 : 0);

                        if (purchasesChange >= 0)
                        {
                            PurchasesTrendText.Text = $"▲ +{purchasesChange:F0}%";
                            PurchasesTrendText.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        }
                        else
                        {
                            PurchasesTrendText.Text = $"▼ {Math.Abs(purchasesChange):F0}%";
                            PurchasesTrendText.Foreground = new SolidColorBrush(Color.FromRgb(0, 179, 131));
                        }
                    }

                    // حساب وتحديث اتجاه الأرباح
                    if (ProfitTrendText != null)
                    {
                        decimal profitChange = previousProfit > 0 ? ((netProfit - previousProfit) / previousProfit) * 100 : (netProfit > 0 ? 100 : 0);

                        if (profitChange >= 0)
                        {
                            ProfitTrendText.Text = $"▲ +{profitChange:F0}%";
                            ProfitTrendText.Foreground = new SolidColorBrush(Color.FromRgb(0, 179, 131));
                        }
                        else
                        {
                            ProfitTrendText.Text = $"▼ {Math.Abs(profitChange):F0}%";
                            ProfitTrendText.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        }
                    }

                    // حساب وتحديث اتجاه هامش الربح
                    if (MarginTrendText != null)
                    {
                        decimal previousMargin = previousSales > 0 ? ((previousSales - previousPurchases) / previousSales) * 100 : 0;
                        decimal marginChange = profitMargin - previousMargin;

                        if (marginChange >= 0)
                        {
                            MarginTrendText.Text = $"▲ +{marginChange:F1} نقطة";
                            MarginTrendText.Foreground = new SolidColorBrush(Color.FromRgb(0, 179, 131));
                        }
                        else
                        {
                            MarginTrendText.Text = $"▼ {Math.Abs(marginChange):F1} نقطة";
                            MarginTrendText.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("✅ تم تحديث بطاقات مؤشرات الأداء");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحديث بطاقات KPIs: {ex.Message}");
                }
            });
        }

        private void UpdateCharts()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (SalesChart != null && monthlySalesData != null && monthlySalesData.Any())
                    {
                        // إنشاء مجموعة السلاسل للرسم البياني
                        var seriesCollection = new SeriesCollection();

                        // إضافة سلسلة المبيعات
                        var salesSeries = new ColumnSeries
                        {
                            Title = "المبيعات",
                            Values = new ChartValues<decimal>(monthlySalesData),
                            DataLabels = true,
                            Fill = new SolidColorBrush(Color.FromRgb(45, 91, 255)),
                            MaxColumnWidth = 40
                        };
                        seriesCollection.Add(salesSeries);

                        // إضافة سلسلة المشتريات إذا كانت البيانات متاحة
                        if (monthlyPurchasesData != null && monthlyPurchasesData.Any())
                        {
                            var purchasesSeries = new ColumnSeries
                            {
                                Title = "المشتريات",
                                Values = new ChartValues<decimal>(monthlyPurchasesData),
                                DataLabels = true,
                                Fill = new SolidColorBrush(Color.FromRgb(255, 107, 107)),
                                MaxColumnWidth = 40
                            };
                            seriesCollection.Add(purchasesSeries);
                        }

                        SalesChart.Series = seriesCollection;

                        // تكوين المحور X (الأشهر)
                        SalesChart.AxisX.Clear();
                        SalesChart.AxisX.Add(new Axis
                        {
                            Labels = monthsList,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(68, 68, 68))
                        });

                        // تكوين المحور Y (القيم)
                        SalesChart.AxisY.Clear();
                        SalesChart.AxisY.Add(new Axis
                        {
                            LabelFormatter = value => value.ToString("N0"),
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                            Separator = new LiveCharts.Wpf.Separator
                            {
                                Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                                StrokeThickness = 0.5
                            }
                        });

                        // إعدادات إضافية للرسم البياني
                        SalesChart.LegendLocation = LegendLocation.Top;
                        SalesChart.DisableAnimations = false;
                        SalesChart.Hoverable = true;
                        SalesChart.DataTooltip = new DefaultTooltip
                        {
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                            Background = new SolidColorBrush(Color.FromRgb(0, 0, 0))
                        };

                        System.Diagnostics.Debug.WriteLine("✅ تم تحديث الرسم البياني الخطي");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحديث الرسم البياني الخطي: {ex.Message}");
                }
            });
        }

        private void UpdateRevenueChart()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (RevenueChart != null)
                    {
                        var seriesCollection = new SeriesCollection();

                        // إضافة توزيع الإيرادات حسب طريقة السداد
                        if (cashReceipts > 0)
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = "نقدي",
                                Values = new ChartValues<decimal> { cashReceipts },
                                DataLabels = true,
                                LabelPoint = chartPoint => chartPoint.Y.ToString("N0"),
                                Fill = new SolidColorBrush(Color.FromRgb(0, 179, 131)),
                                Stroke = new SolidColorBrush(Colors.White),
                                StrokeThickness = 2
                            });
                        }

                        if (checkReceipts > 0)
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = "شيكات",
                                Values = new ChartValues<decimal> { checkReceipts },
                                DataLabels = true,
                                LabelPoint = chartPoint => chartPoint.Y.ToString("N0"),
                                Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                                Stroke = new SolidColorBrush(Colors.White),
                                StrokeThickness = 2
                            });
                        }

                        if (transferReceipts > 0)
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = "تحويلات بنكية",
                                Values = new ChartValues<decimal> { transferReceipts },
                                DataLabels = true,
                                LabelPoint = chartPoint => chartPoint.Y.ToString("N0"),
                                Fill = new SolidColorBrush(Color.FromRgb(45, 91, 255)),
                                Stroke = new SolidColorBrush(Colors.White),
                                StrokeThickness = 2
                            });
                        }

                        // إذا لم تكن هناك إيرادات، أظهر رسالة
                        if (seriesCollection.Count == 0)
                        {
                            seriesCollection.Add(new PieSeries
                            {
                                Title = "لا توجد إيرادات",
                                Values = new ChartValues<decimal> { 1 },
                                DataLabels = true,
                                LabelPoint = chartPoint => "0",
                                Fill = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                            });
                        }

                        RevenueChart.Series = seriesCollection;
                        RevenueChart.LegendLocation = LegendLocation.Bottom;
                        RevenueChart.DisableAnimations = false;

                        System.Diagnostics.Debug.WriteLine("✅ تم تحديث الرسم البياني الدائري");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحديث الرسم البياني الدائري: {ex.Message}");
                }
            });
        }

        private void UpdateDataGrid()
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (DataGridTable == null) return;

                    if (_reportData == null || _reportData.Rows.Count == 0)
                    {
                        DataGridTable.ItemsSource = null;

                        if (RecordsCountText != null)
                        {
                            RecordsCountText.Text = "0";
                        }

                        if (TotalAmountText != null)
                        {
                            TotalAmountText.Text = "0.00";
                        }

                        System.Diagnostics.Debug.WriteLine("⚠️ لا توجد بيانات لعرضها في الجدول");
                        return;
                    }

                    // تعيين مصدر البيانات
                    DataGridTable.ItemsSource = _reportData.DefaultView;

                    // تحديث عدد السجلات
                    if (RecordsCountText != null)
                    {
                        RecordsCountText.Text = _reportData.Rows.Count.ToString();
                    }

                    // حساب وتحديث الإجمالي
                    decimal totalAmount = 0;
                    foreach (DataRow row in _reportData.Rows)
                    {
                        if (row.Table.Columns.Contains("المبلغ") && row["المبلغ"] != DBNull.Value)
                        {
                            totalAmount += Convert.ToDecimal(row["المبلغ"]);
                        }
                    }

                    if (TotalAmountText != null)
                    {
                        TotalAmountText.Text = totalAmount.ToString("N2");
                    }

                    // تحديث لون الإجمالي (أخضر إذا موجب، أحمر إذا سالب)
                    if (TotalAmountText != null)
                    {
                        if (totalAmount >= 0)
                        {
                            TotalAmountText.Foreground = new SolidColorBrush(Color.FromRgb(0, 179, 131));
                        }
                        else
                        {
                            TotalAmountText.Foreground = new SolidColorBrush(Color.FromRgb(255, 77, 79));
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم تحديث الجدول: {_reportData.Rows.Count} صفوف، الإجمالي: {totalAmount:N2}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحديث الجدول: {ex.Message}");
                }
            });
        }

        #endregion

        #region دوال التصدير والطباعة

        private void ExportToExcel(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_reportData == null || _reportData.Rows.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"التقرير_المالي_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("التقرير المالي");

                        // إضافة عنوان التقرير
                        DateTime fromDate = FromDatePicker?.SelectedDate ?? DateTime.Now.AddMonths(-1);
                        DateTime toDate = ToDatePicker?.SelectedDate ?? DateTime.Now;
                        string reportType = (ReportTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "الملخص المالي";

                        worksheet.Cell(1, 1).Value = $"تقرير {reportType}";
                        worksheet.Cell(2, 1).Value = $"من تاريخ: {fromDate:yyyy-MM-dd} إلى تاريخ: {toDate:yyyy-MM-dd}";
                        worksheet.Cell(3, 1).Value = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                        // إضافة رؤوس الأعمدة من الجدول
                        int startRow = 5;
                        for (int i = 0; i < DataGridTable.Columns.Count; i++)
                        {
                            if (DataGridTable.Columns[i].Visibility == Visibility.Visible)
                            {
                                string header = DataGridTable.Columns[i].Header?.ToString() ?? $"عمود{i + 1}";
                                worksheet.Cell(startRow, i + 1).Value = header;
                                worksheet.Cell(startRow, i + 1).Style.Font.Bold = true;
                                worksheet.Cell(startRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                            }
                        }

                        // إضافة البيانات
                        int dataRowStart = 6;
                        for (int i = 0; i < _reportData.Rows.Count; i++)
                        {
                            for (int j = 0; j < _reportData.Columns.Count; j++)
                            {
                                string value = _reportData.Rows[i][j]?.ToString() ?? "";
                                worksheet.Cell(dataRowStart + i, j + 1).Value = value;
                            }
                        }

                        // إضافة الإجمالي في نهاية الجدول
                        int totalRow = dataRowStart + _reportData.Rows.Count;
                        worksheet.Cell(totalRow, _reportData.Columns.Count - 1).Value = "الإجمالي:";
                        worksheet.Cell(totalRow, _reportData.Columns.Count - 1).Style.Font.Bold = true;

                        if (TotalAmountText != null)
                        {
                            worksheet.Cell(totalRow, _reportData.Columns.Count).Value = TotalAmountText.Text;
                            worksheet.Cell(totalRow, _reportData.Columns.Count).Style.Font.Bold = true;
                        }

                        // تحسين عرض الأعمدة
                        worksheet.Columns().AdjustToContents();

                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"تم تصدير {_reportData.Rows.Count} سجل بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Debug.WriteLine($"✅ تم تصدير البيانات إلى Excel: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تصدير Excel: {ex.Message}");
                MessageBox.Show($"خطأ في تصدير البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_reportData == null || _reportData.Rows.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // إنشاء عنصر لعرض التقرير للطباعة
                    StackPanel printPanel = new StackPanel();
                    printPanel.Margin = new Thickness(20);
                    printPanel.Background = Brushes.White;

                    // عنوان التقرير
                    DateTime fromDate = FromDatePicker?.SelectedDate ?? DateTime.Now.AddMonths(-1);
                    DateTime toDate = ToDatePicker?.SelectedDate ?? DateTime.Now;
                    string reportType = (ReportTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "الملخص المالي";

                    TextBlock titleBlock = new TextBlock
                    {
                        Text = $"تقرير {reportType}",
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    printPanel.Children.Add(titleBlock);

                    TextBlock dateBlock = new TextBlock
                    {
                        Text = $"الفترة: {fromDate:yyyy-MM-dd} إلى {toDate:yyyy-MM-dd}",
                        FontSize = 12,
                        Foreground = Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    printPanel.Children.Add(dateBlock);

                    // إنشاء DataGrid جديد للطباعة مع تعريف الأعمدة يدوياً
                    DataGrid printGrid = new DataGrid
                    {
                        ItemsSource = _reportData.DefaultView,
                        AutoGenerateColumns = false,
                        IsReadOnly = true,
                        HeadersVisibility = DataGridHeadersVisibility.Column,
                        FontSize = 10,
                        Margin = new Thickness(0, 10, 0, 0),
                        RowHeight = 30
                    };

                    // إضافة الأعمدة يدوياً باستخدام نفس أسماء الأعمدة
                    string[] columnHeaders = { "نوع المعاملة", "رقم المعاملة", "التاريخ", "الطرف", "المبلغ", "طريقة السداد", "الاتجاه", "الخزينة", "البيان" };

                    foreach (string header in columnHeaders)
                    {
                        DataGridTextColumn newColumn = new DataGridTextColumn();
                        newColumn.Header = header;
                        newColumn.Binding = new System.Windows.Data.Binding(header);
                        newColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                        printGrid.Columns.Add(newColumn);
                    }

                    printPanel.Children.Add(printGrid);

                    // إضافة الإجمالي
                    TextBlock totalBlock = new TextBlock
                    {
                        Text = $"الإجمالي: {TotalAmountText?.Text ?? "0.00"}",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    printPanel.Children.Add(totalBlock);

                    // طباعة الواجهة
                    printDialog.PrintVisual(printPanel, $"تقرير_{reportType}_{DateTime.Now:yyyyMMdd_HHmmss}");

                    System.Diagnostics.Debug.WriteLine("✅ تم طباعة التقرير بنجاح");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في طباعة التقرير: {ex.Message}");
                MessageBox.Show($"خطأ في طباعة التقرير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
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