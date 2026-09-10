using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace RasidAccountingSystem.Views
{
    public partial class SalesInvoiceView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private DatabaseService _databaseService;
        private InvoiceService _invoiceService;
        private ObservableCollection<SalesInvoiceItem> _invoiceItems;
        private string _currentInvoiceNumber;
        private decimal _currentTaxPercent = 0m;
        private int _currentUserId;
        private int _currentEditingInvoiceId = 0;
        private string _currencySymbol = "ر.س";
        private readonly object _dbLock = new object();
        private const int MAX_RETRY_COUNT = 15;
        private const int RETRY_DELAY_MS = 500;
        private const int BUSY_TIMEOUT_SECONDS = 120;
        private bool _isUpdatingPaidAmount = false;
        private bool _isUpdatingTotals = false;

        // ==================== متغيرات AutoComplete للعملاء ====================
        private List<CustomerSearchItem> _allCustomersList = new List<CustomerSearchItem>();
        private List<CustomerSearchItem> _currentFilteredCustomers = new List<CustomerSearchItem>();
        private int _currentCustomerSelectedIndex = -1;
        private bool _isCustomerPopupOpen = false;
        private bool _isSelectingCustomerFromList = false;
        private bool _isNavigatingWithArrows = false;
        private int _selectedCustomerId = 0;

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

        // ==================== متغيرات الأقساط ====================
        private bool _isInstallment = false;
        private List<InstallmentService.InstallmentInput> _installments = null;
        private decimal _paidUpfront = 0;

        #endregion

        #region الكلاسات المساعدة (Helper Classes)

        public class CustomerSimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class CustomerSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class ProductSearchItem
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string Barcode { get; set; }

            // خصائص الوحدات الثلاثة
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

        public class SavedInvoiceItem
        {
            public int InvoiceID { get; set; }
            public string InvoiceNumber { get; set; }
            public DateTime InvoiceDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string PaymentMethod { get; set; }
            public string PaymentStatus { get; set; }
            public string CustomerName { get; set; }
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

        #region المنشئ (Constructor)

        public SalesInvoiceView()
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

        #region دوال التهيئة (Initialization Methods)

        private void InitializeServices()
        {
            try
            {
                _databaseService = new DatabaseService();
                _invoiceService = new InvoiceService(_databaseService);
                _invoiceItems = new ObservableCollection<SalesInvoiceItem>();
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

                if (cmbPaymentMethod != null)
                    cmbPaymentMethod.SelectionChanged += CmbPaymentMethod_SelectionChanged;

                if (cmbStores != null)
                    cmbStores.SelectionChanged += (s, e) => CmbStores_SelectionChanged(s, null);

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

        #region دوال الأحداث (Event Handlers)

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

        private async Task<(decimal QuantityInBaseUnit, int Unit1Factor, int Unit2Factor)> GetProductStockWithUnitInfoAsync(int storeId, int productId)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<(decimal, int, int)>(_databaseService, async (connection) =>
                {
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
                        decimal remainingAmount = grandTotal - _paidUpfront;
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
                        decimal remainingAmount = grandTotal - _paidUpfront;
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
                        decimal remainingAmount = grandTotal - _paidUpfront;
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

                if (paymentMethod == "أجل" && txtPaidAmount != null)
                {
                    txtPaidAmount.Text = "0.00";
                }

                System.Diagnostics.Debug.WriteLine($"طريقة الدفع changed to: {paymentMethod}");
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
                if (sender is Button button && button.Tag is SalesInvoiceItem item && _invoiceItems != null)
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

        #region دوال تحميل البيانات (Data Loading Methods)

        private async Task InitializeFormAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await LoadCurrencySymbolAsync();
                await LoadCustomersForAutoCompleteAsync();
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
                    _invoiceItems = new ObservableCollection<SalesInvoiceItem>();
                    if (dgInvoiceItems != null)
                        dgInvoiceItems.ItemsSource = _invoiceItems;
                }

                ProductSearchItem selectedProduct = GetSelectedProduct();
                if (cmbStores != null && cmbStores.SelectedItem is StoreSimple selectedStore && selectedProduct != null)
                {
                    var stockInfo = await GetProductStockWithUnitInfoAsync(selectedStore.Id, selectedProduct.Id);
                    if (lblProductStock != null && stockInfo.QuantityInBaseUnit > 0)
                    {
                        lblProductStock.Text = $"📦 الرصيد المتوفر: {stockInfo.QuantityInBaseUnit:N0} قطعة";
                        lblProductStock.Visibility = Visibility.Visible;
                    }
                }

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

        private async Task LoadCustomersForAutoCompleteAsync()
        {
            try
            {
                if (_databaseService == null) return;

                _allCustomersList.Clear();
                var customers = await _databaseService.GetCustomersAsync();

                if (customers != null)
                {
                    foreach (var customer in customers)
                    {
                        if (customer != null)
                        {
                            string customerName = !string.IsNullOrEmpty(customer.CustomerNameAr) ? customer.CustomerNameAr : customer.CustomerName;

                            _allCustomersList.Add(new CustomerSearchItem
                            {
                                Id = customer.CustomerID,
                                Code = customer.CustomerCode,
                                Name = customerName,
                                CurrentBalance = customer.CurrentBalance
                            });
                        }
                    }
                }

                _currentFilteredCustomers = new List<CustomerSearchItem>(_allCustomersList);

                if (lstCustomers != null)
                {
                    lstCustomers.ItemsSource = _currentFilteredCustomers;
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {_allCustomersList.Count} عميل للـ AutoComplete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل العملاء للـ AutoComplete: {ex.Message}");
            }
        }

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
                                Price = product.SalePrice,
                                Barcode = product.Barcode ?? "",
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

                System.Diagnostics.Debug.WriteLine($"تم تحميل {_allProductsList.Count} منتج للـ AutoComplete مع معلومات الوحدات");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل المنتجات للـ AutoComplete: {ex.Message}");
            }
        }

        private async Task LoadStoresAsync()
        {
            try
            {
                var storesList = new ObservableCollection<StoreSimple>();

                await DatabaseExecutor.ExecuteVoidAsync(_databaseService, async (connection) =>
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
                }, CancellationToken.None, 3);

                if (cmbStores != null)
                {
                    cmbStores.ItemsSource = storesList;

                    if (storesList.Count > 0)
                    {
                        cmbStores.SelectedItem = storesList[0];

                        ProductSearchItem selectedProduct = GetSelectedProduct();
                        if (selectedProduct != null)
                        {
                            var store = storesList.FirstOrDefault();
                            if (store != null)
                            {
                                var stockInfo = await GetProductStockWithUnitInfoAsync(store.Id, selectedProduct.Id);
                                if (lblProductStock != null)
                                {
                                    if (stockInfo.QuantityInBaseUnit > 0)
                                    {
                                        lblProductStock.Text = $"📦 الرصيد المتوفر: {stockInfo.QuantityInBaseUnit:N0} قطعة";
                                        lblProductStock.Visibility = Visibility.Visible;
                                    }
                                    else
                                    {
                                        lblProductStock.Text = "⚠️ لا يوجد رصيد متوفر";
                                        lblProductStock.Visibility = Visibility.Visible;
                                    }
                                }
                            }
                        }
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
                    string sql = "SELECT MAX(CAST(SUBSTR(InvoiceNumber, 5) AS INTEGER)) FROM SalesInvoices WHERE InvoiceNumber LIKE 'SIN-%'";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = await cmd.ExecuteScalarAsync();
                        int maxNumber = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                        int nextNumber = maxNumber + 1;
                        _currentInvoiceNumber = $"SIN-{nextNumber:D6}";

                        if (lblInvoiceNumber != null)
                            lblInvoiceNumber.Text = _currentInvoiceNumber;

                        return nextNumber;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateNewInvoiceNumber Error: {ex.Message}");
                _currentInvoiceNumber = $"SIN-{DateTime.Now:yyyyMMddHHmmss}";

                if (lblInvoiceNumber != null)
                    lblInvoiceNumber.Text = _currentInvoiceNumber;
            }
        }

        #endregion

        #region دوال البحث المنبثق (Search Dialog Methods)

        private void OpenSearchDialog()
        {
            try
            {
                if (SearchDialogOverlay != null)
                    SearchDialogOverlay.Visibility = Visibility.Visible;

                if (txtSearchInvoiceNumber != null)
                    txtSearchInvoiceNumber.Text = string.Empty;

                if (txtSearchCustomerName != null)
                    txtSearchCustomerName.Text = string.Empty;

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
                string customerName = txtSearchCustomerName != null ? txtSearchCustomerName.Text.Trim() : "";
                DateTime fromDate = (dpFromDateSearch != null && dpFromDateSearch.SelectedDate.HasValue) ? dpFromDateSearch.SelectedDate.Value : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime toDate = (dpToDateSearch != null && dpToDateSearch.SelectedDate.HasValue) ? dpToDateSearch.SelectedDate.Value : DateTime.Now;
                toDate = toDate.AddDays(1).AddSeconds(-1);

                var resultsList = new ObservableCollection<SavedInvoiceItem>();

                await DatabaseExecutor.ExecuteNonTransactionAsync<ObservableCollection<SavedInvoiceItem>>(_databaseService, async (connection) =>
                {
                    string sql = @"
                        SELECT 
                            si.InvoiceID,
                            si.InvoiceNumber,
                            si.InvoiceDate,
                            si.TotalAmount,
                            si.PaymentMethod,
                            si.PaymentStatus,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName
                        FROM SalesInvoices si
                        LEFT JOIN Customers c ON si.CustomerID = c.CustomerID
                        WHERE si.IsVoid = 0
                        AND si.InvoiceDate BETWEEN @fromDate AND @toDate";

                    if (!string.IsNullOrEmpty(invoiceNumber))
                    {
                        sql += " AND si.InvoiceNumber LIKE @invoiceNumber";
                    }

                    if (!string.IsNullOrEmpty(customerName))
                    {
                        sql += " AND (c.CustomerNameAr LIKE @customerName OR c.CustomerName LIKE @customerName)";
                    }

                    sql += " ORDER BY si.InvoiceDate DESC, si.InvoiceID DESC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@fromDate", fromDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@toDate", toDate.ToString("yyyy-MM-dd"));
                        if (!string.IsNullOrEmpty(invoiceNumber))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", $"%{invoiceNumber}%");
                        }
                        if (!string.IsNullOrEmpty(customerName))
                        {
                            cmd.Parameters.AddWithValue("@customerName", $"%{customerName}%");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                resultsList.Add(new SavedInvoiceItem
                                {
                                    InvoiceID = reader.GetInt32(0),
                                    InvoiceNumber = reader.GetString(1),
                                    InvoiceDate = reader.GetDateTime(2),
                                    TotalAmount = reader.GetDecimal(3),
                                    PaymentMethod = ConvertPaymentMethodToArabic(reader.GetString(4)),
                                    PaymentStatus = reader.GetString(5),
                                    CustomerName = reader.GetString(6)
                                });
                            }
                        }
                    }
                    return resultsList;
                }, CancellationToken.None, 3);

                if (dgSearchResults != null)
                    dgSearchResults.ItemsSource = resultsList;

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
                            CustomerID,
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
                        FROM SalesInvoices 
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

                                int customerId = reader.GetInt32(2);

                                if (txtCustomerSearch != null && _allCustomersList != null)
                                {
                                    var customer = _allCustomersList.FirstOrDefault(c => c.Id == customerId);
                                    if (customer != null)
                                    {
                                        txtCustomerSearch.Text = customer.Name;
                                        if (txtCustomerPlaceholder != null)
                                            txtCustomerPlaceholder.Visibility = Visibility.Collapsed;
                                        _selectedCustomerId = customerId;
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

                                _isInstallment = false;
                                _installments = null;
                                _paidUpfront = 0;

                                // ✅ إصلاح: عمود "IsPaid" غير موجود إطلاقاً في جدول Installments -
                                // الجدول يستخدم عمود "Status" نصي (Pending, Paid, Overdue...) بدلاً
                                // من عمود منطقي، بنفس الاصطلاح المستخدم في باقي البرنامج
                                // (راجع InstallmentService.cs). أي قسط حالته ليست "Paid" يُعتبر
                                // غير مسدَّد بالكامل بعد.
                                string checkInstallmentsSql = "SELECT COUNT(*) FROM Installments WHERE InvoiceID = @invoiceId AND Status <> 'Paid'";
                                using (var checkCmd = new SQLiteCommand(checkInstallmentsSql, connection))
                                {
                                    checkCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                                    long count = (long)await checkCmd.ExecuteScalarAsync();
                                    if (count > 0)
                                    {
                                        _isInstallment = true;
                                        _paidUpfront = paidAmount > 0 ? paidAmount : 0;
                                    }
                                }
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
                        FROM SalesInvoiceItems 
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
                                    _invoiceItems.Add(new SalesInvoiceItem
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

        #region دوال حذف الفاتورة بالكامل (Delete Invoice Methods)

        private async void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button == null || button.Tag == null) return;

                int invoiceId = (int)button.Tag;

                string invoiceNumber = "";
                if (button.DataContext is SavedInvoiceItem invoiceItem)
                {
                    invoiceNumber = invoiceItem.InvoiceNumber;
                }
                else
                {
                    if (dgSearchResults != null && dgSearchResults.SelectedItem is SavedInvoiceItem boundItem)
                    {
                        invoiceNumber = boundItem.InvoiceNumber;
                    }
                }

                MessageBoxResult result = MessageBox.Show(
                    $"هل أنت متأكد من حذف الفاتورة رقم {invoiceNumber}؟\n\n" +
                    "⚠️ تحذير: سيتم حذف جميع بيانات الفاتورة بما فيها:\n" +
                    "• تفاصيل المنتجات\n" +
                    "• سندات القبض المرتبطة (إن وجدت)\n" +
                    "• المعاملات المالية المرتبطة\n" +
                    "• معاملات الخزينة والبنوك والشيكات\n\n" +
                    "هذا الإجراء لا يمكن التراجع عنه!",
                    "تأكيد حذف الفاتورة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                Mouse.OverrideCursor = Cursors.Wait;

                bool deleted = await _invoiceService.DeleteSalesInvoiceAsync(invoiceId);

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

        #region دوال العمليات الأساسية (Core Operations)

        private async Task NewInvoiceAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 NewInvoiceAsync - بدء");
                System.Diagnostics.Debug.WriteLine("========================================");

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

                if (txtCustomerSearch != null)
                    txtCustomerSearch.Text = string.Empty;
                if (txtCustomerPlaceholder != null)
                    txtCustomerPlaceholder.Visibility = Visibility.Visible;
                _selectedCustomerId = 0;
                _currentCustomerSelectedIndex = -1;

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

                if (cmbUnit != null && cmbUnit.Items.Count > 0)
                {
                    cmbUnit.SelectedIndex = 0;
                }

                _isInstallment = false;
                _installments = null;
                _paidUpfront = 0;

                System.Diagnostics.Debug.WriteLine($"✅ تم إعادة تعيين قيم الأقساط: _paidUpfront = {_paidUpfront:N2}, _isInstallment = {_isInstallment}");

                UpdatePaymentPanelVisibility();
                CalculateTotals();

                var pnlInstallments = this.FindName("pnlInstallments") as Border;
                if (pnlInstallments != null)
                    pnlInstallments.Visibility = Visibility.Collapsed;

                var rbCash = this.FindName("rbCash") as RadioButton;
                if (rbCash != null)
                    rbCash.IsChecked = true;

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ NewInvoiceAsync - انتهى");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront = {_paidUpfront:N2}, _isInstallment = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"NewInvoiceAsync Error: {ex.Message}");
            }
        }

        private async Task AddProductToInvoiceAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 بدء إضافة منتج إلى فاتورة البيع");
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

                // ✅ تحويل الكمية إلى الوحدة الأساسية (Unit3) بناءً على الوحدة المختارة
                decimal quantityInBaseUnit = quantity;
                switch (selectedUnitTag)
                {
                    case "Unit1":
                        quantityInBaseUnit = quantity * selectedProduct.Unit1Factor * selectedProduct.Unit2Factor;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit1: {quantity} × {selectedProduct.Unit1Factor} × {selectedProduct.Unit2Factor} = {quantityInBaseUnit}");
                        break;
                    case "Unit2":
                        quantityInBaseUnit = quantity * selectedProduct.Unit2Factor;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit2: {quantity} × {selectedProduct.Unit2Factor} = {quantityInBaseUnit}");
                        break;
                    case "Unit3":
                    default:
                        quantityInBaseUnit = quantity;
                        System.Diagnostics.Debug.WriteLine($"📐 تحويل من Unit3: {quantity} = {quantityInBaseUnit} (بدون تحويل)");
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"📊 الكمية المحولة إلى الوحدة الأساسية: {quantityInBaseUnit}");

                // ✅ التحقق من الرصيد المتوفر مع إمكانية البيع حتى لو الرصيد غير كافٍ
                decimal availableStock = 0;
                string stockUnitName = selectedProduct.Unit3 ?? "قطعة";
                string warningMessage = "";

                if (cmbStores != null && cmbStores.SelectedItem is StoreSimple selectedStore)
                {
                    var stockInfo = await GetProductStockWithUnitInfoAsync(selectedStore.Id, selectedProduct.Id);
                    availableStock = stockInfo.QuantityInBaseUnit;

                    // ✅ تحويل الرصيد إلى الوحدة المختارة لعرضه
                    decimal displayStock = availableStock;
                    switch (selectedUnitTag)
                    {
                        case "Unit1":
                            if (stockInfo.Unit1Factor > 0 && stockInfo.Unit2Factor > 0)
                            {
                                displayStock = availableStock / (stockInfo.Unit1Factor * stockInfo.Unit2Factor);
                                stockUnitName = selectedProduct.Unit1 ?? "كرتونة";
                            }
                            break;
                        case "Unit2":
                            if (stockInfo.Unit2Factor > 0)
                            {
                                displayStock = availableStock / stockInfo.Unit2Factor;
                                stockUnitName = selectedProduct.Unit2 ?? "علبة";
                            }
                            break;
                        case "Unit3":
                        default:
                            displayStock = availableStock;
                            stockUnitName = selectedProduct.Unit3 ?? "قطعة";
                            break;
                    }

                    System.Diagnostics.Debug.WriteLine($"📊 الرصيد المتوفر في المخزن: {displayStock} {stockUnitName} (الأساسي: {availableStock})");

                    // ✅ إذا كانت الكمية المطلوبة أكبر من الرصيد المتوفر، عرض رسالة تحذير
                    if (quantityInBaseUnit > availableStock)
                    {
                        string displayQuantityText = quantityInBaseUnit % 1 == 0 ?
                            quantityInBaseUnit.ToString("N0") :
                            quantityInBaseUnit.ToString("N2");

                        string displayStockText = displayStock % 1 == 0 ?
                            displayStock.ToString("N0") :
                            displayStock.ToString("N2");

                        warningMessage =
                            $"⚠️ تحذير: الكمية المطلوبة ({displayQuantityText} {stockUnitName}) " +
                            $"تتجاوز الرصيد المتوفر في المخزن ({displayStockText} {stockUnitName}).\n\n" +
                            $"هل تريد المتابعة مع خصم الكمية من المخزون؟";

                        MessageBoxResult confirmResult = MessageBox.Show(
                            warningMessage,
                            "تنبيه - الرصيد غير كافٍ",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (confirmResult != MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Debug.WriteLine("❌ المستخدم ألغى إضافة المنتج بسبب عدم كفاية الرصيد");
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine("✅ المستخدم وافق على البيع رغم عدم كفاية الرصيد");
                    }
                }

                // ✅ استكمال إضافة المنتج للفاتورة (نفس الكود السابق)
                decimal discountPercent = 0;
                if (txtDiscountPercent != null && decimal.TryParse(txtDiscountPercent.Text, out decimal parsedDiscount))
                {
                    discountPercent = parsedDiscount;
                }

                if (discountPercent < 0) discountPercent = 0;
                if (discountPercent > 100) discountPercent = 100;

                if (txtDiscountPercent != null)
                    txtDiscountPercent.Text = discountPercent.ToString();

                decimal subTotal = unitPrice * quantity;
                decimal discountAmount = subTotal * (discountPercent / 100);
                decimal totalAmount = subTotal - discountAmount;

                System.Diagnostics.Debug.WriteLine($"💰 السعر: UnitPrice={unitPrice}, SubTotal={subTotal}, Discount={discountPercent}%, DiscountAmount={discountAmount}, Total={totalAmount}");

                string displayProductName = selectedProduct.Name;
                if (!string.IsNullOrEmpty(selectedUnitName) && selectedUnitName != "وحدة")
                {
                    displayProductName = $"{selectedProduct.Name} ({selectedUnitName})";
                }

                SalesInvoiceItem existingItem = null;
                if (_invoiceItems != null)
                {
                    existingItem = _invoiceItems.FirstOrDefault(x =>
                        x.ProductID == selectedProduct.Id &&
                        x.UnitTag == selectedUnitTag);
                }

                if (existingItem != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 تحديث منتج موجود: {existingItem.ProductName}");
                    System.Diagnostics.Debug.WriteLine($"   الكمية القديمة: {existingItem.Quantity}, الكمية الجديدة: {quantity}");
                    System.Diagnostics.Debug.WriteLine($"   الكمية المحولة القديمة: {existingItem.QuantityInBaseUnit}, الكمية المحولة الجديدة: {quantityInBaseUnit}");

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
                        _invoiceItems.Add(new SalesInvoiceItem
                        {
                            ProductID = selectedProduct.Id,
                            ProductName = displayProductName,
                            Quantity = quantity,
                            QuantityInBaseUnit = quantityInBaseUnit,
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
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 CalculateTotals - بدء");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine($"📊 عدد الأقساط = {(_installments != null ? _installments.Count : 0)}");

                if (_isUpdatingTotals)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ جاري تحديث الإجماليات، تم تخطي الحساب");
                    return;
                }
                _isUpdatingTotals = true;

                decimal subTotal = 0;
                decimal totalDiscount = 0;

                if (_invoiceItems != null)
                {
                    foreach (SalesInvoiceItem item in _invoiceItems)
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

                System.Diagnostics.Debug.WriteLine($"💰 المجموع الفرعي: {subTotal:N2}");
                System.Diagnostics.Debug.WriteLine($"💰 إجمالي الخصم: {totalDiscount:N2}");
                System.Diagnostics.Debug.WriteLine($"💰 بعد الخصم: {afterDiscount:N2}");
                System.Diagnostics.Debug.WriteLine($"💰 قيمة الضريبة: {taxAmount:N2}");
                System.Diagnostics.Debug.WriteLine($"💰 الإجمالي الكلي: {grandTotal:N2}");

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
                System.Diagnostics.Debug.WriteLine($"📌 طريقة الدفع: {arabicPaymentMethod}");

                if (arabicPaymentMethod == "أجل")
                {
                    if (txtPaidAmount != null)
                        txtPaidAmount.Text = "0.00";

                    if (lblRemainingAmount != null)
                    {
                        decimal remainingAmount = grandTotal - _paidUpfront;
                        System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي (أجل): {remainingAmount:N2} (الإجمالي {grandTotal:N2} - المدفوع {_paidUpfront:N2})");
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }
                else
                {
                    if (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paidAmount) && lblRemainingAmount != null)
                    {
                        decimal remainingAmount = grandTotal - paidAmount;
                        System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي (غير أجل): {remainingAmount:N2} (الإجمالي {grandTotal:N2} - المدفوع {paidAmount:N2})");
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
                        decimal remainingAmount = grandTotal - _paidUpfront;
                        System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي (افتراضي): {remainingAmount:N2} (الإجمالي {grandTotal:N2} - المدفوع {_paidUpfront:N2})");
                        lblRemainingAmount.Text = remainingAmount.ToString("N2");
                        lblRemainingAmount.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                }

                UpdateInstallmentSummary();

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ CalculateTotals - انتهى");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CalculateTotals Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
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
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 بدء عملية حفظ الفاتورة...");
                System.Diagnostics.Debug.WriteLine("========================================");

                CustomerSearchItem selectedCustomer = GetSelectedCustomer();

                if (selectedCustomer == null)
                {
                    MessageBox.Show("اختر العميل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (txtCustomerSearch != null) txtCustomerSearch.Focus();
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

                int customerId = selectedCustomer.Id;
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

                if (_isInstallment)
                {
                    paidAmount = _paidUpfront;
                    System.Diagnostics.Debug.WriteLine($"💰 في حالة التقسيط: paidAmount = _paidUpfront = {_paidUpfront:N2}");
                }

                if (paidAmount > grandTotal)
                {
                    paidAmount = grandTotal;
                    System.Diagnostics.Debug.WriteLine($"💰 تم تعديل المبلغ المدفوع: {paidAmount:N2} (لا يتجاوز الإجمالي)");
                }

                System.Diagnostics.Debug.WriteLine($"💰 المبلغ المدفوع النهائي: {paidAmount:N2}");

                System.Diagnostics.Debug.WriteLine($"📊 عدد العناصر في الفاتورة: {_invoiceItems.Count}");
                foreach (var item in _invoiceItems)
                {
                    System.Diagnostics.Debug.WriteLine($"   - {item.ProductName} | الكمية: {item.Quantity} | الوحدة: {item.UnitName} | الكمية المحولة: {item.QuantityInBaseUnit}");
                }

                var itemsList = _invoiceItems.Select(item => new InvoiceService.SalesInvoiceItemClass
                {
                    ProductID = item.ProductID,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    QuantityInBaseUnit = item.QuantityInBaseUnit,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    DiscountAmount = item.DiscountAmount,
                    TotalAmount = item.TotalAmount
                }).ToList();

                System.Diagnostics.Debug.WriteLine("📤 البيانات المرسلة إلى InvoiceService:");
                foreach (var item in itemsList)
                {
                    System.Diagnostics.Debug.WriteLine($"   - ProductID: {item.ProductID}, Quantity: {item.Quantity}, QuantityInBaseUnit: {item.QuantityInBaseUnit}");
                }

                bool result = false;
                bool isEditing = _currentEditingInvoiceId > 0;

                int? treasuryId = null;
                string checkNumber = null;
                DateTime? checkDate = null;
                string bankName = null;

                if (arabicPaymentMethod != "أجل" && paidAmount > 0 && !_isInstallment)
                {
                    if (arabicPaymentMethod == "نقدي")
                    {
                        if (cmbTreasury == null || cmbTreasury.SelectedItem == null)
                        {
                            MessageBox.Show("اختر الخزينة المستلمة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    result = await _invoiceService.UpdateSalesInvoiceAsync(
                        _currentEditingInvoiceId,
                        _currentInvoiceNumber,
                        invoiceDate,
                        customerId,
                        storeId,
                        subTotal,
                        totalDiscount,
                        taxAmount,
                        _currentTaxPercent,
                        grandTotal,
                        paidAmount,
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
                    result = await _invoiceService.SaveSalesInvoiceAsync(
                        _currentInvoiceNumber,
                        invoiceDate,
                        customerId,
                        storeId,
                        subTotal,
                        totalDiscount,
                        taxAmount,
                        _currentTaxPercent,
                        grandTotal,
                        paidAmount,
                        paymentMethod,
                        referenceNumber,
                        notes,
                        itemsList,
                        _currentUserId,
                        treasuryId,
                        checkNumber,
                        checkDate,
                        bankName,
                        _isInstallment,
                        _installments,
                        _paidUpfront
                    );
                }

                System.Diagnostics.Debug.WriteLine($"📊 نتيجة الحفظ: {(result ? "✅ نجاح" : "❌ فشل")}");

                if (result)
                {
                    if (_invoiceItems != null && _invoiceItems.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("🔍 التحقق من المخزون بعد الحفظ:");
                        foreach (var item in _invoiceItems)
                        {
                            var product = _allProductsList.FirstOrDefault(p => p.Id == item.ProductID);
                            if (product != null)
                            {
                                var stockInfo = await GetProductStockWithUnitInfoAsync(storeId, item.ProductID);
                                System.Diagnostics.Debug.WriteLine($"   - {product.Name}: الرصيد الحالي في المخزن = {stockInfo.QuantityInBaseUnit}, الكمية المباعة = {item.QuantityInBaseUnit}");
                            }
                        }
                    }

                    string successMessage = isEditing ? "تم تحديث الفاتورة بنجاح" : "تم حفظ الفاتورة بنجاح";

                    if (_isInstallment && _installments != null && _installments.Count > 0)
                    {
                        successMessage += $"\n\n✅ تم تقسيم المبلغ إلى {_installments.Count} أقساط";
                        successMessage += $"\nالمقدم المدفوع: {_paidUpfront:N2}";
                        successMessage += $"\nالمبلغ المتبقي للتقسيط: {(grandTotal - _paidUpfront):N2}";

                        try
                        {
                            await Helpers.NotificationHelper.RefreshInstallmentNotificationsAsync();
                            System.Diagnostics.Debug.WriteLine("✅ تم تحديث تنبيهات الأقساط بعد حفظ الفاتورة");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ خطأ في تحديث تنبيهات الأقساط: {ex.Message}");
                        }
                    }
                    else if (paidAmount > 0 && paidAmount < grandTotal)
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

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ انتهاء عملية حفظ الفاتورة");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"SaveInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region دوال جلب بيانات الشركة (Company Data Methods)

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

        #region دوال الطباعة المساعدة (Print Helper Methods)

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

        #region دالة الطباعة الرئيسية (Main Print Method)

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

                string customerName = txtCustomerSearch?.Text ?? "";
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
                    Text = "فاتورة بيع",
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
                AddInvoiceInfo("العميل:", customerName, col2X, infoRowY);
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

                string[] headers = { "م", "المنتج", "الكمية", "سعر الوحدة", "الخصم %", "مبلغ الخصم", "الإجمالي" };
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
                double customerSigX = rightMargin - sigWidth - 10;
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

                AddSignature("توقيع المستلم", customerSigX);
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

        #region دوال تعديل الجدول المباشر (Direct Grid Editing Methods)

        private void dgInvoiceItems_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                var column = e.Column as DataGridTextColumn;
                if (column != null)
                {
                    string header = column.Header?.ToString();
                    if (header != "الكمية" && header != "سعر البيع" && header != "الخصم %")
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
                var editedItem = e.Row.Item as SalesInvoiceItem;
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

                    case "سعر البيع":
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

        private async Task RecalculateItemAmountsAsync(SalesInvoiceItem item)
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

        #region كلاس صنف فاتورة البيع (SalesInvoiceItem Class)

        public class SalesInvoiceItem : INotifyPropertyChanged
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
                set { _productID = value; OnPropertyChanged(nameof(ProductID)); }
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

            public decimal QuantityInBaseUnit
            {
                get => _quantityInBaseUnit;
                set { _quantityInBaseUnit = value; OnPropertyChanged(nameof(QuantityInBaseUnit)); }
            }

            public decimal UnitPrice
            {
                get => _unitPrice;
                set { _unitPrice = value; OnPropertyChanged(nameof(UnitPrice)); }
            }

            public decimal DiscountPercent
            {
                get => _discountPercent;
                set { _discountPercent = value; OnPropertyChanged(nameof(DiscountPercent)); }
            }

            public decimal DiscountAmount
            {
                get => _discountAmount;
                set { _discountAmount = value; OnPropertyChanged(nameof(DiscountAmount)); }
            }

            public decimal TotalAmount
            {
                get => _totalAmount;
                set { _totalAmount = value; OnPropertyChanged(nameof(TotalAmount)); }
            }

            public int SerialNumber
            {
                get => _serialNumber;
                set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
            }

            public string UnitName
            {
                get => _unitName;
                set { _unitName = value; OnPropertyChanged(nameof(UnitName)); }
            }

            public string UnitTag
            {
                get => _unitTag;
                set { _unitTag = value; OnPropertyChanged(nameof(UnitTag)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region دوال الأقساط (Installment Methods)

        private void RbPaymentType_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 RbPaymentType_Checked - بدء");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront قبل التغيير = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment قبل التغيير = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine("========================================");

                var rbInstallment = this.FindName("rbInstallment") as RadioButton;
                var pnlInstallments = this.FindName("pnlInstallments") as Border;

                if (sender is RadioButton rb)
                {
                    System.Diagnostics.Debug.WriteLine($"📌 الـ RadioButton المختار: {rb.Name}");

                    if (rb == rbInstallment)
                    {
                        _isInstallment = true;
                        System.Diagnostics.Debug.WriteLine($"✅ تم تفعيل نظام التقسيط: _isInstallment = {_isInstallment}");

                        if (pnlInstallments != null)
                        {
                            pnlInstallments.Visibility = Visibility.Visible;
                            System.Diagnostics.Debug.WriteLine("✅ تم إظهار لوحة الأقساط");
                        }

                        if (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paid))
                        {
                            if (_paidUpfront == 0)
                            {
                                _paidUpfront = paid;
                                System.Diagnostics.Debug.WriteLine($"💰 تم قراءة المبلغ المدفوع: {_paidUpfront:N2}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"💰 الحفاظ على _paidUpfront الحالي: {_paidUpfront:N2}");
                            }
                        }
                        else
                        {
                            if (_paidUpfront == 0)
                            {
                                _paidUpfront = 0;
                                System.Diagnostics.Debug.WriteLine($"💰 لم يتم قراءة المبلغ المدفوع، تم تعيينه إلى 0");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"💰 الحفاظ على _paidUpfront الحالي: {_paidUpfront:N2}");
                            }
                        }

                        UpdateInstallmentSummary();
                    }
                    else
                    {
                        _isInstallment = false;
                        _installments = null;
                        System.Diagnostics.Debug.WriteLine($"❌ تم إلغاء نظام التقسيط: _isInstallment = {_isInstallment}");

                        if (pnlInstallments != null)
                        {
                            pnlInstallments.Visibility = Visibility.Collapsed;
                            System.Diagnostics.Debug.WriteLine("✅ تم إخفاء لوحة الأقساط");
                        }
                        CalculateTotals();
                    }
                }

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ RbPaymentType_Checked - انتهى");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront النهائي = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment النهائي = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ RbPaymentType_Checked Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void UpdateInstallmentSummary()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 UpdateInstallmentSummary - بدء");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine($"📊 عدد الأقساط = {(_installments != null ? _installments.Count : 0)}");
                System.Diagnostics.Debug.WriteLine("========================================");

                var lblInstallmentRemaining = this.FindName("lblInstallmentRemaining") as TextBlock;
                var lblInstallmentCount = this.FindName("lblInstallmentCount") as TextBlock;
                var lblTotalInstallments = this.FindName("lblTotalInstallments") as TextBlock;
                var lblGrandTotal = this.FindName("lblGrandTotal") as TextBlock;

                if (lblGrandTotal != null && decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal))
                {
                    System.Diagnostics.Debug.WriteLine($"💰 الإجمالي الكلي من lblGrandTotal: {grandTotal:N2}");

                    decimal remaining = grandTotal - _paidUpfront;
                    System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي للتقسيط: {remaining:N2} (الإجمالي {grandTotal:N2} - المدفوع {_paidUpfront:N2})");

                    if (lblInstallmentRemaining != null)
                    {
                        lblInstallmentRemaining.Text = $"{remaining:N2} {_currencySymbol}";
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث lblInstallmentRemaining: {lblInstallmentRemaining.Text}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ lblGrandTotal غير موجود أو القيمة غير صالحة");
                }

                if (_installments != null)
                {
                    decimal totalInstallments = _installments.Sum(x => x.Amount);
                    System.Diagnostics.Debug.WriteLine($"💰 مجموع الأقساط: {totalInstallments:N2}");

                    if (lblInstallmentCount != null)
                    {
                        lblInstallmentCount.Text = _installments.Count.ToString();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث lblInstallmentCount: {lblInstallmentCount.Text}");
                    }

                    if (lblTotalInstallments != null)
                    {
                        lblTotalInstallments.Text = $"{totalInstallments:N2} {_currencySymbol}";
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث lblTotalInstallments: {lblTotalInstallments.Text}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ لا توجد أقساط محفوظة");

                    if (lblInstallmentCount != null)
                    {
                        lblInstallmentCount.Text = "0";
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث lblInstallmentCount: 0");
                    }

                    if (lblTotalInstallments != null)
                    {
                        lblTotalInstallments.Text = $"0.00 {_currencySymbol}";
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث lblTotalInstallments: 0.00");
                    }
                }

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ UpdateInstallmentSummary - انتهى");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine($"📊 عدد الأقساط = {(_installments != null ? _installments.Count : 0)}");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ UpdateInstallmentSummary Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private async void BtnManageInstallments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("🔄 BtnManageInstallments_Click - بدء");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront قبل البدء = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment قبل البدء = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine("========================================");

                var lblGrandTotal = this.FindName("lblGrandTotal") as TextBlock;
                var txtPaidAmount = this.FindName("txtPaidAmount") as TextBox;

                CustomerSearchItem selectedCustomer = GetSelectedCustomer();

                if (selectedCustomer == null)
                {
                    MessageBox.Show("الرجاء اختيار العميل أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    System.Diagnostics.Debug.WriteLine("❌ العميل غير محدد");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"✅ العميل المحدد: {selectedCustomer.Name} (ID: {selectedCustomer.Id})");

                if (lblGrandTotal == null || !decimal.TryParse(lblGrandTotal.Text, out decimal grandTotal) || grandTotal <= 0)
                {
                    MessageBox.Show("الرجاء إدخال المنتجات أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    System.Diagnostics.Debug.WriteLine("❌ الإجمالي غير صالح");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"💰 الإجمالي الكلي: {grandTotal:N2}");

                // ✅ قراءة المبلغ المدفوع مقدمًا من حقل txtPaidAmount
                decimal paidUpfront = 0;
                if (txtPaidAmount != null && decimal.TryParse(txtPaidAmount.Text, out decimal paid))
                {
                    paidUpfront = paid;
                    System.Diagnostics.Debug.WriteLine($"💰 المبلغ المدفوع مقدمًا من txtPaidAmount: {paidUpfront:N2}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"💰 لم يتم قراءة المبلغ المدفوع، تم تعيينه إلى 0");
                }

                // ✅ التأكد من أن المبلغ المدفوع لا يتجاوز الإجمالي
                if (paidUpfront > grandTotal)
                {
                    paidUpfront = grandTotal;
                    if (txtPaidAmount != null)
                        txtPaidAmount.Text = paidUpfront.ToString("N2");
                    System.Diagnostics.Debug.WriteLine($"💰 تم تعديل المبلغ المدفوع ليتناسب مع الإجمالي: {paidUpfront:N2}");
                }

                // ✅ تخزين المبلغ المدفوع في المتغير العام (هذه هي القيمة التي سنحتفظ بها)
                _paidUpfront = paidUpfront;
                System.Diagnostics.Debug.WriteLine($"✅ تم تخزين _paidUpfront = {_paidUpfront:N2}");

                // ✅ حساب المبلغ المتبقي للتقسيط = الإجمالي - المدفوع
                decimal remainingForInstallments = grandTotal - paidUpfront;
                System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي للتقسيط: {remainingForInstallments:N2} (الإجمالي {grandTotal:N2} - المدفوع {paidUpfront:N2})");

                // ✅ إذا كان المبلغ المتبقي صفر أو أقل، لا حاجة للأقساط
                if (remainingForInstallments <= 0)
                {
                    MessageBox.Show("تم دفع المبلغ بالكامل، لا حاجة لتقسيم المبلغ", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    _installments = null;
                    UpdateInstallmentSummary();
                    System.Diagnostics.Debug.WriteLine("ℹ️ المبلغ مدفوع بالكامل، لا حاجة للأقساط");
                    return;
                }

                // ✅ عرض رسالة تأكيد للمستخدم توضح المبلغ الذي سيتم تقسيطه
                MessageBoxResult confirmResult = MessageBox.Show(
                    $"إجمالي الفاتورة: {grandTotal:N2}\n" +
                    $"المبلغ المدفوع مقدمًا: {paidUpfront:N2}\n" +
                    $"المبلغ المتبقي للتقسيط: {remainingForInstallments:N2}\n\n" +
                    $"هل تريد تقسيم المبلغ المتبقي ({remainingForInstallments:N2}) إلى أقساط؟",
                    "تأكيد تقسيم المبلغ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("❌ المستخدم ألغى عملية تقسيم الأقساط");
                    return;
                }
                System.Diagnostics.Debug.WriteLine("✅ المستخدم وافق على تقسيم الأقساط");

                string customerName = selectedCustomer.Name;
                string invoiceNumber = _currentInvoiceNumber;

                System.Diagnostics.Debug.WriteLine($"📌 العميل: {customerName}, رقم الفاتورة: {invoiceNumber}");
                System.Diagnostics.Debug.WriteLine($"📌 المبلغ المرسل للتقسيط: {remainingForInstallments:N2}");

                // ✅ تاريخ الفاتورة الفعلي المُختار في الفاتورة (وليس تاريخ اليوم)
                DateTime installmentsInvoiceDate = (dpInvoiceDate != null && dpInvoiceDate.SelectedDate.HasValue)
                    ? dpInvoiceDate.SelectedDate.Value
                    : DateTime.Now;

                // ✅ تمرير المبلغ المتبقي للتقسيط فقط (وليس الإجمالي)
                var dialog = new InstallmentDialog(
                    selectedCustomer.Id,
                    customerName,
                    invoiceNumber,
                    remainingForInstallments,  // ✅ المبلغ المتبقي فقط
                    _databaseService,
                    installmentsInvoiceDate);  // ✅ حساب مواعيد الاستحقاق من تاريخ الفاتورة

                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    _installments = dialog.GetInstallments();

                    // ✅ ✅ ✅ لا نقوم بتغيير _paidUpfront من النافذة
                    // ✅ نحتفظ بالقيمة التي قرأناها من txtPaidAmount
                    // _paidUpfront = dialog.GetPaidUpfront(); // ❌ نحذف هذا السطر

                    System.Diagnostics.Debug.WriteLine($"✅ تم استلام الأقساط: {_installments.Count} قسط");
                    System.Diagnostics.Debug.WriteLine($"✅ _paidUpfront محتفظ بقيمته = {_paidUpfront:N2}");

                    decimal totalInstallments = _installments.Sum(x => x.Amount);
                    System.Diagnostics.Debug.WriteLine($"💰 مجموع الأقساط: {totalInstallments:N2}");

                    // ✅ التأكيد: _paidUpfront لم يتغير
                    System.Diagnostics.Debug.WriteLine($"✅ التأكيد: _paidUpfront = {_paidUpfront:N2}");

                    UpdateInstallmentSummary();
                    CalculateTotals();

                    MessageBox.Show($"تم حفظ {_installments.Count} قسط بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ المستخدم ألغى نافذة الأقساط");
                }

                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("✅ BtnManageInstallments_Click - انتهى");
                System.Diagnostics.Debug.WriteLine($"📊 _paidUpfront النهائي = {_paidUpfront:N2}");
                System.Diagnostics.Debug.WriteLine($"📊 _isInstallment النهائي = {_isInstallment}");
                System.Diagnostics.Debug.WriteLine($"📊 عدد الأقساط النهائي = {(_installments != null ? _installments.Count : 0)}");
                System.Diagnostics.Debug.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"❌ BtnManageInstallments_Click Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region دوال AutoComplete للعملاء (Customer AutoComplete Methods)

        private void SelectCustomerFromList(CustomerSearchItem customer)
        {
            if (customer == null) return;

            _isSelectingCustomerFromList = true;
            _selectedCustomerId = customer.Id;
            txtCustomerSearch.Text = customer.Name;
            if (popupCustomers != null) popupCustomers.IsOpen = false;
            _isCustomerPopupOpen = false;
            _currentCustomerSelectedIndex = -1;

            _isSelectingCustomerFromList = false;
            System.Diagnostics.Debug.WriteLine($"✅ تم اختيار العميل: {customer.Name} (ID: {customer.Id})");
        }

        private CustomerSearchItem GetSelectedCustomer()
        {
            if (txtCustomerSearch == null) return null;

            if (!string.IsNullOrWhiteSpace(txtCustomerSearch.Text))
            {
                var customer = _allCustomersList.FirstOrDefault(c => c.Name == txtCustomerSearch.Text);
                if (customer != null) return customer;

                customer = _allCustomersList.FirstOrDefault(c => c.Code == txtCustomerSearch.Text);
                if (customer != null) return customer;
            }

            return null;
        }

        #endregion

        #region دوال أحداث AutoComplete للعملاء (Customer AutoComplete Event Handlers)

        private void TxtCustomerSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtCustomerPlaceholder != null) txtCustomerPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void TxtCustomerSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerSearch.Text) && txtCustomerPlaceholder != null)
                txtCustomerPlaceholder.Visibility = Visibility.Visible;

            Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (popupCustomers != null && lstCustomers != null && !lstCustomers.IsMouseOver && !lstCustomers.IsKeyboardFocusWithin)
                {
                    popupCustomers.IsOpen = false;
                    _isCustomerPopupOpen = false;
                }
            }));
        }

        private void TxtCustomerSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingCustomerFromList) return;

            string searchText = txtCustomerSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupCustomers != null)
                {
                    popupCustomers.IsOpen = false;
                    _isCustomerPopupOpen = false;
                }
                _currentCustomerSelectedIndex = -1;
                return;
            }

            _currentFilteredCustomers = _allCustomersList
                .Where(c => c.Name != null && c.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (lstCustomers != null)
            {
                lstCustomers.ItemsSource = _currentFilteredCustomers;
                lstCustomers.SelectedIndex = -1;
                _currentCustomerSelectedIndex = -1;
            }

            if (popupCustomers != null)
            {
                if (_currentFilteredCustomers.Count > 0 && !string.IsNullOrEmpty(searchText))
                {
                    popupCustomers.IsOpen = true;
                    _isCustomerPopupOpen = true;
                }
                else
                {
                    popupCustomers.IsOpen = false;
                    _isCustomerPopupOpen = false;
                }
            }
        }

        private void TxtCustomerSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;

                    if (!_isCustomerPopupOpen && _currentFilteredCustomers.Count > 0 && popupCustomers != null)
                    {
                        popupCustomers.IsOpen = true;
                        _isCustomerPopupOpen = true;
                    }

                    if (lstCustomers != null && lstCustomers.Items.Count > 0)
                    {
                        if (_currentCustomerSelectedIndex < lstCustomers.Items.Count - 1)
                        {
                            _currentCustomerSelectedIndex++;
                            lstCustomers.SelectedIndex = _currentCustomerSelectedIndex;
                            lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = true;

                    if (lstCustomers != null && lstCustomers.Items.Count > 0 && _currentCustomerSelectedIndex > 0)
                    {
                        _currentCustomerSelectedIndex--;
                        lstCustomers.SelectedIndex = _currentCustomerSelectedIndex;
                        lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                    }
                    else if (_currentCustomerSelectedIndex == 0 && lstCustomers != null)
                    {
                        _currentCustomerSelectedIndex = -1;
                        lstCustomers.SelectedIndex = -1;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (_currentCustomerSelectedIndex >= 0 && lstCustomers != null && _currentCustomerSelectedIndex < lstCustomers.Items.Count)
                    {
                        e.Handled = true;
                        var selectedCustomer = lstCustomers.SelectedItem as CustomerSearchItem;
                        if (selectedCustomer != null)
                        {
                            SelectCustomerFromList(selectedCustomer);
                        }
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    if (popupCustomers != null)
                    {
                        popupCustomers.IsOpen = false;
                        _isCustomerPopupOpen = false;
                    }
                    _currentCustomerSelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtCustomerSearch_PreviewKeyDown Error: {ex.Message}");
            }
        }

        private void LstCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCustomers != null && lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
            {
                _currentCustomerSelectedIndex = lstCustomers.SelectedIndex;
            }
        }

        private void LstCustomers_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (lstCustomers != null && lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
                {
                    SelectCustomerFromList(selectedCustomer);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (popupCustomers != null)
                {
                    popupCustomers.IsOpen = false;
                    _isCustomerPopupOpen = false;
                }
                if (txtCustomerSearch != null)
                {
                    txtCustomerSearch.Focus();
                }
            }
        }

        private void LstCustomers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstCustomers != null && lstCustomers.SelectedItem is CustomerSearchItem selectedCustomer)
            {
                SelectCustomerFromList(selectedCustomer);
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

            _selectedUnit1Name = product.Unit1 ?? "";
            _selectedUnit2Name = product.Unit2 ?? "";
            _selectedUnit3Name = product.Unit3 ?? "";
            _selectedUnit1Factor = product.Unit1Factor;
            _selectedUnit2Factor = product.Unit2Factor;
            _selectedUnitPrice1 = product.Price1;
            _selectedUnitPrice2 = product.Price2;
            _selectedUnitPrice3 = product.Price3;

            UpdateUnitComboBox(product);

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

            _ = DisplayProductStockAsync(product);

            if (txtQuantity != null)
            {
                txtQuantity.Focus();
                txtQuantity.SelectAll();
            }

            _isSelectingProductFromList = false;
            System.Diagnostics.Debug.WriteLine($"✅ تم اختيار المنتج: {product.Name} (ID: {product.Id})");
        }

        private void UpdateUnitComboBox(ProductSearchItem product)
        {
            try
            {
                if (cmbUnit == null) return;

                cmbUnit.Items.Clear();

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

                if (cmbUnit.Items.Count == 0)
                {
                    var defaultItem = new ComboBoxItem { Content = "وحدة", Tag = "Unit3" };
                    cmbUnit.Items.Add(defaultItem);
                }

                for (int i = 0; i < cmbUnit.Items.Count; i++)
                {
                    if (cmbUnit.Items[i] is ComboBoxItem item && item.Tag?.ToString() == "Unit3")
                    {
                        cmbUnit.SelectedIndex = i;
                        break;
                    }
                }

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

                var stockInfo = await GetProductStockWithUnitInfoAsync(storeId, productId);
                decimal quantityInBaseUnit = stockInfo.QuantityInBaseUnit;
                int unit1Factor = stockInfo.Unit1Factor;
                int unit2Factor = stockInfo.Unit2Factor;

                string selectedUnitTag = "Unit3";
                if (cmbUnit != null && cmbUnit.SelectedItem is ComboBoxItem selectedUnit)
                {
                    selectedUnitTag = selectedUnit.Tag?.ToString() ?? "Unit3";
                }

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

                if (lblProductStock != null)
                {
                    if (displayQuantity > 0)
                    {
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
}