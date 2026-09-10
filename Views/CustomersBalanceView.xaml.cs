using RasidAccountingSystem.Services;
using RasidAccountingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace RasidAccountingSystem.Views
{
    public partial class CustomersBalanceView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private DatabaseService _dbService;
        private bool _isLoading = false;
        private ObservableCollection<CustomerBalanceItem> _customersBalanceList;

        #endregion

        #region الكلاس المساعد (Helper Class)

        public class CustomerBalanceItem
        {
            public int SerialNumber { get; set; }
            public int CustomerID { get; set; }
            public string CustomerCode { get; set; }
            public string CustomerName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public decimal OpeningBalance { get; set; }
            public decimal TotalSales { get; set; }
            public decimal TotalCollections { get; set; }
            public decimal CurrentBalance { get; set; }
            public string BalanceStatus { get; set; }
            public Brush BalanceColor { get; set; }
            public Brush StatusColor { get; set; }
            public Brush StatusBg { get; set; }
            public string FormattedOpeningBalance => OpeningBalance.ToString("N0");
            public string FormattedTotalSales => TotalSales.ToString("N0");
            public string FormattedTotalCollections => TotalCollections.ToString("N0");
            public string FormattedCurrentBalance => CurrentBalance.ToString("N0");
        }

        #endregion

        #region المنشئ (Constructor)

        public CustomersBalanceView()
        {
            try
            {
                InitializeComponent();
                InitializeDatabase();
                InitializeEvents();

                // ✅ رمز العملة بقى بيتقرأ من إعدادات النظام بدل ما يكون ثابت "ر.س" في التصميم
                string currencySymbol = CurrencyHelper.GetCurrencySymbol();
                lblCurrencyOpening.Text = currencySymbol;
                lblCurrencySales.Text = currencySymbol;
                lblCurrencyCollections.Text = currencySymbol;
                lblCurrencyTotal.Text = currencySymbol;

                this.Loaded += async (s, e) => await LoadCustomersBalanceAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال التهيئة (Initialization Methods)

        private void InitializeDatabase()
        {
            try
            {
                _dbService = new DatabaseService();
                _customersBalanceList = new ObservableCollection<CustomerBalanceItem>();
                CustomersDataGrid.ItemsSource = _customersBalanceList;
                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة خدمة قاعدة البيانات بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeDatabase Error: {ex.Message}");
            }
        }

        private void InitializeEvents()
        {
            try
            {
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
                System.Diagnostics.Debug.WriteLine($"InitializeEvents Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال تحديث الرصيد (Balance Update Methods)

        /// <summary>
        /// تحديث رصيد جميع العملاء بناءً على الحركات المسجلة في جدول CustomerTransactions
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه الحركات
        /// تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync وتريجر
        /// trig_customer_transaction_from_sales) - فكان SUM(CreditAmount) لكل الفواتير يساوي
        /// صفر دائمًا، وكل فتح لشاشة "ميزان العملاء" كان يُصفِّر مساهمة الفواتير من رصيد كل
        /// عميل في جدول Customers نفسه. المعادلة الصحيحة الموحَّدة مع باقي أنحاء البرنامج
        /// (كشف الحساب، RecalculateAllCustomerRunningBalances): الرصيد = OpeningBalance +
        /// SUM(DebitAmount للفواتير) - SUM(CreditAmount للتحصيلات)
        /// </summary>
        private async Task UpdateAllCustomersBalance()
        {
            try
            {
                await DatabaseExecutor.ExecuteVoidAsync(_dbService, async (connection) =>
                {
                    string updateSql = @"
                        UPDATE Customers SET 
                            CurrentBalance = COALESCE((
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
                                WHERE CustomerID = Customers.CustomerID
                            ), OpeningBalance)
                        WHERE CustomerID IN (SELECT CustomerID FROM Customers)";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد {rowsAffected} عميل في جدول العملاء باستخدام المعادلة الصحيحة");
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAllCustomersBalance Error: {ex.Message}");
            }
        }

        /// <summary>
        /// تحديث رصيد عميل فردي بناءً على الحركات المسجلة
        /// </summary>
        private async Task UpdateSingleCustomerBalance(int customerId)
        {
            try
            {
                await DatabaseExecutor.ExecuteVoidAsync(_dbService, async (connection) =>
                {
                    string updateSql = @"
                        UPDATE Customers SET 
                            CurrentBalance = COALESCE((
                                SELECT OpeningBalance + COALESCE(SUM(CreditAmount), 0) - COALESCE(SUM(DebitAmount), 0)
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId
                            ), OpeningBalance)
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل {customerId}");
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSingleCustomerBalance Error: {ex.Message}");
            }
        }

        /// <summary>
        /// تحديث رصيد عميل فردي (المعادلة الصحيحة: فواتير = DebitAmount، تحصيلات/مرتجعات = CreditAmount)
        /// ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice' بدل DebitAmount (نفس باج
        /// UpdateAllCustomersBalance أعلاه) فكانت تُصفِّر مساهمة فواتير العميل من رصيده الحالي.
        /// </summary>
        private async Task UpdateSingleCustomerBalanceFixedAsync(int customerId)
        {
            try
            {
                await DatabaseExecutor.ExecuteVoidAsync(_dbService, async (connection) =>
                {
                    string updateSql = @"
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
                            WHERE CustomerID = @customerId
                        ), OpeningBalance)
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل {customerId} باستخدام المعادلة الصحيحة");
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSingleCustomerBalanceFixedAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال التحميل الأساسية (Data Loading Methods)

        private async Task LoadCustomersBalanceAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // ✅ الخطوة 1: تحديث جميع الأرصدة في جدول العملاء أولاً لضمان الدقة
                await UpdateAllCustomersBalance();

                // ✅ الخطوة 2: مسح القائمة القديمة
                _customersBalanceList.Clear();

                // ✅ الخطوة 3: جلب البيانات المحدثة من قاعدة البيانات باستخدام DatabaseExecutor
                var customersData = await DatabaseExecutor.ExecuteNonTransactionAsync<List<CustomerBalanceData>>(_dbService, async (connection) =>
                {
                    var result = new List<CustomerBalanceData>();

                    string sql = @"
                        SELECT 
                            c.CustomerID,
                            c.CustomerCode,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName,
                            COALESCE(c.Phone, '') as Phone,
                            COALESCE(c.Email, '') as Email,
                            COALESCE(c.OpeningBalance, 0) as OpeningBalance,
                            COALESCE((
                                SELECT SUM(DebitAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = c.CustomerID 
                                AND TransactionType = 'Invoice'
                            ), 0) as TotalSales,
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = c.CustomerID 
                                AND TransactionType = 'Receipt'
                            ), 0) as TotalCollections,
                            COALESCE(c.CurrentBalance, 0) as CurrentBalance
                        FROM Customers c
                        ORDER BY c.CustomerCode";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new CustomerBalanceData
                            {
                                CustomerID = reader.GetInt32(0),
                                CustomerCode = reader.GetString(1),
                                CustomerName = reader.GetString(2),
                                Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                OpeningBalance = reader.GetDecimal(5),
                                TotalSales = reader.GetDecimal(6),
                                TotalCollections = reader.GetDecimal(7),
                                CurrentBalance = reader.GetDecimal(8)
                            });
                        }
                    }

                    return result;
                }, CancellationToken.None, 3);

                int serial = 1;
                decimal grandTotalOpeningBalance = 0;
                decimal grandTotalSales = 0;
                decimal grandTotalCollections = 0;
                decimal grandTotalBalance = 0;

                foreach (var data in customersData)
                {
                    // ✅ التصحيح: حساب الرصيد = OpeningBalance + TotalSales - TotalCollections
                    decimal calculatedBalance = data.OpeningBalance + data.TotalSales - data.TotalCollections;

                    if (data.CurrentBalance != calculatedBalance)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ تصحيح رصيد العميل {data.CustomerCode}: كان {data.CurrentBalance} وأصبح {calculatedBalance}");
                        data.CurrentBalance = calculatedBalance;

                        // تحديث الرصيد في قاعدة البيانات
                        await UpdateSingleCustomerBalanceFixedAsync(data.CustomerID);
                    }

                    // تجميع الإجماليات
                    grandTotalOpeningBalance += data.OpeningBalance;
                    grandTotalSales += data.TotalSales;
                    grandTotalCollections += data.TotalCollections;
                    grandTotalBalance += data.CurrentBalance;

                    // تحديد حالة الحساب والألوان
                    string balanceStatus;
                    Brush balanceColor;
                    Brush statusColor;
                    Brush statusBg;

                    if (data.CurrentBalance > 0)
                    {
                        balanceStatus = "مدين (عليه)";
                        balanceColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        statusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        statusBg = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    }
                    else if (data.CurrentBalance < 0)
                    {
                        balanceStatus = "دائن (له)";
                        balanceColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                        statusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                        statusBg = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                    }
                    else
                    {
                        balanceStatus = "متزن";
                        balanceColor = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                        statusColor = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                        statusBg = new SolidColorBrush(Color.FromRgb(219, 234, 254));
                    }

                    _customersBalanceList.Add(new CustomerBalanceItem
                    {
                        SerialNumber = serial++,
                        CustomerID = data.CustomerID,
                        CustomerCode = data.CustomerCode,
                        CustomerName = data.CustomerName,
                        Phone = data.Phone,
                        Email = data.Email,
                        OpeningBalance = data.OpeningBalance,
                        TotalSales = data.TotalSales,
                        TotalCollections = data.TotalCollections,
                        CurrentBalance = data.CurrentBalance,
                        BalanceStatus = balanceStatus,
                        BalanceColor = balanceColor,
                        StatusColor = statusColor,
                        StatusBg = statusBg
                    });
                }

                // تحديث الإجماليات في واجهة المستخدم
                UpdateSummaryLabels(grandTotalOpeningBalance, grandTotalSales, grandTotalCollections, grandTotalBalance);

                if (_customersBalanceList.Count == 0)
                {
                    CustomersDataGrid.Visibility = Visibility.Collapsed;
                    NoDataMessage.Visibility = Visibility.Visible;
                }
                else
                {
                    CustomersDataGrid.Visibility = Visibility.Visible;
                    NoDataMessage.Visibility = Visibility.Collapsed;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_customersBalanceList.Count} عميل");
                System.Diagnostics.Debug.WriteLine($"📊 إجمالي رصيد أول المدة: {grandTotalOpeningBalance:N0}, إجمالي المبيعات: {grandTotalSales:N0}, إجمالي التحصيلات: {grandTotalCollections:N0}, إجمالي الرصيد: {grandTotalBalance:N0}");

                Mouse.OverrideCursor = null;
            }
            catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"LoadCustomersBalanceAsync - Database locked: {ex.Message}");
                await ShowErrorMessageAsync("تعذر تحميل البيانات بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"LoadCustomersBalanceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                await ShowErrorMessageAsync($"خطأ في تحميل البيانات: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadCustomersBalanceAsync();
            await ShowMessageAsync("تم تحديث البيانات بنجاح", "تحديث");
        }

        private void UpdateSummaryLabels(decimal grandTotalOpeningBalance, decimal grandTotalSales, decimal grandTotalCollections, decimal grandTotalBalance)
        {
            try
            {
                if (TotalOpeningBalanceText != null)
                {
                    TotalOpeningBalanceText.Text = grandTotalOpeningBalance.ToString("N0");
                }

                if (TotalSalesText != null)
                {
                    TotalSalesText.Text = grandTotalSales.ToString("N0");
                }

                if (TotalCollectionsText != null)
                {
                    TotalCollectionsText.Text = grandTotalCollections.ToString("N0");
                }

                if (TotalBalanceText != null)
                {
                    TotalBalanceText.Text = grandTotalBalance.ToString("N0");
                    TotalBalanceText.Foreground = grandTotalBalance >= 0
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                        : new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }

                if (RecordsCountText != null)
                {
                    RecordsCountText.Text = $"عدد العملاء: {_customersBalanceList.Count}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSummaryLabels Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال الطباعة (Print Methods)

        private void PrintReport(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customersBalanceList == null || _customersBalanceList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FixedDocument fixedDocument = CreatePrintDocument(_customersBalanceList.ToList());

                if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        printDialog.PrintDocument(fixedDocument.DocumentPaginator, "ميزان العملاء");
                        MessageBox.Show("تم إرسال المستند إلى الطابعة", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("حدث خطأ في إنشاء مستند الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"PrintReport Error: {ex.Message}");
            }
        }

        private FixedDocument CreatePrintDocument(List<CustomerBalanceItem> customers)
        {
            try
            {
                string companyName = GetCompanyName();
                BitmapImage companyLogo = GetCompanyLogo();

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(794, 1123);

                int itemsPerPage = 16;
                int totalItems = customers.Count;
                int pageCount = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var pageItems = customers.Skip(pageIndex * itemsPerPage).Take(itemsPerPage).ToList();

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

                    Grid headerGrid = CreatePrintHeader(companyName, companyLogo);
                    printPanel.Children.Add(headerGrid);

                    Border separator = new Border
                    {
                        Height = 2,
                        Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                        Margin = new Thickness(0, 10, 0, 15),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    printPanel.Children.Add(separator);

                    Grid infoGrid = CreatePrintInfoGrid();
                    printPanel.Children.Add(infoGrid);

                    Grid dataGrid = CreatePrintDataGrid(pageItems, pageIndex, itemsPerPage);
                    printPanel.Children.Add(dataGrid);

                    if (pageIndex == pageCount - 1)
                    {
                        Border summaryBorder = CreatePrintSummaryBorder(customers);
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

                    fixedPage.Children.Add(printPanel);
                    pageContent.Child = fixedPage;
                    fixedDocument.Pages.Add(pageContent);
                }

                return fixedDocument;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreatePrintDocument Error: {ex.Message}");
                return null;
            }
        }

        private Grid CreatePrintHeader(string companyName, BitmapImage logo)
        {
            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 15);
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (logo != null)
            {
                Border logoBorder = new Border
                {
                    Width = 70,
                    Height = 70,
                    CornerRadius = new CornerRadius(35),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(0, 0, 15, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image logoImage = new Image
                {
                    Source = logo,
                    Width = 60,
                    Height = 60,
                    Stretch = Stretch.UniformToFill
                };

                EllipseGeometry clipGeometry = new EllipseGeometry(new Point(30, 30), 30, 30);
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
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            companyInfoPanel.Children.Add(companyNameBlock);

            TextBlock reportTitleBlock = new TextBlock
            {
                Text = "ميزان العملاء",
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

        private Grid CreatePrintInfoGrid()
        {
            Grid infoGrid = new Grid();
            infoGrid.Margin = new Thickness(0, 0, 0, 15);
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel datePanel = new StackPanel();
            datePanel.Children.Add(new TextBlock
            {
                Text = "تاريخ التقرير:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo")
            });
            datePanel.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(datePanel, 0);
            infoGrid.Children.Add(datePanel);

            StackPanel countPanel = new StackPanel();
            countPanel.HorizontalAlignment = HorizontalAlignment.Left;
            countPanel.Children.Add(new TextBlock
            {
                Text = "عدد العملاء:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo")
            });
            countPanel.Children.Add(new TextBlock
            {
                Text = _customersBalanceList.Count.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(countPanel, 1);
            infoGrid.Children.Add(countPanel);

            return infoGrid;
        }

        private Grid CreatePrintDataGrid(List<CustomerBalanceItem> customers, int pageIndex, int itemsPerPage)
        {
            Grid dataGrid = new Grid();
            dataGrid.Margin = new Thickness(0, 0, 0, 20);
            dataGrid.ShowGridLines = true;

            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            string[] headers = { "م", "كود العميل", "اسم العميل", "رصيد أول المدة", "مدين (مبيعات)", "دائن (متحصلات)", "الرصيد الحالي" };
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(0.5)
                };

                TextBlock headerText = new TextBlock
                {
                    Text = headers[i],
                    FontSize = 11,
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
            int startSerial = (pageIndex * itemsPerPage) + 1;

            foreach (var customer in customers)
            {
                dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Border rowBorder = new Border
                {
                    Background = rowIndex % 2 == 0 ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.White
                };
                Grid.SetRow(rowBorder, rowIndex);
                Grid.SetColumnSpan(rowBorder, headers.Length);
                dataGrid.Children.Add(rowBorder);

                AddPrintGridCell(dataGrid, 0, rowIndex, (startSerial + (rowIndex - 1)).ToString(), HorizontalAlignment.Center);
                AddPrintGridCell(dataGrid, 1, rowIndex, customer.CustomerCode, HorizontalAlignment.Center);
                AddPrintGridCell(dataGrid, 2, rowIndex, customer.CustomerName, HorizontalAlignment.Right);
                AddPrintGridCell(dataGrid, 3, rowIndex, customer.FormattedOpeningBalance, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(245, 158, 11)));
                AddPrintGridCell(dataGrid, 4, rowIndex, customer.FormattedTotalSales, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(239, 68, 68)));
                AddPrintGridCell(dataGrid, 5, rowIndex, customer.FormattedTotalCollections, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(16, 185, 129)));
                AddPrintGridCell(dataGrid, 6, rowIndex, customer.FormattedCurrentBalance, HorizontalAlignment.Center, customer.BalanceColor);

                rowIndex++;
            }

            return dataGrid;
        }

        private void AddPrintGridCell(Grid grid, int column, int row, string text, HorizontalAlignment alignment, Brush foreground = null)
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
                FontSize = 10,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = alignment,
                TextWrapping = TextWrapping.Wrap
            };

            if (foreground != null)
            {
                cellText.Foreground = foreground;
            }

            cellBorder.Child = cellText;
            Grid.SetColumn(cellBorder, column);
            Grid.SetRow(cellBorder, row);
            grid.Children.Add(cellBorder);
        }

        private Border CreatePrintSummaryBorder(List<CustomerBalanceItem> customers)
        {
            decimal totalOpeningBalance = customers.Sum(x => x.OpeningBalance);
            decimal totalSales = customers.Sum(x => x.TotalSales);
            decimal totalCollections = customers.Sum(x => x.TotalCollections);
            decimal totalBalance = customers.Sum(x => x.CurrentBalance);

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
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddPrintSummaryCard(summaryGrid, 0, "إجمالي رصيد أول المدة", totalOpeningBalance, new SolidColorBrush(Color.FromRgb(245, 158, 11)));
            AddPrintSummaryCard(summaryGrid, 1, "إجمالي المبيعات (مدين)", totalSales, new SolidColorBrush(Color.FromRgb(239, 68, 68)));
            AddPrintSummaryCard(summaryGrid, 2, "إجمالي المتحصلات (دائن)", totalCollections, new SolidColorBrush(Color.FromRgb(16, 185, 129)));
            AddPrintSummaryCard(summaryGrid, 3, "إجمالي الرصيد", totalBalance, totalBalance >= 0 ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : new SolidColorBrush(Color.FromRgb(16, 185, 129)));

            summaryBorder.Child = summaryGrid;
            return summaryBorder;
        }

        private void AddPrintSummaryCard(Grid grid, int column, string label, decimal value, Brush color)
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
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cardStack.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value.ToString("N0"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            cardStack.Children.Add(valueBlock);

            cardBorder.Child = cardStack;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        #endregion

        #region دوال التصدير إلى Excel (Export Methods)

        private void ExportToExcel(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customersBalanceList == null || _customersBalanceList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"ميزان_العملاء_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("ميزان العملاء");

                        worksheet.Cell(1, 1).Value = "ميزان العملاء";
                        worksheet.Cell(1, 1).Style.Font.Bold = true;
                        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                        worksheet.Range(1, 1, 1, 7).Merge();

                        worksheet.Cell(2, 1).Value = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        worksheet.Range(2, 1, 2, 7).Merge();

                        int startRow = 4;
                        string[] headers = { "م", "كود العميل", "اسم العميل", "رصيد أول المدة", "مدين (مبيعات)", "دائن (متحصلات)", "الرصيد الحالي" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(startRow, i + 1).Value = headers[i];
                            worksheet.Cell(startRow, i + 1).Style.Font.Bold = true;
                            worksheet.Cell(startRow, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(99, 102, 241);
                            worksheet.Cell(startRow, i + 1).Style.Font.FontColor = XLColor.White;
                            worksheet.Cell(startRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int rowIndex = startRow + 1;
                        int serial = 1;
                        decimal totalOpeningBalance = 0;
                        decimal totalSales = 0;
                        decimal totalCollections = 0;
                        decimal totalBalance = 0;

                        foreach (var customer in _customersBalanceList)
                        {
                            worksheet.Cell(rowIndex, 1).Value = serial++;
                            worksheet.Cell(rowIndex, 2).Value = customer.CustomerCode;
                            worksheet.Cell(rowIndex, 3).Value = customer.CustomerName;
                            worksheet.Cell(rowIndex, 4).Value = customer.OpeningBalance;
                            worksheet.Cell(rowIndex, 5).Value = customer.TotalSales;
                            worksheet.Cell(rowIndex, 6).Value = customer.TotalCollections;
                            worksheet.Cell(rowIndex, 7).Value = customer.CurrentBalance;

                            worksheet.Cell(rowIndex, 4).Style.Font.FontColor = XLColor.FromArgb(245, 158, 11);
                            worksheet.Cell(rowIndex, 5).Style.Font.FontColor = XLColor.Red;
                            worksheet.Cell(rowIndex, 6).Style.Font.FontColor = XLColor.Green;

                            totalOpeningBalance += customer.OpeningBalance;
                            totalSales += customer.TotalSales;
                            totalCollections += customer.TotalCollections;
                            totalBalance += customer.CurrentBalance;

                            if (rowIndex % 2 == 0)
                            {
                                worksheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
                            }

                            rowIndex++;
                        }

                        int totalRow = rowIndex + 1;
                        worksheet.Cell(totalRow, 3).Value = "الإجمالي:";
                        worksheet.Cell(totalRow, 3).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        worksheet.Cell(totalRow, 4).Value = totalOpeningBalance;
                        worksheet.Cell(totalRow, 4).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 5).Value = totalSales;
                        worksheet.Cell(totalRow, 5).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 6).Value = totalCollections;
                        worksheet.Cell(totalRow, 6).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 7).Value = totalBalance;
                        worksheet.Cell(totalRow, 7).Style.Font.Bold = true;

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"تم تصدير {_customersBalanceList.Count} عميل بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تصدير Excel: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ExportToExcel Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال مساعدة (Helper Methods)

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

        #region كلاس بيانات مساعد (Helper Data Class)

        private class CustomerBalanceData
        {
            public int CustomerID { get; set; }
            public string CustomerCode { get; set; }
            public string CustomerName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public decimal OpeningBalance { get; set; }
            public decimal TotalSales { get; set; }
            public decimal TotalCollections { get; set; }
            public decimal CurrentBalance { get; set; }
        }

        #endregion
    }
}