using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ClosedXML.Excel;
using Microsoft.Win32;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class ProfitLossView : UserControl
    {
        #region Private Variables

        private readonly DatabaseService _dbService;
        private readonly string _connectionString;
        private bool _isLoading = false;
        private readonly ObservableCollection<ExpenseItem> _expensesList;
        private string _costPriceColumnName = "CostPrice";

        #endregion

        #region Helper Classes

        public class ExpenseItem
        {
            public string ExpenseName { get; set; }
            public decimal Amount { get; set; }
            public string FormattedAmount => CurrencyHelper.FormatAmount(Amount);
        }

        #endregion

        #region Constructor

        public ProfitLossView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _connectionString = _dbService.GetConnectionString();
                _expensesList = new ObservableCollection<ExpenseItem>();

                dgExpenses.ItemsSource = _expensesList;

                DetectCostPriceColumn();

                InitializeEvents();
                SetDefaultDates();

                this.Loaded += async (s, e) => await LoadProfitLossReportAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Initialization Methods

        private void InitializeEvents()
        {
            try
            {
                btnShowReport.Click += async (s, e) => await LoadProfitLossReportAsync();
                btnExportExcel.Click += ExportToExcel;
                btnPrint.Click += PrintReport;
                cmbPeriodType.SelectionChanged += async (s, e) => await OnPeriodTypeChangedAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeEvents Error: {ex.Message}");
            }
        }

        private void SetDefaultDates()
        {
            try
            {
                dtpStartDate.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                dtpEndDate.SelectedDate = DateTime.Now;
                UpdatePeriodDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetDefaultDates Error: {ex.Message}");
            }
        }

        private void UpdatePeriodDisplay()
        {
            try
            {
                string startDate = dtpStartDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "غير محدد";
                string endDate = dtpEndDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "غير محدد";
                lblPeriod.Text = $"للفترة من {startDate} إلى {endDate}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePeriodDisplay Error: {ex.Message}");
            }
        }

        private async Task OnPeriodTypeChangedAsync()
        {
            try
            {
                string periodType = (cmbPeriodType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "شهري";
                DateTime now = DateTime.Now;

                switch (periodType)
                {
                    case "شهري":
                        dtpStartDate.SelectedDate = new DateTime(now.Year, now.Month, 1);
                        dtpEndDate.SelectedDate = now;
                        break;
                    case "ربع سنوي":
                        int quarter = (now.Month - 1) / 3;
                        int quarterStartMonth = (quarter * 3) + 1;
                        dtpStartDate.SelectedDate = new DateTime(now.Year, quarterStartMonth, 1);
                        dtpEndDate.SelectedDate = dtpStartDate.SelectedDate.Value.AddMonths(3).AddDays(-1);
                        if (dtpEndDate.SelectedDate > now) dtpEndDate.SelectedDate = now;
                        break;
                    case "نصف سنوي":
                        if (now.Month <= 6)
                        {
                            dtpStartDate.SelectedDate = new DateTime(now.Year, 1, 1);
                            dtpEndDate.SelectedDate = new DateTime(now.Year, 6, 30);
                        }
                        else
                        {
                            dtpStartDate.SelectedDate = new DateTime(now.Year, 7, 1);
                            dtpEndDate.SelectedDate = new DateTime(now.Year, 12, 31);
                        }
                        break;
                    case "سنوي":
                        dtpStartDate.SelectedDate = new DateTime(now.Year, 1, 1);
                        dtpEndDate.SelectedDate = new DateTime(now.Year, 12, 31);
                        break;
                }

                UpdatePeriodDisplay();
                await LoadProfitLossReportAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnPeriodTypeChangedAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects the correct Cost Price column name in the database
        /// </summary>
        private void DetectCostPriceColumn()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string pragmaSql = "PRAGMA table_info(StoreInventory)";
                    using (var cmd = new SQLiteCommand(pragmaSql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName.Equals("CostPrice", StringComparison.OrdinalIgnoreCase) ||
                                columnName.Equals("UnitCost", StringComparison.OrdinalIgnoreCase) ||
                                columnName.Equals("AverageCost", StringComparison.OrdinalIgnoreCase))
                            {
                                _costPriceColumnName = columnName;
                                System.Diagnostics.Debug.WriteLine($"✅ Found cost price column: {_costPriceColumnName}");
                                return;
                            }
                        }
                    }

                    // If not found in StoreInventory, check Products table
                    pragmaSql = "PRAGMA table_info(Products)";
                    using (var cmd = new SQLiteCommand(pragmaSql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string columnName = reader["name"].ToString();
                            if (columnName.Equals("CostPrice", StringComparison.OrdinalIgnoreCase) ||
                                columnName.Equals("UnitCost", StringComparison.OrdinalIgnoreCase))
                            {
                                _costPriceColumnName = columnName;
                                System.Diagnostics.Debug.WriteLine($"✅ Found cost price column in Products: {_costPriceColumnName}");
                                return;
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"⚠️ Cost price column not found, using default: {_costPriceColumnName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetectCostPriceColumn Error: {ex.Message}");
            }
        }

        #endregion

        #region Core Business Logic (Profit & Loss Calculation)

        private async Task LoadProfitLossReportAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                DateTime startDate = dtpStartDate.SelectedDate ?? DateTime.Now.AddMonths(-1);
                DateTime endDate = dtpEndDate.SelectedDate ?? DateTime.Now;
                UpdatePeriodDisplay();

                // 1. Total Sales Revenue
                decimal totalSales = await GetTotalSalesAsync(startDate, endDate);
                lblTotalSales.Text = CurrencyHelper.FormatAmount(totalSales);

                // 2. Total Sales Discounts
                decimal salesDiscount = await GetSalesDiscountAsync(startDate, endDate);
                lblSalesDiscount.Text = CurrencyHelper.FormatAmount(salesDiscount);

                // 3. Net Sales Revenue
                decimal netSales = totalSales - salesDiscount;
                lblNetSales.Text = CurrencyHelper.FormatAmount(netSales);

                // 4. Cost of Goods Sold (COGS)
                decimal costOfSales = await GetCostOfSalesAsync(startDate, endDate);
                lblCostOfSales.Text = CurrencyHelper.FormatAmount(costOfSales);

                // 5. Gross Profit
                decimal grossProfit = netSales - costOfSales;
                lblGrossProfit.Text = CurrencyHelper.FormatAmount(grossProfit);
                lblGrossProfit.Foreground = grossProfit >= 0 ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));

                // 6. Operating Expenses (Excluding ALL transfers between treasuries)
                await LoadOperatingExpensesAsync(startDate, endDate);
                decimal totalExpenses = _expensesList.Sum(x => x.Amount);
                lblTotalExpenses.Text = CurrencyHelper.FormatAmount(totalExpenses);

                // 7. Net Profit / Net Loss
                decimal netProfit = grossProfit - totalExpenses;
                lblNetProfit.Text = CurrencyHelper.FormatAmount(Math.Abs(netProfit));

                if (netProfit >= 0)
                {
                    lblNetProfitTitle.Text = "صافي الربح";
                    lblNetProfitTitle.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    lblNetProfit.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
                else
                {
                    lblNetProfitTitle.Text = "صافي الخسارة";
                    lblNetProfitTitle.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    lblNetProfit.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }

                System.Diagnostics.Debug.WriteLine($"✅ Profit & Loss Report Loaded: Net Profit/Loss = {netProfit:N2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProfitLossReportAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل التقرير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _isLoading = false;
            }
        }

        /// <summary>
        /// Gets total sales revenue from SalesInvoices
        /// </summary>
        private async Task<decimal> GetTotalSalesAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT COALESCE(SUM(TotalAmount), 0)
                        FROM SalesInvoices 
                        WHERE InvoiceDate BETWEEN @startDate AND @endDate
                        AND IsVoid = 0
                        AND InvoiceType = 'Sales'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                        object result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTotalSalesAsync Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets total discounts from SalesInvoices
        /// </summary>
        private async Task<decimal> GetSalesDiscountAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT COALESCE(SUM(DiscountAmount), 0)
                        FROM SalesInvoices 
                        WHERE InvoiceDate BETWEEN @startDate AND @endDate
                        AND IsVoid = 0
                        AND InvoiceType = 'Sales'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                        object result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSalesDiscountAsync Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Calculates Cost of Goods Sold (COGS)
        /// Formula: SUM(Quantity * CostPrice) from StoreInventory or Products
        /// </summary>
        private async Task<decimal> GetCostOfSalesAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = $@"
                        SELECT COALESCE(SUM(si.Quantity * COALESCE(si2.CostPrice, p.CostPrice, 0)), 0)
                        FROM SalesInvoiceItems si
                        INNER JOIN SalesInvoices s ON si.InvoiceID = s.InvoiceID
                        LEFT JOIN StoreInventory si2 ON si2.ProductID = si.ProductID AND si2.StoreID = s.StoreID
                        LEFT JOIN Products p ON p.ProductID = si.ProductID
                        WHERE s.InvoiceDate BETWEEN @startDate AND @endDate
                        AND s.IsVoid = 0
                        AND s.InvoiceType = 'Sales'";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                        object result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToDecimal(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCostOfSalesAsync Error: {ex.Message}");

                // Fallback: Use UnitPrice * 0.7 (approximate cost)
                try
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        string fallbackSql = @"
                            SELECT COALESCE(SUM(si.Quantity * si.UnitPrice * 0.7), 0)
                            FROM SalesInvoiceItems si
                            INNER JOIN SalesInvoices s ON si.InvoiceID = s.InvoiceID
                            WHERE s.InvoiceDate BETWEEN @startDate AND @endDate
                            AND s.IsVoid = 0
                            AND s.InvoiceType = 'Sales'";

                        using (var cmd = new SQLiteCommand(fallbackSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));
                            object result = await cmd.ExecuteScalarAsync();
                            System.Diagnostics.Debug.WriteLine($"⚠️ Using fallback COGS calculation (70% of sales price)");
                            return result != null ? Convert.ToDecimal(result) : 0;
                        }
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"GetCostOfSalesAsync Fallback Error: {ex2.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// Loads operating expenses from TreasuryTransactions
        /// EXCLUDES:
        /// - PAYMENT_VOUCHER (supplier payments)
        /// - PURCHASE_INVOICE (purchases)
        /// - TRANSFER (transfers between treasuries - CRITICAL)
        /// - TreasuryTransfer (alternative transfer type)
        /// - Any transaction with description containing transfer keywords
        /// </summary>
        private async Task LoadOperatingExpensesAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _expensesList.Clear();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // استعلام يستبعد جميع أنواع التحويلات بين الخزائن
                    string sql = @"
                        SELECT 
                            COALESCE(tt.Description, 'مصروفات متنوعة') as ExpenseName,
                            SUM(tt.Amount) as TotalAmount
                        FROM TreasuryTransactions tt
                        WHERE tt.TransactionType = 'Payment'
                        AND DATE(tt.TransactionDate) BETWEEN @startDate AND @endDate
                        AND (
                            tt.ReferenceType IS NULL 
                            OR (
                                tt.ReferenceType NOT IN ('PAYMENT_VOUCHER', 'PURCHASE_INVOICE', 'TRANSFER', 'TreasuryTransfer')
                                AND (
                                    tt.Description IS NULL 
                                    OR (
                                        tt.Description NOT LIKE '%تحويل%' 
                                        AND tt.Description NOT LIKE '%نقل%'
                                        AND tt.Description NOT LIKE '%Transfer%'
                                        AND tt.Description NOT LIKE '%نقل خزينة%'
                                        AND tt.Description NOT LIKE '%تحويل خزينة%'
                                        AND tt.Description NOT LIKE '%من خزينة%'
                                        AND tt.Description NOT LIKE '%إلى خزينة%'
                                    )
                                )
                            )
                        )
                        GROUP BY COALESCE(tt.Description, 'مصروفات متنوعة')
                        ORDER BY TotalAmount DESC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string name = reader.GetString(0);
                                decimal amount = reader.GetDecimal(1);

                                // تصفية إضافية في الكود للتأكد من عدم ظهور أي تحويل
                                if (amount > 0 &&
                                    !string.IsNullOrWhiteSpace(name) &&
                                    !name.Contains("تحويل") &&
                                    !name.Contains("نقل") &&
                                    !name.Contains("Transfer") &&
                                    !name.Contains("نقل خزينة") &&
                                    !name.Contains("تحويل خزينة") &&
                                    !name.Contains("من خزينة") &&
                                    !name.Contains("إلى خزينة"))
                                {
                                    _expensesList.Add(new ExpenseItem
                                    {
                                        ExpenseName = name,
                                        Amount = amount
                                    });
                                }
                            }
                        }
                    }

                    // جلب المصروفات من العهد (Custody)
                    string custodySql = @"
                        SELECT 
                            COALESCE(ct.ExpenseType, 'مصروفات عهدة') as ExpenseName,
                            SUM(ct.Amount) as TotalAmount
                        FROM CustodyTransactions ct
                        WHERE ct.TransactionType = 'Return'
                        AND DATE(ct.TransactionDate) BETWEEN @startDate AND @endDate
                        GROUP BY COALESCE(ct.ExpenseType, 'مصروفات عهدة')
                        ORDER BY TotalAmount DESC";

                    using (var cmd = new SQLiteCommand(custodySql, connection))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string name = reader.GetString(0);
                                decimal amount = reader.GetDecimal(1);

                                if (amount > 0 && !string.IsNullOrWhiteSpace(name))
                                {
                                    var existing = _expensesList.FirstOrDefault(x => x.ExpenseName == name);
                                    if (existing != null)
                                    {
                                        existing.Amount += amount;
                                    }
                                    else
                                    {
                                        _expensesList.Add(new ExpenseItem
                                        {
                                            ExpenseName = name,
                                            Amount = amount
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Loaded {_expensesList.Count} operating expense items (all transfers excluded).");

                // طباعة قائمة المصروفات للتحقق
                foreach (var expense in _expensesList)
                {
                    System.Diagnostics.Debug.WriteLine($"   - {expense.ExpenseName}: {expense.Amount:N2}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadOperatingExpensesAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region Printing Functionality

        private void PrintReport(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_expensesList == null)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    FixedDocument fixedDocument = CreatePrintDocument();
                    if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                    {
                        printDialog.PrintDocument(fixedDocument.DocumentPaginator, "قائمة الأرباح والخسائر");
                        MessageBox.Show("تم إرسال المستند إلى الطابعة", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("حدث خطأ في إنشاء مستند الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"PrintReport Error: {ex.Message}");
            }
        }

        private FixedDocument CreatePrintDocument()
        {
            try
            {
                string companyName = GetCompanyName();
                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(794, 1123);

                PageContent pageContent = new PageContent();
                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = 794;
                fixedPage.Height = 1123;
                fixedPage.Background = Brushes.White;
                fixedPage.FlowDirection = FlowDirection.RightToLeft;

                StackPanel printPanel = new StackPanel();
                printPanel.Width = 714;
                printPanel.Margin = new Thickness(40);
                printPanel.Background = Brushes.White;
                printPanel.FlowDirection = FlowDirection.RightToLeft;

                // Company Name
                printPanel.Children.Add(new TextBlock
                {
                    Text = companyName,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                });

                // Report Title
                printPanel.Children.Add(new TextBlock
                {
                    Text = "قائمة الأرباح والخسائر",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Period
                printPanel.Children.Add(new TextBlock
                {
                    Text = lblPeriod.Text,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // Separator
                printPanel.Children.Add(new Border
                {
                    Height = 2,
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Margin = new Thickness(0, 0, 0, 20),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });

                // Data Grid
                Grid dataGrid = CreatePrintDataGrid();
                printPanel.Children.Add(dataGrid);

                fixedPage.Children.Add(printPanel);
                pageContent.Child = fixedPage;
                fixedDocument.Pages.Add(pageContent);

                return fixedDocument;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreatePrintDocument Error: {ex.Message}");
                return null;
            }
        }

        private Grid CreatePrintDataGrid()
        {
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 0, 0, 20);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rowIndex = 0;

            // Revenue Section
            AddPrintSectionHeader(grid, ref rowIndex, "أولاً: الإيرادات");
            AddPrintRow(grid, ref rowIndex, "إجمالي المبيعات", GetFormattedValue(lblTotalSales.Text));
            AddPrintRow(grid, ref rowIndex, "(-) خصم المسموحات", GetFormattedValue(lblSalesDiscount.Text));
            AddPrintSubtotal(grid, ref rowIndex, "صافي المبيعات", GetFormattedValue(lblNetSales.Text));

            // Cost of Sales Section
            AddPrintSectionHeader(grid, ref rowIndex, "ثانياً: تكلفة المبيعات");
            AddPrintRow(grid, ref rowIndex, "تكلفة المبيعات", GetFormattedValue(lblCostOfSales.Text));
            AddPrintSubtotal(grid, ref rowIndex, "مجمل الربح", GetFormattedValue(lblGrossProfit.Text));

            // Expenses Section
            AddPrintSectionHeader(grid, ref rowIndex, "ثالثاً: المصروفات التشغيلية");
            foreach (var expense in _expensesList)
            {
                AddPrintRow(grid, ref rowIndex, expense.ExpenseName, expense.FormattedAmount);
            }
            AddPrintSubtotal(grid, ref rowIndex, "إجمالي المصروفات", GetFormattedValue(lblTotalExpenses.Text));

            // Net Profit/Loss
            AddPrintTotal(grid, ref rowIndex, lblNetProfitTitle.Text, GetFormattedValue(lblNetProfit.Text));

            return grid;
        }

        private void AddPrintSectionHeader(Grid grid, ref int rowIndex, string title)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(238, 242, 255)),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 10, 0, 2),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetRow(border, rowIndex);
            Grid.SetColumnSpan(border, 2);

            TextBlock text = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                FontFamily = new FontFamily("Cairo")
            };
            border.Child = text;
            grid.Children.Add(border);

            rowIndex++;
        }

        private void AddPrintRow(Grid grid, ref int rowIndex, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontFamily = new FontFamily("Cairo"),
                Padding = new Thickness(8, 4, 8, 4)
            };
            Grid.SetRow(labelBlock, rowIndex);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 11,
                FontFamily = new FontFamily("Cairo"),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(valueBlock, rowIndex);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            rowIndex++;
        }

        private void AddPrintSubtotal(Grid grid, ref int rowIndex, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetRow(border, rowIndex);
            Grid.SetColumnSpan(border, 2);

            Grid innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo")
            };
            Grid.SetColumn(labelBlock, 0);
            innerGrid.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(valueBlock, 1);
            innerGrid.Children.Add(valueBlock);

            border.Child = innerGrid;
            grid.Children.Add(border);

            rowIndex++;
        }

        private void AddPrintTotal(Grid grid, ref int rowIndex, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 15, 0, 0),
                CornerRadius = new CornerRadius(6)
            };
            Grid.SetRow(border, rowIndex);
            Grid.SetColumnSpan(border, 2);

            Grid innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cairo")
            };
            Grid.SetColumn(labelBlock, 0);
            innerGrid.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(valueBlock, 1);
            innerGrid.Children.Add(valueBlock);

            border.Child = innerGrid;
            grid.Children.Add(border);
        }

        private string GetFormattedValue(string text)
        {
            return string.IsNullOrEmpty(text) ? $"0 {CurrencyHelper.GetCurrencySymbol()}" : text;
        }

        #endregion

        #region Excel Export Functionality

        private void ExportToExcel(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"قائمة_الأرباح_والخسائر_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("قائمة الأرباح والخسائر");

                        // Company Name
                        worksheet.Cell(1, 1).Value = GetCompanyName();
                        worksheet.Cell(1, 1).Style.Font.Bold = true;
                        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                        worksheet.Range(1, 1, 1, 2).Merge();

                        // Report Title
                        worksheet.Cell(2, 1).Value = "قائمة الأرباح والخسائر";
                        worksheet.Cell(2, 1).Style.Font.Bold = true;
                        worksheet.Cell(2, 1).Style.Font.FontSize = 14;
                        worksheet.Range(2, 1, 2, 2).Merge();

                        // Period
                        worksheet.Cell(3, 1).Value = lblPeriod.Text;
                        worksheet.Range(3, 1, 3, 2).Merge();

                        int startRow = 5;

                        // Revenue Section
                        worksheet.Cell(startRow, 1).Value = "أولاً: الإيرادات";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(238, 242, 255);
                        worksheet.Range(startRow, 1, startRow, 2).Merge();

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "إجمالي المبيعات";
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblTotalSales.Text);
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Green;

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "(-) خصم المسموحات";
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblSalesDiscount.Text);
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Red;

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "صافي المبيعات";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblNetSales.Text);
                        worksheet.Cell(startRow, 2).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Green;

                        startRow += 2;

                        // Cost of Sales Section
                        worksheet.Cell(startRow, 1).Value = "ثانياً: تكلفة المبيعات";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(238, 242, 255);
                        worksheet.Range(startRow, 1, startRow, 2).Merge();

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "تكلفة المبيعات";
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblCostOfSales.Text);
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Red;

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "مجمل الربح";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblGrossProfit.Text);
                        worksheet.Cell(startRow, 2).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Blue;

                        startRow += 2;

                        // Expenses Section
                        worksheet.Cell(startRow, 1).Value = "ثالثاً: المصروفات التشغيلية";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(238, 242, 255);
                        worksheet.Range(startRow, 1, startRow, 2).Merge();

                        startRow++;
                        foreach (var expense in _expensesList)
                        {
                            worksheet.Cell(startRow, 1).Value = expense.ExpenseName;
                            worksheet.Cell(startRow, 2).Value = expense.Amount;
                            worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Red;
                            startRow++;
                        }

                        startRow++;
                        worksheet.Cell(startRow, 1).Value = "إجمالي المصروفات";
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblTotalExpenses.Text);
                        worksheet.Cell(startRow, 2).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.Red;

                        startRow += 2;

                        // Net Profit/Loss Section
                        worksheet.Cell(startRow, 1).Value = lblNetProfitTitle.Text;
                        worksheet.Cell(startRow, 1).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(99, 102, 241);
                        worksheet.Cell(startRow, 1).Style.Font.FontColor = XLColor.White;
                        worksheet.Cell(startRow, 2).Value = ExtractNumericValue(lblNetProfit.Text);
                        worksheet.Cell(startRow, 2).Style.Font.Bold = true;
                        worksheet.Cell(startRow, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(99, 102, 241);
                        worksheet.Cell(startRow, 2).Style.Font.FontColor = XLColor.White;
                        worksheet.Range(startRow, 1, startRow, 2).Merge();

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show("تم تصدير التقرير إلى Excel بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تصدير Excel: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ExportToExcel Error: {ex.Message}");
            }
        }

        private decimal ExtractNumericValue(string formattedValue)
        {
            if (string.IsNullOrEmpty(formattedValue)) return 0;
            string numericPart = System.Text.RegularExpressions.Regex.Replace(formattedValue, "[^0-9.-]", "");
            if (decimal.TryParse(numericPart, out decimal result))
                return result;
            return 0;
        }

        #endregion

        #region Helper Methods

        private string GetCompanyName()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT CompanyName FROM CompanySettings LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "النظام المحاسبي المتكامل";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCompanyName Error: {ex.Message}");
                return "النظام المحاسبي المتكامل";
            }
        }

        #endregion
    }
}