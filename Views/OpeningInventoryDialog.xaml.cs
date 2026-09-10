using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class OpeningInventoryDialog : Window
    {
        private DatabaseService _dbService;
        private string _connectionString;
        private int _storeId;
        private DateTime _openingDate;
        private ObservableCollection<OpeningProductItem> _productsList;
        private string _openingNumber;

        public OpeningInventoryDialog(DatabaseService dbService, int storeId, DateTime openingDate)
        {
            InitializeComponent();
            _dbService = dbService;
            _connectionString = _dbService.GetConnectionString();
            _storeId = storeId;
            _openingDate = openingDate;
            _productsList = new ObservableCollection<OpeningProductItem>();
            dgProducts.ItemsSource = _productsList;

            Loaded += async (sender, EventArgs) => await LoadDialogData();
        }

        private async Task LoadDialogData()
        {
            await LoadStoreInformation();
            await GenerateOpeningNumber();

            lblOpeningDate.Text = _openingDate.ToString("yyyy-MM-dd");

            txtQuantity.TextChanged += (sender, EventArgs) => CalculateTotalAmount();
            txtCostPrice.TextChanged += (sender, EventArgs) => CalculateTotalAmount();
        }

        private async Task LoadStoreInformation()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string selectStoreSql = "SELECT StoreNameAr FROM Stores WHERE StoreID = @storeId";
                using (var storeCommand = new SQLiteCommand(selectStoreSql, connection))
                {
                    storeCommand.Parameters.AddWithValue("@storeId", _storeId);
                    object storeNameResult = await storeCommand.ExecuteScalarAsync();
                    lblStoreName.Text = storeNameResult?.ToString() ?? "مخزن غير معروف";
                }
            }
        }

        private async Task GenerateOpeningNumber()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string maxNumberSql = "SELECT MAX(CAST(SUBSTR(OpeningNumber, 5) AS INTEGER)) FROM OpeningInventory WHERE OpeningNumber LIKE 'OPN-%'";
                using (var numberCommand = new SQLiteCommand(maxNumberSql, connection))
                {
                    object maxNumberResult = await numberCommand.ExecuteScalarAsync();
                    int nextNumber = (maxNumberResult == DBNull.Value) ? 1 : Convert.ToInt32(maxNumberResult) + 1;
                    _openingNumber = $"OPN-{nextNumber:D6}";
                    lblOpeningNumber.Text = _openingNumber;
                }
            }
        }

        private async void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchKeyword = txtProductSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchKeyword))
            {
                lstSearchResults.Visibility = Visibility.Collapsed;
                return;
            }

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string searchProductsSql = @"
                    SELECT ProductID, ProductCode, ProductNameAr, SalePrice
                    FROM Products 
                    WHERE IsActive = 1 
                    AND (ProductCode LIKE @searchKeyword OR ProductNameAr LIKE @searchKeyword)
                    LIMIT 10";

                var searchResults = new ObservableCollection<ProductSearchItem>();
                using (var searchCommand = new SQLiteCommand(searchProductsSql, connection))
                {
                    searchCommand.Parameters.AddWithValue("@searchKeyword", $"%{searchKeyword}%");
                    using (var searchReader = await searchCommand.ExecuteReaderAsync())
                    {
                        while (await searchReader.ReadAsync())
                        {
                            searchResults.Add(new ProductSearchItem
                            {
                                ProductID = searchReader.GetInt32(0),
                                ProductCode = searchReader.GetString(1),
                                ProductName = searchReader.GetString(2),
                                SalePrice = searchReader.GetDecimal(3)
                            });
                        }
                    }
                }

                lstSearchResults.ItemsSource = searchResults;
                lstSearchResults.Visibility = searchResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSearchResults.SelectedItem is ProductSearchItem selectedProductItem)
            {
                txtProductSearch.Text = $"{selectedProductItem.ProductCode} - {selectedProductItem.ProductName}";
                txtProductSearch.Tag = selectedProductItem.ProductID;
                txtCostPrice.Text = selectedProductItem.SalePrice.ToString("N2");
                lstSearchResults.Visibility = Visibility.Collapsed;
                txtQuantity.Focus();
            }
        }

        private void CalculateTotalAmount()
        {
            if (decimal.TryParse(txtQuantity.Text, out decimal quantityValue) &&
                decimal.TryParse(txtCostPrice.Text, out decimal costPriceValue))
            {
                decimal totalValue = quantityValue * costPriceValue;
                txtTotalAmount.Text = totalValue.ToString("N2");
            }
            else
            {
                txtTotalAmount.Text = "0";
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (txtProductSearch.Tag == null)
            {
                MessageBox.Show("الرجاء اختيار منتج من قائمة البحث", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtQuantity.Text, out decimal quantityValue) || quantityValue <= 0)
            {
                MessageBox.Show("الرجاء إدخال كمية صحيحة أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtQuantity.Focus();
                return;
            }

            if (!decimal.TryParse(txtCostPrice.Text, out decimal costPriceValue) || costPriceValue < 0)
            {
                MessageBox.Show("الرجاء إدخال سعر شراء صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtCostPrice.Focus();
                return;
            }

            int selectedProductId = (int)txtProductSearch.Tag;
            string selectedProductName = txtProductSearch.Text;

            foreach (var existingProduct in _productsList)
            {
                if (existingProduct.ProductID == selectedProductId)
                {
                    MessageBox.Show("هذا المنتج مضاف بالفعل في القائمة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            decimal totalAmountValue = quantityValue * costPriceValue;

            _productsList.Add(new OpeningProductItem
            {
                ProductID = selectedProductId,
                ProductName = selectedProductName,
                Quantity = quantityValue,
                CostPrice = costPriceValue,
                TotalAmount = totalAmountValue
            });

            txtProductSearch.Text = "";
            txtProductSearch.Tag = null;
            txtQuantity.Text = "0";
            txtCostPrice.Text = "0";
            txtTotalAmount.Text = "0";
            txtProductSearch.Focus();

            UpdateTotalValueDisplay();
        }

        private void RemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            Button removeButton = sender as Button;
            if (removeButton?.Tag is OpeningProductItem productToRemove)
            {
                _productsList.Remove(productToRemove);
                UpdateTotalValueDisplay();
            }
        }

        private void UpdateTotalValueDisplay()
        {
            decimal grandTotal = 0;
            foreach (var productItem in _productsList)
            {
                grandTotal += productItem.TotalAmount;
            }
            lblTotalValue.Text = $"الإجمالي: {grandTotal:N2}";
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_productsList.Count == 0)
            {
                MessageBox.Show("الرجاء إضافة منتج واحد على الأقل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        int newOpeningId = 0;

                        string insertOpeningSql = @"
                            INSERT INTO OpeningInventory (OpeningNumber, OpeningDate, StoreID, IsPosted, CreatedBy, CreatedDate)
                            VALUES (@openingNumber, @openingDate, @storeId, 0, @userId, CURRENT_TIMESTAMP);
                            SELECT last_insert_rowid();";

                        using (var openingCommand = new SQLiteCommand(insertOpeningSql, connection, transaction))
                        {
                            openingCommand.Parameters.AddWithValue("@openingNumber", _openingNumber);
                            openingCommand.Parameters.AddWithValue("@openingDate", _openingDate.ToString("yyyy-MM-dd"));
                            openingCommand.Parameters.AddWithValue("@storeId", _storeId);
                            openingCommand.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            object openingIdResult = await openingCommand.ExecuteScalarAsync();
                            newOpeningId = Convert.ToInt32(openingIdResult);
                        }

                        if (newOpeningId == 0)
                        {
                            transaction.Rollback();
                            MessageBox.Show("فشل في حفظ الرصيد الافتتاحي", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        string insertDetailSql = @"
                            INSERT INTO OpeningInventoryDetails (OpeningID, ProductID, Quantity, CostPrice, TotalAmount)
                            VALUES (@openingId, @productId, @quantity, @costPrice, @totalAmount)";

                        foreach (var productItem in _productsList)
                        {
                            using (var detailCommand = new SQLiteCommand(insertDetailSql, connection, transaction))
                            {
                                detailCommand.Parameters.AddWithValue("@openingId", newOpeningId);
                                detailCommand.Parameters.AddWithValue("@productId", productItem.ProductID);
                                detailCommand.Parameters.AddWithValue("@quantity", productItem.Quantity);
                                detailCommand.Parameters.AddWithValue("@costPrice", productItem.CostPrice);
                                detailCommand.Parameters.AddWithValue("@totalAmount", productItem.TotalAmount);
                                await detailCommand.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        DialogResult = true;
                        Close();
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show($"خطأ في حفظ الرصيد الافتتاحي: {exception.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class OpeningProductItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ProductSearchItem
    {
        public int ProductID { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public decimal SalePrice { get; set; }
    }
}