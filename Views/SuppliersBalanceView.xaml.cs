using System;
using RasidAccountingSystem.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Microsoft.Win32;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class SuppliersBalanceView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private DatabaseService _dbService;
        private bool _isLoading = false;
        private ObservableCollection<SupplierBalanceItem> _suppliersBalanceList;
        private CultureInfo _englishCulture;

        #endregion

        #region الكلاس المساعد (Helper Class) - مع خصائص منسقة

        public class SupplierBalanceItem
        {
            public int SerialNumber { get; set; }
            public int SupplierID { get; set; }
            public string SupplierCode { get; set; }
            public string SupplierName { get; set; }
            public decimal OpeningBalance { get; set; }
            public decimal TotalPurchases { get; set; }
            public decimal TotalPayments { get; set; }
            public decimal CurrentBalance { get; set; }
            public Brush BalanceColor { get; set; }

            // خصائص منسقة لعرض الأرقام بالإنجليزية
            public string FormattedOpeningBalance { get; set; }
            public string FormattedTotalPurchases { get; set; }
            public string FormattedTotalPayments { get; set; }
            public string FormattedCurrentBalance { get; set; }
        }

        #endregion

        #region المنشئ (Constructor)

        public SuppliersBalanceView()
        {
            try
            {
                InitializeComponent();
                InitializeCulture();
                InitializeDatabase();
                InitializeEvents();

                // ✅ رمز العملة بقى بيتقرأ من إعدادات النظام بدل ما يكون ثابت "ر.س" في التصميم
                string currencySymbol = CurrencyHelper.GetCurrencySymbol();
                lblCurrencyOpening.Text = currencySymbol;
                lblCurrencyPurchases.Text = currencySymbol;
                lblCurrencyPayments.Text = currencySymbol;
                lblCurrencyTotal.Text = currencySymbol;

                this.Loaded += async (s, e) => await LoadSuppliersBalanceAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال التهيئة (Initialization Methods)

        private void InitializeCulture()
        {
            try
            {
                _englishCulture = new CultureInfo("en-US");
                _englishCulture.NumberFormat.NumberDecimalSeparator = ".";
                _englishCulture.NumberFormat.NumberGroupSeparator = ",";
                _englishCulture.NumberFormat.CurrencyDecimalSeparator = ".";
                _englishCulture.NumberFormat.CurrencyGroupSeparator = ",";
                System.Diagnostics.Debug.WriteLine("✅ تم تهيئة الثقافة الإنجليزية بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeCulture Error: {ex.Message}");
                _englishCulture = CultureInfo.InvariantCulture;
            }
        }

        private void InitializeDatabase()
        {
            try
            {
                _dbService = new DatabaseService();
                _suppliersBalanceList = new ObservableCollection<SupplierBalanceItem>();
                SuppliersDataGrid.ItemsSource = _suppliersBalanceList;
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

        #region دوال التحميل الأساسية (Data Loading Methods)

        private async Task LoadSuppliersBalanceAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _suppliersBalanceList.Clear();

                using (var connection = _dbService.GetConnection())
                {
                    await connection.OpenAsync();

                    // ✅ المعادلة الصحيحة: المدفوعات تستخدم DebitAmount
                    string sql = @"
                SELECT 
                    s.SupplierID,
                    s.SupplierCode,
                    COALESCE(s.SupplierNameAr, s.SupplierName, '') as SupplierName,
                    COALESCE(s.OpeningBalance, 0) as OpeningBalance,
                    COALESCE((
                        SELECT SUM(CreditAmount) 
                        FROM SupplierTransactions 
                        WHERE SupplierID = s.SupplierID 
                        AND TransactionType = 'Purchase'
                    ), 0) as TotalPurchases,
                    COALESCE((
                        SELECT SUM(DebitAmount) 
                        FROM SupplierTransactions 
                        WHERE SupplierID = s.SupplierID 
                        AND TransactionType = 'Payment'
                    ), 0) as TotalPayments  -- ✅ تم التعديل: DebitAmount بدلاً من CreditAmount
                FROM Suppliers s
                ORDER BY s.SupplierCode";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        int serial = 1;
                        decimal grandTotalOpeningBalance = 0;
                        decimal grandTotalPurchases = 0;
                        decimal grandTotalPayments = 0;
                        decimal grandTotalBalance = 0;

                        while (await reader.ReadAsync())
                        {
                            int supplierId = SafeToInt(reader["SupplierID"]);
                            string supplierCode = reader["SupplierCode"]?.ToString() ?? "";
                            string supplierName = reader["SupplierName"]?.ToString() ?? "";
                            decimal openingBalance = SafeToDecimal(reader["OpeningBalance"]);
                            decimal totalPurchases = SafeToDecimal(reader["TotalPurchases"]);
                            decimal totalPayments = SafeToDecimal(reader["TotalPayments"]);

                            // ✅ الرصيد الحالي = الرصيد الافتتاحي + المشتريات - المدفوعات
                            decimal currentBalance = (openingBalance + totalPurchases) - totalPayments;

                            grandTotalOpeningBalance += openingBalance;
                            grandTotalPurchases += totalPurchases;
                            grandTotalPayments += totalPayments;
                            grandTotalBalance += currentBalance;

                            string formattedOpeningBalance = openingBalance.ToString("N0", _englishCulture);
                            string formattedTotalPurchases = totalPurchases.ToString("N0", _englishCulture);
                            string formattedTotalPayments = totalPayments.ToString("N0", _englishCulture);
                            string formattedCurrentBalance = currentBalance.ToString("N0", _englishCulture);

                            Brush balanceColor;
                            if (currentBalance > 0)
                            {
                                balanceColor = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // أحمر للدائن (للمورد)
                            }
                            else if (currentBalance < 0)
                            {
                                balanceColor = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // أخضر للمدين
                            }
                            else
                            {
                                balanceColor = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                            }

                            _suppliersBalanceList.Add(new SupplierBalanceItem
                            {
                                SerialNumber = serial++,
                                SupplierID = supplierId,
                                SupplierCode = supplierCode,
                                SupplierName = supplierName,
                                OpeningBalance = openingBalance,
                                TotalPurchases = totalPurchases,
                                TotalPayments = totalPayments,
                                CurrentBalance = currentBalance,
                                BalanceColor = balanceColor,
                                FormattedOpeningBalance = formattedOpeningBalance,
                                FormattedTotalPurchases = formattedTotalPurchases,
                                FormattedTotalPayments = formattedTotalPayments,
                                FormattedCurrentBalance = formattedCurrentBalance
                            });
                        }

                        UpdateSummaryLabels(grandTotalOpeningBalance, grandTotalPurchases, grandTotalPayments, grandTotalBalance);

                        if (_suppliersBalanceList.Count == 0)
                        {
                            SuppliersDataGrid.Visibility = Visibility.Collapsed;
                            NoDataMessage.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            SuppliersDataGrid.Visibility = Visibility.Visible;
                            NoDataMessage.Visibility = Visibility.Collapsed;
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_suppliersBalanceList.Count} مورد");
                        System.Diagnostics.Debug.WriteLine($"📊 إجمالي المشتريات: {grandTotalPurchases}, إجمالي المدفوعات: {grandTotalPayments}");
                    }
                }

                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"LoadSuppliersBalanceAsync Error: {ex.Message}");
                await ShowErrorMessageAsync($"خطأ في تحميل البيانات: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadSuppliersBalanceAsync();
            await ShowMessageAsync("تم تحديث البيانات بنجاح", "تحديث");
        }

        private void UpdateSummaryLabels(decimal grandTotalOpeningBalance, decimal grandTotalPurchases, decimal grandTotalPayments, decimal grandTotalBalance)
        {
            try
            {
                if (TotalOpeningBalanceText != null)
                {
                    TotalOpeningBalanceText.Text = grandTotalOpeningBalance.ToString("N0", _englishCulture);
                    TotalOpeningBalanceText.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                }

                if (TotalPurchasesText != null)
                {
                    TotalPurchasesText.Text = grandTotalPurchases.ToString("N0", _englishCulture);
                    TotalPurchasesText.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                }

                if (TotalPaymentsText != null)
                {
                    TotalPaymentsText.Text = grandTotalPayments.ToString("N0", _englishCulture);
                    TotalPaymentsText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }

                if (TotalBalanceText != null)
                {
                    TotalBalanceText.Text = grandTotalBalance.ToString("N0", _englishCulture);

                    if (grandTotalBalance > 0)
                    {
                        TotalBalanceText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    }
                    else if (grandTotalBalance < 0)
                    {
                        TotalBalanceText.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                    }
                    else
                    {
                        TotalBalanceText.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    }
                }

                if (RecordsCountText != null)
                {
                    RecordsCountText.Text = $"عدد الموردين: {_suppliersBalanceList.Count}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSummaryLabels Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال تنسيق الأرقام (Number Formatting Methods)

        private string FormatNumber(decimal number)
        {
            return number.ToString("N2", _englishCulture);
        }

        private string FormatNumberNoDecimal(decimal number)
        {
            return number.ToString("N0", _englishCulture);
        }

        #endregion

        #region دوال الطباعة (Print Methods)

        private void PrintReport(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_suppliersBalanceList == null || _suppliersBalanceList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FixedDocument fixedDocument = CreatePrintDocument(_suppliersBalanceList.ToList());

                if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                {
                    PrintPreviewWindow previewWindow = new PrintPreviewWindow(fixedDocument);
                    previewWindow.Owner = Window.GetWindow(this);
                    previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    previewWindow.ShowDialog();
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

        private FixedDocument CreatePrintDocument(List<SupplierBalanceItem> suppliers)
        {
            try
            {
                string companyName = GetCompanyName();
                BitmapImage companyLogo = GetCompanyLogo();

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(794, 1123);

                int itemsPerPage = 16;
                int totalItems = suppliers.Count;
                int pageCount = (int)Math.Ceiling((double)totalItems / itemsPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var pageItems = suppliers.Skip(pageIndex * itemsPerPage).Take(itemsPerPage).ToList();

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
                        Background = new SolidColorBrush(Color.FromRgb(0, 71, 255)),
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
                        Border summaryBorder = CreatePrintSummaryBorder(suppliers);
                        printPanel.Children.Add(summaryBorder);
                    }

                    TextBlock pageNumberBlock = new TextBlock
                    {
                        Text = $"صفحة {pageIndex + 1} من {pageCount}",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                        FontFamily = new FontFamily("Segoe UI"),
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
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 71, 255)),
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
                Foreground = new SolidColorBrush(Color.FromRgb(0, 71, 255)),
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            companyInfoPanel.Children.Add(companyNameBlock);

            TextBlock reportTitleBlock = new TextBlock
            {
                Text = "ميزان الموردين",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Segoe UI"),
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
                FontFamily = new FontFamily("Segoe UI")
            });
            datePanel.Children.Add(new TextBlock
            {
                Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(datePanel, 0);
            infoGrid.Children.Add(datePanel);

            StackPanel countPanel = new StackPanel();
            countPanel.HorizontalAlignment = HorizontalAlignment.Left;
            countPanel.Children.Add(new TextBlock
            {
                Text = "عدد الموردين:",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Segoe UI")
            });
            countPanel.Children.Add(new TextBlock
            {
                Text = _suppliersBalanceList.Count.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(countPanel, 1);
            infoGrid.Children.Add(countPanel);

            return infoGrid;
        }

        private Grid CreatePrintDataGrid(List<SupplierBalanceItem> suppliers, int pageIndex, int itemsPerPage)
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

            string[] headers = { "م", "كود المورد", "اسم المورد", "رصيد أول المدة", "دائن (مشتريات)", "مدين (مدفوعات)", "الرصيد الحالي" };
            for (int i = 0; i < headers.Length; i++)
            {
                Border headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 71, 255)),
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
                    FontFamily = new FontFamily("Segoe UI")
                };
                headerBorder.Child = headerText;

                Grid.SetColumn(headerBorder, i);
                dataGrid.Children.Add(headerBorder);
            }

            int rowIndex = 1;
            int startSerial = (pageIndex * itemsPerPage) + 1;

            foreach (var supplier in suppliers)
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
                AddPrintGridCell(dataGrid, 1, rowIndex, supplier.SupplierCode, HorizontalAlignment.Center);
                AddPrintGridCell(dataGrid, 2, rowIndex, supplier.SupplierName, HorizontalAlignment.Center);
                AddPrintGridCell(dataGrid, 3, rowIndex, supplier.FormattedOpeningBalance, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(243, 156, 18)));
                AddPrintGridCell(dataGrid, 4, rowIndex, supplier.FormattedTotalPurchases, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(39, 174, 96)));
                AddPrintGridCell(dataGrid, 5, rowIndex, supplier.FormattedTotalPayments, HorizontalAlignment.Center, new SolidColorBrush(Color.FromRgb(231, 76, 60)));
                AddPrintGridCell(dataGrid, 6, rowIndex, supplier.FormattedCurrentBalance, HorizontalAlignment.Center, supplier.BalanceColor);

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
                FontFamily = new FontFamily("Segoe UI"),
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

        private Border CreatePrintSummaryBorder(List<SupplierBalanceItem> suppliers)
        {
            decimal totalOpeningBalance = suppliers.Sum(x => x.OpeningBalance);
            decimal totalPurchases = suppliers.Sum(x => x.TotalPurchases);
            decimal totalPayments = suppliers.Sum(x => x.TotalPayments);
            decimal totalBalance = suppliers.Sum(x => x.CurrentBalance);

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

            AddPrintSummaryCard(summaryGrid, 0, "إجمالي رصيد أول المدة", totalOpeningBalance, new SolidColorBrush(Color.FromRgb(243, 156, 18)));
            AddPrintSummaryCard(summaryGrid, 1, "إجمالي المشتريات (دائن)", totalPurchases, new SolidColorBrush(Color.FromRgb(39, 174, 96)));
            AddPrintSummaryCard(summaryGrid, 2, "إجمالي المدفوعات (مدين)", totalPayments, new SolidColorBrush(Color.FromRgb(231, 76, 60)));

            Brush balanceColor = totalBalance >= 0 ? new SolidColorBrush(Color.FromRgb(39, 174, 96)) : new SolidColorBrush(Color.FromRgb(231, 76, 60));
            AddPrintSummaryCard(summaryGrid, 3, "إجمالي الرصيد", totalBalance, balanceColor);

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
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cardStack.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = FormatNumberNoDecimal(value),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                FontFamily = new FontFamily("Segoe UI"),
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
                if (_suppliersBalanceList == null || _suppliersBalanceList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"ميزان_الموردين_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("ميزان الموردين");

                        worksheet.Cell(1, 1).Value = "ميزان الموردين";
                        worksheet.Cell(1, 1).Style.Font.Bold = true;
                        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                        worksheet.Range(1, 1, 1, 7).Merge();

                        worksheet.Cell(2, 1).Value = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        worksheet.Range(2, 1, 2, 7).Merge();

                        int startRow = 4;
                        string[] headers = { "م", "كود المورد", "اسم المورد", "رصيد أول المدة", "دائن (مشتريات)", "مدين (مدفوعات)", "الرصيد الحالي" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(startRow, i + 1).Value = headers[i];
                            worksheet.Cell(startRow, i + 1).Style.Font.Bold = true;
                            worksheet.Cell(startRow, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0, 71, 255);
                            worksheet.Cell(startRow, i + 1).Style.Font.FontColor = XLColor.White;
                            worksheet.Cell(startRow, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int rowIndex = startRow + 1;
                        int serial = 1;
                        decimal totalOpeningBalance = 0;
                        decimal totalPurchases = 0;
                        decimal totalPayments = 0;
                        decimal totalBalance = 0;

                        foreach (var supplier in _suppliersBalanceList)
                        {
                            worksheet.Cell(rowIndex, 1).Value = serial++;
                            worksheet.Cell(rowIndex, 2).Value = supplier.SupplierCode;
                            worksheet.Cell(rowIndex, 3).Value = supplier.SupplierName;
                            worksheet.Cell(rowIndex, 4).Value = supplier.OpeningBalance;
                            worksheet.Cell(rowIndex, 5).Value = supplier.TotalPurchases;
                            worksheet.Cell(rowIndex, 6).Value = supplier.TotalPayments;
                            worksheet.Cell(rowIndex, 7).Value = supplier.CurrentBalance;

                            worksheet.Cell(rowIndex, 4).Style.NumberFormat.Format = "#,##0";
                            worksheet.Cell(rowIndex, 5).Style.NumberFormat.Format = "#,##0";
                            worksheet.Cell(rowIndex, 6).Style.NumberFormat.Format = "#,##0";
                            worksheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "#,##0";

                            worksheet.Cell(rowIndex, 4).Style.Font.FontColor = XLColor.FromArgb(243, 156, 18);
                            worksheet.Cell(rowIndex, 5).Style.Font.FontColor = XLColor.Green;
                            worksheet.Cell(rowIndex, 6).Style.Font.FontColor = XLColor.Red;

                            if (supplier.CurrentBalance > 0)
                            {
                                worksheet.Cell(rowIndex, 7).Style.Font.FontColor = XLColor.Red;
                            }
                            else if (supplier.CurrentBalance < 0)
                            {
                                worksheet.Cell(rowIndex, 7).Style.Font.FontColor = XLColor.Green;
                            }

                            totalOpeningBalance += supplier.OpeningBalance;
                            totalPurchases += supplier.TotalPurchases;
                            totalPayments += supplier.TotalPayments;
                            totalBalance += supplier.CurrentBalance;

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
                        worksheet.Cell(totalRow, 4).Style.Font.FontColor = XLColor.FromArgb(243, 156, 18);
                        worksheet.Cell(totalRow, 4).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(totalRow, 5).Value = totalPurchases;
                        worksheet.Cell(totalRow, 5).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 5).Style.Font.FontColor = XLColor.Green;
                        worksheet.Cell(totalRow, 5).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(totalRow, 6).Value = totalPayments;
                        worksheet.Cell(totalRow, 6).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 6).Style.Font.FontColor = XLColor.Red;
                        worksheet.Cell(totalRow, 6).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(totalRow, 7).Value = totalBalance;
                        worksheet.Cell(totalRow, 7).Style.Font.Bold = true;
                        worksheet.Cell(totalRow, 7).Style.NumberFormat.Format = "#,##0";

                        if (totalBalance > 0)
                        {
                            worksheet.Cell(totalRow, 7).Style.Font.FontColor = XLColor.Red;
                        }
                        else if (totalBalance < 0)
                        {
                            worksheet.Cell(totalRow, 7).Style.Font.FontColor = XLColor.Green;
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"تم تصدير {_suppliersBalanceList.Count} مورد بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private int SafeToInt(object value, int defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            try { return Convert.ToInt32(value); }
            catch { return defaultValue; }
        }

        private decimal SafeToDecimal(object value, decimal defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            try { return Convert.ToDecimal(value); }
            catch { return defaultValue; }
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