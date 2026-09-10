using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RasidAccountingSystem.Services;
using OfficeOpenXml;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MahApps.Metro.Controls;

namespace RasidAccountingSystem.Views
{
    public partial class ProductsView : UserControl
    {
        #region Private Fields

        private DatabaseService _dbService;
        private string _connectionString;
        private ObservableCollection<ProductItem> _productsList;
        private ObservableCollection<CategoryItem> _categoriesList;
        private ProductItem _selectedProduct;
        private int _currentPage = 1;
        private int _pageSize = 12;
        private int _totalPages = 1;
        private int _totalProductsCount = 0;
        private CancellationTokenSource _searchCts;
        private bool _isHeaderCheckBoxUpdating = false;
        private string _selectedExcelFilePath = string.Empty;
        private bool _isLoading = false;

        #endregion

        #region Constructor

        public ProductsView()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeEvents();
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        #endregion

        #region Initialization Methods

        private void InitializeDatabase()
        {
            _dbService = new DatabaseService();
            _connectionString = _dbService.GetConnectionString();
            _productsList = new ObservableCollection<ProductItem>();
            _categoriesList = new ObservableCollection<CategoryItem>();
            dgProducts.ItemsSource = _productsList;

            dgProducts.Visibility = Visibility.Visible;
            lblNoData.Visibility = Visibility.Collapsed;
        }

        private void InitializeEvents()
        {
            Loaded += async (s, e) => await LoadAllData();

            btnAddProduct.Click += async (s, e) => await OpenAddProductDialog();
            btnRefresh.Click += async (s, e) => await LoadProducts(txtSearch.Text.Trim());
            btnImportExcel.Click += (s, e) => ShowImportDialog();

            btnPrevPage.Click += async (s, e) =>
            {
                if (_currentPage > 1 && !_isLoading)
                {
                    _currentPage--;
                    await LoadProducts(txtSearch.Text.Trim());
                }
            };

            btnNextPage.Click += async (s, e) =>
            {
                if (_currentPage < _totalPages && !_isLoading)
                {
                    _currentPage++;
                    await LoadProducts(txtSearch.Text.Trim());
                }
            };

            txtSearch.TextChanged += OnSearchTextChanged;

            dgProducts.MouseDoubleClick += async (s, e) =>
            {
                if (dgProducts.SelectedItem is ProductItem product)
                    await OpenEditProductDialog(product.ProductID);
            };

            dgProducts.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Delete && dgProducts.SelectedItem is ProductItem product)
                {
                    MessageBoxResult confirmationResult = MessageBox.Show(
                        "هل أنت متأكد من حذف هذا المنتج؟",
                        "تأكيد الحذف",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmationResult == MessageBoxResult.Yes)
                    {
                        await DeleteProduct(product.ProductID);
                    }
                }
            };

            btnBulkDelete.Click += async (s, e) => await BulkDelete();
        }

        #endregion

        #region Import Methods

        private void ShowImportDialog()
        {
            ImportOverlay.Visibility = Visibility.Visible;
            _selectedExcelFilePath = string.Empty;
            FileNameText.Text = "اضغط لاختيار ملف Excel...";
            FilePathText.Text = "";
            FilePathText.Visibility = Visibility.Collapsed;
            BtnStartImport.IsEnabled = false;
            ProgressBorder.Visibility = Visibility.Collapsed;
            ResultBorder.Visibility = Visibility.Collapsed;

            BtnSelectFile.Click -= BtnSelectFile_Click;
            BtnSelectFile.Click += BtnSelectFile_Click;

            BtnStartImport.Click -= BtnStartImport_Click;
            BtnStartImport.Click += BtnStartImport_Click;

            BtnCloseImport.Click -= BtnCloseImport_Click;
            BtnCloseImport.Click += BtnCloseImport_Click;
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "اختر ملف Excel",
                Filter = "ملفات Excel|*.xlsx;*.xls|جميع الملفات|*.*",
                FilterIndex = 1,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedExcelFilePath = openFileDialog.FileName;
                FileNameText.Text = System.IO.Path.GetFileName(_selectedExcelFilePath);
                FilePathText.Text = _selectedExcelFilePath;
                FilePathText.Visibility = Visibility.Visible;
                BtnStartImport.IsEnabled = true;
                ResultBorder.Visibility = Visibility.Collapsed;
                ProgressBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnStartImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedExcelFilePath) || !File.Exists(_selectedExcelFilePath))
            {
                MessageBox.Show("الرجاء اختيار ملف Excel صالح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSelectFile.IsEnabled = false;
            BtnStartImport.IsEnabled = false;
            BtnCloseImport.IsEnabled = false;
            ProgressBorder.Visibility = Visibility.Visible;
            ResultBorder.Visibility = Visibility.Collapsed;
            ImportProgressBar.Value = 0;
            ProgressDetailText.Text = "جاري قراءة الملف...";
            ProgressText.Text = "جاري قراءة الملف...";

            try
            {
                var result = await Task.Run(() => ImportProductsFromExcelFile(_selectedExcelFilePath));

                ProgressBorder.Visibility = Visibility.Collapsed;
                ResultBorder.Visibility = Visibility.Visible;

                if (result.Success)
                {
                    ResultBorder.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                    ResultText.Text = $"✅ {result.Message}\n\nتم استيراد {result.ImportedCount} منتج بنجاح.\n{result.SkippedCount} منتج مكرر أو غير صالح تم تخطيه.";
                    ResultText.Foreground = new SolidColorBrush(Color.FromRgb(6, 78, 59));

                    await LoadProducts(txtSearch.Text.Trim());
                }
                else
                {
                    ResultBorder.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    ResultText.Text = $"❌ {result.Message}";
                    ResultText.Foreground = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                }
            }
            catch (Exception ex)
            {
                ProgressBorder.Visibility = Visibility.Collapsed;
                ResultBorder.Visibility = Visibility.Visible;
                ResultBorder.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                ResultText.Text = $"❌ حدث خطأ أثناء الاستيراد: {ex.Message}";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            }
            finally
            {
                BtnSelectFile.IsEnabled = true;
                BtnCloseImport.IsEnabled = true;
            }
        }

        private ImportResult ImportProductsFromExcelFile(string filePath)
        {
            var result = new ImportResult { Success = true, Message = "", ImportedCount = 0, SkippedCount = 0 };
            var importedProducts = 0;
            var skippedProducts = 0;
            var errors = new System.Text.StringBuilder();
            var productsToImport = new List<ExcelProductData>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null)
                {
                    result.Success = false;
                    result.Message = "لم يتم العثور على أوراق عمل في الملف";
                    return result;
                }

                int rowCount = worksheet.Dimension.Rows;
                if (rowCount <= 1)
                {
                    result.Success = false;
                    result.Message = "الملف لا يحتوي على بيانات (يجب أن يكون الصف الأول للعناوين)";
                    return result;
                }

                var headers = new ExcelColumnMapping();
                for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                {
                    string headerValue = worksheet.Cells[1, col].Text?.Trim().ToLower() ?? "";

                    if (headerValue.Contains("كود") || headerValue.Contains("code") || headerValue.Contains("الرقم"))
                        headers.ProductCodeColumn = col;
                    else if (headerValue.Contains("اسم") || headerValue.Contains("name") || headerValue.Contains("المنتج"))
                        headers.ProductNameColumn = col;
                    else if (headerValue.Contains("سعر شراء") || headerValue.Contains("cost") || headerValue.Contains("تكلفة"))
                        headers.CostPriceColumn = col;
                    else if (headerValue.Contains("سعر بيع") || headerValue.Contains("sale") || headerValue.Contains("بيع"))
                        headers.SalePriceColumn = col;
                    else if (headerValue.Contains("تصنيف") || headerValue.Contains("category"))
                        headers.CategoryColumn = col;
                    else if (headerValue.Contains("وحدة") || headerValue.Contains("unit"))
                        headers.UnitColumn = col;
                    else if (headerValue.Contains("باركود") || headerValue.Contains("barcode"))
                        headers.BarcodeColumn = col;
                    else if (headerValue.Contains("كمية") || headerValue.Contains("quantity") || headerValue.Contains("رصيد") || headerValue.Contains("افتتاحية"))
                        headers.QuantityColumn = col;
                    else if (headerValue.Contains("مخزن") || headerValue.Contains("store") || headerValue.Contains("المخزن"))
                        headers.StoreColumn = col;
                }

                if (!headers.ProductCodeColumn.HasValue || !headers.ProductNameColumn.HasValue)
                {
                    result.Success = false;
                    result.Message = "الملف يجب أن يحتوي على عمود 'كود المنتج' وعمود 'اسم المنتج' على الأقل";
                    return result;
                }

                var storeNames = new HashSet<string>();
                for (int row = 2; row <= rowCount; row++)
                {
                    if (headers.StoreColumn.HasValue)
                    {
                        string storeName = worksheet.Cells[row, headers.StoreColumn.Value].Text?.Trim();
                        if (!string.IsNullOrEmpty(storeName))
                        {
                            storeNames.Add(storeName);
                        }
                    }
                }

                var storeMapping = new Dictionary<string, int>();
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();

                    foreach (var storeName in storeNames)
                    {
                        int? storeId = GetOrCreateStore(connection, storeName);
                        if (storeId.HasValue)
                        {
                            storeMapping[storeName] = storeId.Value;
                        }
                    }
                }

                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        string productCode = worksheet.Cells[row, headers.ProductCodeColumn.Value].Text?.Trim();
                        string productName = worksheet.Cells[row, headers.ProductNameColumn.Value].Text?.Trim();

                        if (string.IsNullOrEmpty(productCode) || string.IsNullOrEmpty(productName))
                        {
                            skippedProducts++;
                            errors.AppendLine($"الصف {row}: كود المنتج أو اسم المنتج فارغ");
                            continue;
                        }

                        decimal costPrice = 0;
                        if (headers.CostPriceColumn.HasValue)
                        {
                            string costText = worksheet.Cells[row, headers.CostPriceColumn.Value].Text?.Trim();
                            if (!string.IsNullOrEmpty(costText))
                                decimal.TryParse(costText, out costPrice);
                        }

                        decimal salePrice = 0;
                        if (headers.SalePriceColumn.HasValue)
                        {
                            string saleText = worksheet.Cells[row, headers.SalePriceColumn.Value].Text?.Trim();
                            if (!string.IsNullOrEmpty(saleText))
                                decimal.TryParse(saleText, out salePrice);
                        }

                        string categoryName = null;
                        if (headers.CategoryColumn.HasValue)
                        {
                            categoryName = worksheet.Cells[row, headers.CategoryColumn.Value].Text?.Trim();
                            if (string.IsNullOrEmpty(categoryName)) categoryName = null;
                        }

                        string unit = null;
                        if (headers.UnitColumn.HasValue)
                        {
                            unit = worksheet.Cells[row, headers.UnitColumn.Value].Text?.Trim();
                            if (string.IsNullOrEmpty(unit)) unit = null;
                        }

                        string barcode = null;
                        if (headers.BarcodeColumn.HasValue)
                        {
                            barcode = worksheet.Cells[row, headers.BarcodeColumn.Value].Text?.Trim();
                            if (string.IsNullOrEmpty(barcode)) barcode = null;
                        }

                        decimal openingQuantity = 0;
                        if (headers.QuantityColumn.HasValue)
                        {
                            string qtyText = worksheet.Cells[row, headers.QuantityColumn.Value].Text?.Trim();
                            if (!string.IsNullOrEmpty(qtyText))
                                decimal.TryParse(qtyText, out openingQuantity);
                        }

                        string storeName = null;
                        int? storeId = null;
                        if (headers.StoreColumn.HasValue)
                        {
                            storeName = worksheet.Cells[row, headers.StoreColumn.Value].Text?.Trim();
                            if (!string.IsNullOrEmpty(storeName) && storeMapping.ContainsKey(storeName))
                            {
                                storeId = storeMapping[storeName];
                            }
                        }

                        var productData = new ExcelProductData
                        {
                            ProductCode = productCode,
                            ProductName = productName,
                            CostPrice = costPrice,
                            SalePrice = salePrice,
                            CategoryName = categoryName,
                            Unit = unit,
                            Barcode = barcode,
                            OpeningQuantity = openingQuantity,
                            StoreName = storeName,
                            StoreId = storeId,
                            RowNumber = row
                        };

                        productsToImport.Add(productData);
                    }
                    catch (Exception ex)
                    {
                        skippedProducts++;
                        errors.AppendLine($"الصف {row}: {ex.Message}");
                    }
                }

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var product in productsToImport)
                        {
                            try
                            {
                                bool exists = CheckProductCodeExists(connection, product.ProductCode);
                                if (exists)
                                {
                                    skippedProducts++;
                                    errors.AppendLine($"الصف {product.RowNumber}: كود المنتج '{product.ProductCode}' موجود مسبقاً");
                                    continue;
                                }

                                int? categoryId = null;
                                if (!string.IsNullOrEmpty(product.CategoryName))
                                {
                                    categoryId = GetOrCreateCategory(connection, product.CategoryName);
                                }

                                int productId = InsertProductAndGetId(connection, product, categoryId);
                                importedProducts++;

                                if (product.OpeningQuantity > 0)
                                {
                                    int targetStoreId = product.StoreId ?? GetMainStoreId(connection);
                                    if (targetStoreId > 0)
                                    {
                                        AddOpeningStockToStore(connection, productId, targetStoreId, product.OpeningQuantity, product.CostPrice);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                skippedProducts++;
                                errors.AppendLine($"الصف {product.RowNumber}: {ex.Message}");
                            }
                        }
                        transaction.Commit();
                    }
                }
            }

            result.ImportedCount = importedProducts;
            result.SkippedCount = skippedProducts;
            result.Message = $"تم الاستيراد بنجاح. تم إضافة {importedProducts} منتج جديد.";
            if (skippedProducts > 0)
            {
                result.Message += $" تم تخطي {skippedProducts} منتج مكرر أو غير صالح.";
            }

            return result;
        }

        private bool CheckProductCodeExists(SQLiteConnection connection, string productCode)
        {
            string sql = "SELECT COUNT(*) FROM Products WHERE ProductCode = @productCode AND IsActive = 1";
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@productCode", productCode);
                long count = (long)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        private int? GetOrCreateStore(SQLiteConnection connection, string storeName)
        {
            string selectSql = "SELECT StoreID FROM Stores WHERE StoreNameAr = @storeName AND IsActive = 1";
            using (var cmd = new SQLiteCommand(selectSql, connection))
            {
                cmd.Parameters.AddWithValue("@storeName", storeName);
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            string insertSql = @"
                INSERT INTO Stores (StoreCode, StoreNameAr, IsActive, CreatedDate) 
                VALUES (@storeCode, @storeName, 1, CURRENT_TIMESTAMP)";
            using (var cmd = new SQLiteCommand(insertSql, connection))
            {
                string storeCode = GenerateStoreCode(connection);
                cmd.Parameters.AddWithValue("@storeCode", storeCode);
                cmd.Parameters.AddWithValue("@storeName", storeName);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand(selectSql, connection))
            {
                cmd.Parameters.AddWithValue("@storeName", storeName);
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            return null;
        }

        private string GenerateStoreCode(SQLiteConnection connection)
        {
            string maxCodeSql = "SELECT MAX(CAST(SUBSTR(StoreCode, 5) AS INTEGER)) FROM Stores WHERE StoreCode LIKE 'STR-%'";
            using (var cmd = new SQLiteCommand(maxCodeSql, connection))
            {
                object result = cmd.ExecuteScalar();
                int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                return $"STR-{nextNumber:D6}";
            }
        }

        private int? GetOrCreateCategory(SQLiteConnection connection, string categoryName)
        {
            string selectSql = "SELECT CategoryID FROM ProductCategories WHERE CategoryNameAr = @categoryName AND IsActive = 1";
            using (var cmd = new SQLiteCommand(selectSql, connection))
            {
                cmd.Parameters.AddWithValue("@categoryName", categoryName);
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            string insertSql = "INSERT INTO ProductCategories (CategoryNameAr, CategoryCode, IsActive, CreatedDate) VALUES (@categoryName, @categoryCode, 1, CURRENT_TIMESTAMP)";
            using (var cmd = new SQLiteCommand(insertSql, connection))
            {
                string categoryCode = GenerateCategoryCode(connection);
                cmd.Parameters.AddWithValue("@categoryName", categoryName);
                cmd.Parameters.AddWithValue("@categoryCode", categoryCode);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand(selectSql, connection))
            {
                cmd.Parameters.AddWithValue("@categoryName", categoryName);
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            return null;
        }

        private string GenerateCategoryCode(SQLiteConnection connection)
        {
            string maxCodeSql = "SELECT MAX(CAST(SUBSTR(CategoryCode, 5) AS INTEGER)) FROM ProductCategories WHERE CategoryCode LIKE 'CAT-%'";
            using (var cmd = new SQLiteCommand(maxCodeSql, connection))
            {
                object result = cmd.ExecuteScalar();
                int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                return $"CAT-{nextNumber:D6}";
            }
        }

        private int InsertProductAndGetId(SQLiteConnection connection, ExcelProductData product, int? categoryId)
        {
            string insertSql = @"
                INSERT INTO Products (ProductCode, ProductNameAr, CategoryID, Unit, Barcode, CostPrice, SalePrice, IsActive, CreatedDate, CreatedBy)
                VALUES (@productCode, @productName, @categoryId, @unit, @barcode, @costPrice, @salePrice, 1, CURRENT_TIMESTAMP, @userId);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(insertSql, connection))
            {
                cmd.Parameters.AddWithValue("@productCode", product.ProductCode);
                cmd.Parameters.AddWithValue("@productName", product.ProductName);
                cmd.Parameters.AddWithValue("@categoryId", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@unit", string.IsNullOrEmpty(product.Unit) ? (object)DBNull.Value : product.Unit);
                cmd.Parameters.AddWithValue("@barcode", string.IsNullOrEmpty(product.Barcode) ? (object)DBNull.Value : product.Barcode);
                cmd.Parameters.AddWithValue("@costPrice", product.CostPrice);
                cmd.Parameters.AddWithValue("@salePrice", product.SalePrice);
                cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private int GetMainStoreId(SQLiteConnection connection)
        {
            string sql = "SELECT StoreID FROM Stores WHERE StoreCode = 'MAIN' AND IsActive = 1 LIMIT 1";
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }
            }

            string insertSql = "INSERT INTO Stores (StoreCode, StoreNameAr, IsActive, CreatedDate) VALUES ('MAIN', 'المخزن الرئيسي', 1, CURRENT_TIMESTAMP); SELECT last_insert_rowid();";
            using (var cmd = new SQLiteCommand(insertSql, connection))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void AddOpeningStockToStore(SQLiteConnection connection, int productId, int storeId, decimal quantity, decimal costPrice)
        {
            string checkStockSql = "SELECT COUNT(*) FROM StoreInventory WHERE ProductID = @productId AND StoreID = @storeId";
            using (var cmd = new SQLiteCommand(checkStockSql, connection))
            {
                cmd.Parameters.AddWithValue("@productId", productId);
                cmd.Parameters.AddWithValue("@storeId", storeId);
                long exists = (long)cmd.ExecuteScalar();

                if (exists > 0)
                {
                    string updateSql = "UPDATE StoreInventory SET Quantity = Quantity + @quantity WHERE ProductID = @productId AND StoreID = @storeId";
                    using (var updateCmd = new SQLiteCommand(updateSql, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@quantity", quantity);
                        updateCmd.Parameters.AddWithValue("@productId", productId);
                        updateCmd.Parameters.AddWithValue("@storeId", storeId);
                        updateCmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    string insertStockSql = @"
                        INSERT INTO StoreInventory (ProductID, StoreID, Quantity, CostPrice, LastUpdated) 
                        VALUES (@productId, @storeId, @quantity, @costPrice, CURRENT_TIMESTAMP)";
                    using (var insertCmd = new SQLiteCommand(insertStockSql, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@productId", productId);
                        insertCmd.Parameters.AddWithValue("@storeId", storeId);
                        insertCmd.Parameters.AddWithValue("@quantity", quantity);
                        insertCmd.Parameters.AddWithValue("@costPrice", costPrice);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void BtnCloseImport_Click(object sender, RoutedEventArgs e)
        {
            ImportOverlay.Visibility = Visibility.Collapsed;
            _selectedExcelFilePath = string.Empty;
        }

        #endregion

        #region Bulk Methods

        private void UpdateBulkBar()
        {
            int selectedCount = 0;
            foreach (var item in _productsList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            BulkText.Text = $"{selectedCount} محدد";
            BulkBar.Visibility = selectedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HeaderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isHeaderCheckBoxUpdating) return;
            _isHeaderCheckBoxUpdating = true;

            foreach (var item in _productsList)
            {
                item.IsSelected = true;
            }

            _isHeaderCheckBoxUpdating = false;
            UpdateBulkBar();
        }

        private void HeaderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHeaderCheckBoxUpdating) return;
            _isHeaderCheckBoxUpdating = true;

            foreach (var item in _productsList)
            {
                item.IsSelected = false;
            }

            _isHeaderCheckBoxUpdating = false;
            UpdateBulkBar();
        }

        private void RowCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateBulkBar();
            UpdateHeaderCheckBoxState();
        }

        private void RowCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateBulkBar();
            UpdateHeaderCheckBoxState();
        }

        private void UpdateHeaderCheckBoxState()
        {
            if (_isHeaderCheckBoxUpdating) return;

            int selectedCount = 0;
            foreach (var item in _productsList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            var headerCheckBox = FindHeaderCheckBox();
            if (headerCheckBox != null)
            {
                _isHeaderCheckBoxUpdating = true;

                if (selectedCount == 0)
                {
                    headerCheckBox.IsChecked = false;
                }
                else if (selectedCount == _productsList.Count)
                {
                    headerCheckBox.IsChecked = true;
                }
                else
                {
                    headerCheckBox.IsChecked = null;
                }

                _isHeaderCheckBoxUpdating = false;
            }
        }

        private CheckBox FindHeaderCheckBox()
        {
            if (dgProducts.Columns.Count > 0)
            {
                var column = dgProducts.Columns[0];
                if (column is DataGridTemplateColumn templateColumn)
                {
                    var headerTemplate = templateColumn.HeaderTemplate;
                    if (headerTemplate != null)
                    {
                        var content = headerTemplate.LoadContent();
                        if (content is FrameworkElement element)
                        {
                            return element.FindName("HeaderCheckBox") as CheckBox;
                        }
                    }
                }
            }
            return null;
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(300, _searchCts.Token);
                _currentPage = 1;
                await LoadProducts(txtSearch.Text.Trim());
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task BulkDelete()
        {
            int selectedCount = 0;
            foreach (var item in _productsList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            if (selectedCount == 0)
            {
                MessageBox.Show("الرجاء تحديد المنتجات التي تريد حذفها", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirmationResult = MessageBox.Show(
                $"هل أنت متأكد من حذف {selectedCount} منتج؟",
                "تأكيد الحذف الجماعي",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmationResult == MessageBoxResult.Yes)
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string deleteSql = "UPDATE Products SET IsActive = 0 WHERE ProductID = @id";
                        using (var deleteCommand = new SQLiteCommand(deleteSql, connection))
                        {
                            foreach (var item in _productsList)
                            {
                                if (item.IsSelected)
                                {
                                    deleteCommand.Parameters.Clear();
                                    deleteCommand.Parameters.AddWithValue("@id", item.ProductID);
                                    await deleteCommand.ExecuteNonQueryAsync();
                                }
                            }
                        }
                        transaction.Commit();
                    }
                }

                await LoadProducts(txtSearch.Text.Trim());

                foreach (var item in _productsList)
                {
                    item.IsSelected = false;
                }

                UpdateBulkBar();
                MessageBox.Show("تم حذف المنتجات المحددة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Load Methods

        private async Task LoadAllData()
        {
            await LoadCategories();
            await LoadProducts();
        }

        private async Task LoadCategories()
        {
            try
            {
                _categoriesList.Clear();
                _categoriesList.Add(new CategoryItem { CategoryID = 0, CategoryNameAr = "-- اختر التصنيف --" });

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string selectCategoriesSql = "SELECT CategoryID, CategoryNameAr FROM ProductCategories WHERE IsActive = 1 ORDER BY CategoryNameAr";
                    using (var categoryCommand = new SQLiteCommand(selectCategoriesSql, connection))
                    using (var categoryReader = await categoryCommand.ExecuteReaderAsync())
                    {
                        while (await categoryReader.ReadAsync())
                        {
                            _categoriesList.Add(new CategoryItem
                            {
                                CategoryID = categoryReader.GetInt32(0),
                                CategoryNameAr = categoryReader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل التصنيفات: {exception.Message}");
            }
        }

        private async Task LoadProducts(string searchText = "")
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 بدء تحميل المنتجات، الصفحة: {_currentPage}, البحث: '{searchText}'");
                _isLoading = true;

                dgProducts.Visibility = Visibility.Visible;
                lblNoData.Visibility = Visibility.Collapsed;

                _productsList.Clear();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string countSql = "SELECT COUNT(*) FROM Products WHERE IsActive = 1";
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        countSql += " AND (ProductCode LIKE @searchText OR ProductNameAr LIKE @searchText OR ProductNameEn LIKE @searchText OR Barcode LIKE @searchText)";
                    }

                    using (var countCommand = new SQLiteCommand(countSql, connection))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            countCommand.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                        }
                        _totalProductsCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
                        _totalPages = (int)Math.Ceiling((double)_totalProductsCount / _pageSize);
                        if (_totalPages == 0) _totalPages = 1;
                        lblPageInfo.Text = $"{_currentPage} / {_totalPages}";

                        int startIndex = (_currentPage - 1) * _pageSize + 1;
                        int endIndex = Math.Min(_currentPage * _pageSize, _totalProductsCount);
                        lblPaginationInfo.Text = $"عرض {startIndex} إلى {endIndex} من أصل {_totalProductsCount:N0} منتج";
                    }

                    string selectProductsSql = @"
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
                    COALESCE(si.Quantity, 0) as CurrentQuantity,
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
                    COALESCE(p.QuantityInBaseUnit, 0) as QuantityInBaseUnit
                FROM Products p
                LEFT JOIN ProductCategories pc ON p.CategoryID = pc.CategoryID
                LEFT JOIN StoreInventory si ON p.ProductID = si.ProductID AND si.StoreID = (SELECT StoreID FROM Stores WHERE StoreCode = 'MAIN' AND IsActive = 1 LIMIT 1)
                WHERE p.IsActive = 1";

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        selectProductsSql += " AND (p.ProductCode LIKE @searchText OR p.ProductNameAr LIKE @searchText OR p.ProductNameEn LIKE @searchText OR p.Barcode LIKE @searchText)";
                    }

                    selectProductsSql += " ORDER BY p.ProductCode LIMIT @pageLimit OFFSET @pageOffset";

                    using (var productCommand = new SQLiteCommand(selectProductsSql, connection))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            productCommand.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                        }
                        productCommand.Parameters.AddWithValue("@pageLimit", _pageSize);
                        productCommand.Parameters.AddWithValue("@pageOffset", (_currentPage - 1) * _pageSize);

                        using (var productReader = await productCommand.ExecuteReaderAsync())
                        {
                            while (await productReader.ReadAsync())
                            {
                                var newProduct = new ProductItem
                                {
                                    ProductID = Convert.ToInt32(productReader["ProductID"]),
                                    ProductCode = productReader["ProductCode"]?.ToString() ?? "",
                                    ProductNameAr = productReader["ProductNameAr"]?.ToString() ?? "",
                                    ProductNameEn = productReader["ProductNameEn"] != DBNull.Value ? productReader["ProductNameEn"].ToString() : "",
                                    CategoryID = productReader["CategoryID"] != DBNull.Value ? Convert.ToInt32(productReader["CategoryID"]) : 0,
                                    Unit = productReader["Unit"] != DBNull.Value ? productReader["Unit"].ToString() : "",
                                    Barcode = productReader["Barcode"] != DBNull.Value ? productReader["Barcode"].ToString() : "",
                                    CostPrice = productReader["CostPrice"] != DBNull.Value ? Convert.ToDecimal(productReader["CostPrice"]) : 0,
                                    SalePrice = productReader["SalePrice"] != DBNull.Value ? Convert.ToDecimal(productReader["SalePrice"]) : 0,
                                    WholesalePrice = productReader["WholesalePrice"] != DBNull.Value ? Convert.ToDecimal(productReader["WholesalePrice"]) : 0,
                                    MinimumQuantity = productReader["MinimumQuantity"] != DBNull.Value ? Convert.ToDecimal(productReader["MinimumQuantity"]) : 0,
                                    MaximumQuantity = productReader["MaximumQuantity"] != DBNull.Value ? Convert.ToDecimal(productReader["MaximumQuantity"]) : 0,
                                    ReorderLevel = productReader["ReorderLevel"] != DBNull.Value ? Convert.ToDecimal(productReader["ReorderLevel"]) : 0,
                                    IsActive = productReader["IsActive"] != DBNull.Value ? Convert.ToInt32(productReader["IsActive"]) == 1 : true,
                                    CategoryName = productReader["CategoryName"] != DBNull.Value ? productReader["CategoryName"].ToString() : "",
                                    CurrentQuantity = productReader["CurrentQuantity"] != DBNull.Value ? Convert.ToDecimal(productReader["CurrentQuantity"]) : 0,
                                    Unit1 = productReader["Unit1"] != DBNull.Value ? productReader["Unit1"].ToString() : "",
                                    Unit2 = productReader["Unit2"] != DBNull.Value ? productReader["Unit2"].ToString() : "",
                                    Unit3 = productReader["Unit3"] != DBNull.Value ? productReader["Unit3"].ToString() : "",
                                    Unit1Factor = productReader["Unit1Factor"] != DBNull.Value ? Convert.ToInt32(productReader["Unit1Factor"]) : 1,
                                    Unit2Factor = productReader["Unit2Factor"] != DBNull.Value ? Convert.ToInt32(productReader["Unit2Factor"]) : 1,
                                    Price1 = productReader["Price1"] != DBNull.Value ? Convert.ToDecimal(productReader["Price1"]) : 0,
                                    Price2 = productReader["Price2"] != DBNull.Value ? Convert.ToDecimal(productReader["Price2"]) : 0,
                                    Price3 = productReader["Price3"] != DBNull.Value ? Convert.ToDecimal(productReader["Price3"]) : 0,
                                    ReorderUnit = productReader["ReorderUnit"] != DBNull.Value ? productReader["ReorderUnit"].ToString() : "",
                                    ReorderLevelInBaseUnit = productReader["ReorderLevelInBaseUnit"] != DBNull.Value ? Convert.ToInt32(productReader["ReorderLevelInBaseUnit"]) : 0,
                                    QuantityInBaseUnit = productReader["QuantityInBaseUnit"] != DBNull.Value ? Convert.ToInt64(productReader["QuantityInBaseUnit"]) : 0
                                };
                                _productsList.Add(newProduct);
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_productsList.Count} منتج من أصل {_totalProductsCount}");

                lblTotalProducts.Text = _totalProductsCount.ToString("N0");
                lblTotalBadge.Text = _totalProductsCount.ToString("N0");

                if (_productsList.Count == 0)
                {
                    dgProducts.Visibility = Visibility.Collapsed;
                    lblNoData.Visibility = Visibility.Visible;
                }
                else
                {
                    dgProducts.Visibility = Visibility.Visible;
                    lblNoData.Visibility = Visibility.Collapsed;
                }

                UpdateBulkBar();
                _isLoading = false;
            }
            catch (Exception exception)
            {
                _isLoading = false;
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل المنتجات: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception.StackTrace}");

                dgProducts.Visibility = Visibility.Visible;
                lblNoData.Visibility = Visibility.Visible;
                lblNoData.Children.Clear();
                lblNoData.Children.Add(new TextBlock
                {
                    Text = $"⚠️ حدث خطأ: {exception.Message}",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.Red),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }
        }

        #endregion

        #region Helper Methods

        private async Task<string> GenerateUniqueProductCode()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string maxCodeSql = "SELECT MAX(CAST(SUBSTR(ProductCode, 5) AS INTEGER)) FROM Products WHERE ProductCode LIKE 'PRD-%'";
                using (var codeCommand = new SQLiteCommand(maxCodeSql, connection))
                {
                    object maxCodeResult = await codeCommand.ExecuteScalarAsync();
                    int nextCodeNumber = (maxCodeResult == DBNull.Value) ? 1 : Convert.ToInt32(maxCodeResult) + 1;
                    return $"PRD-{nextCodeNumber:D6}";
                }
            }
        }

        private async Task<bool> IsProductCodeExists(string productCode, int excludeProductId = 0)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string checkCodeSql = "SELECT COUNT(*) FROM Products WHERE ProductCode = @productCode AND IsActive = 1 AND ProductID != @excludeProductId";
                using (var checkCommand = new SQLiteCommand(checkCodeSql, connection))
                {
                    checkCommand.Parameters.AddWithValue("@productCode", productCode);
                    checkCommand.Parameters.AddWithValue("@excludeProductId", excludeProductId);
                    long codeCount = (long)await checkCommand.ExecuteScalarAsync();
                    return codeCount > 0;
                }
            }
        }

        private void ShowDialog(Window dialogWindow)
        {
            var parentWindow = Window.GetWindow(this);

            if (parentWindow != null)
            {
                var blurEffect = new BlurEffect
                {
                    Radius = 8,
                    KernelType = KernelType.Gaussian
                };
                parentWindow.Effect = blurEffect;
                parentWindow.IsEnabled = false;

                dialogWindow.Owner = parentWindow;
                dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialogWindow.ShowDialog();

                parentWindow.Effect = null;
                parentWindow.IsEnabled = true;
                parentWindow.Activate();
                parentWindow.Focus();
            }
            else
            {
                dialogWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialogWindow.ShowDialog();
            }
        }

        #endregion

        #region Product Dialog Methods

        private async Task OpenAddProductDialog()
        {
            var dialog = new MetroWindow
            {
                Width = 680,
                Height = 580,
                Title = "إضافة منتج جديد",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ShowTitleBar = true,
                ShowCloseButton = true,
                ShowMinButton = false,
                ShowMaxRestoreButton = false,
                FlowDirection = FlowDirection.RightToLeft,
                Topmost = true,
                ShowInTaskbar = false
            };

            string generatedCode = await GenerateUniqueProductCode();
            var content = CreateProductDialogContent(false, dialog, generatedCode, null);
            dialog.Content = content;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                await LoadAllData();
                MessageBox.Show("تم إضافة المنتج بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task OpenEditProductDialog(int productId)
        {
            ProductItem productToEdit = null;
            foreach (var currentProduct in _productsList)
            {
                if (currentProduct.ProductID == productId)
                {
                    productToEdit = currentProduct;
                    break;
                }
            }

            if (productToEdit == null) return;

            _selectedProduct = productToEdit;

            var dialog = new MetroWindow
            {
                Width = 680,
                Height = 580,
                Title = "تعديل منتج",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ShowTitleBar = true,
                ShowCloseButton = true,
                ShowMinButton = false,
                ShowMaxRestoreButton = false,
                FlowDirection = FlowDirection.RightToLeft,
                Topmost = true,
                ShowInTaskbar = false
            };

            var content = CreateProductDialogContent(true, dialog, null, productToEdit);
            dialog.Content = content;

            var result = dialog.ShowDialog();

            if (result == true)
            {
                await LoadAllData();
                MessageBox.Show("تم تعديل المنتج بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ============================================================
        // ✅ CreateProductDialogContent - مع الحساب التلقائي للأسعار
        // ============================================================
        private Border CreateProductDialogContent(bool isEditMode, MetroWindow dialogWindow, string generatedProductCode, ProductItem existingProduct)
        {
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.15,
                    Color = Colors.Black
                }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(12, 8, 12, 12)
            };

            var mainContainer = new StackPanel();

            // ============================================================
            // ✅ الصف الأول: الباركود - كود المنتج - اسم المنتج
            // ============================================================
            var topRowGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            topRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var barcodePanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            barcodePanel.Children.Add(new TextBlock
            {
                Text = "الباركود",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var barcodeBox = new TextBox
            {
                Height = 30,
                FontSize = 12,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            barcodePanel.Children.Add(barcodeBox);
            Grid.SetColumn(barcodePanel, 0);
            topRowGrid.Children.Add(barcodePanel);

            var codePanel = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            codePanel.Children.Add(new TextBlock
            {
                Text = "كود المنتج",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var codeBox = new TextBox
            {
                Height = 30,
                FontSize = 12,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            codePanel.Children.Add(codeBox);
            Grid.SetColumn(codePanel, 1);
            topRowGrid.Children.Add(codePanel);

            var namePanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            namePanel.Children.Add(new TextBlock
            {
                Text = "اسم المنتج",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var nameBox = new TextBox
            {
                Height = 30,
                FontSize = 12,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            namePanel.Children.Add(nameBox);
            Grid.SetColumn(namePanel, 2);
            topRowGrid.Children.Add(namePanel);

            mainContainer.Children.Add(topRowGrid);

            // ============================================================
            // ✅ الفاصل
            // ============================================================
            mainContainer.Children.Add(new Separator
            {
                Margin = new Thickness(0, 4, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            });

            // ============================================================
            // ✅ عنوان الوحدات مع توضيح
            // ============================================================
            var unitHeaderPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            unitHeaderPanel.Children.Add(new TextBlock
            {
                Text = "الوحدات والأسعار (النظام الهرمي)",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // ✅ إضافة توضيح إضافي
            unitHeaderPanel.Children.Add(new TextBlock
            {
                Text = "الوحدة الأولى = (عامل تحويل ١) × الوحدة الثانية  |  الوحدة الثانية = (عامل تحويل ٢) × الوحدة الثالثة (الأساسية)",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            mainContainer.Children.Add(unitHeaderPanel);

            // ============================================================
            // ✅ الوحدة الثالثة (الأساسية)
            // ============================================================
            var unit3Grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            unit3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            unit3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unit3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var unit3NamePanel = new StackPanel();
            unit3NamePanel.Children.Add(new TextBlock
            {
                Text = "الوحدة الثالثة (الأساسية)",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit3Box = new TextBox
            {
                Height = 28,
                FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit3NamePanel.Children.Add(unit3Box);
            Grid.SetColumn(unit3NamePanel, 0);
            unit3Grid.Children.Add(unit3NamePanel);

            var unit3FactorPanel = new StackPanel();
            unit3FactorPanel.Children.Add(new TextBlock
            {
                Text = "= 1 (أساسية)",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit3FactorBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "1",
                IsEnabled = false,
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit3FactorPanel.Children.Add(unit3FactorBox);
            Grid.SetColumn(unit3FactorPanel, 1);
            unit3Grid.Children.Add(unit3FactorPanel);

            var unit3PricePanel = new StackPanel();
            unit3PricePanel.Children.Add(new TextBlock
            {
                Text = "السعر",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit3PriceBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "0",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            unit3PricePanel.Children.Add(unit3PriceBox);
            Grid.SetColumn(unit3PricePanel, 2);
            unit3Grid.Children.Add(unit3PricePanel);
            mainContainer.Children.Add(unit3Grid);

            // ============================================================
            // ✅ الوحدة الثانية (الوسطى) مع توضيح
            // ============================================================
            var unit2Grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            unit2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            unit2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unit2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var unit2NamePanel = new StackPanel();
            unit2NamePanel.Children.Add(new TextBlock
            {
                Text = "الوحدة الثانية (الوسطى)",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit2Box = new TextBox
            {
                Height = 28,
                FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit2NamePanel.Children.Add(unit2Box);
            Grid.SetColumn(unit2NamePanel, 0);
            unit2Grid.Children.Add(unit2NamePanel);

            var unit2FactorPanel = new StackPanel();
            unit2FactorPanel.Children.Add(new TextBlock
            {
                Text = "= ? × الأساسية",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit2FactorBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "12",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit2FactorPanel.Children.Add(unit2FactorBox);
            Grid.SetColumn(unit2FactorPanel, 1);
            unit2Grid.Children.Add(unit2FactorPanel);

            var unit2PricePanel = new StackPanel();
            unit2PricePanel.Children.Add(new TextBlock
            {
                Text = "السعر",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit2PriceBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "0",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            unit2PricePanel.Children.Add(unit2PriceBox);
            Grid.SetColumn(unit2PricePanel, 2);
            unit2Grid.Children.Add(unit2PricePanel);
            mainContainer.Children.Add(unit2Grid);

            // ============================================================
            // ✅ الوحدة الأولى (الكبرى) مع توضيح
            // ============================================================
            var unit1Grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            unit1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            unit1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            unit1Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var unit1NamePanel = new StackPanel();
            unit1NamePanel.Children.Add(new TextBlock
            {
                Text = "الوحدة الأولى (الكبرى)",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit1Box = new TextBox
            {
                Height = 28,
                FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit1NamePanel.Children.Add(unit1Box);
            Grid.SetColumn(unit1NamePanel, 0);
            unit1Grid.Children.Add(unit1NamePanel);

            var unit1FactorPanel = new StackPanel();
            unit1FactorPanel.Children.Add(new TextBlock
            {
                Text = "= ? × الوحدة الثانية",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit1FactorBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "12",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            unit1FactorPanel.Children.Add(unit1FactorBox);
            Grid.SetColumn(unit1FactorPanel, 1);
            unit1Grid.Children.Add(unit1FactorPanel);

            var unit1PricePanel = new StackPanel();
            unit1PricePanel.Children.Add(new TextBlock
            {
                Text = "السعر",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var unit1PriceBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "0",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            unit1PricePanel.Children.Add(unit1PriceBox);
            Grid.SetColumn(unit1PricePanel, 2);
            unit1Grid.Children.Add(unit1PricePanel);
            mainContainer.Children.Add(unit1Grid);

            // ============================================================
            // ✅ الفاصل
            // ============================================================
            mainContainer.Children.Add(new Separator
            {
                Margin = new Thickness(0, 4, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            });

            // ============================================================
            // ✅ الصف السفلي: حد الطلب - الكمية - وحدة العرض
            // ============================================================
            var bottomRowGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            bottomRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var reorderLevelPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            reorderLevelPanel.Children.Add(new TextBlock
            {
                Text = "حد الطلب",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var reorderLevelBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "0",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            reorderLevelPanel.Children.Add(reorderLevelBox);
            Grid.SetColumn(reorderLevelPanel, 0);
            bottomRowGrid.Children.Add(reorderLevelPanel);

            var quantityPanel = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            quantityPanel.Children.Add(new TextBlock
            {
                Text = "الكمية الحالية",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var quantityBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                Text = "0",
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
            quantityPanel.Children.Add(quantityBox);
            Grid.SetColumn(quantityPanel, 1);
            bottomRowGrid.Children.Add(quantityPanel);

            var reorderUnitPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            reorderUnitPanel.Children.Add(new TextBlock
            {
                Text = "وحدة العرض",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 3)
            });
            var reorderUnitBox = new TextBox
            {
                Height = 28,
                FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Text = ""
            };
            reorderUnitPanel.Children.Add(reorderUnitBox);
            Grid.SetColumn(reorderUnitPanel, 2);
            bottomRowGrid.Children.Add(reorderUnitPanel);
            mainContainer.Children.Add(bottomRowGrid);

            // ============================================================
            // ✅ الأزرار
            // ============================================================
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var saveBtn = new Button
            {
                Content = "حفظ",
                Width = 90,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };

            var cancelBtn = new Button
            {
                Content = "إلغاء",
                Width = 90,
                Height = 32,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12
            };

            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            mainContainer.Children.Add(buttonPanel);

            scrollViewer.Content = mainContainer;
            mainGrid.Children.Add(scrollViewer);
            Grid.SetRow(scrollViewer, 0);

            mainBorder.Child = mainGrid;

            // ============================================================
            // ✅ منطق الباركود
            // ============================================================
            string _lastProcessedBarcode = string.Empty;
            DateTime _lastProcessTime = DateTime.Now;
            TimeSpan _debounceTime = TimeSpan.FromMilliseconds(500);
            System.Windows.Threading.DispatcherTimer _autoMoveTimer = null;

            void ProcessBarcode(string barcode)
            {
                if (string.IsNullOrWhiteSpace(barcode)) return;
                if (barcode == _lastProcessedBarcode && (DateTime.Now - _lastProcessTime) < _debounceTime) return;

                _lastProcessedBarcode = barcode;
                _lastProcessTime = DateTime.Now;
                barcodeBox.Text = barcode;
                PlayBeep();
                nameBox?.Focus();
            }

            barcodeBox.TextChanged += (s, e) =>
            {
                string currentText = barcodeBox.Text.Trim();
                if (string.IsNullOrEmpty(currentText)) return;
                if (currentText.Length >= 3)
                {
                    _autoMoveTimer?.Stop();
                    _autoMoveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    _autoMoveTimer.Tick += (timerSender, timerArgs) =>
                    {
                        _autoMoveTimer?.Stop();
                        string finalText = barcodeBox.Text.Trim();
                        if (!string.IsNullOrEmpty(finalText) && finalText != _lastProcessedBarcode)
                            ProcessBarcode(finalText);
                    };
                    _autoMoveTimer.Start();
                }
            };

            barcodeBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    string barcode = barcodeBox.Text.Trim();
                    if (!string.IsNullOrEmpty(barcode)) ProcessBarcode(barcode);
                }
            };

            barcodeBox.LostKeyboardFocus += (s, e) =>
            {
                string barcode = barcodeBox.Text.Trim();
                if (!string.IsNullOrEmpty(barcode) && barcode != _lastProcessedBarcode) ProcessBarcode(barcode);
            };

            barcodeBox.Loaded += (s, e) => { barcodeBox.Focus(); };

            // ============================================================
            // ✅ ملء البيانات في حالة التعديل
            // ============================================================
            if (isEditMode && existingProduct != null)
            {
                barcodeBox.Text = existingProduct.Barcode ?? "";
                codeBox.Text = existingProduct.ProductCode;
                nameBox.Text = existingProduct.ProductNameAr;
                unit3Box.Text = existingProduct.Unit3 ?? "";
                unit2Box.Text = existingProduct.Unit2 ?? "";
                unit1Box.Text = existingProduct.Unit1 ?? "";
                unit3PriceBox.Text = existingProduct.Price3.ToString("N2");
                unit2FactorBox.Text = existingProduct.Unit2Factor.ToString();
                unit2PriceBox.Text = existingProduct.Price2.ToString("N2");
                unit1FactorBox.Text = existingProduct.Unit1Factor.ToString();
                unit1PriceBox.Text = existingProduct.Price1.ToString("N2");
                reorderLevelBox.Text = existingProduct.ReorderLevelInBaseUnit.ToString();
                quantityBox.Text = existingProduct.QuantityInBaseUnit.ToString();
                reorderUnitBox.Text = existingProduct.ReorderUnit ?? "";
            }
            else
            {
                codeBox.Text = generatedProductCode;
                barcodeBox.Focus();
                unit3PriceBox.Focus();
            }

            // ============================================================
            // ✅ الحساب التلقائي للأسعار
            // ============================================================

            bool _isUpdatingPrices = false;

            void CalculatePricesFromPrice3()
            {
                if (_isUpdatingPrices) return;
                _isUpdatingPrices = true;

                try
                {
                    if (decimal.TryParse(unit3PriceBox.Text.Trim(), out decimal price3) && price3 > 0)
                    {
                        if (int.TryParse(unit2FactorBox.Text.Trim(), out int unit2Factor) && unit2Factor > 0)
                        {
                            decimal price2 = price3 * unit2Factor;
                            unit2PriceBox.Text = price2.ToString("N2");

                            if (int.TryParse(unit1FactorBox.Text.Trim(), out int unit1Factor) && unit1Factor > 0)
                            {
                                decimal price1 = price2 * unit1Factor;
                                unit1PriceBox.Text = price1.ToString("N2");
                            }
                        }
                    }
                }
                finally
                {
                    _isUpdatingPrices = false;
                }
            }

            void CalculatePrice1FromPrice2()
            {
                if (_isUpdatingPrices) return;
                _isUpdatingPrices = true;

                try
                {
                    if (decimal.TryParse(unit2PriceBox.Text.Trim(), out decimal price2) && price2 > 0)
                    {
                        if (int.TryParse(unit1FactorBox.Text.Trim(), out int unit1Factor) && unit1Factor > 0)
                        {
                            decimal price1 = price2 * unit1Factor;
                            unit1PriceBox.Text = price1.ToString("N2");
                        }
                    }
                }
                finally
                {
                    _isUpdatingPrices = false;
                }
            }

            void CalculatePrice2FromPrice1()
            {
                if (_isUpdatingPrices) return;
                _isUpdatingPrices = true;

                try
                {
                    if (decimal.TryParse(unit1PriceBox.Text.Trim(), out decimal price1) && price1 > 0)
                    {
                        if (int.TryParse(unit1FactorBox.Text.Trim(), out int unit1Factor) && unit1Factor > 0)
                        {
                            decimal price2 = price1 / unit1Factor;
                            unit2PriceBox.Text = price2.ToString("N2");

                            if (int.TryParse(unit2FactorBox.Text.Trim(), out int unit2Factor) && unit2Factor > 0)
                            {
                                decimal price3 = price2 / unit2Factor;
                                unit3PriceBox.Text = price3.ToString("N2");
                            }
                        }
                    }
                }
                finally
                {
                    _isUpdatingPrices = false;
                }
            }

            unit3PriceBox.TextChanged += (s, e) =>
            {
                if (!_isUpdatingPrices)
                {
                    CalculatePricesFromPrice3();
                }
            };

            unit2FactorBox.TextChanged += (s, e) =>
            {
                if (!_isUpdatingPrices)
                {
                    if (decimal.TryParse(unit3PriceBox.Text.Trim(), out decimal price3) && price3 > 0)
                    {
                        CalculatePricesFromPrice3();
                    }
                    else if (decimal.TryParse(unit2PriceBox.Text.Trim(), out decimal price2) && price2 > 0)
                    {
                        CalculatePrice1FromPrice2();
                    }
                }
            };

            unit1FactorBox.TextChanged += (s, e) =>
            {
                if (!_isUpdatingPrices)
                {
                    if (decimal.TryParse(unit2PriceBox.Text.Trim(), out decimal price2) && price2 > 0)
                    {
                        CalculatePrice1FromPrice2();
                    }
                    else if (decimal.TryParse(unit3PriceBox.Text.Trim(), out decimal price3) && price3 > 0)
                    {
                        CalculatePricesFromPrice3();
                    }
                }
            };

            unit2PriceBox.TextChanged += (s, e) =>
            {
                if (!_isUpdatingPrices)
                {
                    CalculatePrice1FromPrice2();
                }
            };

            unit1PriceBox.TextChanged += (s, e) =>
            {
                if (!_isUpdatingPrices)
                {
                    CalculatePrice2FromPrice1();
                }
            };

            // ============================================================
            // ✅ حدث الحفظ
            // ============================================================
            bool isSaving = false;

            saveBtn.Click += async (s, ev) =>
            {
                if (isSaving) return;
                isSaving = true;

                try
                {
                    string barcode = barcodeBox.Text.Trim();
                    string code = codeBox.Text.Trim();
                    string name = nameBox.Text.Trim();
                    string unit1 = unit1Box.Text.Trim();
                    string unit2 = unit2Box.Text.Trim();
                    string unit3 = unit3Box.Text.Trim();
                    string reorderUnit = reorderUnitBox.Text.Trim();

                    if (!int.TryParse(unit1FactorBox.Text.Trim(), out int unit1Factor) || unit1Factor <= 0)
                    {
                        MessageBox.Show("عامل تحويل الوحدة الأولى غير صحيح (يجب أن يكون رقماً موجباً)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit1FactorBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!int.TryParse(unit2FactorBox.Text.Trim(), out int unit2Factor) || unit2Factor <= 0)
                    {
                        MessageBox.Show("عامل تحويل الوحدة الثانية غير صحيح (يجب أن يكون رقماً موجباً)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit2FactorBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!decimal.TryParse(unit1PriceBox.Text.Trim(), out decimal price1) || price1 < 0)
                    {
                        MessageBox.Show("سعر الوحدة الأولى غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit1PriceBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!decimal.TryParse(unit2PriceBox.Text.Trim(), out decimal price2) || price2 < 0)
                    {
                        MessageBox.Show("سعر الوحدة الثانية غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit2PriceBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!decimal.TryParse(unit3PriceBox.Text.Trim(), out decimal price3) || price3 < 0)
                    {
                        MessageBox.Show("سعر الوحدة الثالثة غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit3PriceBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!long.TryParse(quantityBox.Text.Trim(), out long quantityInBaseUnit) || quantityInBaseUnit < 0)
                    {
                        MessageBox.Show("الكمية غير صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        quantityBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (!int.TryParse(reorderLevelBox.Text.Trim(), out int reorderLevelInBaseUnit) || reorderLevelInBaseUnit < 0)
                    {
                        MessageBox.Show("حد الطلب غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        reorderLevelBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (string.IsNullOrEmpty(code))
                    {
                        MessageBox.Show("يرجى إدخال كود المنتج", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        codeBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("يرجى إدخال اسم المنتج", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        nameBox.Focus();
                        isSaving = false;
                        return;
                    }
                    if (string.IsNullOrEmpty(unit1))
                    {
                        MessageBox.Show("يرجى إدخال الوحدة الأولى", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit1Box.Focus();
                        isSaving = false;
                        return;
                    }
                    if (string.IsNullOrEmpty(unit3))
                    {
                        MessageBox.Show("يرجى إدخال الوحدة الثالثة (الأساسية)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        unit3Box.Focus();
                        isSaving = false;
                        return;
                    }

                    int excludeId = isEditMode && existingProduct != null ? existingProduct.ProductID : 0;
                    bool codeExists = await IsProductCodeExists(code, excludeId);

                    if (codeExists)
                    {
                        MessageBox.Show($"كود المنتج '{code}' موجود مسبقاً. يرجى إدخال كود مختلف.", "كود مكرر", MessageBoxButton.OK, MessageBoxImage.Warning);
                        codeBox.Focus();
                        isSaving = false;
                        return;
                    }

                    if (isEditMode && existingProduct != null)
                    {
                        existingProduct.Barcode = barcode;
                        existingProduct.ProductCode = code;
                        existingProduct.ProductNameAr = name;
                        existingProduct.Unit1 = unit1;
                        existingProduct.Unit2 = unit2;
                        existingProduct.Unit3 = unit3;
                        existingProduct.Unit1Factor = unit1Factor;
                        existingProduct.Unit2Factor = unit2Factor;
                        existingProduct.Price1 = price1;
                        existingProduct.Price2 = price2;
                        existingProduct.Price3 = price3;
                        existingProduct.QuantityInBaseUnit = quantityInBaseUnit;
                        existingProduct.ReorderLevelInBaseUnit = reorderLevelInBaseUnit;
                        existingProduct.ReorderUnit = reorderUnit;

                        await UpdateProduct(existingProduct);
                    }
                    else
                    {
                        await AddProduct(code, name, unit1, unit2, unit3, unit1Factor, unit2Factor, price1, price2, price3, quantityInBaseUnit, reorderLevelInBaseUnit, barcode);
                    }

                    dialogWindow.DialogResult = true;
                    dialogWindow.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    isSaving = false;
                }
            };

            cancelBtn.Click += (s, ev) =>
            {
                dialogWindow.DialogResult = false;
                dialogWindow.Close();
            };

            return mainBorder;
        }

        private void PlayBeep()
        {
            try
            {
                System.Media.SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayBeep Error: {ex.Message}");
                try { Console.Beep(1200, 250); } catch { }
            }
        }

        #endregion

        #region Product CRUD Methods

        private async Task AddProduct(
            string productCode,
            string productName,
            string unit1,
            string unit2,
            string unit3,
            int unit1Factor,
            int unit2Factor,
            decimal price1,
            decimal price2,
            decimal price3,
            long quantityInBaseUnit,
            int reorderLevel,
            string barcode = "")
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string insertProductSql = @"
                    INSERT INTO Products (
                        ProductCode, ProductNameAr, 
                        Unit1, Unit2, Unit3,
                        Unit1Factor, Unit2Factor,
                        Price1, Price2, Price3,
                        QuantityInBaseUnit, ReorderLevelInBaseUnit,
                        Barcode,
                        IsActive, CreatedDate, CreatedBy
                    ) VALUES (
                        @productCode, @productName,
                        @unit1, @unit2, @unit3,
                        @unit1Factor, @unit2Factor,
                        @price1, @price2, @price3,
                        @quantity, @reorderLevel,
                        @barcode,
                        1, CURRENT_TIMESTAMP, @userId)";

                using (var insertCommand = new SQLiteCommand(insertProductSql, connection))
                {
                    insertCommand.Parameters.AddWithValue("@productCode", productCode);
                    insertCommand.Parameters.AddWithValue("@productName", productName);
                    insertCommand.Parameters.AddWithValue("@unit1", unit1 ?? "");
                    insertCommand.Parameters.AddWithValue("@unit2", unit2 ?? "");
                    insertCommand.Parameters.AddWithValue("@unit3", unit3 ?? "");
                    insertCommand.Parameters.AddWithValue("@unit1Factor", unit1Factor);
                    insertCommand.Parameters.AddWithValue("@unit2Factor", unit2Factor);
                    insertCommand.Parameters.AddWithValue("@price1", price1);
                    insertCommand.Parameters.AddWithValue("@price2", price2);
                    insertCommand.Parameters.AddWithValue("@price3", price3);
                    insertCommand.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                    insertCommand.Parameters.AddWithValue("@reorderLevel", reorderLevel);
                    insertCommand.Parameters.AddWithValue("@barcode", barcode ?? "");
                    insertCommand.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateProduct(ProductItem product)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string updateProductSql = @"
                    UPDATE Products SET 
                        ProductCode = @productCode,
                        ProductNameAr = @productName,
                        Unit1 = @unit1,
                        Unit2 = @unit2,
                        Unit3 = @unit3,
                        Unit1Factor = @unit1Factor,
                        Unit2Factor = @unit2Factor,
                        Price1 = @price1,
                        Price2 = @price2,
                        Price3 = @price3,
                        QuantityInBaseUnit = @quantity,
                        ReorderLevelInBaseUnit = @reorderLevel,
                        Barcode = @barcode,
                        ReorderUnit = @reorderUnit,
                        ModifiedDate = CURRENT_TIMESTAMP
                    WHERE ProductID = @productId";

                using (var updateCommand = new SQLiteCommand(updateProductSql, connection))
                {
                    updateCommand.Parameters.AddWithValue("@productCode", product.ProductCode);
                    updateCommand.Parameters.AddWithValue("@productName", product.ProductNameAr);
                    updateCommand.Parameters.AddWithValue("@unit1", product.Unit1 ?? "");
                    updateCommand.Parameters.AddWithValue("@unit2", product.Unit2 ?? "");
                    updateCommand.Parameters.AddWithValue("@unit3", product.Unit3 ?? "");
                    updateCommand.Parameters.AddWithValue("@unit1Factor", product.Unit1Factor);
                    updateCommand.Parameters.AddWithValue("@unit2Factor", product.Unit2Factor);
                    updateCommand.Parameters.AddWithValue("@price1", product.Price1);
                    updateCommand.Parameters.AddWithValue("@price2", product.Price2);
                    updateCommand.Parameters.AddWithValue("@price3", product.Price3);
                    updateCommand.Parameters.AddWithValue("@quantity", product.QuantityInBaseUnit);
                    updateCommand.Parameters.AddWithValue("@reorderLevel", product.ReorderLevelInBaseUnit);
                    updateCommand.Parameters.AddWithValue("@barcode", product.Barcode ?? "");
                    updateCommand.Parameters.AddWithValue("@reorderUnit", product.ReorderUnit ?? "");
                    updateCommand.Parameters.AddWithValue("@productId", product.ProductID);

                    await updateCommand.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task DeleteProduct(int productId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string deleteProductSql = "UPDATE Products SET IsActive = 0 WHERE ProductID = @productId";
                using (var deleteCommand = new SQLiteCommand(deleteProductSql, connection))
                {
                    deleteCommand.Parameters.AddWithValue("@productId", productId);
                    await deleteCommand.ExecuteNonQueryAsync();
                }
            }
            await LoadProducts(txtSearch.Text.Trim());
            MessageBox.Show("تم حذف المنتج بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Row Click Handlers

        private async void EditRow_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null && clickedButton.Tag != null)
            {
                int selectedProductId = (int)clickedButton.Tag;
                await OpenEditProductDialog(selectedProductId);
            }
        }

        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null && clickedButton.Tag != null)
            {
                int selectedProductId = (int)clickedButton.Tag;

                MessageBoxResult userConfirmation = MessageBox.Show(
                    "هل أنت متأكد من حذف هذا المنتج؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (userConfirmation == MessageBoxResult.Yes)
                {
                    await DeleteProduct(selectedProductId);
                }
            }
        }

        private async void AddStock_Click(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            if (clickedButton != null && clickedButton.Tag != null)
            {
                int selectedProductId = (int)clickedButton.Tag;
                await OpenAddStockDialog(selectedProductId);
            }
        }

        #endregion

        #region Add Stock Dialog

        private async Task OpenAddStockDialog(int productId)
        {
            ProductItem selectedProduct = null;
            foreach (var currentProduct in _productsList)
            {
                if (currentProduct.ProductID == productId)
                {
                    selectedProduct = currentProduct;
                    break;
                }
            }

            if (selectedProduct == null)
            {
                MessageBox.Show("لم يتم العثور على المنتج", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var storesList = new ObservableCollection<StoreSimple>();
            storesList.Add(new StoreSimple { Id = 0, Name = "-- اختر المخزن --" });

            using (var connection = new SQLiteConnection(_connectionString))
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

            if (storesList.Count == 1)
            {
                MessageBox.Show("لا توجد مخازن نشطة في النظام. الرجاء إضافة مخزن أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var stockDialog = new Window
            {
                Width = 450,
                Height = 520,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var dialogMainBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 25,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    Color = Colors.Black
                }
            };

            var dialogGrid = new Grid();
            dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var dialogTopBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Height = 8
            };
            dialogGrid.Children.Add(dialogTopBar);
            Grid.SetRow(dialogTopBar, 0);

            var dialogContentGrid = new Grid
            {
                Margin = new Thickness(25, 20, 25, 20)
            };
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            dialogContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var dialogTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            dialogTitlePanel.Children.Add(new TextBlock { Text = "📦", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            dialogTitlePanel.Children.Add(new TextBlock
            {
                Text = "إضافة رصيد افتتاحي للمنتج",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            });
            Grid.SetRow(dialogTitlePanel, 0);
            dialogContentGrid.Children.Add(dialogTitlePanel);

            var productInfoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var productInfoGrid = new Grid();
            productInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            productInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            productInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            productInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var productCodeLabel = new TextBlock { Text = "كود المنتج:", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(productCodeLabel, 0);
            Grid.SetColumn(productCodeLabel, 0);
            productInfoGrid.Children.Add(productCodeLabel);

            var productCodeValue = new TextBlock { Text = selectedProduct.ProductCode, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetRow(productCodeValue, 0);
            Grid.SetColumn(productCodeValue, 1);
            productInfoGrid.Children.Add(productCodeValue);

            var productNameLabel = new TextBlock { Text = "اسم المنتج:", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(productNameLabel, 1);
            Grid.SetColumn(productNameLabel, 0);
            productInfoGrid.Children.Add(productNameLabel);

            var productNameValue = new TextBlock { Text = selectedProduct.ProductNameAr, FontSize = 13, Foreground = Brushes.Black, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 8, 0, 0) };
            Grid.SetRow(productNameValue, 1);
            Grid.SetColumn(productNameValue, 1);
            productInfoGrid.Children.Add(productNameValue);

            productInfoBorder.Child = productInfoGrid;
            Grid.SetRow(productInfoBorder, 1);
            dialogContentGrid.Children.Add(productInfoBorder);

            var storeLabel = new TextBlock
            {
                Text = "اختر المخزن:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(storeLabel, 2);
            dialogContentGrid.Children.Add(storeLabel);

            var storeComboBox = new ComboBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15)
            };
            storeComboBox.ItemsSource = storesList;
            storeComboBox.DisplayMemberPath = "Name";
            storeComboBox.SelectedValuePath = "Id";
            if (storesList.Count > 1)
            {
                storeComboBox.SelectedIndex = 1;
            }
            Grid.SetRow(storeComboBox, 3);
            dialogContentGrid.Children.Add(storeComboBox);

            var currentStockBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var currentStockGrid = new Grid();
            currentStockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            currentStockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var currentStockLabel = new TextBlock { Text = "الرصيد الحالي:", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(currentStockLabel, 0);
            currentStockGrid.Children.Add(currentStockLabel);

            var currentStockValue = new TextBlock
            {
                Text = selectedProduct.CurrentQuantity.ToString("N2"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(currentStockValue, 1);
            currentStockGrid.Children.Add(currentStockValue);

            currentStockBorder.Child = currentStockGrid;
            Grid.SetRow(currentStockBorder, 4);
            dialogContentGrid.Children.Add(currentStockBorder);

            var quantityLabel = new TextBlock
            {
                Text = "الكمية المراد إضافتها:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(quantityLabel, 5);
            dialogContentGrid.Children.Add(quantityLabel);

            var quantityTextBox = new TextBox
            {
                Height = 40,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(quantityTextBox, 6);
            dialogContentGrid.Children.Add(quantityTextBox);

            var costPriceLabel = new TextBlock
            {
                Text = "سعر الشراء (اختياري):",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(costPriceLabel, 7);
            dialogContentGrid.Children.Add(costPriceLabel);

            var costPriceTextBox = new TextBox
            {
                Height = 40,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Text = "0",
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(costPriceTextBox, 8);
            dialogContentGrid.Children.Add(costPriceTextBox);

            var dialogButtonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(dialogButtonPanel, 9);
            dialogContentGrid.Children.Add(dialogButtonPanel);

            var saveStockButton = new Button
            {
                Content = "حفظ الرصيد",
                Width = 120,
                Height = 38,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };

            var saveStockButtonBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = saveStockButton
            };

            var cancelStockButton = new Button
            {
                Content = "إلغاء",
                Width = 100,
                Height = 38,
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };

            var cancelStockButtonBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = cancelStockButton
            };

            dialogButtonPanel.Children.Add(saveStockButtonBorder);
            dialogButtonPanel.Children.Add(cancelStockButtonBorder);

            dialogGrid.Children.Add(dialogContentGrid);
            Grid.SetRow(dialogContentGrid, 1);

            dialogMainBorder.Child = dialogGrid;
            stockDialog.Content = dialogMainBorder;

            quantityTextBox.Focus();

            storeComboBox.SelectionChanged += async (s, ev) =>
            {
                if (storeComboBox.SelectedItem is StoreSimple selectedStore && selectedStore.Id > 0)
                {
                    decimal stock = await _dbService.GetCurrentStockInStoreAsync(selectedProduct.ProductID, selectedStore.Id);
                    currentStockValue.Text = stock.ToString("N2");
                }
                else
                {
                    currentStockValue.Text = selectedProduct.CurrentQuantity.ToString("N2");
                }
            };

            saveStockButton.Click += async (sender, eventArgs) =>
            {
                if (!decimal.TryParse(quantityTextBox.Text, out decimal enteredQuantity) || enteredQuantity <= 0)
                {
                    MessageBox.Show("الرجاء إدخال كمية صحيحة أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    quantityTextBox.Focus();
                    return;
                }

                if (storeComboBox.SelectedItem == null || (storeComboBox.SelectedItem is StoreSimple selectedStore && selectedStore.Id == 0))
                {
                    MessageBox.Show("الرجاء اختيار المخزن", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int selectedStoreId = ((StoreSimple)storeComboBox.SelectedItem).Id;
                decimal enteredCostPriceValue = 0;
                if (!string.IsNullOrWhiteSpace(costPriceTextBox.Text))
                {
                    decimal.TryParse(costPriceTextBox.Text, out enteredCostPriceValue);
                }

                bool operationSuccess = await _dbService.AddOpeningStockAsync(selectedProduct.ProductID, selectedStoreId, enteredQuantity, enteredCostPriceValue);

                if (operationSuccess)
                {
                    MessageBox.Show($"تم إضافة {enteredQuantity:N2} وحدة بنجاح إلى رصيد المنتج في المخزن {((StoreSimple)storeComboBox.SelectedItem).Name}", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    stockDialog.DialogResult = true;
                    stockDialog.Close();
                    await LoadProducts(txtSearch.Text.Trim());
                }
                else
                {
                    MessageBox.Show("فشل في إضافة الرصيد. يرجى المحاولة مرة أخرى.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelStockButton.Click += (sender, eventArgs) =>
            {
                stockDialog.DialogResult = false;
                stockDialog.Close();
            };

            ShowDialog(stockDialog);
        }

        #endregion
    }

    // ============================================================
    // ✅ الكلاسات المساعدة للاستيراد من Excel
    // ============================================================

    public class StoreSimple
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }

    public class ExcelColumnMapping
    {
        public int? ProductCodeColumn { get; set; }
        public int? ProductNameColumn { get; set; }
        public int? CostPriceColumn { get; set; }
        public int? SalePriceColumn { get; set; }
        public int? CategoryColumn { get; set; }
        public int? UnitColumn { get; set; }
        public int? BarcodeColumn { get; set; }
        public int? QuantityColumn { get; set; }
        public int? StoreColumn { get; set; }
    }

    public class ExcelProductData
    {
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public string CategoryName { get; set; }
        public string Unit { get; set; }
        public string Barcode { get; set; }
        public decimal OpeningQuantity { get; set; }
        public string StoreName { get; set; }
        public int? StoreId { get; set; }
        public int RowNumber { get; set; }
    }

    public class ImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
    }

    // ============================================================
    // ✅ كلاس المنتج
    // ============================================================

    public class ProductItem : INotifyPropertyChanged
    {
        private int _productID;
        private string _productCode = string.Empty;
        private string _productNameAr = string.Empty;
        private string _productNameEn = string.Empty;
        private int _categoryID;
        private string _unit = string.Empty;
        private string _barcode = string.Empty;
        private decimal _costPrice;
        private decimal _salePrice;
        private decimal _wholesalePrice;
        private decimal _minimumQuantity;
        private decimal _maximumQuantity;
        private decimal _reorderLevel;
        private bool _isActive;
        private string _categoryName = string.Empty;
        private decimal _currentQuantity;
        private bool _isSelected;

        private string _unit1 = string.Empty;
        private string _unit2 = string.Empty;
        private string _unit3 = string.Empty;
        private int _unit1Factor = 1;
        private int _unit2Factor = 1;
        private decimal _price1;
        private decimal _price2;
        private decimal _price3;
        private string _reorderUnit = string.Empty;
        private int _reorderLevelInBaseUnit;
        private long _quantityInBaseUnit;

        public int ProductID
        {
            get => _productID;
            set { _productID = value; OnPropertyChanged(nameof(ProductID)); }
        }

        public string ProductCode
        {
            get => _productCode;
            set { _productCode = value; OnPropertyChanged(nameof(ProductCode)); }
        }

        public string ProductNameAr
        {
            get => _productNameAr;
            set { _productNameAr = value; OnPropertyChanged(nameof(ProductNameAr)); }
        }

        public string ProductNameEn
        {
            get => _productNameEn;
            set { _productNameEn = value; OnPropertyChanged(nameof(ProductNameEn)); }
        }

        public int CategoryID
        {
            get => _categoryID;
            set { _categoryID = value; OnPropertyChanged(nameof(CategoryID)); }
        }

        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(nameof(Unit)); }
        }

        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(nameof(Barcode)); }
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set { _costPrice = value; OnPropertyChanged(nameof(CostPrice)); }
        }

        public decimal SalePrice
        {
            get => _salePrice;
            set { _salePrice = value; OnPropertyChanged(nameof(SalePrice)); }
        }

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set { _wholesalePrice = value; OnPropertyChanged(nameof(WholesalePrice)); }
        }

        public decimal MinimumQuantity
        {
            get => _minimumQuantity;
            set { _minimumQuantity = value; OnPropertyChanged(nameof(MinimumQuantity)); }
        }

        public decimal MaximumQuantity
        {
            get => _maximumQuantity;
            set { _maximumQuantity = value; OnPropertyChanged(nameof(MaximumQuantity)); }
        }

        public decimal ReorderLevel
        {
            get => _reorderLevel;
            set { _reorderLevel = value; OnPropertyChanged(nameof(ReorderLevel)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public string CategoryName
        {
            get => _categoryName;
            set { _categoryName = value; OnPropertyChanged(nameof(CategoryName)); }
        }

        public decimal CurrentQuantity
        {
            get => _currentQuantity;
            set { _currentQuantity = value; OnPropertyChanged(nameof(CurrentQuantity)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        // ✅ خصائص الوحدات الثلاثة
        public string Unit1
        {
            get => _unit1;
            set { _unit1 = value; OnPropertyChanged(nameof(Unit1)); OnPropertyChanged(nameof(DisplayUnit1)); }
        }

        public string Unit2
        {
            get => _unit2;
            set { _unit2 = value; OnPropertyChanged(nameof(Unit2)); OnPropertyChanged(nameof(DisplayUnit2)); }
        }

        public string Unit3
        {
            get => _unit3;
            set { _unit3 = value; OnPropertyChanged(nameof(Unit3)); OnPropertyChanged(nameof(DisplayUnit3)); }
        }

        public int Unit1Factor
        {
            get => _unit1Factor;
            set { _unit1Factor = value; OnPropertyChanged(nameof(Unit1Factor)); OnPropertyChanged(nameof(Unit1FactorDisplay)); }
        }

        public int Unit2Factor
        {
            get => _unit2Factor;
            set { _unit2Factor = value; OnPropertyChanged(nameof(Unit2Factor)); OnPropertyChanged(nameof(Unit2FactorDisplay)); }
        }

        public decimal Price1
        {
            get => _price1;
            set { _price1 = value; OnPropertyChanged(nameof(Price1)); }
        }

        public decimal Price2
        {
            get => _price2;
            set { _price2 = value; OnPropertyChanged(nameof(Price2)); }
        }

        public decimal Price3
        {
            get => _price3;
            set { _price3 = value; OnPropertyChanged(nameof(Price3)); }
        }

        public string ReorderUnit
        {
            get => _reorderUnit;
            set { _reorderUnit = value; OnPropertyChanged(nameof(ReorderUnit)); }
        }

        public int ReorderLevelInBaseUnit
        {
            get => _reorderLevelInBaseUnit;
            set { _reorderLevelInBaseUnit = value; OnPropertyChanged(nameof(ReorderLevelInBaseUnit)); }
        }

        public long QuantityInBaseUnit
        {
            get => _quantityInBaseUnit;
            set { _quantityInBaseUnit = value; OnPropertyChanged(nameof(QuantityInBaseUnit)); }
        }

        // ✅ خصائص العرض مع التوضيح
        public string DisplayUnit1 => !string.IsNullOrEmpty(Unit1) ? $"{Unit1} ({Unit1Factor} × {Unit2})" : "";
        public string DisplayUnit2 => !string.IsNullOrEmpty(Unit2) ? $"{Unit2} ({Unit2Factor} × {Unit3})" : "";
        public string DisplayUnit3 => !string.IsNullOrEmpty(Unit3) ? $"{Unit3} (أساسية)" : "";

        public string Unit1FactorDisplay => Unit1Factor > 1 ? $"= {Unit1Factor} × {Unit2}" : "";
        public string Unit2FactorDisplay => Unit2Factor > 1 ? $"= {Unit2Factor} × {Unit3}" : "";

        public string DisplayPrice1 => Price1.ToString("N2");
        public string DisplayPrice2 => Price2.ToString("N2");
        public string DisplayPrice3 => Price3.ToString("N2");
        public string DisplayReorderLevel => ReorderLevelInBaseUnit.ToString("N0");
        public string DisplayQuantity => QuantityInBaseUnit.ToString("N0");

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CategoryItem : INotifyPropertyChanged
    {
        private int _categoryID;
        private string _categoryNameAr = string.Empty;
        private string _categoryCode = string.Empty;
        private bool _isActive;

        public int CategoryID
        {
            get => _categoryID;
            set { _categoryID = value; OnPropertyChanged(nameof(CategoryID)); }
        }

        public string CategoryNameAr
        {
            get => _categoryNameAr;
            set { _categoryNameAr = value; OnPropertyChanged(nameof(CategoryNameAr)); }
        }

        public string CategoryCode
        {
            get => _categoryCode;
            set { _categoryCode = value; OnPropertyChanged(nameof(CategoryCode)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public override string ToString() => CategoryNameAr;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}