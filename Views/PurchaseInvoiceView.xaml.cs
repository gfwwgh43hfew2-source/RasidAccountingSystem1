using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace RasidAccountingSystem.Views
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string ?? "";

            if (status == "Paid" || status == "مدفوعة")
                return new SolidColorBrush(Color.FromRgb(16, 185, 129));
            else if (status == "Partial" || status == "مدفوعة جزئياً")
                return new SolidColorBrush(Color.FromRgb(245, 158, 11));
            else
                return new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class PurchaseInvoiceView : UserControl
    {
        #region Private Fields

        private DatabaseService _databaseService;
        private InvoiceService _invoiceService;
        private ObservableCollection<PurchaseInvoiceItem> _invoiceItems;
        private string _currentInvoiceNumber;
        private decimal _currentTaxPercent = 0m;
        private int _currentUserId;
        private int _currentEditingInvoiceId = 0;
        private string _currencySymbol = "ر.س";
        private bool _isUpdatingPaidAmount = false;
        private bool _isUpdatingTotals = false;

        // ==================== متغيرات AutoComplete للموردين ====================
        private List<SupplierSearchItem> _allSuppliersList = new List<SupplierSearchItem>();
        private List<SupplierSearchItem> _currentFilteredSuppliers = new List<SupplierSearchItem>();
        private int _currentSupplierSelectedIndex = -1;
        private bool _isSupplierPopupOpen = false;
        private bool _isSelectingSupplierFromList = false;
        private int _selectedSupplierId = 0;

        // ==================== متغيرات AutoComplete للمنتجات ====================
        private List<ProductSearchItem> _allProductsList = new List<ProductSearchItem>();
        private List<ProductSearchItem> _currentFilteredProducts = new List<ProductSearchItem>();
        private int _currentProductSelectedIndex = -1;
        private bool _isProductPopupOpen = false;
        private bool _isSelectingProductFromList = false;

        // ==================== متغيرات دعم الوحدات ====================
        private decimal _selectedUnitPrice3 = 0;
        private decimal _selectedUnitPrice2 = 0;
        private decimal _selectedUnitPrice1 = 0;
        private int _selectedUnit1Factor = 1;
        private int _selectedUnit2Factor = 1;
        private string _selectedUnit3Name = "";
        private string _selectedUnit2Name = "";
        private string _selectedUnit1Name = "";

        #endregion

        #region Helper Classes

        public class SupplierSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class SupplierSimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class ProductSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string Barcode { get; set; }

            // ✅ خصائص الوحدات الثلاثة
            public string Unit1 { get; set; } = "";
            public string Unit2 { get; set; } = "";
            public string Unit3 { get; set; } = "";
            public int Unit1Factor { get; set; } = 1;
            public int Unit2Factor { get; set; } = 1;
            public decimal Price1 { get; set; } = 0;
            public decimal Price2 { get; set; } = 0;
            public decimal Price3 { get; set; } = 0;

            public override string ToString() => Name;
        }

        public class ProductSimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public override string ToString() => Name;
        }

        public class StoreSimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class TreasuryData
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public string CurrencySymbol { get; set; } = "ر.س";
            public override string ToString() => $"{Name} (الرصيد: {CurrentBalance:N2} {CurrencySymbol})";
        }

        public class BankAccountData
        {
            public int BankAccountID { get; set; }
            public string BankName { get; set; }
            public string AccountNumber { get; set; }
            public decimal CurrentBalance { get; set; }
            public string CurrencySymbol { get; set; } = "ر.س";
            public override string ToString() => $"{BankName} - {AccountNumber} (الرصيد: {CurrentBalance:N2} {CurrencySymbol})";
        }

        public class SavedPurchaseInvoiceItem
        {
            public int InvoiceID { get; set; }
            public string InvoiceNumber { get; set; }
            public DateTime InvoiceDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string PaymentMethod { get; set; }
            public string PaymentStatus { get; set; }
            public string SupplierName { get; set; }
        }

        public class CompanyData
        {
            public string CompanyName { get; set; }
            public string LogoPath { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string TaxNumber { get; set; }
        }

        #endregion

        #region Constructor

        public PurchaseInvoiceView()
        {
            try
            {
                InitializeComponent();
                InitializeServices();
                InitializeEvents();
                this.Loaded += async (s, e) => await InitializeFormAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Initialization Methods

        private void InitializeServices()
        {
            try
            {
                _databaseService = new DatabaseService();
                _invoiceService = new InvoiceService(_databaseService);
                _invoiceItems = new ObservableCollection<PurchaseInvoiceItem>();
                _currentUserId = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1;
                _currentEditingInvoiceId = 0;

                if (dgInvoiceItems != null)
                {
                    dgInvoiceItems.ItemsSource = _invoiceItems;
                }

                if (_invoiceItems != null)
                {
                    _invoiceItems.CollectionChanged += (s, e) =>
                    {
                        UpdateSerialNumbers();
                        CalculateTotals();
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeServices Error: {ex.Message}");
                throw;
            }
        }

        private void InitializeEvents()
        {
            try
            {
                if (btnNewInvoice != null)
                    btnNewInvoice.Click += async (s, e) => await NewInvoiceAsync();

                if (btnSaveInvoice != null)
                    btnSaveInvoice.Click += async (s, e) => await SaveInvoiceAsync();

                if (btnAddProduct != null)
                    btnAddProduct.Click += async (s, e) => await AddProductToInvoiceAsync();

                if (btnPrintInvoice != null)
                    btnPrintInvoice.Click += async (s, e) => await PrintInvoiceAsync();

                if (btnOpenSearchDialog != null)
                    btnOpenSearchDialog.Click += (s, e) => OpenSearchDialog();

                if (btnCloseSearchDialog != null)
                    btnCloseSearchDialog.Click += (s, e) => CloseSearchDialog();

                if (btnSearchInvoices != null)
                    btnSearchInvoices.Click += async (s, e) => await SearchInvoicesAsync();

                if (cmbStores != null)
                {
                    cmbStores.SelectionChanged += (s, e) => CmbStores_SelectionChanged(s, null);
                }

                if (cmbPaymentMethod != null)
                    cmbPaymentMethod.SelectionChanged += CmbPaymentMethod_SelectionChanged;

                if (txtTaxPercent != null)
                    txtTaxPercent.TextChanged += TxtTaxPercent_TextChanged;

                if (txtQuantity != null)
                {
                    txtQuantity.KeyDown += OnInputKeyDown;
                    txtQuantity.TextChanged += OnNumericTextChanged;
                }

                if (txtUnitPrice != null)
                {
                    txtUnitPrice.KeyDown += OnInputKeyDown;
                    txtUnitPrice.TextChanged += OnNumericTextChanged;
                }

                if (txtDiscountPercent != null)
                {
                    txtDiscountPercent.KeyDown += OnInputKeyDown;
                    txtDiscountPercent.TextChanged += OnDiscountTextChanged;
                }

                if (txtPaidAmount != null)
                {
                    txtPaidAmount.TextChanged += TxtPaidAmount_TextChanged;
                    txtPaidAmount.GotFocus += TxtPaidAmount_GotFocus;
                    txtPaidAmount.LostFocus += TxtPaidAmount_LostFocus;
                }

                // ✅ إضافة حدث تغيير الوحدة
                if (cmbUnit != null)
                {
                    cmbUnit.SelectionChanged += CmbUnit_SelectionChanged;
                }

                if (SearchDialogOverlay != null)
                {
                    SearchDialogOverlay.MouseDown += (s, e) =>
                    {
                        if (e.OriginalSource == SearchDialogOverlay)
                            CloseSearchDialog();
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeEvents Error: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void UpdateSerialNumbers()
        {
            try
            {
                if (_invoiceItems == null) return;

                for (int i = 0; i < _invoiceItems.Count; i++)
                {
                    if (_invoiceItems[i] != null)
                    {
                        _invoiceItems[i].SerialNumber = i + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSerialNumbers Error: {ex.Message}");
            }
        }

        private async void CmbStores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ProductSearchItem selectedProduct = GetSelectedProduct();

                if (selectedProduct != null)
                {
                    await DisplayProductStockAsync(selectedProduct);
                }
                else
                {
                    if (lblProductStock != null)
                    {
                        lblProductStock.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CmbStores_SelectionChanged Error: {ex.Message}");
            }
        }

        /// <summary>
        /// دالة الحصول على رصيد المنتج في المخزن المحدد مع معلومات الوحدات
        /// </summary>
        private async Task<(decimal QuantityInBaseUnit, int Unit1Factor, int Unit2Factor)> GetProductStockWithUnitInfoAsync(int storeId, int productId)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<(decimal, int, int)>(_databaseService, async (connection) =>
                {
                    // الحصول على الرصيد بالوحدة الأساسية مع معلومات الوحدات
                    string sql = @"
                        SELECT 
                            COALESCE(si.Quantity, 0) as Quantity,
                            COALESCE(p.Unit1Factor, 1) as Unit1Factor,
                            COALESCE(p.Unit2Factor, 1) as Unit2Factor
                        FROM StoreInventory si
                        LEFT JOIN Products p ON si.ProductID = p.ProductID
                        WHERE si.StoreID = @storeId AND si.ProductID = @productId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@storeId", storeId);
                        cmd.Parameters.AddWithValue("@productId", productId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                decimal quantity = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                                int unit1Factor = reader.IsDBNull(1) ? 1 : reader.GetInt32(1);
                                int unit2Factor = reader.IsDBNull(2) ? 1 : reader.GetInt32(2);
                                return (quantity, unit1Factor, unit2Factor);
                            }
                        }
                    }
                    return (0, 1, 1);
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductStockWithUnitInfoAsync Error: {ex.Message}");
                return (0, 1, 1);
            }
        }

        private async Task<decimal> GetProductStockAsync(int storeId, int productId)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<decimal>(_databaseService, async (connection) =>
                {
                    string sql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@storeId", storeId);
                        cmd.Parameters.AddWithValue("@productId", productId);
                        object result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToDecimal(result) : 0;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetProductStockAsync Error: {ex.Message}");
                return 0;
            }
        }

        private void TxtTaxPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                        return;
                    }

                    if (!decimal.TryParse(textBox.Text, out decimal taxPercent))
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                        return;
                    }

                    if (taxPercent < 0)
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                    }
                    else if (taxPercent > 100)
                    {
                        textBox.Text = "100";
                        textBox.SelectAll();
                    }
                    else
                    {
                        _currentTaxPercent = taxPercent;
                    }

                    CalculateTotals();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtTaxPercent_TextChanged Error: {ex.Message}");
            }
        }

        private void TxtPaidAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingPaidAmount) return;

                TextBox textBox = sender as TextBox;
                if (textBox == null) return;

                string text = textBox.Text;

                if (string.IsNullOrEmpty(text))
                {
                    if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) && lblRemainingAmount != null)
                    {
                        lblRemainingAmount.Text = grandTotal.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    return;
                }

                if (decimal.TryParse(text, out decimal paidAmount))
                {
                    if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) && lblRemainingAmount != null)
                    {
                        if (paidAmount > grandTotal)
                        {
                            paidAmount = grandTotal;
                        }

                        decimal remainingAmount = grandTotal - paidAmount;
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");

                        if (remainingAmount <= 0)
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        }
                        else
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtPaidAmount_TextChanged Error: {ex.Message}");
            }
        }

        private void TxtPaidAmount_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                TextBox textBox = sender as TextBox;
                if (textBox == null) return;

                textBox.SelectAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtPaidAmount_GotFocus Error: {ex.Message}");
            }
        }

        private void TxtPaidAmount_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _isUpdatingPaidAmount = true;

                TextBox textBox = sender as TextBox;
                if (textBox == null) return;

                string text = textBox.Text;

                if (string.IsNullOrWhiteSpace(text))
                {
                    textBox.Text = "0.00";

                    if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) && lblRemainingAmount != null)
                    {
                        decimal remainingAmount = grandTotal;
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    return;
                }

                if (decimal.TryParse(text, out decimal paidAmount))
                {
                    if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) && lblRemainingAmount != null)
                    {
                        if (paidAmount > grandTotal)
                        {
                            paidAmount = grandTotal;
                        }

                        textBox.Text = paidAmount.ToString("N2");

                        decimal remainingAmount = grandTotal - paidAmount;
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");

                        if (remainingAmount <= 0)
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        }
                        else
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        }
                    }
                }
                else
                {
                    textBox.Text = "0.00";

                    if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) && lblRemainingAmount != null)
                    {
                        decimal remainingAmount = grandTotal;
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtPaidAmount_LostFocus Error: {ex.Message}");
                if (sender is TextBox textBox)
                {
                    textBox.Text = "0.00";
                }
            }
            finally
            {
                _isUpdatingPaidAmount = false;
            }
        }

        private async Task LoadCurrencySymbolAsync()
        {
            try
            {
                if (_databaseService == null) return;

                string symbol = await _databaseService.GetCurrencySymbolAsync();
                if (!string.IsNullOrEmpty(symbol))
                {
                    _currencySymbol = symbol;
                }
                else
                {
                    _currencySymbol = "ر.س";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrencySymbolAsync Error: {ex.Message}");
                _currencySymbol = "ر.س";
            }
        }

        private void UpdatePaymentPanelVisibility()
        {
            try
            {
                string paymentMethod = "نقدي";

                if (cmbPaymentMethod != null && cmbPaymentMethod.SelectedItem is ComboBoxItem selectedItem)
                {
                    paymentMethod = selectedItem.Content?.ToString() ?? "نقدي";
                }

                if (pnlTreasury != null)
                    pnlTreasury.Visibility = (paymentMethod == "نقدي") ? Visibility.Visible : Visibility.Collapsed;

                if (pnlBank != null)
                    pnlBank.Visibility = (paymentMethod == "تحويل بنكي") ? Visibility.Visible : Visibility.Collapsed;

                if (pnlCheckDetails != null)
                    pnlCheckDetails.Visibility = (paymentMethod == "شيك") ? Visibility.Visible : Visibility.Collapsed;

                if (pnlCreditMessage != null)
                    pnlCreditMessage.Visibility = (paymentMethod == "أجل") ? Visibility.Visible : Visibility.Collapsed;

                if (pnlPaidAmount != null)
                {
                    if (paymentMethod == "أجل")
                    {
                        pnlPaidAmount.Visibility = Visibility.Collapsed;
                        if (txtPaidAmount != null)
                            txtPaidAmount.Text = "0.00";
                    }
                    else
                    {
                        pnlPaidAmount.Visibility = Visibility.Visible;
                    }
                }

                if (paymentMethod == "أجل" && txtPaidAmount != null)
                {
                    txtPaidAmount.Text = "0.00";
                }

                System.Diagnostics.Debug.WriteLine($"✅ طريقة الدفع changed to: {paymentMethod}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdatePaymentPanelVisibility Error: {ex.Message}");
            }
        }

        private void CmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePaymentPanelVisibility();
            CalculateTotals();
        }

        private async void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    await AddProductToInvoiceAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnInputKeyDown Error: {ex.Message}");
            }
        }

        private void OnNumericTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                        return;
                    }
                    if (!decimal.TryParse(textBox.Text, out _))
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNumericTextChanged Error: {ex.Message}");
            }
        }

        private void OnDiscountTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox && decimal.TryParse(textBox.Text, out decimal discount))
                {
                    if (discount < 0)
                    {
                        textBox.Text = "0";
                        textBox.SelectAll();
                    }
                    else if (discount > 100)
                    {
                        textBox.Text = "100";
                        textBox.SelectAll();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnDiscountTextChanged Error: {ex.Message}");
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is PurchaseInvoiceItem item && _invoiceItems != null)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"هل أنت متأكد من حذف المنتج ({item.ProductName})؟",
                        "تأكيد الحذف",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _invoiceItems.Remove(item);
                        UpdateSerialNumbers();
                        CalculateTotals();
                        MessageBox.Show("تم حذف المنتج بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حذف المنتج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteItem_Click Error: {ex.Message}");
            }
        }

        #endregion

        #region Data Loading Methods

        private async Task InitializeFormAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await LoadCurrencySymbolAsync();
                await LoadSuppliersForAutoCompleteAsync();
                await LoadProductsForAutoCompleteAsync();
                await LoadStoresAsync();
                await LoadTreasuriesAsync();
                await LoadBankAccountsAsync();
                await GenerateNewInvoiceNumber();

                if (dpInvoiceDate != null)
                    dpInvoiceDate.SelectedDate = DateTime.Now;

                if (dpFromDateSearch != null)
                    dpFromDateSearch.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (dpToDateSearch != null)
                    dpToDateSearch.SelectedDate = DateTime.Now;

                if (dpCheckDate != null)
                    dpCheckDate.SelectedDate = DateTime.Now;

                if (cmbPaymentMethod != null)
                {
                    cmbPaymentMethod.SelectedIndex = 0;
                    UpdatePaymentPanelVisibility();
                }

                if (_invoiceItems == null)
                {
                    _invoiceItems = new ObservableCollection<PurchaseInvoiceItem>();
                    if (dgInvoiceItems != null)
                        dgInvoiceItems.ItemsSource = _invoiceItems;
                }

                if (lblProductStock != null)
                    lblProductStock.Visibility = Visibility.Collapsed;

                CalculateTotals();
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تهيئة النموذج: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"InitializeFormAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// تحميل جميع الموردين للـ AutoComplete
        /// </summary>
        private async Task LoadSuppliersForAutoCompleteAsync()
        {
            try
            {
                if (_databaseService == null) return;

                _allSuppliersList.Clear();
                var suppliers = await _databaseService.GetSuppliersAsync();

                if (suppliers != null)
                {
                    foreach (var supplier in suppliers)
                    {
                        if (supplier != null)
                        {
                            string supplierName = !string.IsNullOrEmpty(supplier.SupplierNameAr) ? supplier.SupplierNameAr : supplier.SupplierName;

                            _allSuppliersList.Add(new SupplierSearchItem
                            {
                                Id = supplier.SupplierID,
                                Code = supplier.SupplierCode,
                                Name = supplierName,
                                CurrentBalance = supplier.CurrentBalance
                            });
                        }
                    }
                }

                _currentFilteredSuppliers = new List<SupplierSearchItem>(_allSuppliersList);

                if (lstSuppliers != null)
                {
                    lstSuppliers.ItemsSource = _currentFilteredSuppliers;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_allSuppliersList.Count} مورد للـ AutoComplete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل الموردين للـ AutoComplete: {ex.Message}");
            }
        }

        /// <summary>
        /// تحميل جميع المنتجات للـ AutoComplete مع معلومات الوحدات
        /// </summary>
        private async Task LoadProductsForAutoCompleteAsync()
        {
            try
            {
                if (_databaseService == null) return;

                _allProductsList.Clear();
                var products = await _databaseService.GetProductsAsync();

                if (products != null)
                {
                    foreach (var product in products)
                    {
                        if (product != null)
                        {
                            _allProductsList.Add(new ProductSearchItem
                            {
                                Id = product.ProductID,
                                Code = product.ProductCode,
                                Name = product.ProductNameAr,
                                Price = product.CostPrice,
                                Barcode = product.Barcode ?? "",
                                // ✅ تحميل معلومات الوحدات
                                Unit1 = product.Unit1 ?? "",
                                Unit2 = product.Unit2 ?? "",
                                Unit3 = product.Unit3 ?? "",
                                Unit1Factor = product.Unit1Factor,
                                Unit2Factor = product.Unit2Factor,
                                Price1 = product.Price1,
                                Price2 = product.Price2,
                                Price3 = product.Price3
                            });
                        }
                    }
                }

                _currentFilteredProducts = new List<ProductSearchItem>(_allProductsList);

                if (lstProducts != null)
                {
                    lstProducts.ItemsSource = _currentFilteredProducts;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {_allProductsList.Count} منتج للـ AutoComplete مع معلومات الوحدات");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل المنتجات للـ AutoComplete: {ex.Message}");
            }
        }

        private async Task LoadStoresAsync()
        {
            try
            {
                var storesList = new List<StoreSimple>();

                await DatabaseExecutor.ExecuteNonTransactionAsync<List<StoreSimple>>(_databaseService, async (connection) =>
                {
                    string sqlQuery = "SELECT StoreID, StoreNameAr FROM Stores WHERE IsActive = 1 ORDER BY StoreNameAr";

                    using (var command = new SQLiteCommand(sqlQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
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
                    return storesList;
                }, CancellationToken.None, 3);

                if (cmbStores != null)
                {
                    var storesObservable = new ObservableCollection<StoreSimple>(storesList);
                    cmbStores.ItemsSource = storesObservable;

                    if (storesObservable.Count > 0)
                    {
                        cmbStores.SelectedItem = storesObservable[0];
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {storesList.Count} مخزن");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadStoresAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadTreasuriesAsync()
        {
            try
            {
                var treasuriesList = new List<TreasuryData>();

                await DatabaseExecutor.ExecuteNonTransactionAsync<List<TreasuryData>>(_databaseService, async (connection) =>
                {
                    string sql = "SELECT TreasuryID, TreasuryCode, TreasuryNameAr, CurrentBalance FROM Treasury WHERE IsActive = 1 ORDER BY TreasuryNameAr";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            treasuriesList.Add(new TreasuryData
                            {
                                Id = reader.GetInt32(0),
                                Code = reader.GetString(1),
                                Name = reader.GetString(2),
                                CurrentBalance = reader.GetDecimal(3),
                                CurrencySymbol = _currencySymbol
                            });
                        }
                    }
                    return treasuriesList;
                }, CancellationToken.None, 3);

                if (cmbTreasury != null)
                {
                    cmbTreasury.ItemsSource = new ObservableCollection<TreasuryData>(treasuriesList);

                    if (treasuriesList.Count > 0)
                    {
                        cmbTreasury.SelectedItem = treasuriesList[0];
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {treasuriesList.Count} خزينة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuriesAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل الخزائن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadBankAccountsAsync()
        {
            try
            {
                var bankAccountsList = new List<BankAccountData>();

                await DatabaseExecutor.ExecuteNonTransactionAsync<List<BankAccountData>>(_databaseService, async (connection) =>
                {
                    string sql = @"SELECT BankAccountID, BankName, AccountNumber, CurrentBalance 
                                   FROM BankAccounts WHERE IsActive = 1 ORDER BY BankName";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            bankAccountsList.Add(new BankAccountData
                            {
                                BankAccountID = reader.GetInt32(0),
                                BankName = reader.GetString(1),
                                AccountNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                CurrentBalance = reader.GetDecimal(3),
                                CurrencySymbol = _currencySymbol
                            });
                        }
                    }
                    return bankAccountsList;
                }, CancellationToken.None, 3);

                if (cmbBankAccount != null)
                {
                    cmbBankAccount.ItemsSource = new ObservableCollection<BankAccountData>(bankAccountsList);

                    if (bankAccountsList.Count > 0)
                    {
                        cmbBankAccount.SelectedItem = bankAccountsList[0];
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {bankAccountsList.Count} حساب بنكي");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadBankAccountsAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل الحسابات البنكية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task GenerateNewInvoiceNumber()
        {
            try
            {
                await DatabaseExecutor.ExecuteNonTransactionAsync<int>(_databaseService, async (connection) =>
                {
                    string sql = "SELECT MAX(CAST(SUBSTR(InvoiceNumber, 5) AS INTEGER)) FROM PurchaseInvoices WHERE InvoiceNumber LIKE 'PIN-%'";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = await cmd.ExecuteScalarAsync();
                        int maxNumber = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                        int nextNumber = maxNumber + 1;
                        _currentInvoiceNumber = $"PIN-{nextNumber:D6}";

                        if (lblInvoiceNumber != null)
                            lblInvoiceNumber.Text = _currentInvoiceNumber;

                        return nextNumber;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateNewInvoiceNumber Error: {ex.Message}");
                _currentInvoiceNumber = $"PIN-{DateTime.Now:yyyyMMddHHmmss}";

                if (lblInvoiceNumber != null)
                    lblInvoiceNumber.Text = _currentInvoiceNumber;
            }
        }

        #endregion

        #region Search Dialog Methods

        private void OpenSearchDialog()
        {
            try
            {
                if (SearchDialogOverlay != null)
                    SearchDialogOverlay.Visibility = Visibility.Visible;

                if (txtSearchInvoiceNumber != null)
                    txtSearchInvoiceNumber.Text = string.Empty;

                if (txtSearchSupplierName != null)
                    txtSearchSupplierName.Text = string.Empty;

                if (dpFromDateSearch != null)
                    dpFromDateSearch.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                if (dpToDateSearch != null)
                    dpToDateSearch.SelectedDate = DateTime.Now;

                if (dgSearchResults != null)
                    dgSearchResults.ItemsSource = null;

                if (lblNoSearchResults != null)
                    lblNoSearchResults.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenSearchDialog Error: {ex.Message}");
            }
        }

        private void CloseSearchDialog()
        {
            try
            {
                if (SearchDialogOverlay != null)
                    SearchDialogOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CloseSearchDialog Error: {ex.Message}");
            }
        }

        private async Task SearchInvoicesAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                string invoiceNumber = txtSearchInvoiceNumber != null ? txtSearchInvoiceNumber.Text.Trim() : "";
                string supplierName = txtSearchSupplierName != null ? txtSearchSupplierName.Text.Trim() : "";
                DateTime fromDate = (dpFromDateSearch != null && dpFromDateSearch.SelectedDate.HasValue) ? dpFromDateSearch.SelectedDate.Value : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime toDate = (dpToDateSearch != null && dpToDateSearch.SelectedDate.HasValue) ? dpToDateSearch.SelectedDate.Value : DateTime.Now;
                toDate = toDate.AddDays(1).AddSeconds(-1);

                var resultsList = new List<SavedPurchaseInvoiceItem>();

                await DatabaseExecutor.ExecuteNonTransactionAsync<List<SavedPurchaseInvoiceItem>>(_databaseService, async (connection) =>
                {
                    string sql = @"
                        SELECT 
                            pi.InvoiceID,
                            pi.InvoiceNumber,
                            pi.InvoiceDate,
                            pi.TotalAmount,
                            pi.PaymentMethod,
                            pi.PaymentStatus,
                            COALESCE(s.SupplierNameAr, s.SupplierName, '') as SupplierName
                        FROM PurchaseInvoices pi
                        LEFT JOIN Suppliers s ON pi.SupplierID = s.SupplierID
                        WHERE pi.IsVoid = 0
                        AND pi.InvoiceDate BETWEEN @fromDate AND @toDate";

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        sql += " AND pi.InvoiceNumber LIKE @invoiceNumber";
                    }

                    if (!string.IsNullOrEmpty(supplierName))
                    {
                        sql += " AND (s.SupplierNameAr LIKE @supplierName OR s.SupplierName LIKE @supplierName)";
                    }

                    sql += " ORDER BY pi.InvoiceDate DESC, pi.InvoiceID DESC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@toDate", toDate.ToString("yyyy-MM-dd"));
                        if (!string.IsNullOrEmpty(invoiceNumber))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", $"%{invoiceNumber}%");
                        }
                        if (!string.IsNullOrEmpty(supplierName))
                        {
                            cmd.Parameters.AddWithValue("@supplierName", $"%{supplierName}%");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                resultsList.Add(new SavedPurchaseInvoiceItem
                                {
                                    InvoiceID = reader.GetInt32(0),
                                    InvoiceNumber = reader.GetString(1),
                                    InvoiceDate = reader.GetDateTime(2),
                                    TotalAmount = reader.GetDecimal(3),
                                    PaymentMethod = ConvertPaymentMethodToArabic(reader.GetString(4)),
                                    PaymentStatus = reader.GetString(5),
                                    SupplierName = reader.GetString(6)
                                });
                            }
                        }
                    }
                    return resultsList;
                }, CancellationToken.None, 3);

                if (dgSearchResults != null)
                    dgSearchResults.ItemsSource = new ObservableCollection<SavedPurchaseInvoiceItem>(resultsList);

                if (lblNoSearchResults != null)
                {
                    if (resultsList.Count == 0)
                    {
                        lblNoSearchResults.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        lblNoSearchResults.Visibility = Visibility.Collapsed;
                    }
                }

                if (lblSearchStatus != null)
                    lblSearchStatus.Text = $"تم العثور على {resultsList.Count} فاتورة";

                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"تم العثور على {resultsList.Count} فاتورة");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في البحث: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"SearchInvoicesAsync Error: {ex.Message}");
            }
        }

        private string ConvertPaymentMethodToArabic(string englishMethod)
        {
            switch (englishMethod)
            {
                case "Cash":
                    return "نقدي";
                case "Bank":
                    return "تحويل بنكي";
                case "Check":
                    return "شيك";
                case "Credit":
                    return "أجل";
                case "Transfer":
                    return "تحويل بنكي";
                default:
                    return englishMethod;
            }
        }

        private string ConvertPaymentMethodToEnglish(string arabicPaymentMethod)
        {
            switch (arabicPaymentMethod)
            {
                case "نقدي":
                    return "Cash";
                case "تحويل بنكي":
                    return "Transfer";
                case "شيك":
                    return "Check";
                case "أجل":
                    return "Credit";
                default:
                    return "Cash";
            }
        }

        private async Task LoadInvoiceData(int invoiceId)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _currentEditingInvoiceId = invoiceId;

                await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_databaseService, async (connection) =>
                {
                    string invoiceSql = @"
                        SELECT 
                            InvoiceNumber,
                            InvoiceDate,
                            SupplierID,
                            StoreID,
                            PaymentMethod,
                            ReferenceNumber,
                            Notes,
                            SubTotal,
                            DiscountAmount,
                            TaxAmount,
                            TaxPercent,
                            TotalAmount,
                            PaidAmount,
                            RemainingAmount,
                            PaymentStatus
                        FROM PurchaseInvoices 
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(invoiceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                _currentInvoiceNumber = reader.GetString(0);
                                if (lblInvoiceNumber != null)
                                    lblInvoiceNumber.Text = _currentInvoiceNumber;

                                if (dpInvoiceDate != null)
                                    dpInvoiceDate.SelectedDate = reader.GetDateTime(1);

                                int supplierId = reader.GetInt32(2);

                                if (txtSupplierSearch != null && _allSuppliersList != null)
                                {
                                    var supplier = _allSuppliersList.FirstOrDefault(s => s.Id == supplierId);
                                    if (supplier != null)
                                    {
                                        txtSupplierSearch.Text = supplier.Name;
                                        if (txtSupplierPlaceholder != null)
                                            txtSupplierPlaceholder.Visibility = Visibility.Collapsed;
                                        _selectedSupplierId = supplierId;
                                    }
                                }

                                int storeId = reader.GetInt32(3);
                                if (cmbStores != null && cmbStores.ItemsSource != null)
                                {
                                    foreach (var item in cmbStores.ItemsSource)
                                    {
                                        var store = item as StoreSimple;
                                        if (store != null && store.Id == storeId)
                                        {
                                            cmbStores.SelectedItem = item;
                                            break;
                                        }
                                    }
                                }

                                string paymentMethod = reader.GetString(4);
                                string arabicPaymentMethod = ConvertPaymentMethodToArabic(paymentMethod);
                                if (cmbPaymentMethod != null)
                                {
                                    for (int i = 0; i < cmbPaymentMethod.Items.Count; i++)
                                    {
                                        var item = cmbPaymentMethod.Items[i] as ComboBoxItem;
                                        if (item != null && item.Content.ToString() == arabicPaymentMethod)
                                        {
                                            cmbPaymentMethod.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                }

                                if (txtReferenceNumber != null)
                                    txtReferenceNumber.Text = reader.IsDBNull(5) ? "" : reader.GetString(5);

                                if (txtNotes != null)
                                    txtNotes.Text = reader.IsDBNull(6) ? "" : reader.GetString(6);

                                decimal subTotal = reader.GetDecimal(7);
                                decimal discountAmount = reader.GetDecimal(8);
                                decimal taxAmount = reader.GetDecimal(9);
                                decimal taxPercent = reader.GetDecimal(10);
                                decimal totalAmount = reader.GetDecimal(11);
                                decimal paidAmount = reader.GetDecimal(12);
                                decimal remainingAmount = reader.GetDecimal(13);
                                string paymentStatus = reader.GetString(14);

                                _currentTaxPercent = taxPercent;
                                if (txtTaxPercent != null)
                                    txtTaxPercent.Text = _currentTaxPercent.ToString("N0");

                                if (lblSubTotal != null)
                                    lblSubTotal.Text = subTotal.ToString("N2");

                                if (lblTotalDiscount != null)
                                    lblTotalDiscount.Text = discountAmount.ToString("N2");

                                if (lblTaxAmount != null)
                                    lblTaxAmount.Text = taxAmount.ToString("N2");

                                if (lblGrandTotal != null)
                                    lblGrandTotal.Text = totalAmount.ToString("N2");

                                if (txtPaidAmount != null)
                                    txtPaidAmount.Text = paidAmount.ToString("N2");

                                if (lblRemainingAmount != null)
                                    lblRemainingAmount.Text = remainingAmount.ToString("N2");
                            }
                        }
                    }

                    if (_invoiceItems != null)
                        _invoiceItems.Clear();

                    string itemsSql = @"
                        SELECT 
                            ProductID,
                            Quantity,
                            UnitPrice,
                            DiscountPercent,
                            DiscountAmount,
                            TotalAmount
                        FROM PurchaseInvoiceItems 
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(itemsSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int serial = 1;
                            while (await reader.ReadAsync())
                            {
                                int productId = reader.GetInt32(0);
                                string productName = "";

                                var product = _allProductsList.FirstOrDefault(p => p.Id == productId);
                                if (product != null)
                                {
                                    productName = product.Name;
                                }

                                if (_invoiceItems != null)
                                {
                                    _invoiceItems.Add(new PurchaseInvoiceItem
                                    {
                                        SerialNumber = serial++,
                                        ProductID = productId,
                                        ProductName = productName,
                                        Quantity = reader.GetDecimal(1),
                                        UnitPrice = reader.GetDecimal(2),
                                        DiscountPercent = reader.GetDecimal(3),
                                        DiscountAmount = reader.GetDecimal(4),
                                        TotalAmount = reader.GetDecimal(5)
                                    });
                                }
                            }
                        }
                    }

                    UpdateSerialNumbers();
                    CalculateTotals();
                    UpdatePaymentPanelVisibility();

                    return true;
                }, CancellationToken.None, 3);

                CloseSearchDialog();
                Mouse.OverrideCursor = null;
                MessageBox.Show($"تم تحميل الفاتورة رقم {_currentInvoiceNumber}", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تحميل الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"LoadInvoiceData Error: {ex.Message}");
            }
        }

        private async void ViewSelectedInvoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button != null && button.Tag != null)
                {
                    int invoiceId = (int)button.Tag;
                    CloseSearchDialog();
                    await LoadInvoiceData(invoiceId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ViewSelectedInvoice_Click Error: {ex.Message}");
            }
        }

        #endregion

        #region Delete Invoice Methods

        private async void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button == null || button.Tag == null) return;

                int invoiceId = (int)button.Tag;

                string invoiceNumber = "";
                if (button.DataContext is SavedPurchaseInvoiceItem invoiceItem)
                {
                    invoiceNumber = invoiceItem.InvoiceNumber;
                }
                else
                {
                    if (dgSearchResults != null && dgSearchResults.SelectedItem is SavedPurchaseInvoiceItem boundItem)
                    {
                        invoiceNumber = boundItem.InvoiceNumber;
                    }
                }

                MessageBoxResult result = MessageBox.Show(
                    $"هل أنت متأكد من حذف الفاتورة رقم {invoiceNumber}؟\n\n" +
                    "⚠️ تحذير: سيتم حذف جميع بيانات الفاتورة بما فيها:\n" +
                    "• تفاصيل المنتجات\n" +
                    "• سندات الصرف المرتبطة (إن وجدت)\n" +
                    "• المعاملات المالية المرتبطة\n" +
                    "• معاملات الخزينة والبنوك والشيكات\n" +
                    "• حركات المخزون\n\n" +
                    "هذا الإجراء لا يمكن التراجع عنه!",
                    "تأكيد حذف الفاتورة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                Mouse.OverrideCursor = Cursors.Wait;

                bool deleted = await _invoiceService.DeletePurchaseInvoiceAsync(invoiceId);

                Mouse.OverrideCursor = null;

                if (deleted)
                {
                    MessageBox.Show($"تم حذف الفاتورة رقم {invoiceNumber} بنجاح", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                    await SearchInvoicesAsync();

                    if (_currentEditingInvoiceId == invoiceId)
                    {
                        await NewInvoiceAsync();
                    }
                }
                else
                {
                    MessageBox.Show($"فشل حذف الفاتورة رقم {invoiceNumber}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في حذف الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteInvoice_Click Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Core Operations

        private async Task NewInvoiceAsync()
        {
            try
            {
                if (_invoiceItems != null)
                    _invoiceItems.Clear();

                _currentEditingInvoiceId = 0;

                if (txtReferenceNumber != null)
                    txtReferenceNumber.Text = string.Empty;

                if (txtNotes != null)
                    txtNotes.Text = string.Empty;

                if (txtQuantity != null)
                    txtQuantity.Text = "1";

                if (txtUnitPrice != null)
                    txtUnitPrice.Text = "0";

                if (txtDiscountPercent != null)
                    txtDiscountPercent.Text = "0";

                if (txtCheckNumber != null)
                    txtCheckNumber.Text = string.Empty;

                if (txtCheckBankName != null)
                    txtCheckBankName.Text = string.Empty;

                if (txtCheckPayerName != null)
                    txtCheckPayerName.Text = string.Empty;

                if (txtPaidAmount != null)
                    txtPaidAmount.Text = "0.00";

                if (dpCheckDate != null)
                    dpCheckDate.SelectedDate = DateTime.Now;

                if (lblProductStock != null)
                    lblProductStock.Visibility = Visibility.Collapsed;

                await GenerateNewInvoiceNumber();

                if (dpInvoiceDate != null)
                    dpInvoiceDate.SelectedDate = DateTime.Now;

                if (txtSupplierSearch != null)
                    txtSupplierSearch.Text = string.Empty;
                if (txtSupplierPlaceholder != null)
                    txtSupplierPlaceholder.Visibility = Visibility.Visible;
                _selectedSupplierId = 0;
                _currentSupplierSelectedIndex = -1;

                if (txtProductSearch != null)
                    txtProductSearch.Text = string.Empty;
                if (txtProductPlaceholder != null)
                    txtProductPlaceholder.Visibility = Visibility.Visible;
                _currentProductSelectedIndex = -1;

                if (txtProductCode != null)
                    txtProductCode.Text = string.Empty;

                if (cmbStores != null && cmbStores.SelectedItem == null && cmbStores.ItemsSource != null)
                {
                    var firstStore = cmbStores.ItemsSource.Cast<object>().FirstOrDefault();
                    if (firstStore != null)
                        cmbStores.SelectedItem = firstStore;
                }

                if (cmbPaymentMethod != null && cmbPaymentMethod.Items.Count > 0)
                    cmbPaymentMethod.SelectedIndex = 0;

                // ✅ إعادة تعيين ComboBox الوحدات
                if (cmbUnit != null && cmbUnit.Items.Count > 0)
                {
                    cmbUnit.SelectedIndex = 0;
                }

                UpdatePaymentPanelVisibility();
                CalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"NewInvoiceAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// دالة إضافة منتج إلى فاتورة المشتريات مع دعم الوحدات
        /// </summary>
        private async Task AddProductToInvoiceAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 بدء إضافة منتج إلى فاتورة المشتريات");
                System.Diagnostics.Debug.WriteLine("========================================");

                ProductSearchItem selectedProduct = GetSelectedProduct();

                if (selectedProduct == null)
                {
                    string productCode = txtProductCode?.Text?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(productCode))
                    {
                        selectedProduct = _allProductsList.FirstOrDefault(p =>
                            p.Code == productCode || p.Barcode == productCode);
                    }
                }

                if (selectedProduct == null)
                {
                    MessageBox.Show("اختر منتجاً من القائمة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtProductSearch != null) txtProductSearch.Focus();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"📦 المنتج المختار: ID={selectedProduct.Id}, Name={selectedProduct.Name}");
                System.Diagnostics.Debug.WriteLine($"   Unit1={selectedProduct.Unit1}, Unit1Factor={selectedProduct.Unit1Factor}");
                System.Diagnostics.Debug.WriteLine($"   Unit2={selectedProduct.Unit2}, Unit2Factor={selectedProduct.Unit2Factor}");
                System.Diagnostics.Debug.WriteLine($"   Unit3={selectedProduct.Unit3}");
                System.Diagnostics.Debug.WriteLine($"   Price1={selectedProduct.Price1}, Price2={selectedProduct.Price2}, Price3={selectedProduct.Price3}");

                if (txtQuantity == null || !decimal.TryParse(txtQuantity.Text, out decimal quantity) || quantity <= 0)
                {
                    MessageBox.Show("الكمية غير صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtQuantity != null)
                    {
                        txtQuantity.Text = "1";
                        txtQuantity.Focus();
                        txtQuantity.SelectAll();
                    }
                    return;
                }

                if (txtUnitPrice == null || !decimal.TryParse(txtUnitPrice.Text, out decimal unitPrice) || unitPrice < 0)
                {
                    MessageBox.Show("السعر غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtUnitPrice != null)
                    {
                        txtUnitPrice.Text = "0";
                        txtUnitPrice.Focus();
                        txtUnitPrice.SelectAll();
                    }
                    return;
                }

                // ✅ الحصول على الوحدة المختارة
                string selectedUnitName = "وحدة";
                string selectedUnitTag = "Unit3";
                if (cmbUnit != null && cmbUnit.SelectedItem is ComboBoxItem selectedUnit)
                {
                    selectedUnitName = selectedUnit.Content?.ToString() ?? "وحدة";
                    selectedUnitTag = selectedUnit.Tag?.ToString() ?? "Unit3";
                }

                System.Diagnostics.Debug.WriteLine($"📦 الوحدة المختارة: {selectedUnitName} (Tag: {selectedUnitTag})");
                System.Diagnostics.Debug.WriteLine($"📦 الكمية المدخلة: {quantity}");

                // ✅ ✅ ✅ تحويل الكمية إلى الوحدة الأساسية (Unit3) بناءً على الوحدة المختارة
                decimal quantityInBaseUnit = quantity;
                switch (selectedUnitTag)
                {
                    case "Unit1":
                        // ✅ الوحدة الكبرى (كرتونة): 1 كرتونة = Unit1Factor × Unit2Factor قطعة
                        quantityInBaseUnit = quantity * selectedProduct.Unit1Factor * selectedProduct.Unit2Factor;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit1: {quantity} × {selectedProduct.Unit1Factor} × {selectedProduct.Unit2Factor} = {quantityInBaseUnit}");
                        break;
                    case "Unit2":
                        // ✅ الوحدة الوسطى (علبة): 1 علبة = Unit2Factor قطعة
                        quantityInBaseUnit = quantity * selectedProduct.Unit2Factor;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit2: {quantity} × {selectedProduct.Unit2Factor} = {quantityInBaseUnit}");
                        break;
                    case "Unit3":
                    default:
                        // ✅ الوحدة الأساسية (قطعة): بدون تحويل
                        quantityInBaseUnit = quantity;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit3: {quantity} = {quantityInBaseUnit} (بدون تحويل)");
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"📊 الكمية المحولة إلى الوحدة الأساسية: {quantityInBaseUnit}");

                decimal discountPercent = 0;
                if (txtDiscountPercent != null && decimal.TryParse(txtDiscountPercent.Text, out decimal parsedDiscount))
                {
                    discountPercent = parsedDiscount;
                }

                if (discountPercent < 0) discountPercent = 0;
                if (discountPercent > 100) discountPercent = 100;

                if (txtDiscountPercent != null)
                    txtDiscountPercent.Text = discountPercent.ToString();

                // ✅ حساب السعر بناءً على الكمية الأصلية (المختارة)
                decimal subTotal = unitPrice * quantity;
                decimal discountAmount = subTotal * (discountPercent / 100);
                decimal totalAmount = subTotal - discountAmount;

                System.Diagnostics.Debug.WriteLine($"💰 السعر: UnitPrice={unitPrice}, SubTotal={subTotal}, Discount={discountPercent}%, DiscountAmount={discountAmount}, Total={totalAmount}");

                // ✅ إضافة اسم الوحدة إلى اسم المنتج المعروض
                string displayProductName = selectedProduct.Name;
                if (!string.IsNullOrEmpty(selectedUnitName) && selectedUnitName != "وحدة")
                {
                    displayProductName = $"{selectedProduct.Name} ({selectedUnitName})";
                }

                PurchaseInvoiceItem existingItem = null;
                if (_invoiceItems != null)
                {
                    // ✅ البحث عن عنصر بنفس المنتج ونفس الوحدة
                    existingItem = _invoiceItems.FirstOrDefault(x =>
                        x.ProductID == selectedProduct.Id &&
                        x.UnitTag == selectedUnitTag);
                }

                if (existingItem != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 تحديث منتج موجود: {existingItem.ProductName}");
                    System.Diagnostics.Debug.WriteLine($"   الكمية القديمة: {existingItem.Quantity}, الكمية الجديدة: {quantity}");
                    System.Diagnostics.Debug.WriteLine($"   الكمية المحولة القديمة: {existingItem.QuantityInBaseUnit}, الكمية المحولة الجديدة: {quantityInBaseUnit}");

                    // ✅ تحديث الكمية الأصلية والكمية المحولة
                    existingItem.Quantity += quantity;
                    existingItem.QuantityInBaseUnit += quantityInBaseUnit;
                    existingItem.UnitPrice = unitPrice;
                    existingItem.UnitName = selectedUnitName;
                    existingItem.UnitTag = selectedUnitTag;

                    decimal newSubTotal = existingItem.UnitPrice * existingItem.Quantity;
                    existingItem.DiscountAmount = newSubTotal * (existingItem.DiscountPercent / 100);
                    existingItem.TotalAmount = newSubTotal - existingItem.DiscountAmount;

                    System.Diagnostics.Debug.WriteLine($"   الكمية بعد التحديث: {existingItem.Quantity}, الكمية المحولة: {existingItem.QuantityInBaseUnit}");

                    MessageBox.Show($"تم تحديث كمية {displayProductName}", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateSerialNumbers();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"➕ إضافة منتج جديد: {displayProductName}");
                    if (_invoiceItems != null)
                    {
                        // ✅ ✅ ✅ إضافة QuantityInBaseUnit إلى العنصر الجديد
                        _invoiceItems.Add(new PurchaseInvoiceItem
                        {
                            ProductID = selectedProduct.Id,
                            ProductName = displayProductName,
                            Quantity = quantity,
                            QuantityInBaseUnit = quantityInBaseUnit,  // ✅ هذه هي الإضافة الجديدة
                            UnitPrice = unitPrice,
                            DiscountPercent = discountPercent,
                            DiscountAmount = discountAmount,
                            TotalAmount = totalAmount,
                            SerialNumber = _invoiceItems.Count + 1,
                            UnitName = selectedUnitName,
                            UnitTag = selectedUnitTag
                        });
                        System.Diagnostics.Debug.WriteLine($"✅ تمت الإضافة: Quantity={quantity}, QuantityInBaseUnit={quantityInBaseUnit}");
                    }
                    MessageBox.Show($"تم إضافة {displayProductName}", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // ✅ ✅ ✅ تحديث الرصيد بعد إضافة المنتج (مع الوحدة المختارة)
                _ = DisplayProductStockAsync(selectedProduct);

                if (txtProductCode != null)
                    txtProductCode.Text = string.Empty;

                if (txtProductSearch != null)
                    txtProductSearch.Text = string.Empty;

                if (txtProductPlaceholder != null)
                    txtProductPlaceholder.Visibility = Visibility.Visible;

                if (txtQuantity != null)
                    txtQuantity.Text = "1";

                if (txtUnitPrice != null)
                    txtUnitPrice.Text = "0";

                if (txtDiscountPercent != null)
                    txtDiscountPercent.Text = "0";

                UpdateSerialNumbers();
                CalculateTotals();

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ انتهاء إضافة المنتج");
                System.Diagnostics.Debug.WriteLine("========================================");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"AddProductToInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void CalculateTotals()
        {
            try
            {
                if (_isUpdatingTotals) return;
                _isUpdatingTotals = true;

                decimal subTotal = 0;
                decimal totalDiscount = 0;

                if (_invoiceItems != null)
                {
                    foreach (PurchaseInvoiceItem item in _invoiceItems)
                    {
                        if (item != null)
                        {
                            decimal itemSubTotal = item.UnitPrice * item.Quantity;
                            decimal itemDiscount = (itemSubTotal * item.DiscountPercent) / 100;
                            subTotal += itemSubTotal;
                            totalDiscount += itemDiscount;
                        }
                    }
                }

                decimal afterDiscount = subTotal - totalDiscount;
                decimal taxAmount = afterDiscount * (_currentTaxPercent / 100);
                decimal grandTotal = afterDiscount + taxAmount;

                if (lblSubTotal != null)
                    lblSubTotal.Text = subTotal.ToString("N2");

                if (lblTotalDiscount != null)
                    lblTotalDiscount.Text = totalDiscount.ToString("N2");

                if (lblTaxAmount != null)
                    lblTaxAmount.Text = taxAmount.ToString("N2");

                if (lblGrandTotal != null)
                    lblGrandTotal.Text = grandTotal.ToString("N2");

                string arabicPaymentMethod = "نقدي";
                if (cmbPaymentMethod != null && cmbPaymentMethod.SelectedItem is ComboBoxItem selectedPayment)
                {
                    arabicPaymentMethod = selectedPayment.Content?.ToString() ?? "نقدي";
                }

                if (arabicPaymentMethod == "أجل")
                {
                    if (txtPaidAmount != null)
                        txtPaidAmount.Text = "0.00";

                    if (lblRemainingAmount != null)
                    {
                        lblRemainingAmount.Text = grandTotal.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }
                else
                {
                    if (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paidAmount) && lblRemainingAmount != null)
                    {
                        decimal remainingAmount = grandTotal - paidAmount;
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");

                        if (remainingAmount <= 0)
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        }
                        else
                        {
                            lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        }
                    }
                    else if (lblRemainingAmount != null)
                    {
                        lblRemainingAmount.Text = grandTotal.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CalculateTotals Error: {ex.Message}");
            }
            finally
            {
                _isUpdatingTotals = false;
            }
        }

        private async Task SaveInvoiceAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 بدء حفظ فاتورة المشتريات...");

                SupplierSearchItem selectedSupplier = GetSelectedSupplier();

                if (selectedSupplier == null)
                {
                    MessageBox.Show("اختر المورد", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtSupplierSearch != null) txtSupplierSearch.Focus();
                    return;
                }

                if (cmbStores == null || cmbStores.SelectedItem == null)
                {
                    MessageBox.Show("اختر المخزن", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (cmbStores != null) cmbStores.Focus();
                    return;
                }

                if (_invoiceItems == null || _invoiceItems.Count == 0)
                {
                    MessageBox.Show("أضف منتجاً واحداً على الأقل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtProductSearch != null) txtProductSearch.Focus();
                    return;
                }

                int supplierId = selectedSupplier.Id;
                int storeId = ((StoreSimple)cmbStores.SelectedItem).Id;
                DateTime invoiceDate = (dpInvoiceDate != null && dpInvoiceDate.SelectedDate.HasValue) ? dpInvoiceDate.SelectedDate.Value : DateTime.Now;

                string arabicPaymentMethod = "نقدي";
                if (cmbPaymentMethod != null && cmbPaymentMethod.SelectedItem is ComboBoxItem selectedPayment)
                {
                    arabicPaymentMethod = selectedPayment.Content?.ToString() ?? "نقدي";
                }
                string paymentMethod = ConvertPaymentMethodToEnglish(arabicPaymentMethod);

                string referenceNumber = (txtReferenceNumber != null) ? txtReferenceNumber.Text?.Trim() ?? string.Empty : string.Empty;
                string notes = (txtNotes != null) ? txtNotes.Text?.Trim() ?? string.Empty : string.Empty;

                decimal subTotal = decimal.Parse(lblSubTotal?.Text ?? "0");
                decimal totalDiscount = decimal.Parse(lblTotalDiscount?.Text ?? "0");
                decimal taxAmount = decimal.Parse(lblTaxAmount?.Text ?? "0");
                decimal grandTotal = decimal.Parse(lblGrandTotal?.Text ?? "0");
                decimal paidAmount = 0;

                if (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paidAmountValue))
                {
                    paidAmount = paidAmountValue;
                }

                if (arabicPaymentMethod == "أجل")
                {
                    paidAmount = 0;
                }

                if (paidAmount > grandTotal)
                {
                    paidAmount = grandTotal;
                }

                // ✅ إنشاء قائمة العناصر مع الكمية المحولة للوحدة الأساسية
                var itemsList = _invoiceItems.Select(item => new InvoiceService.PurchaseInvoiceItemClass
                {
                    ProductID = item.ProductID,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    QuantityInBaseUnit = item.QuantityInBaseUnit, // ✅ إضافة الكمية المحولة
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    DiscountAmount = item.DiscountAmount,
                    TotalAmount = item.TotalAmount
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"📊 عدد العناصر في الفاتورة: {_invoiceItems.Count}");
                foreach (var item in _invoiceItems)
                {
                    System.Diagnostics.Debug.WriteLine($"   - {item.ProductName} | الكمية: {item.Quantity} | الوحدة: {item.UnitName} | الكمية المحولة: {item.QuantityInBaseUnit}");
                }

                bool result = false;
                bool isEditing = _currentEditingInvoiceId > 0;

                int? treasuryId = null;
                string checkNumber = null;
                DateTime? checkDate = null;
                string bankName = null;

                if (arabicPaymentMethod != "أجل" && paidAmount > 0)
                {
                    if (arabicPaymentMethod == "نقدي")
                    {
                        if (cmbTreasury == null || cmbTreasury.SelectedItem == null)
                        {
                            MessageBox.Show("اختر الخزينة للدفع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        treasuryId = ((TreasuryData)cmbTreasury.SelectedItem).Id;
                    }
                    else if (arabicPaymentMethod == "تحويل بنكي")
                    {
                        if (cmbBankAccount == null || cmbBankAccount.SelectedItem == null)
                        {
                            MessageBox.Show("اختر الحساب البنكي", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        bankName = ((BankAccountData)cmbBankAccount.SelectedItem).BankName;
                    }
                    else if (arabicPaymentMethod == "شيك")
                    {
                        if (txtCheckNumber == null || string.IsNullOrWhiteSpace(txtCheckNumber.Text))
                        {
                            MessageBox.Show("أدخل رقم الشيك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            if (txtCheckNumber != null) txtCheckNumber.Focus();
                            return;
                        }
                        checkNumber = txtCheckNumber.Text.Trim();
                        checkDate = (dpCheckDate != null && dpCheckDate.SelectedDate.HasValue) ? dpCheckDate.SelectedDate.Value : DateTime.Now;
                        bankName = (txtCheckBankName != null) ? txtCheckBankName.Text?.Trim() ?? "" : "";
                    }
                }

                if (isEditing)
                {
                    result = await _invoiceService.UpdatePurchaseInvoiceAsync(
                        _currentEditingInvoiceId,
                        _currentInvoiceNumber,
                        invoiceDate,
                        supplierId,
                        storeId,
                        subTotal,
                        totalDiscount,
                        taxAmount,
                        _currentTaxPercent,
                        grandTotal,
                        paymentMethod,
                        referenceNumber,
                        notes,
                        itemsList,
                        _currentUserId,
                        treasuryId,
                        checkNumber,
                        checkDate,
                        bankName
                    );
                }
                else
                {
                    result = await _invoiceService.SavePurchaseInvoiceAsync(
                        _currentInvoiceNumber,
                        invoiceDate,
                        supplierId,
                        storeId,
                        subTotal,
                        totalDiscount,
                        taxAmount,
                        _currentTaxPercent,
                        grandTotal,
                        paymentMethod,
                        referenceNumber,
                        notes,
                        itemsList,
                        _currentUserId,
                        treasuryId,
                        checkNumber,
                        checkDate,
                        bankName
                    );
                }

                if (result)
                {
                    string successMessage = isEditing ? "تم تحديث الفاتورة بنجاح" : "تم حفظ الفاتورة بنجاح";

                    if (paidAmount > 0 && paidAmount < grandTotal)
                    {
                        successMessage += $"\n\nملاحظة: تم تسديد دفعة مقدمة بقيمة {paidAmount:N2}\nالمبلغ المتبقي: {(grandTotal - paidAmount):N2}";
                    }

                    MessageBoxResult printResult = MessageBox.Show(
                        $"{successMessage}\n\nهل تريد طباعة الفاتورة؟",
                        "تم الحفظ بنجاح",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (printResult == MessageBoxResult.Yes)
                    {
                        await PrintInvoiceAsync();
                    }

                    await NewInvoiceAsync();
                }
                else
                {
                    MessageBox.Show("حدث خطأ أثناء حفظ الفاتورة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"SaveInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Company Data Methods

        private async Task<CompanyData> GetCompanyDataAsync()
        {
            CompanyData companyData = new CompanyData();

            try
            {
                await DatabaseExecutor.ExecuteNonTransactionAsync<CompanyData>(_databaseService, async (connection) =>
                {
                    string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='CompanySettings'";
                    using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                    {
                        object result = await checkCmd.ExecuteScalarAsync();
                        if (result == null)
                        {
                            companyData.CompanyName = "";
                            companyData.LogoPath = "";
                            companyData.Email = "";
                            companyData.Phone = "";
                            companyData.Address = "";
                            companyData.TaxNumber = "";
                            return companyData;
                        }
                    }

                    string sql = "SELECT CompanyName, LogoPath, Email, Phone, Address, TaxNumber FROM CompanySettings LIMIT 1";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            companyData.CompanyName = reader["CompanyName"]?.ToString() ?? "";
                            companyData.LogoPath = reader["LogoPath"]?.ToString() ?? "";
                            companyData.Email = reader["Email"]?.ToString() ?? "";
                            companyData.Phone = reader["Phone"]?.ToString() ?? "";
                            companyData.Address = reader["Address"]?.ToString() ?? "";
                            companyData.TaxNumber = reader["TaxNumber"]?.ToString() ?? "";
                        }
                        else
                        {
                            companyData.CompanyName = "";
                            companyData.LogoPath = "";
                            companyData.Email = "";
                            companyData.Phone = "";
                            companyData.Address = "";
                            companyData.TaxNumber = "";
                        }
                    }
                    return companyData;
                }, CancellationToken.None, 3);

                if (!string.IsNullOrEmpty(companyData.LogoPath) && !File.Exists(companyData.LogoPath))
                {
                    companyData.LogoPath = "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCompanyDataAsync Error: {ex.Message}");
                companyData.CompanyName = "";
                companyData.LogoPath = "";
                companyData.Email = "";
                companyData.Phone = "";
                companyData.Address = "";
                companyData.TaxNumber = "";
            }

            return companyData;
        }

        #endregion

        #region Print Helper Methods

        private Size MeasureStringForPrint(string text, Typeface typeface, double fontSize)
        {
            try
            {
                FormattedText formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.GetCultureInfo("ar-SA"),
                    System.Windows.FlowDirection.RightToLeft,
                    typeface,
                    fontSize,
                    System.Windows.Media.Brushes.Black,
                    new NumberSubstitution(),
                    1);
                return new Size(formattedText.Width, formattedText.Height);
            }
            catch
            {
                return new Size(100, 20);
            }
        }

        private void AddDottedLine(Canvas canvas, double startX, double endX, double y)
        {
            System.Windows.Shapes.Line dottedLine = new System.Windows.Shapes.Line();
            dottedLine.X1 = startX;
            dottedLine.X2 = endX;
            dottedLine.Y1 = y;
            dottedLine.Y2 = y;
            dottedLine.Stroke = System.Windows.Media.Brushes.Gray;
            dottedLine.StrokeThickness = 1;
            dottedLine.StrokeDashArray = new DoubleCollection { 3, 3 };
            dottedLine.Opacity = 0.6;
            canvas.Children.Add(dottedLine);
        }

        private void AddInfoRowRightAligned(Canvas canvas, double x, double y, string label, string value)
        {
            Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

            TextBlock labelText = new TextBlock();
            labelText.Text = label;
            labelText.FontSize = 11;
            labelText.FontWeight = FontWeights.SemiBold;
            labelText.Foreground = System.Windows.Media.Brushes.Gray;
            labelText.FontFamily = new FontFamily("Segoe UI");

            Canvas.SetLeft(labelText, x);
            Canvas.SetTop(labelText, y);
            canvas.Children.Add(labelText);

            Size labelSize = MeasureStringForPrint(label, typeface, 11);

            TextBlock valueText = new TextBlock();
            valueText.Text = value;
            valueText.FontSize = 11;
            valueText.Foreground = System.Windows.Media.Brushes.Black;
            valueText.FontWeight = FontWeights.SemiBold;
            valueText.FontFamily = new FontFamily("Segoe UI");

            Canvas.SetLeft(valueText, x + labelSize.Width + 10);
            Canvas.SetTop(valueText, y);
            canvas.Children.Add(valueText);
        }

        private void AddSummaryRowRightAligned(Canvas canvas, double x, double y, string label, string value, bool isBold, double rowWidth, Brush valueColor = null)
        {
            TextBlock labelText = new TextBlock();
            labelText.Text = label;
            labelText.FontSize = isBold ? 13 : 11;
            labelText.FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal;
            labelText.Foreground = System.Windows.Media.Brushes.Gray;
            labelText.FontFamily = new FontFamily("Segoe UI");

            Canvas.SetLeft(labelText, x);
            Canvas.SetTop(labelText, y);
            canvas.Children.Add(labelText);

            TextBlock valueText = new TextBlock();
            valueText.Text = value;
            valueText.FontSize = isBold ? 16 : 12;
            valueText.FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold;
            valueText.Foreground = valueColor ?? System.Windows.Media.Brushes.Black;
            valueText.FontFamily = new FontFamily("Segoe UI");
            valueText.TextAlignment = TextAlignment.Left;

            Canvas.SetLeft(valueText, x + rowWidth - 80);
            Canvas.SetTop(valueText, y);
            canvas.Children.Add(valueText);
        }

        private FrameworkElement CreateLogoImageForPrint(string logoPath, double maxWidth, double maxHeight)
        {
            try
            {
                if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    Image logoImage = new Image();
                    logoImage.Source = bitmapImage;
                    logoImage.Width = maxWidth;
                    logoImage.Height = maxHeight;
                    logoImage.Stretch = Stretch.Uniform;
                    logoImage.HorizontalAlignment = HorizontalAlignment.Left;

                    return logoImage;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateLogoImageForPrint Error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Main Print Method

        private async Task PrintInvoiceAsync()
        {
            try
            {
                if (_invoiceItems == null || _invoiceItems.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CompanyData companyData = await GetCompanyDataAsync();

                FixedDocument fixedDocument = new FixedDocument();

                double pageWidth = 800;
                double pageHeight = 1100;

                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = pageWidth;
                fixedPage.Height = pageHeight;
                fixedPage.FlowDirection = FlowDirection.LeftToRight;
                fixedPage.Language = System.Windows.Markup.XmlLanguage.GetLanguage("ar-SA");

                Canvas canvas = new Canvas();
                canvas.Width = pageWidth;
                canvas.Height = pageHeight;

                string supplierName = txtSupplierSearch?.Text ?? "";
                string storeName = (cmbStores != null && cmbStores.SelectedItem != null) ? ((StoreSimple)cmbStores.SelectedItem).Name : "";
                string paymentMethod = (cmbPaymentMethod != null && cmbPaymentMethod.SelectedItem != null) ? ((ComboBoxItem)cmbPaymentMethod.SelectedItem).Content?.ToString() : "";

                decimal subTotal = (lblSubTotal != null) ? decimal.Parse(lblSubTotal.Text) : 0;
                decimal totalDiscount = (lblTotalDiscount != null) ? decimal.Parse(lblTotalDiscount.Text) : 0;
                decimal taxAmount = (lblTaxAmount != null) ? decimal.Parse(lblTaxAmount.Text) : 0;
                decimal grandTotal = (lblGrandTotal != null) ? decimal.Parse(lblGrandTotal.Text) : 0;
                decimal paidAmount = (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paid)) ? paid : 0;
                decimal remainingAmount = grandTotal - paidAmount;

                double currentY = 20;
                double rightMargin = pageWidth - 30;
                double leftMargin = 30;

                double logoWidth = 100;
                double logoHeight = 50;
                double logoX = rightMargin - logoWidth;

                bool logoAdded = false;
                if (!string.IsNullOrEmpty(companyData.LogoPath) && File.Exists(companyData.LogoPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage(new Uri(companyData.LogoPath));
                        var logo = new Image { Source = bitmap, Width = logoWidth, Height = logoHeight, Stretch = Stretch.Uniform };
                        Canvas.SetLeft(logo, logoX);
                        Canvas.SetTop(logo, currentY);
                        canvas.Children.Add(logo);
                        logoAdded = true;
                    }
                    catch { }
                }

                if (!logoAdded)
                {
                    string defaultLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Rasid.png");
                    if (File.Exists(defaultLogo))
                    {
                        try
                        {
                            var bitmap = new BitmapImage(new Uri(defaultLogo));
                            var logo = new Image { Source = bitmap, Width = logoWidth, Height = logoHeight, Stretch = Stretch.Uniform };
                            Canvas.SetLeft(logo, logoX);
                            Canvas.SetTop(logo, currentY);
                            canvas.Children.Add(logo);
                            logoAdded = true;
                        }
                        catch { }
                    }
                }

                if (logoAdded) currentY += logoHeight + 10;
                else currentY += 10;

                if (!string.IsNullOrEmpty(companyData.CompanyName))
                {
                    var companyName = new TextBlock
                    {
                        Text = companyData.CompanyName,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 51, 102)),
                        TextAlignment = TextAlignment.Right,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 180
                    };
                    Canvas.SetLeft(companyName, rightMargin - 180);
                    Canvas.SetTop(companyName, currentY);
                    canvas.Children.Add(companyName);
                    currentY += 50;
                }
                else
                {
                    currentY += 10;
                }

                var title = new TextBlock
                {
                    Text = "فاتورة مشتريات",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Arial"),
                    TextAlignment = TextAlignment.Center,
                    Width = 200,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(title, (pageWidth - 200) / 2);
                Canvas.SetTop(title, currentY - 35);
                canvas.Children.Add(title);

                double infoX = leftMargin;
                double infoY = currentY - 65;

                void AddCompanyInfo(string text)
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        var tb = new TextBlock
                        {
                            Text = text,
                            FontSize = 10,
                            Foreground = Brushes.Black,
                            TextAlignment = TextAlignment.Right,
                            FontFamily = new FontFamily("Segoe UI")
                        };
                        Canvas.SetLeft(tb, infoX);
                        Canvas.SetTop(tb, infoY);
                        canvas.Children.Add(tb);
                        infoY += 18;
                    }
                }

                if (!string.IsNullOrEmpty(companyData.TaxNumber)) AddCompanyInfo($"الرقم الضريبي: {companyData.TaxNumber}");
                if (!string.IsNullOrEmpty(companyData.Phone)) AddCompanyInfo($"هاتف: {companyData.Phone}");
                if (!string.IsNullOrEmpty(companyData.Email)) AddCompanyInfo($"بريد: {companyData.Email}");
                if (!string.IsNullOrEmpty(companyData.Address)) AddCompanyInfo(companyData.Address);

                currentY += 30;
                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 20;

                double offset5cm = 140;
                double col1X = leftMargin + offset5cm;
                double col2X = (pageWidth / 2) + offset5cm;

                void AddInvoiceInfo(string label, string value, double x, double y)
                {
                    double valueWidth = 130;
                    double labelWidth = 80;
                    double spacing = 2;

                    var valueText = new TextBlock
                    {
                        Text = value,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.Black,
                        FontFamily = new FontFamily("Segoe UI"),
                        TextAlignment = TextAlignment.Right,
                        Width = valueWidth
                    };
                    Canvas.SetLeft(valueText, x);
                    Canvas.SetTop(valueText, y);
                    canvas.Children.Add(valueText);

                    var labelText = new TextBlock
                    {
                        Text = label,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.Black,
                        FontFamily = new FontFamily("Segoe UI"),
                        FlowDirection = FlowDirection.RightToLeft,
                        TextAlignment = TextAlignment.Left,
                        Width = labelWidth
                    };
                    Canvas.SetLeft(labelText, x + valueWidth + spacing);
                    Canvas.SetTop(labelText, y);
                    canvas.Children.Add(labelText);
                }

                double infoRowY = currentY;

                string invoiceDateStr = (dpInvoiceDate != null && dpInvoiceDate.SelectedDate.HasValue) ? dpInvoiceDate.SelectedDate.Value.ToString("yyyy/MM/dd") : DateTime.Now.ToString("yyyy/MM/dd");

                AddInvoiceInfo("التاريخ:", invoiceDateStr, col1X, infoRowY);
                AddInvoiceInfo("رقم الفاتورة:", _currentInvoiceNumber, col2X, infoRowY);
                infoRowY += 22;

                AddInvoiceInfo("المخزن:", storeName, col1X, infoRowY);
                AddInvoiceInfo("المورد:", supplierName, col2X, infoRowY);
                infoRowY += 22;

                string notesText = (txtNotes != null) ? txtNotes.Text : "";
                if (!string.IsNullOrEmpty(notesText))
                {
                    AddInvoiceInfo("ملاحظات:", notesText, col1X, infoRowY);
                    AddInvoiceInfo("طريقة الدفع:", paymentMethod, col2X, infoRowY);
                }
                else
                {
                    AddInvoiceInfo("طريقة الدفع:", paymentMethod, col2X, infoRowY);
                }
                infoRowY += 22;

                string referenceText = (txtReferenceNumber != null) ? txtReferenceNumber.Text : "";
                if (!string.IsNullOrEmpty(referenceText))
                {
                    AddInvoiceInfo("رقم المرجع:", referenceText, col2X, infoRowY);
                    infoRowY += 22;
                }

                if (paidAmount > 0)
                {
                    AddInvoiceInfo("المبلغ المدفوع:", paidAmount.ToString("N2"), col1X, infoRowY);
                    AddInvoiceInfo("المبلغ المتبقي:", remainingAmount.ToString("N2"), col2X, infoRowY);
                    infoRowY += 22;
                }

                currentY = infoRowY + 20;
                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 20;

                double[] colWidths = { 45, 230, 70, 90, 70, 90, 100 };
                double[] colStarts = new double[7];

                double startX = rightMargin;
                for (int i = 0; i < colWidths.Length; i++)
                {
                    startX -= colWidths[i];
                    colStarts[i] = startX;
                }

                string[] headers = { "م", "المنتج", "الكمية", "سعر الشراء", "الخصم %", "مبلغ الخصم", "الإجمالي" };
                double headerHeight = 35;
                double headerY = currentY;

                for (int i = 0; i < headers.Length; i++)
                {
                    var headerBg = new Border
                    {
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1),
                        Background = Brushes.LightGray,
                        Width = colWidths[i],
                        Height = headerHeight
                    };
                    Canvas.SetLeft(headerBg, colStarts[i]);
                    Canvas.SetTop(headerBg, headerY);
                    canvas.Children.Add(headerBg);

                    var headerText = new TextBlock
                    {
                        Text = headers[i],
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Width = colWidths[i],
                        Height = headerHeight,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(headerText, colStarts[i]);
                    Canvas.SetTop(headerText, headerY + (headerHeight - 20) / 2);
                    canvas.Children.Add(headerText);
                }
                currentY += headerHeight;

                double rowHeight = 28;
                int visibleRows = 15;
                int maxRows = Math.Max(_invoiceItems.Count, visibleRows);

                for (int i = 0; i < maxRows; i++)
                {
                    double rowY = currentY + (i * rowHeight);

                    for (int j = 0; j < colWidths.Length; j++)
                    {
                        var cellBorder = new Border
                        {
                            BorderBrush = Brushes.LightGray,
                            BorderThickness = new Thickness(1),
                            Width = colWidths[j],
                            Height = rowHeight
                        };
                        Canvas.SetLeft(cellBorder, colStarts[j]);
                        Canvas.SetTop(cellBorder, rowY);
                        canvas.Children.Add(cellBorder);

                        string cellText = "";
                        if (i < _invoiceItems.Count && _invoiceItems[i] != null)
                        {
                            var item = _invoiceItems[i];
                            switch (j)
                            {
                                case 0: cellText = item.SerialNumber.ToString(); break;
                                case 1: cellText = item.ProductName; break;
                                case 2: cellText = item.Quantity.ToString("N0"); break;
                                case 3: cellText = item.UnitPrice.ToString("N2"); break;
                                case 4: cellText = item.DiscountPercent.ToString("N0"); break;
                                case 5: cellText = item.DiscountAmount.ToString("N2"); break;
                                case 6: cellText = item.TotalAmount.ToString("N2"); break;
                            }
                        }

                        if (!string.IsNullOrEmpty(cellText))
                        {
                            double textY = rowY + (rowHeight - 16) / 2;

                            var cellTextBlock = new TextBlock
                            {
                                Text = cellText,
                                FontSize = 10,
                                FontFamily = new FontFamily("Segoe UI"),
                                TextAlignment = TextAlignment.Center,
                                Width = colWidths[j],
                                Foreground = Brushes.Black
                            };
                            Canvas.SetLeft(cellTextBlock, colStarts[j]);
                            Canvas.SetTop(cellTextBlock, textY);
                            canvas.Children.Add(cellTextBlock);
                        }
                    }
                }

                currentY += (maxRows * rowHeight) + 10;
                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 15;

                double summaryWidth = 250;
                double summaryX = leftMargin;

                void AddSummaryRow(string label, string value, bool isBold, Brush color = null)
                {
                    double valueWidth = 110;
                    double labelWidth = 130;
                    double spacing = 5;

                    var valueText = new TextBlock
                    {
                        Text = value,
                        FontSize = isBold ? 14 : 11,
                        FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold,
                        Foreground = color ?? Brushes.Black,
                        TextAlignment = TextAlignment.Left,
                        Width = valueWidth,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Canvas.SetLeft(valueText, summaryX);
                    Canvas.SetTop(valueText, currentY);
                    canvas.Children.Add(valueText);

                    var labelText = new TextBlock
                    {
                        Text = label,
                        FontSize = isBold ? 13 : 11,
                        FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = Brushes.Black,
                        TextAlignment = TextAlignment.Right,
                        Width = labelWidth,
                        FontFamily = new FontFamily("Segoe UI"),
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Canvas.SetLeft(labelText, summaryX + valueWidth + spacing);
                    Canvas.SetTop(labelText, currentY);
                    canvas.Children.Add(labelText);
                    currentY += 26;
                }

                AddSummaryRow("المجموع الفرعي:", subTotal.ToString("N2"), false);
                AddSummaryRow("إجمالي الخصم:", totalDiscount.ToString("N2"), false, Brushes.Red);
                AddSummaryRow($"الضريبة ({_currentTaxPercent}%):", taxAmount.ToString("N2"), false);

                var line = new Border
                {
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Width = summaryWidth
                };
                Canvas.SetLeft(line, summaryX);
                Canvas.SetTop(line, currentY);
                canvas.Children.Add(line);
                currentY += 15;

                AddSummaryRow("الإجمالي النهائي:", grandTotal.ToString("N2"), true);

                if (paidAmount > 0)
                {
                    currentY += 15;
                    AddSummaryRow("المبلغ المدفوع:", paidAmount.ToString("N2"), false, new SolidColorBrush(Color.FromRgb(34, 197, 94)));
                    AddSummaryRow("المبلغ المتبقي:", remainingAmount.ToString("N2"), false, new SolidColorBrush(Color.FromRgb(239, 68, 68)));
                }

                currentY += 50;

                double sigWidth = 180;
                double sigHeight = 50;
                double supplierSigX = rightMargin - sigWidth - 10;
                double companySigX = leftMargin;

                void AddSignature(string sigTitle, double x)
                {
                    var border = new Border
                    {
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Width = sigWidth,
                        Height = sigHeight
                    };
                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, currentY);
                    canvas.Children.Add(border);

                    var text = new TextBlock
                    {
                        Text = sigTitle,
                        FontSize = 11,
                        Foreground = Brushes.Black,
                        TextAlignment = TextAlignment.Center,
                        Width = sigWidth,
                        FontFamily = new FontFamily("Segoe UI"),
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    Canvas.SetLeft(text, x);
                    Canvas.SetTop(text, currentY + sigHeight + 5);
                    canvas.Children.Add(text);
                }

                AddSignature("توقيع المستلم", supplierSigX);
                AddSignature("توقيع الشركة وختمها", companySigX);
                currentY += sigHeight + 40;

                if (currentY < pageHeight - 50)
                {
                    var thanks = new TextBlock
                    {
                        Text = "شكراً لتعاملكم معنا",
                        FontSize = 12,
                        FontStyle = FontStyles.Italic,
                        Foreground = Brushes.Black,
                        TextAlignment = TextAlignment.Center,
                        Width = pageWidth - 60,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Canvas.SetLeft(thanks, leftMargin);
                    Canvas.SetTop(thanks, currentY);
                    canvas.Children.Add(thanks);
                }

                fixedPage.Children.Add(canvas);
                var pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);
                fixedDocument.Pages.Add(pageContent);

                var previewWindow = new PrintPreviewWindow(fixedDocument);
                previewWindow.Owner = Window.GetWindow(this);
                previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في معاينة الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"PrintInvoiceAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region Direct Table Editing

        private void dgInvoiceItems_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                var column = e.Column as DataGridTextColumn;
                if (column != null)
                {
                    string header = column.Header?.ToString();
                    if (header != "الكمية" && header != "سعر الشراء" && header != "الخصم %")
                    {
                        e.Cancel = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BeginningEdit Error: {ex.Message}");
            }
        }

        private async void dgInvoiceItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                var editedItem = e.Row.Item as PurchaseInvoiceItem;
                if (editedItem == null) return;

                var editColumn = e.Column as DataGridTextColumn;
                if (editColumn == null) return;

                var textBox = e.EditingElement as TextBox;
                if (textBox == null) return;

                string header = editColumn.Header?.ToString();
                bool needsUpdate = true;

                switch (header)
                {
                    case "الكمية":
                        if (decimal.TryParse(textBox.Text, out decimal newQuantity))
                        {
                            if (newQuantity < 0) newQuantity = 0;
                            if (newQuantity > 1000000) newQuantity = 1000000;
                            editedItem.Quantity = newQuantity;
                        }
                        else
                        {
                            textBox.Text = editedItem.Quantity.ToString();
                            needsUpdate = false;
                        }
                        break;

                    case "سعر الشراء":
                        if (decimal.TryParse(textBox.Text, out decimal newPrice))
                        {
                            if (newPrice < 0) newPrice = 0;
                            editedItem.UnitPrice = newPrice;
                        }
                        else
                        {
                            textBox.Text = editedItem.UnitPrice.ToString("N2");
                            needsUpdate = false;
                        }
                        break;

                    case "الخصم %":
                        if (decimal.TryParse(textBox.Text, out decimal newDiscount))
                        {
                            if (newDiscount < 0) newDiscount = 0;
                            if (newDiscount > 100) newDiscount = 100;
                            editedItem.DiscountPercent = newDiscount;
                            textBox.Text = newDiscount.ToString();
                        }
                        else
                        {
                            textBox.Text = editedItem.DiscountPercent.ToString();
                            needsUpdate = false;
                        }
                        break;
                }

                if (needsUpdate)
                {
                    await RecalculateItemAmountsAsync(editedItem);
                    RefreshDataGridDisplay();
                    CalculateTotals();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CellEditEnding Error: {ex.Message}");
            }
        }

        private async Task RecalculateItemAmountsAsync(PurchaseInvoiceItem item)
        {
            try
            {
                await Task.Run(() =>
                {
                    decimal itemSubTotal = item.UnitPrice * item.Quantity;
                    item.DiscountAmount = itemSubTotal * (item.DiscountPercent / 100);
                    item.TotalAmount = itemSubTotal - item.DiscountAmount;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateItemAmountsAsync Error: {ex.Message}");
            }
        }

        private void RefreshDataGridDisplay()
        {
            try
            {
                if (dgInvoiceItems != null && dgInvoiceItems.ItemsSource != null)
                {
                    var temp = dgInvoiceItems.ItemsSource;
                    dgInvoiceItems.ItemsSource = null;
                    dgInvoiceItems.ItemsSource = temp;
                    UpdateSerialNumbers();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshDataGridDisplay Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال AutoComplete للموردين (Supplier AutoComplete Methods)

        private void SelectSupplierFromList(SupplierSearchItem supplier)
        {
            if (supplier == null) return;

            _isSelectingSupplierFromList = true;
            _selectedSupplierId = supplier.Id;
            txtSupplierSearch.Text = supplier.Name;
            if (popupSuppliers != null) popupSuppliers.IsOpen = false;
            _isSupplierPopupOpen = false;
            _currentSupplierSelectedIndex = -1;

            _isSelectingSupplierFromList = false;
            System.Diagnostics.Debug.WriteLine($"✅ تم اختيار المورد: {supplier.Name} (ID: {supplier.Id})");
        }

        private SupplierSearchItem GetSelectedSupplier()
        {
            if (txtSupplierSearch == null) return null;

            if (!string.IsNullOrWhiteSpace(txtSupplierSearch.Text))
            {
                var supplier = _allSuppliersList.FirstOrDefault(s => s.Name == txtSupplierSearch.Text);
                if (supplier != null) return supplier;

                supplier = _allSuppliersList.FirstOrDefault(s => s.Code == txtSupplierSearch.Text);
                if (supplier != null) return supplier;
            }

            return null;
        }

        #endregion

        #region دوال أحداث AutoComplete للموردين (Supplier AutoComplete Event Handlers)

        private void TxtSupplierSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSupplierPlaceholder != null) txtSupplierPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void TxtSupplierSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSupplierSearch.Text) && txtSupplierPlaceholder != null)
                txtSupplierPlaceholder.Visibility = Visibility.Visible;

            Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (popupSuppliers != null && lstSuppliers != null && !lstSuppliers.IsMouseOver && !lstSuppliers.IsKeyboardFocusWithin)
                {
                    popupSuppliers.IsOpen = false;
                    _isSupplierPopupOpen = false;
                }
            }));
        }

        private void TxtSupplierSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingSupplierFromList) return;

            string searchText = txtSupplierSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupSuppliers != null)
                {
                    popupSuppliers.IsOpen = false;
                    _isSupplierPopupOpen = false;
                }
                _currentSupplierSelectedIndex = -1;
                return;
            }

            _currentFilteredSuppliers = _allSuppliersList
                .Where(s => s.Name != null && s.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (lstSuppliers != null)
            {
                lstSuppliers.ItemsSource = _currentFilteredSuppliers;
                lstSuppliers.SelectedIndex = -1;
                _currentSupplierSelectedIndex = -1;
            }

            if (popupSuppliers != null)
            {
                if (_currentFilteredSuppliers.Count > 0 && !string.IsNullOrEmpty(searchText))
                {
                    popupSuppliers.IsOpen = true;
                    _isSupplierPopupOpen = true;
                }
                else
                {
                    popupSuppliers.IsOpen = false;
                    _isSupplierPopupOpen = false;
                }
            }
        }

        private void TxtSupplierSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;

                    if (!_isSupplierPopupOpen && _currentFilteredSuppliers.Count > 0 && popupSuppliers != null)
                    {
                        popupSuppliers.IsOpen = true;
                        _isSupplierPopupOpen = true;
                    }

                    if (lstSuppliers != null && lstSuppliers.Items.Count > 0)
                    {
                        if (_currentSupplierSelectedIndex < lstSuppliers.Items.Count - 1)
                        {
                            _currentSupplierSelectedIndex++;
                            lstSuppliers.SelectedIndex = _currentSupplierSelectedIndex;
                            lstSuppliers.ScrollIntoView(lstSuppliers.SelectedItem);
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;

                    if (lstSuppliers != null && lstSuppliers.Items.Count > 0 && _currentSupplierSelectedIndex > 0)
                    {
                        _currentSupplierSelectedIndex--;
                        lstSuppliers.SelectedIndex = _currentSupplierSelectedIndex;
                        lstSuppliers.ScrollIntoView(lstSuppliers.SelectedItem);
                    }
                    else if (_currentSupplierSelectedIndex == 0 && lstSuppliers != null)
                    {
                        _currentSupplierSelectedIndex = -1;
                        lstSuppliers.SelectedIndex = -1;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (_currentSupplierSelectedIndex >= 0 && lstSuppliers != null && _currentSupplierSelectedIndex < lstSuppliers.Items.Count)
                    {
                        e.Handled = true;
                        var selectedSupplier = lstSuppliers.SelectedItem as SupplierSearchItem;
                        if (selectedSupplier != null)
                        {
                            SelectSupplierFromList(selectedSupplier);
                        }
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (popupSuppliers != null)
                    {
                        popupSuppliers.IsOpen = false;
                        _isSupplierPopupOpen = false;
                    }
                    _currentSupplierSelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtSupplierSearch_PreviewKeyDown Error: {ex.Message}");
            }
        }

        private void LstSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSuppliers != null && lstSuppliers.SelectedItem is SupplierSearchItem selectedSupplier)
            {
                _currentSupplierSelectedIndex = lstSuppliers.SelectedIndex;
            }
        }

        private void LstSuppliers_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (lstSuppliers != null && lstSuppliers.SelectedItem is SupplierSearchItem selectedSupplier)
                {
                    SelectSupplierFromList(selectedSupplier);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (popupSuppliers != null)
                {
                    popupSuppliers.IsOpen = false;
                    _isSupplierPopupOpen = false;
                }
                if (txtSupplierSearch != null)
                {
                    txtSupplierSearch.Focus();
                }
            }
        }

        private void LstSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSuppliers != null && lstSuppliers.SelectedItem is SupplierSearchItem selectedSupplier)
            {
                SelectSupplierFromList(selectedSupplier);
            }
        }

        #endregion

        #region دوال AutoComplete للمنتجات (Product AutoComplete Methods)

        private void SelectProductFromList(ProductSearchItem product)
        {
            if (product == null) return;

            _isSelectingProductFromList = true;

            if (txtProductSearch != null)
            {
                txtProductSearch.Text = product.Name;
                if (txtProductPlaceholder != null)
                    txtProductPlaceholder.Visibility = Visibility.Collapsed;
            }

            if (txtProductCode != null)
                txtProductCode.Text = product.Code ?? "";

            // ✅ حفظ معلومات الوحدات للمنتج المختار
            _selectedUnit1Name = product.Unit1 ?? "";
            _selectedUnit2Name = product.Unit2 ?? "";
            _selectedUnit3Name = product.Unit3 ?? "";
            _selectedUnit1Factor = product.Unit1Factor;
            _selectedUnit2Factor = product.Unit2Factor;
            _selectedUnitPrice1 = product.Price1;
            _selectedUnitPrice2 = product.Price2;
            _selectedUnitPrice3 = product.Price3;

            // ✅ تحديث قائمة الوحدات في ComboBox
            UpdateUnitComboBox(product);

            // ✅ تعيين السعر الافتراضي (الوحدة الثالثة - الأساسية)
            if (product.Price3 > 0)
            {
                txtUnitPrice.Text = product.Price3.ToString("N2");
            }
            else
            {
                txtUnitPrice.Text = product.Price.ToString("N2");
            }

            if (popupProducts != null)
            {
                popupProducts.IsOpen = false;
                _isProductPopupOpen = false;
            }
            _currentProductSelectedIndex = -1;

            // ✅ عرض رصيد المنتج فوراً عند اختياره (مع الوحدة الافتراضية)
            _ = DisplayProductStockAsync(product);

            if (txtQuantity != null)
            {
                txtQuantity.Focus();
                txtQuantity.SelectAll();
            }

            _isSelectingProductFromList = false;
            System.Diagnostics.Debug.WriteLine($"✅ تم اختيار المنتج: {product.Name} (ID: {product.Id})");
        }

        /// <summary>
        /// دالة تحديث قائمة الوحدات في ComboBox بناءً على المنتج المختار
        /// </summary>
        private void UpdateUnitComboBox(ProductSearchItem product)
        {
            try
            {
                if (cmbUnit == null) return;

                cmbUnit.Items.Clear();

                // ✅ إضافة الوحدات المتوفرة فقط
                if (!string.IsNullOrEmpty(product.Unit1) && product.Unit1Factor > 0)
                {
                    var item1 = new ComboBoxItem { Content = product.Unit1, Tag = "Unit1" };
                    cmbUnit.Items.Add(item1);
                }

                if (!string.IsNullOrEmpty(product.Unit2) && product.Unit2Factor > 0)
                {
                    var item2 = new ComboBoxItem { Content = product.Unit2, Tag = "Unit2" };
                    cmbUnit.Items.Add(item2);
                }

                if (!string.IsNullOrEmpty(product.Unit3))
                {
                    var item3 = new ComboBoxItem { Content = product.Unit3, Tag = "Unit3" };
                    cmbUnit.Items.Add(item3);
                }

                // ✅ إذا لم توجد وحدات، أضف وحدة افتراضية
                if (cmbUnit.Items.Count == 0)
                {
                    var defaultItem = new ComboBoxItem { Content = "وحدة", Tag = "Unit3" };
                    cmbUnit.Items.Add(defaultItem);
                }

                // ✅ اختيار الوحدة الثالثة كافتراضية (الأساسية)
                for (int i = 0; i < cmbUnit.Items.Count; i++)
                {
                    if (cmbUnit.Items[i] is ComboBoxItem item && item.Tag?.ToString() == "Unit3")
                    {
                        cmbUnit.SelectedIndex = i;
                        break;
                    }
                }

                // ✅ إذا لم يتم العثور على الوحدة 3، اختر الأولى
                if (cmbUnit.SelectedIndex == -1)
                {
                    cmbUnit.SelectedIndex = 0;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث قائمة الوحدات: {cmbUnit.Items.Count} وحدة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUnitComboBox Error: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ دالة عرض رصيد المنتج في المخزن المحدد مع تحويله حسب الوحدة المختارة
        /// </summary>
        private async Task DisplayProductStockAsync(ProductSearchItem product)
        {
            try
            {
                if (product == null)
                {
                    if (lblProductStock != null)
                        lblProductStock.Visibility = Visibility.Collapsed;
                    return;
                }

                if (cmbStores == null || cmbStores.SelectedItem == null)
                {
                    if (lblProductStock != null)
                        lblProductStock.Visibility = Visibility.Collapsed;
                    return;
                }

                if (!(cmbStores.SelectedItem is StoreSimple selectedStore))
                {
                    if (lblProductStock != null)
                        lblProductStock.Visibility = Visibility.Collapsed;
                    return;
                }

                int storeId = selectedStore.Id;
                int productId = product.Id;

                // ✅ الحصول على الرصيد بالوحدة الأساسية مع معلومات الوحدات
                var stockInfo = await GetProductStockWithUnitInfoAsync(storeId, productId);
                decimal quantityInBaseUnit = stockInfo.QuantityInBaseUnit;
                int unit1Factor = stockInfo.Unit1Factor;
                int unit2Factor = stockInfo.Unit2Factor;

                // ✅ الحصول على الوحدة المختارة
                string selectedUnitTag = "Unit3";
                if (cmbUnit != null && cmbUnit.SelectedItem is ComboBoxItem selectedUnit)
                {
                    selectedUnitTag = selectedUnit.Tag?.ToString() ?? "Unit3";
                }

                // ✅ تحويل الرصيد إلى الوحدة المختارة
                decimal displayQuantity = quantityInBaseUnit;
                string unitDisplayName = product.Unit3 ?? "قطعة";

                switch (selectedUnitTag)
                {
                    case "Unit1":
                        if (unit1Factor > 0 && unit2Factor > 0)
                        {
                            displayQuantity = quantityInBaseUnit / (unit1Factor * unit2Factor);
                            unitDisplayName = product.Unit1 ?? "كرتونة";
                        }
                        break;
                    case "Unit2":
                        if (unit2Factor > 0)
                        {
                            displayQuantity = quantityInBaseUnit / unit2Factor;
                            unitDisplayName = product.Unit2 ?? "علبة";
                        }
                        break;
                    case "Unit3":
                    default:
                        displayQuantity = quantityInBaseUnit;
                        unitDisplayName = product.Unit3 ?? "قطعة";
                        break;
                }

                // ✅ عرض الرصيد مع الوحدة المختارة
                if (lblProductStock != null)
                {
                    if (displayQuantity > 0)
                    {
                        // عرض الرصيد مع تحويله إلى عدد صحيح إذا كان عدداً صحيحاً
                        string displayQuantityText = displayQuantity % 1 == 0 ?
                            displayQuantity.ToString("N0") :
                            displayQuantity.ToString("N2");

                        lblProductStock.Text = $"📦 الرصيد المتوفر: {displayQuantityText} {unitDisplayName}";
                        lblProductStock.Visibility = Visibility.Visible;
                        lblProductStock.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                        System.Diagnostics.Debug.WriteLine($"✅ تم عرض رصيد المنتج: {displayQuantity} {unitDisplayName} (الأساسي: {quantityInBaseUnit})");
                    }
                    else
                    {
                        lblProductStock.Text = $"⚠️ لا يوجد رصيد متوفر ({unitDisplayName})";
                        lblProductStock.Visibility = Visibility.Visible;
                        lblProductStock.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        System.Diagnostics.Debug.WriteLine($"⚠️ لا يوجد رصيد للمنتج: {product.Name} بالوحدة {unitDisplayName}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DisplayProductStockAsync Error: {ex.Message}");
                if (lblProductStock != null)
                {
                    lblProductStock.Visibility = Visibility.Collapsed;
                }
            }
        }

        private ProductSearchItem GetSelectedProduct()
        {
            if (txtProductSearch == null) return null;

            if (!string.IsNullOrWhiteSpace(txtProductSearch.Text))
            {
                var product = _allProductsList.FirstOrDefault(p => p.Name == txtProductSearch.Text);
                if (product != null) return product;

                product = _allProductsList.FirstOrDefault(p => p.Code == txtProductSearch.Text);
                if (product != null) return product;

                product = _allProductsList.FirstOrDefault(p => p.Barcode == txtProductSearch.Text);
                if (product != null) return product;
            }

            return null;
        }

        /// <summary>
        /// ✅ دالة تحديث السعر عند تغيير الوحدة المختارة - مع تحديث الرصيد أيضاً
        /// </summary>
        private void CmbUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ProductSearchItem selectedProduct = GetSelectedProduct();
                if (selectedProduct == null) return;

                if (cmbUnit == null || cmbUnit.SelectedItem == null) return;

                string unitTag = "";
                if (cmbUnit.SelectedItem is ComboBoxItem selectedItem)
                {
                    unitTag = selectedItem.Tag?.ToString() ?? "Unit3";
                }

                // ✅ تحديث السعر بناءً على الوحدة المختارة
                switch (unitTag)
                {
                    case "Unit1":
                        if (selectedProduct.Price1 > 0)
                        {
                            txtUnitPrice.Text = selectedProduct.Price1.ToString("N2");
                            _selectedUnitPrice1 = selectedProduct.Price1;
                        }
                        else
                        {
                            // ✅ حساب سعر الوحدة 1 من الوحدة 3
                            decimal calculatedPrice1 = selectedProduct.Price3 * selectedProduct.Unit2Factor * selectedProduct.Unit1Factor;
                            txtUnitPrice.Text = calculatedPrice1.ToString("N2");
                            _selectedUnitPrice1 = calculatedPrice1;
                        }
                        break;

                    case "Unit2":
                        if (selectedProduct.Price2 > 0)
                        {
                            txtUnitPrice.Text = selectedProduct.Price2.ToString("N2");
                            _selectedUnitPrice2 = selectedProduct.Price2;
                        }
                        else
                        {
                            // ✅ حساب سعر الوحدة 2 من الوحدة 3
                            decimal calculatedPrice2 = selectedProduct.Price3 * selectedProduct.Unit2Factor;
                            txtUnitPrice.Text = calculatedPrice2.ToString("N2");
                            _selectedUnitPrice2 = calculatedPrice2;
                        }
                        break;

                    case "Unit3":
                    default:
                        if (selectedProduct.Price3 > 0)
                        {
                            txtUnitPrice.Text = selectedProduct.Price3.ToString("N2");
                            _selectedUnitPrice3 = selectedProduct.Price3;
                        }
                        else
                        {
                            txtUnitPrice.Text = selectedProduct.Price.ToString("N2");
                            _selectedUnitPrice3 = selectedProduct.Price;
                        }
                        break;
                }

                // ✅ ✅ ✅ تحديث الرصيد عند تغيير الوحدة
                _ = DisplayProductStockAsync(selectedProduct);

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث السعر للوحدة {unitTag}: {txtUnitPrice.Text}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CmbUnit_SelectionChanged Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال أحداث AutoComplete للمنتجات (Product AutoComplete Event Handlers)

        private void TxtProductSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtProductPlaceholder != null) txtProductPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void TxtProductSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProductSearch.Text) && txtProductPlaceholder != null)
                txtProductPlaceholder.Visibility = Visibility.Visible;

            Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (popupProducts != null && lstProducts != null && !lstProducts.IsMouseOver && !lstProducts.IsKeyboardFocusWithin)
                {
                    popupProducts.IsOpen = false;
                    _isProductPopupOpen = false;
                }
            }));
        }

        private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingProductFromList) return;

            string searchText = txtProductSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupProducts != null)
                {
                    popupProducts.IsOpen = false;
                    _isProductPopupOpen = false;
                }
                _currentProductSelectedIndex = -1;
                return;
            }

            _currentFilteredProducts = _allProductsList
                .Where(p =>
                    (p.Name != null && p.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Code != null && p.Code.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.Barcode != null && p.Barcode.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (lstProducts != null)
            {
                lstProducts.ItemsSource = _currentFilteredProducts;
                lstProducts.SelectedIndex = -1;
                _currentProductSelectedIndex = -1;
            }

            if (popupProducts != null)
            {
                if (_currentFilteredProducts.Count > 0 && !string.IsNullOrEmpty(searchText))
                {
                    popupProducts.IsOpen = true;
                    _isProductPopupOpen = true;
                }
                else
                {
                    popupProducts.IsOpen = false;
                    _isProductPopupOpen = false;
                }
            }
        }

        private void TxtProductSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;

                    if (!_isProductPopupOpen && _currentFilteredProducts.Count > 0 && popupProducts != null)
                    {
                        popupProducts.IsOpen = true;
                        _isProductPopupOpen = true;
                    }

                    if (lstProducts != null && lstProducts.Items.Count > 0)
                    {
                        if (_currentProductSelectedIndex < lstProducts.Items.Count - 1)
                        {
                            _currentProductSelectedIndex++;
                            lstProducts.SelectedIndex = _currentProductSelectedIndex;
                            lstProducts.ScrollIntoView(lstProducts.SelectedItem);
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;

                    if (lstProducts != null && lstProducts.Items.Count > 0 && _currentProductSelectedIndex > 0)
                    {
                        _currentProductSelectedIndex--;
                        lstProducts.SelectedIndex = _currentProductSelectedIndex;
                        lstProducts.ScrollIntoView(lstProducts.SelectedItem);
                    }
                    else if (_currentProductSelectedIndex == 0 && lstProducts != null)
                    {
                        _currentProductSelectedIndex = -1;
                        lstProducts.SelectedIndex = -1;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (_currentProductSelectedIndex >= 0 && lstProducts != null && _currentProductSelectedIndex < lstProducts.Items.Count)
                    {
                        e.Handled = true;
                        var selectedProduct = lstProducts.SelectedItem as ProductSearchItem;
                        if (selectedProduct != null)
                        {
                            SelectProductFromList(selectedProduct);
                        }
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (popupProducts != null)
                    {
                        popupProducts.IsOpen = false;
                        _isProductPopupOpen = false;
                    }
                    _currentProductSelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtProductSearch_PreviewKeyDown Error: {ex.Message}");
            }
        }

        private void LstProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstProducts != null && lstProducts.SelectedItem is ProductSearchItem selectedProduct)
            {
                _currentProductSelectedIndex = lstProducts.SelectedIndex;
            }
        }

        private void LstProducts_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (lstProducts != null && lstProducts.SelectedItem is ProductSearchItem selectedProduct)
                {
                    SelectProductFromList(selectedProduct);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (popupProducts != null)
                {
                    popupProducts.IsOpen = false;
                    _isProductPopupOpen = false;
                }
                if (txtProductSearch != null)
                {
                    txtProductSearch.Focus();
                }
            }
        }

        private void LstProducts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstProducts != null && lstProducts.SelectedItem is ProductSearchItem selectedProduct)
            {
                SelectProductFromList(selectedProduct);
            }
        }

        #endregion
    }

    #region PurchaseInvoiceItem Class

    public class PurchaseInvoiceItem : INotifyPropertyChanged
    {
        private int _productID;
        private string _productName = string.Empty;
        private decimal _quantity;
        private decimal _quantityInBaseUnit;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private decimal _discountAmount;
        private decimal _totalAmount;
        private int _serialNumber;
        private string _unitName = string.Empty;
        private string _unitTag = "Unit3";

        public int ProductID
        {
            get => _productID;
            set
            {
                if (_productID != value)
                {
                    _productID = value;
                    OnPropertyChanged(nameof(ProductID));
                }
            }
        }

        public string ProductName
        {
            get => _productName;
            set
            {
                if (_productName != value)
                {
                    _productName = value;
                    OnPropertyChanged(nameof(ProductName));
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                }
            }
        }

        /// <summary>
        /// ✅ الكمية المحولة إلى الوحدة الأساسية (Unit3)
        /// </summary>
        public decimal QuantityInBaseUnit
        {
            get => _quantityInBaseUnit;
            set
            {
                if (_quantityInBaseUnit != value)
                {
                    _quantityInBaseUnit = value;
                    OnPropertyChanged(nameof(QuantityInBaseUnit));
                }
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged(nameof(UnitPrice));
                }
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = value;
                    OnPropertyChanged(nameof(DiscountPercent));
                }
            }
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                if (_discountAmount != value)
                {
                    _discountAmount = value;
                    OnPropertyChanged(nameof(DiscountAmount));
                }
            }
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set
            {
                if (_totalAmount != value)
                {
                    _totalAmount = value;
                    OnPropertyChanged(nameof(TotalAmount));
                }
            }
        }

        public int SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged(nameof(SerialNumber));
                }
            }
        }

        public string UnitName
        {
            get => _unitName;
            set
            {
                if (_unitName != value)
                {
                    _unitName = value;
                    OnPropertyChanged(nameof(UnitName));
                }
            }
        }

        public string UnitTag
        {
            get => _unitTag;
            set
            {
                if (_unitTag != value)
                {
                    _unitTag = value;
                    OnPropertyChanged(nameof(UnitTag));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}