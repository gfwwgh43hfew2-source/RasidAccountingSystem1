using RasidAccountingSystem.Services;
using RasidAccountingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// ✅ كلاس مساعد لعرض المخازن في ComboBox
    /// </summary>
    public class WarehouseSimple
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// ✅ نافذة إنشاء تسوية جردية جديدة مع دعم نظام الوحدات الثلاثة
    /// </summary>
    public partial class InventoryAdjustmentDialog : Window
    {
        #region المتغيرات الخاصة (Private Fields)

        /// <summary>
        /// خدمة التسويات الجردية
        /// </summary>
        private readonly InventoryAdjustmentService _adjustmentService;

        /// <summary>
        /// خدمة قاعدة البيانات
        /// </summary>
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// قائمة أصناف التسوية
        /// </summary>
        private ObservableCollection<AdjustmentItemDetail> _items;

        /// <summary>
        /// قائمة جميع المنتجات للبحث
        /// </summary>
        private List<ProductSearchItem> _allProducts;

        /// <summary>
        /// الرقم التسلسلي للصف التالي
        /// </summary>
        private int _nextRowNumber = 1;

        /// <summary>
        /// المنتج المختار حالياً
        /// </summary>
        private ProductSearchItem _currentSelectedProduct = null;

        /// <summary>
        /// كمية النظام بالوحدة الأساسية (يتم تخزينها مؤقتاً)
        /// </summary>
        private decimal _systemQuantityInBaseUnit = 0;

        #endregion

        #region كلاس مساعد للبحث عن المنتجات (مع دعم الوحدات)

        /// <summary>
        /// كلاس يمثل منتجاً للبحث مع معلومات الوحدات الثلاثة
        /// </summary>
        public class ProductSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }

            /// <summary>
            /// اسم الوحدة الأولى (الوحدة الكبرى - مثل: كرتونة)
            /// </summary>
            public string Unit1 { get; set; } = "";

            /// <summary>
            /// اسم الوحدة الثانية (الوحدة الوسطى - مثل: علبة)
            /// </summary>
            public string Unit2 { get; set; } = "";

            /// <summary>
            /// اسم الوحدة الثالثة (الوحدة الأساسية - مثل: قطعة)
            /// </summary>
            public string Unit3 { get; set; } = "";

            /// <summary>
            /// عامل التحويل من الوحدة الأولى إلى الوحدة الثانية
            /// </summary>
            public int Unit1Factor { get; set; } = 1;

            /// <summary>
            /// عامل التحويل من الوحدة الثانية إلى الوحدة الثالثة (الأساسية)
            /// </summary>
            public int Unit2Factor { get; set; } = 1;

            /// <summary>
            /// سعر الوحدة الأولى (الكرتونة)
            /// </summary>
            public decimal Price1 { get; set; } = 0;

            /// <summary>
            /// سعر الوحدة الثانية (العلبة)
            /// </summary>
            public decimal Price2 { get; set; } = 0;

            /// <summary>
            /// سعر الوحدة الثالثة (القطعة - الأساسية)
            /// </summary>
            public decimal Price3 { get; set; } = 0;
        }

        #endregion

        #region المنشئ (Constructor)

        /// <summary>
        /// منشئ النافذة - يقوم بتهيئة الخدمات وتحميل البيانات
        /// </summary>
        public InventoryAdjustmentDialog()
        {
            try
            {
                InitializeComponent();

                // تهيئة الخدمات
                _adjustmentService = new InventoryAdjustmentService();
                _databaseService = new DatabaseService();

                // تهيئة قائمة الأصناف
                _items = new ObservableCollection<AdjustmentItemDetail>();

                // ربط حدث Loaded لتحميل البيانات عند ظهور النافذة
                this.Loaded += async (sender, eventArguments) => await LoadDataAsync();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء تهيئة النافذة: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"Constructor Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");
            }
        }

        #endregion

        #region دوال تحميل البيانات (Data Loading Methods)

        /// <summary>
        /// تحميل جميع البيانات المطلوبة للنافذة (المخازن، المنتجات، إلخ)
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                // تغيير مؤشر الماوس إلى شكل الانتظار
                Mouse.OverrideCursor = Cursors.Wait;

                // ============================================
                // 1. تحميل قائمة المخازن
                // ============================================
                List<WarehouseSimple> warehouses = await GetWarehousesAsync();
                cmbWarehouse.ItemsSource = warehouses;

                // اختيار أول مخزن بشكل افتراضي
                if (warehouses.Count > 0)
                {
                    cmbWarehouse.SelectedItem = warehouses[0];
                }

                // ============================================
                // 2. تحميل قائمة المنتجات للـ AutoComplete
                // ============================================
                await LoadProductsAsync();

                // ============================================
                // 3. ربط قائمة الأصناف بالجدول
                // ============================================
                dgInvoiceItems.ItemsSource = _items;

                // ============================================
                // 4. إنشاء رقم تسوية جديد
                // ============================================
                lblAdjustmentNumber.Text = $"ADJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

                // ============================================
                // 5. تعيين التاريخ الحالي
                // ============================================
                dpAdjustmentDate.SelectedDate = DateTime.Now;

                // ============================================
                // 6. تعيين نوع التسوية الافتراضي
                // ============================================
                if (cmbAdjustmentType != null && cmbAdjustmentType.Items.Count > 0)
                {
                    cmbAdjustmentType.SelectedIndex = 0;
                }

                // استعادة مؤشر الماوس
                Mouse.OverrideCursor = null;

                System.Diagnostics.Debug.WriteLine("✅ تم تحميل بيانات نافذة التسوية الجردية بنجاح");
            }
            catch (Exception exception)
            {
                // استعادة مؤشر الماوس في حالة الخطأ
                Mouse.OverrideCursor = null;

                MessageBox.Show(
                    $"حدث خطأ أثناء تحميل البيانات: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"LoadDataAsync Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// تحميل قائمة المخازن من قاعدة البيانات
        /// </summary>
        /// <returns>قائمة من WarehouseSimple تحتوي على معرف واسم كل مخزن</returns>
        private async Task<List<WarehouseSimple>> GetWarehousesAsync()
        {
            List<WarehouseSimple> warehousesList = new List<WarehouseSimple>();

            try
            {
                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    string sqlQuery = @"
                        SELECT 
                            StoreID, 
                            StoreNameAr 
                        FROM Stores 
                        WHERE IsActive = 1 
                        ORDER BY StoreNameAr
                    ";

                    using (var command = new SQLiteCommand(sqlQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            warehousesList.Add(new WarehouseSimple
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {warehousesList.Count} مخزن");
                return warehousesList;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"GetWarehousesAsync Error: {exception.Message}");
                throw;
            }
        }

        /// <summary>
        /// تحميل جميع المنتجات مع معلومات الوحدات الثلاثة
        /// </summary>
        private async Task LoadProductsAsync()
        {
            try
            {
                _allProducts = new List<ProductSearchItem>();

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    string sqlQuery = @"
                        SELECT 
                            ProductID, 
                            ProductCode, 
                            ProductNameAr, 
                            SalePrice,
                            COALESCE(Unit1, '') as Unit1,
                            COALESCE(Unit2, '') as Unit2,
                            COALESCE(Unit3, '') as Unit3,
                            COALESCE(Unit1Factor, 1) as Unit1Factor,
                            COALESCE(Unit2Factor, 1) as Unit2Factor,
                            COALESCE(Price1, 0) as Price1,
                            COALESCE(Price2, 0) as Price2,
                            COALESCE(Price3, 0) as Price3
                        FROM Products 
                        WHERE IsActive = 1 
                        ORDER BY ProductNameAr
                    ";

                    using (var command = new SQLiteCommand(sqlQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _allProducts.Add(new ProductSearchItem
                            {
                                Id = reader.GetInt32(0),
                                Code = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Name = reader.GetString(2),
                                Price = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                Unit1 = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Unit2 = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Unit3 = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Unit1Factor = reader.IsDBNull(7) ? 1 : reader.GetInt32(7),
                                Unit2Factor = reader.IsDBNull(8) ? 1 : reader.GetInt32(8),
                                Price1 = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                                Price2 = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                Price3 = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11)
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_allProducts.Count} منتج مع معلومات الوحدات");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProductsAsync Error: {exception.Message}");
                throw;
            }
        }

        #endregion

        #region دوال AutoComplete للمنتجات (Product AutoComplete Methods)

        /// <summary>
        /// حدث تغيير النص في حقل البحث عن المنتج
        /// </summary>
        private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs eventArguments)
        {
            try
            {
                string searchText = txtProductSearch.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(searchText))
                {
                    popupProducts.IsOpen = false;
                    return;
                }

                // تصفية المنتجات حسب النص المدخل
                List<ProductSearchItem> filteredProducts = _allProducts
                    .Where(product =>
                        product.Name.ToLower().Contains(searchText.ToLower()) ||
                        product.Code.ToLower().Contains(searchText.ToLower()))
                    .ToList();

                lstProducts.ItemsSource = filteredProducts;

                // فتح القائمة المنبثقة إذا كانت هناك نتائج
                popupProducts.IsOpen = filteredProducts.Count > 0;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"TxtProductSearch_TextChanged Error: {exception.Message}");
            }
        }

        /// <summary>
        /// حدث الضغط على مفتاح في حقل البحث عن المنتج
        /// </summary>
        private void TxtProductSearch_KeyDown(object sender, KeyEventArgs eventArguments)
        {
            try
            {
                if (eventArguments.Key == Key.Enter && popupProducts.IsOpen && lstProducts.SelectedItem != null)
                {
                    // اختيار المنتج عند الضغط على Enter
                    SelectProduct(lstProducts.SelectedItem as ProductSearchItem);
                    eventArguments.Handled = true;
                }
                else if (eventArguments.Key == Key.Escape)
                {
                    // إغلاق القائمة المنبثقة عند الضغط على Escape
                    popupProducts.IsOpen = false;
                    eventArguments.Handled = true;
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"TxtProductSearch_KeyDown Error: {exception.Message}");
            }
        }

        /// <summary>
        /// حدث تغيير التحديد في قائمة المنتجات
        /// </summary>
        private void LstProducts_SelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
        {
            try
            {
                if (lstProducts.SelectedItem is ProductSearchItem selectedProduct)
                {
                    // حفظ المنتج المختار
                    _currentSelectedProduct = selectedProduct;

                    // تحديث قائمة الوحدات
                    UpdateUnitComboBox(selectedProduct);

                    // تحميل كمية النظام في المخزن المختار
                    _ = LoadSystemQuantityAsync(selectedProduct.Id);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"LstProducts_SelectionChanged Error: {exception.Message}");
            }
        }

        /// <summary>
        /// حدث النقر المزدوج على منتج في القائمة
        /// </summary>
        private void LstProducts_MouseDoubleClick(object sender, MouseButtonEventArgs eventArguments)
        {
            try
            {
                if (lstProducts.SelectedItem is ProductSearchItem selectedProduct)
                {
                    SelectProduct(selectedProduct);
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"LstProducts_MouseDoubleClick Error: {exception.Message}");
            }
        }

        /// <summary>
        /// تحديث قائمة الوحدات في ComboBox بناءً على المنتج المختار
        /// </summary>
        /// <param name="product">المنتج المختار</param>
        private void UpdateUnitComboBox(ProductSearchItem product)
        {
            try
            {
                if (cmbUnit == null || product == null)
                {
                    return;
                }

                // مسح القائمة الحالية
                cmbUnit.Items.Clear();

                // ============================================
                // إضافة الوحدات المتوفرة فقط
                // ============================================

                // الوحدة الأولى (الوحدة الكبرى - مثل: كرتونة)
                if (!string.IsNullOrEmpty(product.Unit1) && product.Unit1Factor > 0)
                {
                    var item1 = new ComboBoxItem
                    {
                        Content = product.Unit1,
                        Tag = "Unit1"
                    };
                    cmbUnit.Items.Add(item1);
                }

                // الوحدة الثانية (الوحدة الوسطى - مثل: علبة)
                if (!string.IsNullOrEmpty(product.Unit2) && product.Unit2Factor > 0)
                {
                    var item2 = new ComboBoxItem
                    {
                        Content = product.Unit2,
                        Tag = "Unit2"
                    };
                    cmbUnit.Items.Add(item2);
                }

                // الوحدة الثالثة (الوحدة الأساسية - مثل: قطعة)
                if (!string.IsNullOrEmpty(product.Unit3))
                {
                    var item3 = new ComboBoxItem
                    {
                        Content = product.Unit3,
                        Tag = "Unit3"
                    };
                    cmbUnit.Items.Add(item3);
                }

                // ============================================
                // إذا لم توجد وحدات، أضف وحدة افتراضية
                // ============================================
                if (cmbUnit.Items.Count == 0)
                {
                    var defaultItem = new ComboBoxItem
                    {
                        Content = "وحدة",
                        Tag = "Unit3"
                    };
                    cmbUnit.Items.Add(defaultItem);
                }

                // ============================================
                // اختيار الوحدة الثالثة كافتراضية (الأساسية)
                // ============================================
                for (int i = 0; i < cmbUnit.Items.Count; i++)
                {
                    if (cmbUnit.Items[i] is ComboBoxItem item && item.Tag?.ToString() == "Unit3")
                    {
                        cmbUnit.SelectedIndex = i;
                        break;
                    }
                }

                // إذا لم يتم العثور على الوحدة 3، اختر الأولى
                if (cmbUnit.SelectedIndex == -1)
                {
                    cmbUnit.SelectedIndex = 0;
                }

                // ============================================
                // ✅ تحديث السعر وكمية النظام بناءً على الوحدة المختارة
                // ============================================
                UpdatePriceAndStockFromUnit(product);

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث قائمة الوحدات: {cmbUnit.Items.Count} وحدة");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUnitComboBox Error: {exception.Message}");
            }
        }

        /// <summary>
        /// ✅ حدث تغيير الوحدة المختارة - يتم تحديث السعر وكمية النظام
        /// </summary>
        private void CmbUnit_SelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
        {
            try
            {
                // الحصول على المنتج المختار
                ProductSearchItem selectedProduct = GetSelectedProductFromSearch();

                if (selectedProduct == null)
                {
                    // إذا لم يتم العثور على المنتج من خلال البحث، استخدم المنتج المخزن
                    selectedProduct = _currentSelectedProduct;
                }

                if (selectedProduct == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ لا يوجد منتج مختار لتحديث السعر والكمية");
                    return;
                }

                // ✅ تحديث السعر وكمية النظام بناءً على الوحدة المختارة
                UpdatePriceAndStockFromUnit(selectedProduct);

                System.Diagnostics.Debug.WriteLine($"✅ تم تغيير الوحدة وتحديث السعر وكمية النظام");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"CmbUnit_SelectionChanged Error: {exception.Message}");
            }
        }

        /// <summary>
        /// ✅ دالة موحدة لتحديث السعر وكمية النظام بناءً على الوحدة المختارة
        /// </summary>
        /// <param name="product">المنتج المختار</param>
        private void UpdatePriceAndStockFromUnit(ProductSearchItem product)
        {
            try
            {
                if (product == null || cmbUnit.SelectedItem == null)
                {
                    return;
                }

                // الحصول على علامة الوحدة المختارة
                string unitTag = "";
                if (cmbUnit.SelectedItem is ComboBoxItem selectedItem)
                {
                    unitTag = selectedItem.Tag?.ToString() ?? "Unit3";
                }

                // ============================================
                // 1. تحديث السعر بناءً على الوحدة المختارة
                // ============================================
                decimal unitPrice = 0;
                string unitDisplayName = "";

                switch (unitTag)
                {
                    case "Unit1":
                        // سعر الوحدة الأولى (الوحدة الكبرى)
                        unitDisplayName = product.Unit1 ?? "كرتونة";
                        if (product.Price1 > 0)
                        {
                            unitPrice = product.Price1;
                        }
                        else
                        {
                            // حساب سعر الوحدة 1 من الوحدة 3
                            unitPrice = product.Price3 * product.Unit2Factor * product.Unit1Factor;
                        }
                        break;

                    case "Unit2":
                        // سعر الوحدة الثانية (الوحدة الوسطى)
                        unitDisplayName = product.Unit2 ?? "علبة";
                        if (product.Price2 > 0)
                        {
                            unitPrice = product.Price2;
                        }
                        else
                        {
                            // حساب سعر الوحدة 2 من الوحدة 3
                            unitPrice = product.Price3 * product.Unit2Factor;
                        }
                        break;

                    case "Unit3":
                    default:
                        // سعر الوحدة الثالثة (الوحدة الأساسية)
                        unitDisplayName = product.Unit3 ?? "قطعة";
                        unitPrice = product.Price3 > 0 ? product.Price3 : product.Price;
                        break;
                }

                // تعيين السعر في حقل سعر الوحدة
                txtUnitCost.Text = unitPrice.ToString("N2");

                System.Diagnostics.Debug.WriteLine($"💰 تم تحديث السعر: {unitPrice:N2} لكل {unitDisplayName}");

                // ============================================
                // 2. تحديث كمية النظام بناءً على الوحدة المختارة
                // ============================================
                if (_systemQuantityInBaseUnit > 0)
                {
                    decimal displayQuantity = _systemQuantityInBaseUnit;
                    string stockUnitDisplayName = unitDisplayName;

                    switch (unitTag)
                    {
                        case "Unit1":
                            if (product.Unit1Factor > 0 && product.Unit2Factor > 0)
                            {
                                displayQuantity = _systemQuantityInBaseUnit / (product.Unit1Factor * product.Unit2Factor);
                                stockUnitDisplayName = product.Unit1 ?? "كرتونة";
                            }
                            break;

                        case "Unit2":
                            if (product.Unit2Factor > 0)
                            {
                                displayQuantity = _systemQuantityInBaseUnit / product.Unit2Factor;
                                stockUnitDisplayName = product.Unit2 ?? "علبة";
                            }
                            break;

                        case "Unit3":
                        default:
                            displayQuantity = _systemQuantityInBaseUnit;
                            stockUnitDisplayName = product.Unit3 ?? "قطعة";
                            break;
                    }

                    // عرض كمية النظام مع الوحدة المناسبة
                    txtSystemQuantity.Text = displayQuantity.ToString("N2");
                    System.Diagnostics.Debug.WriteLine($"📦 تم تحديث كمية النظام: {displayQuantity:N2} {stockUnitDisplayName}");
                }
                else
                {
                    txtSystemQuantity.Text = "0.00";
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePriceAndStockFromUnit Error: {exception.Message}");
            }
        }

        /// <summary>
        /// الحصول على المنتج المختار من حقل البحث
        /// </summary>
        /// <returns>المنتج المختار أو null</returns>
        private ProductSearchItem GetSelectedProductFromSearch()
        {
            try
            {
                if (string.IsNullOrEmpty(txtProductSearch.Text))
                {
                    return null;
                }

                return _allProducts.FirstOrDefault(product => product.Name == txtProductSearch.Text);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedProductFromSearch Error: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// تحميل كمية المنتج في المخزن المحدد
        /// </summary>
        /// <param name="productId">معرف المنتج</param>
        private async Task LoadSystemQuantityAsync(int productId)
        {
            try
            {
                if (cmbWarehouse.SelectedValue == null)
                {
                    return;
                }

                int warehouseId = Convert.ToInt32(cmbWarehouse.SelectedValue);
                decimal systemQuantity = await _adjustmentService.GetProductQuantityInWarehouseAsync(warehouseId, productId);

                // ✅ تخزين الكمية بالوحدة الأساسية
                _systemQuantityInBaseUnit = systemQuantity;

                // ✅ تحديث العرض بناءً على الوحدة المختارة
                ProductSearchItem selectedProduct = GetSelectedProductFromSearch();
                if (selectedProduct == null)
                {
                    selectedProduct = _currentSelectedProduct;
                }

                if (selectedProduct != null)
                {
                    UpdatePriceAndStockFromUnit(selectedProduct);
                }
                else
                {
                    txtSystemQuantity.Text = systemQuantity.ToString("N2");
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل كمية النظام: {systemQuantity:N2} (بالوحدة الأساسية)");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSystemQuantityAsync Error: {exception.Message}");
            }
        }

        /// <summary>
        /// اختيار منتج من القائمة
        /// </summary>
        /// <param name="product">المنتج المختار</param>
        private void SelectProduct(ProductSearchItem product)
        {
            try
            {
                if (product == null)
                {
                    return;
                }

                // حفظ المنتج المختار
                _currentSelectedProduct = product;

                // تعيين اسم المنتج في حقل البحث
                txtProductSearch.Text = product.Name;

                // تحديث قائمة الوحدات (ستقوم بتحديث السعر وكمية النظام تلقائياً)
                UpdateUnitComboBox(product);

                // إغلاق القائمة المنبثقة
                popupProducts.IsOpen = false;

                // التركيز على حقل الكمية الفعلية
                txtActualQuantity.Focus();
                txtActualQuantity.SelectAll();

                System.Diagnostics.Debug.WriteLine($"✅ تم اختيار المنتج: {product.Name} (ID: {product.Id})");
                System.Diagnostics.Debug.WriteLine($"   الوحدة الأولى: {product.Unit1} (العامل: {product.Unit1Factor}, السعر: {product.Price1:N2})");
                System.Diagnostics.Debug.WriteLine($"   الوحدة الثانية: {product.Unit2} (العامل: {product.Unit2Factor}, السعر: {product.Price2:N2})");
                System.Diagnostics.Debug.WriteLine($"   الوحدة الثالثة: {product.Unit3} (السعر: {product.Price3:N2})");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"SelectProduct Error: {exception.Message}");
            }
        }

        #endregion

        #region دوال إدارة الأصناف (Items Management Methods)

        /// <summary>
        /// حدث النقر على زر إضافة منتج
        /// </summary>
        private void BtnAddProduct_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                AddProductToGrid();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء إضافة المنتج: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"BtnAddProduct_Click Error: {exception.Message}");
            }
        }

        /// <summary>
        /// إضافة منتج إلى جدول التسوية - الطريقة الصحيحة
        /// </summary>
        private void AddProductToGrid()
        {
            try
            {
                // ============================================
                // 1. التحقق من اختيار منتج
                // ============================================
                if (string.IsNullOrEmpty(txtProductSearch.Text))
                {
                    MessageBox.Show(
                        "يرجى اختيار منتج من القائمة",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtProductSearch.Focus();
                    return;
                }

                ProductSearchItem selectedProduct = _allProducts.FirstOrDefault(product => product.Name == txtProductSearch.Text);

                if (selectedProduct == null)
                {
                    MessageBox.Show(
                        "المنتج غير موجود في قاعدة البيانات",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtProductSearch.Focus();
                    txtProductSearch.SelectAll();
                    return;
                }

                // ============================================
                // 2. الحصول على الوحدة المختارة
                // ============================================
                string selectedUnitName = "وحدة";
                string selectedUnitTag = "Unit3";

                if (cmbUnit.SelectedItem is ComboBoxItem selectedUnit)
                {
                    selectedUnitName = selectedUnit.Content?.ToString() ?? "وحدة";
                    selectedUnitTag = selectedUnit.Tag?.ToString() ?? "Unit3";
                }

                // ============================================
                // 3. التحقق من صحة الكمية الفعلية
                // ============================================
                if (!decimal.TryParse(txtActualQuantity.Text, out decimal actualQuantity) || actualQuantity < 0)
                {
                    MessageBox.Show(
                        "الكمية الفعلية غير صحيحة.\nيرجى إدخال قيمة رقمية موجبة.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtActualQuantity.Focus();
                    txtActualQuantity.SelectAll();
                    return;
                }

                // ============================================
                // 4. التحقق من صحة سعر الوحدة
                // ============================================
                if (!decimal.TryParse(txtUnitCost.Text, out decimal unitCost) || unitCost < 0)
                {
                    MessageBox.Show(
                        "سعر الوحدة غير صحيح.\nيرجى إدخال قيمة رقمية موجبة.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtUnitCost.Focus();
                    txtUnitCost.SelectAll();
                    return;
                }

                // ============================================
                // 5. ⭐⭐⭐ حساب الفرق بشكل صحيح ⭐⭐⭐                // استخدام كمية النظام المخزنة (بالوحدة الأساسية)
                // وليس الكمية المعروضة في حقل txtSystemQuantity
                // ============================================
                // النظام: 1500 قطعة، الفعلي: 100 قطعة
                // الفرق = 100 - 1500 = -1400 (عجز)
                decimal systemQuantity = _systemQuantityInBaseUnit; // ✅ القيمة المخزنة (بالقطعة)
                decimal difference = actualQuantity - systemQuantity;

                System.Diagnostics.Debug.WriteLine($"📊 حساب الفرق:");
                System.Diagnostics.Debug.WriteLine($"   كمية النظام (بالوحدة الأساسية): {systemQuantity:N2}");
                System.Diagnostics.Debug.WriteLine($"   الكمية الفعلية: {actualQuantity:N2} {selectedUnitName}");
                System.Diagnostics.Debug.WriteLine($"   الفرق: {difference:N2}");

                // ============================================
                // 6. التحقق من وجود فرق
                // ============================================
                if (difference == 0)
                {
                    MessageBoxResult confirmationResult = MessageBox.Show(
                        "الكمية الفعلية مطابقة لكمية النظام.\n\n" +
                        "هل تريد إضافة هذا الصنف مع فرق صفر؟",
                        "تأكيد الإضافة",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmationResult != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                // ============================================
                // 7. تحديد نوع التعديل
                // ============================================
                // difference > 0 => Increase (زيادة)
                // difference < 0 => Decrease (عجز)
                string adjustmentType = difference >= 0 ? "Increase" : "Decrease";
                decimal absoluteDifference = Math.Abs(difference);

                System.Diagnostics.Debug.WriteLine($"   نوع التعديل: {adjustmentType}");
                System.Diagnostics.Debug.WriteLine($"   الفرق المطلق: {absoluteDifference:N2}");

                // ============================================
                // 8. حساب الكمية المحولة إلى الوحدة الأساسية (للتخزين)
                // ============================================
                decimal differenceInBaseUnit = absoluteDifference;

                // تحويل الفرق إلى الوحدة الأساسية بناءً على الوحدة المختارة
                if (selectedUnitTag == "Unit2")
                {
                    differenceInBaseUnit = absoluteDifference * selectedProduct.Unit2Factor;
                }
                else if (selectedUnitTag == "Unit1")
                {
                    differenceInBaseUnit = absoluteDifference * selectedProduct.Unit2Factor * selectedProduct.Unit1Factor;
                }

                // حساب التكلفة الإجمالية (بالوحدة المختارة)
                decimal totalCost = absoluteDifference * unitCost;

                System.Diagnostics.Debug.WriteLine($"   الفرق بالوحدة الأساسية: {differenceInBaseUnit:N2}");
                System.Diagnostics.Debug.WriteLine($"   التكلفة الإجمالية: {totalCost:N2}");

                // ============================================
                // 9. إضافة الصنف إلى القائمة
                // ============================================
                _items.Add(new AdjustmentItemDetail
                {
                    RowNumber = _nextRowNumber++,
                    ProductID = selectedProduct.Id,
                    ProductName = selectedProduct.Name,
                    ProductCode = selectedProduct.Code,
                    SystemQuantity = systemQuantity,
                    ActualQuantity = actualQuantity,
                    DifferenceQuantity = absoluteDifference,   // ✅ القيمة المطلقة (للإيجابية دائماً)
                    AdjustmentType = adjustmentType,           // ✅ "Increase" أو "Decrease"
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    UnitName = selectedUnitName,
                    UnitTag = selectedUnitTag,
                    QuantityInBaseUnit = differenceInBaseUnit
                });

                System.Diagnostics.Debug.WriteLine($"✅ تم إضافة المنتج: {selectedProduct.Name}");
                System.Diagnostics.Debug.WriteLine($"   كمية النظام: {systemQuantity:N2} (بالوحدة الأساسية)");
                System.Diagnostics.Debug.WriteLine($"   الكمية الفعلية: {actualQuantity:N2} {selectedUnitName}");
                System.Diagnostics.Debug.WriteLine($"   الفرق: {difference:N2} {selectedUnitName}");
                System.Diagnostics.Debug.WriteLine($"   نوع التعديل: {adjustmentType}");
                System.Diagnostics.Debug.WriteLine($"   الفرق المطلق: {absoluteDifference:N2}");
                System.Diagnostics.Debug.WriteLine($"   الفرق بالوحدة الأساسية: {differenceInBaseUnit:N2}");
                System.Diagnostics.Debug.WriteLine($"   سعر الوحدة: {unitCost:N2}");
                System.Diagnostics.Debug.WriteLine($"   التكلفة الإجمالية: {totalCost:N2}");

                // ============================================
                // 10. إعادة تعيين الحقول
                // ============================================
                txtProductSearch.Text = string.Empty;
                txtSystemQuantity.Text = "0.00";
                txtActualQuantity.Text = "0.00";
                txtUnitCost.Text = "0.00";
                _systemQuantityInBaseUnit = 0;
                _currentSelectedProduct = null;
                cmbUnit.Items.Clear();

                // التركيز على حقل البحث
                txtProductSearch.Focus();

                // ============================================
                // 11. تحديث الإجماليات
                // ============================================
                UpdateTotals();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء إضافة المنتج: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"AddProductToGrid Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// حدث النقر على زر حذف صنف
        /// </summary>
        private void DeleteItem_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                Button deleteButton = sender as Button;

                if (deleteButton == null)
                {
                    return;
                }

                AdjustmentItemDetail itemToDelete = deleteButton.Tag as AdjustmentItemDetail;

                if (itemToDelete == null)
                {
                    return;
                }

                // طلب تأكيد من المستخدم
                MessageBoxResult confirmationResult = MessageBox.Show(
                    $"هل تريد حذف المنتج '{itemToDelete.ProductName}' من قائمة التسوية؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmationResult == MessageBoxResult.Yes)
                {
                    _items.Remove(itemToDelete);

                    // إعادة ترقيم الصفوف
                    int rowNumber = 1;
                    foreach (var item in _items)
                    {
                        item.RowNumber = rowNumber++;
                    }
                    _nextRowNumber = rowNumber;

                    // تحديث الإجماليات
                    UpdateTotals();

                    System.Diagnostics.Debug.WriteLine($"✅ تم حذف المنتج: {itemToDelete.ProductName}");
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء حذف المنتج: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"DeleteItem_Click Error: {exception.Message}");
            }
        }

        /// <summary>
        /// تحديث الإجماليات (عدد الأصناف والتكلفة الإجمالية)
        /// </summary>
        private void UpdateTotals()
        {
            try
            {
                int itemCount = _items.Count;
                decimal totalCost = _items.Sum(item => item.TotalCost);

                lblTotalItems.Text = $"عدد الأصناف: {itemCount}";
                lblTotalCost.Text = $"التكلفة الإجمالية: {totalCost:N2} {CurrencyHelper.GetCurrencySymbol()}";

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث الإجماليات: {itemCount} صنف, التكلفة: {totalCost:N2}");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTotals Error: {exception.Message}");
            }
        }

        #endregion

        #region دوال الحفظ والإلغاء (Save and Cancel Methods)

        /// <summary>
        /// حدث النقر على زر حفظ التسوية
        /// </summary>
        private async void BtnSave_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                // ============================================
                // 1. التحقق من وجود أصناف
                // ============================================
                if (_items.Count == 0)
                {
                    MessageBox.Show(
                        "يرجى إضافة صنف واحد على الأقل قبل حفظ التسوية.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    txtProductSearch.Focus();
                    return;
                }

                // ============================================
                // 2. التحقق من اختيار مخزن
                // ============================================
                if (cmbWarehouse.SelectedValue == null)
                {
                    MessageBox.Show(
                        "يرجى اختيار المخزن المراد تسويته.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    cmbWarehouse.Focus();
                    return;
                }

                // ============================================
                // 3. تعطيل الأزرار وتغيير مؤشر الماوس
                // ============================================
                Mouse.OverrideCursor = Cursors.Wait;
                btnSave.IsEnabled = false;

                // ============================================
                // 4. إنشاء نموذج التسوية
                // ============================================
                AdjustmentCreateModel adjustmentModel = new AdjustmentCreateModel
                {
                    WarehouseID = Convert.ToInt32(cmbWarehouse.SelectedValue),
                    AdjustmentDate = dpAdjustmentDate.SelectedDate ?? DateTime.Now,
                    Reason = (cmbAdjustmentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "جرد دوري",
                    Notes = txtNotes.Text,
                    Items = _items.ToList()
                };

                // ============================================
                // 5. حفظ التسوية في قاعدة البيانات
                // ============================================
                int adjustmentId = await _adjustmentService.CreateAdjustmentAsync(adjustmentModel, LoginView.CurrentUserId);

                // ============================================
                // 6. التحقق من نجاح الحفظ
                // ============================================
                if (adjustmentId > 0)
                {
                    MessageBox.Show(
                        $"تم إنشاء التسوية الجردية بنجاح.\n\n" +
                        $"عدد الأصناف: {_items.Count}\n" +
                        $"التكلفة الإجمالية: {_items.Sum(item => item.TotalCost):N2} {CurrencyHelper.GetCurrencySymbol()}",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // إرجاع نتيجة النجاح وإغلاق النافذة
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "حدث خطأ أثناء إنشاء التسوية.\n\n" +
                        "يرجى المحاولة مرة أخرى.",
                        "خطأ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                // استعادة المؤشر والأزرار
                Mouse.OverrideCursor = null;
                btnSave.IsEnabled = true;
            }
            catch (Exception exception)
            {
                // استعادة المؤشر والأزرار في حالة الخطأ
                Mouse.OverrideCursor = null;
                btnSave.IsEnabled = true;

                MessageBox.Show(
                    $"حدث خطأ أثناء حفظ التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"BtnSave_Click Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// حدث النقر على زر إلغاء
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                // إلغاء وإغلاق النافذة
                DialogResult = false;
                Close();

                System.Diagnostics.Debug.WriteLine("✅ تم إلغاء التسوية الجردية");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"BtnCancel_Click Error: {exception.Message}");
            }
        }

        #endregion
    }
}