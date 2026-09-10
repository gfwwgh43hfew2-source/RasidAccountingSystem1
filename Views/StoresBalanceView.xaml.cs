using System;
using RasidAccountingSystem.Helpers;
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
    public partial class StoresBalanceView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _databaseService;
        private ObservableCollection<StoreInventoryItem> _storeInventoryList;
        private int _selectedStoreId = 0;
        private bool _isLoading = false;

        #endregion

        #region كلاس أرصدة المخازن

        public class StoreInventoryItem : INotifyPropertyChanged
        {
            private int _serialNumber;
            private string _productCode;
            private string _productName;
            private decimal _quantity;
            private decimal _reservedQuantity;
            private decimal _availableQuantity;
            private decimal _costPrice;
            private decimal _totalValue;
            private string _storeName;
            private int _storeId;

            public int SerialNumber
            {
                get => _serialNumber;
                set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
            }

            public string ProductCode
            {
                get => _productCode;
                set { _productCode = value; OnPropertyChanged(nameof(ProductCode)); }
            }

            public string ProductName
            {
                get => _productName;
                set { _productName = value; OnPropertyChanged(nameof(ProductName)); }
            }

            public decimal Quantity
            {
                get => _quantity;
                set { _quantity = value; OnPropertyChanged(nameof(Quantity)); }
            }

            public decimal ReservedQuantity
            {
                get => _reservedQuantity;
                set { _reservedQuantity = value; OnPropertyChanged(nameof(ReservedQuantity)); }
            }

            public decimal AvailableQuantity
            {
                get => _availableQuantity;
                set { _availableQuantity = value; OnPropertyChanged(nameof(AvailableQuantity)); }
            }

            public decimal CostPrice
            {
                get => _costPrice;
                set { _costPrice = value; OnPropertyChanged(nameof(CostPrice)); }
            }

            public decimal TotalValue
            {
                get => _totalValue;
                set { _totalValue = value; OnPropertyChanged(nameof(TotalValue)); }
            }

            public string StoreName
            {
                get => _storeName;
                set { _storeName = value; OnPropertyChanged(nameof(StoreName)); }
            }

            public int StoreId
            {
                get => _storeId;
                set { _storeId = value; OnPropertyChanged(nameof(StoreId)); }
            }

            public string StockStatusText
            {
                get
                {
                    if (Quantity <= 0)
                        return "نفد من المخزون";
                    else if (Quantity <= 10)
                        return "مخزون منخفض";
                    else if (Quantity <= 50)
                        return "مخزون متوسط";
                    else
                        return "مخزون جيد";
                }
            }

            public SolidColorBrush StockStatusColor
            {
                get
                {
                    if (Quantity <= 0)
                        return new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    else if (Quantity <= 10)
                        return new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    else if (Quantity <= 50)
                        return new SolidColorBrush(Color.FromRgb(219, 234, 254));
                    else
                        return new SolidColorBrush(Color.FromRgb(209, 250, 229));
                }
            }

            public SolidColorBrush StockStatusForeground
            {
                get
                {
                    if (Quantity <= 0)
                        return new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    else if (Quantity <= 10)
                        return new SolidColorBrush(Color.FromRgb(217, 119, 6));
                    else if (Quantity <= 50)
                        return new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    else
                        return new SolidColorBrush(Color.FromRgb(5, 150, 105));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

        public StoresBalanceView()
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
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region دوال التهيئة

        private void InitializeDatabase()
        {
            try
            {
                _databaseService = new DatabaseService();
                _storeInventoryList = new ObservableCollection<StoreInventoryItem>();
                dgStoreInventory.ItemsSource = _storeInventoryList;
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
                btnRefresh.Click += async (s, e) => await RefreshDataAsync();
                btnSearch.Click += async (s, e) => await LoadStoreInventoryAsync();
                cmbStores.SelectionChanged += async (s, e) => await LoadStoreInventoryAsync();
                txtSearch.TextChanged += async (s, e) => await LoadStoreInventoryAsync();
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
                await LoadStoresAsync();
                await LoadStoreInventoryAsync();
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تهيئة النموذج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"InitializeFormAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region دوال تحميل البيانات

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

        private async Task LoadStoreInventoryAsync()
        {
            try
            {
                if (_isLoading) return;
                _isLoading = true;

                Mouse.OverrideCursor = Cursors.Wait;
                _storeInventoryList.Clear();

                _selectedStoreId = cmbStores.SelectedItem != null ? ((StoreSimple)cmbStores.SelectedItem).Id : 0;
                string searchText = txtSearch.Text.Trim();

                System.Diagnostics.Debug.WriteLine($"المخزن المحدد: {_selectedStoreId}");
                System.Diagnostics.Debug.WriteLine($"نص البحث: {searchText}");

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    // ✅ استعلام معدل لجلب البيانات من Products مع LEFT JOIN StoreInventory
                    string sql = @"
                        SELECT 
                            COALESCE(si.StoreID, 0) as StoreID,
                            p.ProductID,
                            COALESCE(si.Quantity, 0) as Quantity,
                            COALESCE(si.ReservedQuantity, 0) as ReservedQuantity,
                            COALESCE(si.AvailableQuantity, si.Quantity, p.QuantityInBaseUnit, 0) as AvailableQuantity,
                            COALESCE(si.CostPrice, p.CostPrice, 0) as CostPrice,
                            p.ProductCode,
                            p.ProductNameAr,
                            COALESCE(s.StoreNameAr, 'مخزن غير محدد') as StoreName,
                            p.QuantityInBaseUnit as ProductTotalQuantity
                        FROM Products p
                        LEFT JOIN StoreInventory si ON p.ProductID = si.ProductID
                        LEFT JOIN Stores s ON si.StoreID = s.StoreID
                        WHERE p.IsActive = 1";

                    // ✅ إضافة شرط المخزن
                    if (_selectedStoreId > 0)
                    {
                        sql += " AND si.StoreID = @storeId";
                    }

                    // ✅ إضافة شرط البحث
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        sql += " AND (p.ProductCode LIKE @search OR p.ProductNameAr LIKE @search)";
                    }

                    // ✅ ترتيب النتائج
                    sql += " ORDER BY p.ProductCode";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (_selectedStoreId > 0)
                        {
                            cmd.Parameters.AddWithValue("@storeId", _selectedStoreId);
                            System.Diagnostics.Debug.WriteLine($"تم إضافة فلتر المخزن: {_selectedStoreId}");
                        }
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                            System.Diagnostics.Debug.WriteLine($"تم إضافة فلتر البحث: {searchText}");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int serial = 1;
                            while (await reader.ReadAsync())
                            {
                                int storeId = reader.GetInt32(0);
                                decimal quantity = reader.GetDecimal(2);
                                decimal reservedQuantity = reader.GetDecimal(3);
                                decimal availableQuantity = reader.GetDecimal(4);
                                decimal costPrice = reader.GetDecimal(5);
                                string productCode = reader.GetString(6);
                                string productName = reader.GetString(7);
                                string storeName = reader.GetString(8);
                                decimal productTotalQuantity = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9);

                                // ✅ إذا لم تكن هناك كمية في StoreInventory، استخدم الكمية الإجمالية من Products
                                if (quantity == 0 && productTotalQuantity > 0 && _selectedStoreId == 0)
                                {
                                    quantity = productTotalQuantity;
                                    availableQuantity = productTotalQuantity;
                                    System.Diagnostics.Debug.WriteLine($"⚠️ استخدم الكمية من Products بدلاً من StoreInventory: {productCode} = {productTotalQuantity}");
                                }

                                // ✅ عرض المنتجات حتى لو كانت الكمية 0 (لتتبع المخزون)
                                var item = new StoreInventoryItem
                                {
                                    SerialNumber = serial++,
                                    ProductCode = productCode,
                                    ProductName = productName,
                                    Quantity = quantity,
                                    ReservedQuantity = reservedQuantity,
                                    AvailableQuantity = availableQuantity > 0 ? availableQuantity : quantity,
                                    CostPrice = costPrice,
                                    TotalValue = (availableQuantity > 0 ? availableQuantity : quantity) * costPrice,
                                    StoreName = storeName,
                                    StoreId = storeId
                                };

                                _storeInventoryList.Add(item);

                                System.Diagnostics.Debug.WriteLine($"المنتج: {productCode}, المخزن: {storeName}, الكمية: {quantity}, الكمية الإجمالية: {productTotalQuantity}");
                            }
                        }
                    }
                }

                // ✅ إذا كان الجدول فارغاً، عرض رسالة مناسبة
                if (_storeInventoryList.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ لا توجد بيانات مخزون لعرضها");
                }

                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"تم تحميل {_storeInventoryList.Count} صنف مخزني");

                // ✅ تحديث الإجماليات
                UpdateTotals();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تحميل أرصدة المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"LoadStoreInventoryAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadStoreInventoryAsync();
            MessageBox.Show("تم تحديث البيانات", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region دوال تحديث الإجماليات

        private void UpdateTotals()
        {
            try
            {
                if (_storeInventoryList == null || _storeInventoryList.Count == 0)
                {
                    lblTotalItems.Text = "عدد الأصناف: 0";
                    lblTotalQuantity.Text = "إجمالي الكمية: 0";
                    lblTotalValue.Text = $"القيمة الإجمالية: 0.00 {CurrencyHelper.GetCurrencySymbol()}";
                    lblAverageCost.Text = $"متوسط السعر: 0.00 {CurrencyHelper.GetCurrencySymbol()}";
                    return;
                }

                int totalItems = _storeInventoryList.Count;
                decimal totalQuantity = _storeInventoryList.Sum(x => x.Quantity);
                decimal totalValue = _storeInventoryList.Sum(x => x.TotalValue);
                decimal averageCost = totalQuantity > 0 ? totalValue / totalQuantity : 0;

                lblTotalItems.Text = $"عدد الأصناف: {totalItems}";
                lblTotalQuantity.Text = $"إجمالي الكمية: {totalQuantity:N2}";
                lblTotalValue.Text = $"القيمة الإجمالية: {totalValue:N2} {CurrencyHelper.GetCurrencySymbol()}";
                lblAverageCost.Text = $"متوسط السعر: {averageCost:N2} {CurrencyHelper.GetCurrencySymbol()}";

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث الإجماليات: الأصناف={totalItems}, الكمية={totalQuantity}, القيمة={totalValue}, المتوسط={averageCost}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTotals Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال الأحداث

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadStoreInventoryAsync();
        }

        #endregion
    }
}