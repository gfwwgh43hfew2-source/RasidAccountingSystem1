using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class ItemCardView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _databaseService;
        private ObservableCollection<TransactionItem> _transactionsList;
        private int _selectedProductId = 0;
        private int _selectedStoreId = 0;

        #endregion

        #region كلاس حركة الصنف

        public class TransactionItem : INotifyPropertyChanged
        {
            private int _serialNumber;
            private DateTime _transactionDate;
            private string _transactionType;
            private string _referenceNumber;
            private decimal _quantity;
            private decimal _unitPrice;
            private decimal _totalAmount;
            private decimal _quantityBefore;
            private decimal _quantityAfter;
            private string _description;
            private string _storeName;

            public int SerialNumber
            {
                get => _serialNumber;
                set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
            }

            public DateTime TransactionDate
            {
                get => _transactionDate;
                set { _transactionDate = value; OnPropertyChanged(nameof(TransactionDate)); }
            }

            public string TransactionType
            {
                get => _transactionType;
                set
                {
                    _transactionType = value;
                    OnPropertyChanged(nameof(TransactionType));
                    OnPropertyChanged(nameof(TransactionTypeColor));
                }
            }

            public string ReferenceNumber
            {
                get => _referenceNumber;
                set { _referenceNumber = value; OnPropertyChanged(nameof(ReferenceNumber)); }
            }

            public decimal Quantity
            {
                get => _quantity;
                set { _quantity = value; OnPropertyChanged(nameof(Quantity)); }
            }

            public decimal UnitPrice
            {
                get => _unitPrice;
                set { _unitPrice = value; OnPropertyChanged(nameof(UnitPrice)); }
            }

            public decimal TotalAmount
            {
                get => _totalAmount;
                set { _totalAmount = value; OnPropertyChanged(nameof(TotalAmount)); }
            }

            public decimal QuantityBefore
            {
                get => _quantityBefore;
                set { _quantityBefore = value; OnPropertyChanged(nameof(QuantityBefore)); }
            }

            public decimal QuantityAfter
            {
                get => _quantityAfter;
                set { _quantityAfter = value; OnPropertyChanged(nameof(QuantityAfter)); }
            }

            public string Description
            {
                get => _description;
                set { _description = value; OnPropertyChanged(nameof(Description)); }
            }

            public string StoreName
            {
                get => _storeName;
                set { _storeName = value; OnPropertyChanged(nameof(StoreName)); }
            }

            public SolidColorBrush TransactionTypeColor
            {
                get
                {
                    switch (TransactionType)
                    {
                        case "مشتريات":
                            return new SolidColorBrush(Color.FromRgb(5, 150, 105));
                        case "مبيعات":
                            return new SolidColorBrush(Color.FromRgb(220, 38, 38));
                        case "مرتجع":
                            return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                        case "تحويل":
                            return new SolidColorBrush(Color.FromRgb(37, 99, 235));
                        case "تسوية":
                            return new SolidColorBrush(Color.FromRgb(107, 114, 128));
                        default:
                            return new SolidColorBrush(Color.FromRgb(107, 114, 128));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region كلاس المنتج البسيط

        public class ProductSimple
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public override string ToString() => $"{Code} - {Name}";
        }

        #endregion

        #region كلاس المخزن البسيط

        public class StoreSimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        #endregion

        #region المنشئ

        public ItemCardView()
        {
            try
            {
                InitializeComponent();
                InitializeDatabase();
                InitializeEvents();
                Loaded += async (s, e) => await InitializeFormAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال التهيئة

        private void InitializeDatabase()
        {
            try
            {
                _databaseService = new DatabaseService();
                _transactionsList = new ObservableCollection<TransactionItem>();
                dgTransactions.ItemsSource = _transactionsList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تهيئة قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"InitializeDatabase Error: {ex.Message}");
            }
        }

        private void InitializeEvents()
        {
            try
            {
                btnSearch.Click += async (s, e) => await LoadTransactionsAsync();
                btnShow.Click += async (s, e) => await LoadTransactionsAsync();
                btnRefresh.Click += async (s, e) => await RefreshDataAsync();
                btnPrint.Click += async (s, e) => await PrintReportAsync();
                cmbProducts.SelectionChanged += async (s, e) => await OnProductSelectionChanged();
                cmbStores.SelectionChanged += async (s, e) => await OnStoreSelectionChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeEvents Error: {ex.Message}");
            }
        }

        private async Task InitializeFormAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                await LoadProductsAsync();
                await LoadStoresAsync();

                // ✅ إصلاح: لا نضع تاريخاً افتراضياً حتى يرى المستخدم كل الحركات من أول استخدام
                // (يظل بإمكان المستخدم اختيار فترة معينة يدوياً من حقلي التاريخ عند الحاجة)
                dpFromDate.SelectedDate = null;
                dpToDate.SelectedDate = null;

                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تهيئة النموذج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"InitializeFormAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال تحميل البيانات

        private async Task LoadProductsAsync()
        {
            try
            {
                var productsList = new ObservableCollection<ProductSimple>();
                var products = await _databaseService.GetProductsAsync();

                foreach (var product in products)
                {
                    productsList.Add(new ProductSimple
                    {
                        Id = product.ProductID,
                        Code = product.ProductCode,
                        Name = product.ProductNameAr
                    });
                }

                cmbProducts.ItemsSource = productsList;
                cmbProducts.SelectedItem = null;

                System.Diagnostics.Debug.WriteLine($"تم تحميل {productsList.Count} منتج");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProductsAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل المنتجات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadStoresAsync()
        {
            try
            {
                var storesList = new ObservableCollection<StoreSimple>();
                storesList.Add(new StoreSimple { Id = 0, Name = "-- جميع المخازن --" });

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();
                    string sql = "SELECT StoreID, StoreNameAr FROM Stores WHERE IsActive = 1 ORDER BY StoreNameAr";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            storesList.Add(new StoreSimple
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                cmbStores.ItemsSource = storesList;

                if (storesList.Count > 0)
                {
                    cmbStores.SelectedItem = storesList[0];
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {storesList.Count} مخزن");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadStoresAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnProductSelectionChanged()
        {
            if (cmbProducts.SelectedItem != null)
            {
                var selectedProduct = (ProductSimple)cmbProducts.SelectedItem;
                _selectedProductId = selectedProduct.Id;
                lblProductName.Text = selectedProduct.Name;
                lblProductCode.Text = selectedProduct.Code;

                await LoadCurrentBalance();
            }
            else
            {
                _selectedProductId = 0;
                lblProductName.Text = "--";
                lblProductCode.Text = "--";
                lblCurrentBalance.Text = "0";
                lblTotalValue.Text = "0.00";
            }
        }

        private async Task OnStoreSelectionChanged()
        {
            if (cmbStores.SelectedItem != null)
            {
                var selectedStore = (StoreSimple)cmbStores.SelectedItem;
                _selectedStoreId = selectedStore.Id;
                lblStoreName.Text = selectedStore.Name;

                await LoadCurrentBalance();
            }
            else
            {
                _selectedStoreId = 0;
                lblStoreName.Text = "--";
                lblCurrentBalance.Text = "0";
                lblTotalValue.Text = "0.00";
            }
        }

        private async Task LoadCurrentBalance()
        {
            try
            {
                if (_selectedProductId == 0)
                {
                    return;
                }

                decimal currentBalance = 0;
                decimal costPrice = 0;

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT SUM(Quantity) as TotalQuantity, AVG(CostPrice) as AvgCostPrice
                        FROM StoreInventory 
                        WHERE ProductID = @productId";

                    if (_selectedStoreId > 0)
                    {
                        sql += " AND StoreID = @storeId";
                    }

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@productId", _selectedProductId);
                        if (_selectedStoreId > 0)
                        {
                            cmd.Parameters.AddWithValue("@storeId", _selectedStoreId);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                currentBalance = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                                costPrice = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            }
                        }
                    }
                }

                lblCurrentBalance.Text = currentBalance.ToString("N0");
                lblTotalValue.Text = (currentBalance * costPrice).ToString("N2");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentBalance Error: {ex.Message}");
            }
        }

        private async Task LoadTransactionsAsync()
        {
            try
            {
                if (_selectedProductId == 0)
                {
                    MessageBox.Show("الرجاء اختيار صنف أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;
                _transactionsList.Clear();

                // ✅ إصلاح: التاريخ اختياري تماماً الآن. لو الحقلين فاضيين، تظهر كل حركات الصنف
                // بدون أي قيد على التاريخ. ولو المستخدم حدد تاريخ (من / إلى / الاثنين)، يُطبَّق فقط ما تم تحديده.
                DateTime? fromDate = dpFromDate.SelectedDate;
                DateTime? toDate = dpToDate.SelectedDate?.AddDays(1).AddSeconds(-1);

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            it.TransactionDate,
                            it.TransactionType,
                            it.ReferenceNumber,
                            it.Quantity,
                            it.UnitPrice,
                            it.TotalAmount,
                            it.QuantityBefore,
                            it.QuantityAfter,
                            it.Description,
                            s.StoreNameAr
                        FROM InventoryTransactions it
                        INNER JOIN Stores s ON it.StoreID = s.StoreID
                        WHERE it.ProductID = @productId";

                    if (fromDate.HasValue)
                    {
                        sql += " AND it.TransactionDate >= @fromDate";
                    }

                    if (toDate.HasValue)
                    {
                        sql += " AND it.TransactionDate <= @toDate";
                    }

                    if (_selectedStoreId > 0)
                    {
                        sql += " AND it.StoreID = @storeId";
                    }

                    sql += " ORDER BY it.TransactionDate ASC, it.TransactionID ASC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@productId", _selectedProductId);

                        if (fromDate.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@fromDate", fromDate.Value.ToString("yyyy-MM-dd"));
                        }

                        if (toDate.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@toDate", toDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        if (_selectedStoreId > 0)
                        {
                            cmd.Parameters.AddWithValue("@storeId", _selectedStoreId);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int serial = 1;
                            decimal totalIn = 0;
                            decimal totalOut = 0;

                            while (await reader.ReadAsync())
                            {
                                string transactionType = reader.GetString(1);
                                decimal quantity = reader.GetDecimal(3);

                                if (transactionType == "Purchase")
                                {
                                    totalIn += quantity;
                                }
                                else if (transactionType == "Sale")
                                {
                                    totalOut += quantity;
                                }

                                _transactionsList.Add(new TransactionItem
                                {
                                    SerialNumber = serial++,
                                    TransactionDate = reader.GetDateTime(0),
                                    TransactionType = ConvertTransactionType(transactionType),
                                    ReferenceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Quantity = quantity,
                                    UnitPrice = reader.GetDecimal(4),
                                    TotalAmount = reader.GetDecimal(5),
                                    QuantityBefore = reader.GetDecimal(6),
                                    QuantityAfter = reader.GetDecimal(7),
                                    Description = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    StoreName = reader.GetString(9)
                                });
                            }

                            lblTotalIn.Text = totalIn.ToString("N0");
                            lblTotalOut.Text = totalOut.ToString("N0");
                            lblNetMovement.Text = (totalIn - totalOut).ToString("N0");
                        }
                    }
                }

                if (_transactionsList.Count == 0)
                {
                    dgTransactions.Visibility = Visibility.Collapsed;
                    lblNoData.Visibility = Visibility.Visible;
                }
                else
                {
                    dgTransactions.Visibility = Visibility.Visible;
                    lblNoData.Visibility = Visibility.Collapsed;
                }

                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"تم تحميل {_transactionsList.Count} حركة للصنف ID = {_selectedProductId}");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تحميل الحركات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"LoadTransactionsAsync Error: {ex.Message}");
            }
        }

        private string ConvertTransactionType(string type)
        {
            switch (type)
            {
                case "Purchase":
                    return "مشتريات";
                case "Sale":
                    return "مبيعات";
                case "Return":
                    return "مرتجع";
                case "Transfer":
                    return "تحويل";
                case "Adjustment":
                    return "تسوية";
                default:
                    return type;
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadProductsAsync();
            await LoadStoresAsync();
            await LoadCurrentBalance();
            await LoadTransactionsAsync();
            MessageBox.Show("تم تحديث البيانات", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task PrintReportAsync()
        {
            try
            {
                if (_transactionsList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    StackPanel printPanel = new StackPanel();
                    printPanel.Margin = new Thickness(20);
                    printPanel.Width = 800;

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = "كارتة صنف",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = $"الصنف: {lblProductName.Text} - الكود: {lblProductCode.Text}",
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 5)
                    });

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = $"المخزن: {lblStoreName.Text}",
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 5)
                    });

                    string periodText;
                    if (dpFromDate.SelectedDate.HasValue && dpToDate.SelectedDate.HasValue)
                    {
                        periodText = $"الفترة: {dpFromDate.SelectedDate:yyyy/MM/dd} إلى {dpToDate.SelectedDate:yyyy/MM/dd}";
                    }
                    else if (dpFromDate.SelectedDate.HasValue)
                    {
                        periodText = $"الفترة: من {dpFromDate.SelectedDate:yyyy/MM/dd} حتى الآن";
                    }
                    else if (dpToDate.SelectedDate.HasValue)
                    {
                        periodText = $"الفترة: حتى {dpToDate.SelectedDate:yyyy/MM/dd}";
                    }
                    else
                    {
                        periodText = "الفترة: كل الحركات (بدون تحديد تاريخ)";
                    }

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = periodText,
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 15)
                    });

                    printPanel.Children.Add(new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.Black,
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    Grid itemsGrid = new Grid();
                    for (int i = 0; i <= 9; i++)
                    {
                        itemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(40) : new GridLength(1, GridUnitType.Star) });
                    }

                    itemsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    AddPrintHeader(itemsGrid, 0, "م");
                    AddPrintHeader(itemsGrid, 1, "التاريخ");
                    AddPrintHeader(itemsGrid, 2, "نوع الحركة");
                    AddPrintHeader(itemsGrid, 3, "رقم المرجع");
                    AddPrintHeader(itemsGrid, 4, "الكمية");
                    AddPrintHeader(itemsGrid, 5, "سعر الوحدة");
                    AddPrintHeader(itemsGrid, 6, "الإجمالي");
                    AddPrintHeader(itemsGrid, 7, "الرصيد قبل");
                    AddPrintHeader(itemsGrid, 8, "الرصيد بعد");
                    AddPrintHeader(itemsGrid, 9, "الملاحظات");

                    int rowIndex = 1;
                    foreach (var item in _transactionsList)
                    {
                        itemsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        AddPrintCell(itemsGrid, 0, rowIndex, item.SerialNumber.ToString());
                        AddPrintCell(itemsGrid, 1, rowIndex, item.TransactionDate.ToString("yyyy/MM/dd"));
                        AddPrintCell(itemsGrid, 2, rowIndex, item.TransactionType);
                        AddPrintCell(itemsGrid, 3, rowIndex, item.ReferenceNumber);
                        AddPrintCell(itemsGrid, 4, rowIndex, item.Quantity.ToString("N0"));
                        AddPrintCell(itemsGrid, 5, rowIndex, item.UnitPrice.ToString("N2"));
                        AddPrintCell(itemsGrid, 6, rowIndex, item.TotalAmount.ToString("N2"));
                        AddPrintCell(itemsGrid, 7, rowIndex, item.QuantityBefore.ToString("N0"));
                        AddPrintCell(itemsGrid, 8, rowIndex, item.QuantityAfter.ToString("N0"));
                        AddPrintCell(itemsGrid, 9, rowIndex, item.Description);
                        rowIndex++;
                    }

                    printPanel.Children.Add(itemsGrid);

                    printPanel.Children.Add(new Border
                    {
                        BorderBrush = System.Windows.Media.Brushes.Black,
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Margin = new Thickness(0, 10, 0, 10)
                    });

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = $"إجمالي الوارد: {lblTotalIn.Text} | إجمالي المنصرف: {lblTotalOut.Text} | صافي الحركة: {lblNetMovement.Text}",
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 5),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    printPanel.Children.Add(new TextBlock
                    {
                        Text = $"الرصيد الحالي: {lblCurrentBalance.Text} | القيمة الإجمالية: {lblTotalValue.Text}",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    printDialog.PrintVisual(printPanel, $"كارتة_صنف_{lblProductCode.Text}_{DateTime.Now:yyyyMMdd}");
                    MessageBox.Show("تم إرسال التقرير للطباعة", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"PrintReportAsync Error: {ex.Message}");
            }
        }

        private void AddPrintHeader(Grid grid, int column, string text)
        {
            TextBlock header = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(2),
                Padding = new Thickness(4)
            };
            Grid.SetColumn(header, column);
            Grid.SetRow(header, 0);
            grid.Children.Add(header);
        }

        private void AddPrintCell(Grid grid, int column, int row, string text)
        {
            TextBlock cell = new TextBlock
            {
                Text = text,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(2),
                Padding = new Thickness(4)
            };
            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, row);
            grid.Children.Add(cell);
        }

        #endregion
    }
}