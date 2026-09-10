using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
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

namespace RasidAccountingSystem.Views
{
    public partial class ReceiptVouchersView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private ObservableCollection<ReceiptVoucherItem> _vouchersList;
        private bool _isInitialized = false;
        private bool _isSaving = false;
        private bool _isDeleting = false;

        // متغيرات AutoComplete للعملاء
        private List<CustomerSimple> AllCustomersList = new List<CustomerSimple>();
        private CustomerSimple SelectedCustomerObj = null;
        private bool _isSelectingCustomerFromList = false;
        private bool _isClearing = false;
        private bool _isNavigatingWithArrows = false;

        // متغيرات AutoComplete للفواتير
        private List<InvoiceSimple> AllInvoicesList = new List<InvoiceSimple>();
        private InvoiceSimple SelectedInvoiceObj = null;
        private bool _isSelectingInvoiceFromList = false;
        private bool _isNavigatingWithArrowsForInvoices = false;

        // متغيرات ComboBox لطرق السداد
        private List<PaymentMethodItem> AllPaymentMethodsList = new List<PaymentMethodItem>();
        private PaymentMethodItem SelectedPaymentMethodObj = null;
        private bool _isSelectingPaymentMethodFromList = false;

        // متغيرات ComboBox للخزائن
        private List<TreasurySimple> AllTreasuriesList = new List<TreasurySimple>();
        private TreasurySimple SelectedTreasuryObj = null;
        private bool _isSelectingTreasuryFromList = false;

        #endregion

        #region الكلاسات المساعدة (Helper Classes)

        public class CustomerSimple
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class TreasurySimple
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public decimal CurrentBalance { get; set; }
            public override string ToString() => Name;
        }

        public class InvoiceSimple
        {
            public int Id { get; set; }
            public string DisplayText { get; set; }
            public decimal RemainingAmount { get; set; }
            public string InvoiceNumber { get; set; }
            public override string ToString() => DisplayText;
        }

        public class PaymentMethodItem
        {
            public string Value { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }

        #endregion

        #region المنشئ

        public ReceiptVouchersView()
        {
            InitializeComponent();
            this.Loaded += ReceiptVouchersView_Loaded;
        }

        private async void ReceiptVouchersView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _dbService = new DatabaseService();
                if (_dbService == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("فشل في تهيئة خدمة قاعدة البيانات", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                _vouchersList = new ObservableCollection<ReceiptVoucherItem>();
                dgVouchers.ItemsSource = _vouchersList;

                CurrencyHelper.ConnectionString = _dbService.GetConnectionString();
                CurrencyHelper.LoadCurrencySettings();

                await LoadVoucherNumberAsync();
                await LoadAllCustomersForAutoCompleteAsync();
                await LoadAllTreasuriesForAutoCompleteAsync();
                await LoadAllInvoicesForAutoCompleteAsync();
                await LoadPaymentMethodsAsync();
                await LoadVouchersAsync();

                await Dispatcher.InvokeAsync(() =>
                {
                    cboPaymentMethod.ItemsSource = AllPaymentMethodsList;
                    cboTreasury.ItemsSource = AllTreasuriesList;
                });

                AttachEvents();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في تحميل الصفحة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                System.Diagnostics.Debug.WriteLine($"Error in Loaded: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void AttachEvents()
        {
            try
            {
                if (txtAmount != null)
                {
                    txtAmount.TextChanged += TxtAmount_TextChanged;
                }

                if (btnSave != null)
                {
                    btnSave.Click += BtnSave_Click;
                }

                if (btnClear != null)
                {
                    btnClear.Click += BtnClear_Click;
                }

                if (rbAll != null)
                {
                    rbAll.Checked += Filter_Checked;
                }

                if (rbToday != null)
                {
                    rbToday.Checked += Filter_Checked;
                }

                if (rbThisWeek != null)
                {
                    rbThisWeek.Checked += Filter_Checked;
                }

                if (rbThisMonth != null)
                {
                    rbThisMonth.Checked += Filter_Checked;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error attaching events: {ex.Message}");
            }
        }

        #endregion

        #region إدارة التمرير وإغلاق القوائم المنسدلة

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 || e.HorizontalChange != 0)
            {
                CloseAllPopups();
            }
        }

        private void CloseAllPopups()
        {
            if (popupCustomers != null && popupCustomers.IsOpen) popupCustomers.IsOpen = false;
            if (popupInvoices != null && popupInvoices.IsOpen) popupInvoices.IsOpen = false;
        }

        #endregion

        #region دوال مساعدة لتحسين التركيز على القوائم المنسدلة

        private void FocusListBoxAndSelectFirst(ListBox listBox, Popup popup)
        {
            if (listBox == null || popup == null || listBox.Items.Count == 0)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!popup.IsOpen) return;

                listBox.UpdateLayout();
                listBox.Focus();

                if (listBox.SelectedIndex == -1 && listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;

                if (listBox.SelectedItem != null)
                    listBox.ScrollIntoView(listBox.SelectedItem);

            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        #endregion

        #region تحميل البيانات الأساسية

        private async Task LoadVoucherNumberAsync()
        {
            try
            {
                if (_dbService == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        txtVoucherNumber.Text = $"REC-{DateTime.Now:yyyyMMddHHmmss}";
                    });
                    return;
                }

                string voucherNumber = await _dbService.GenerateReceiptVoucherNumberAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    txtVoucherNumber.Text = voucherNumber;
                    dpVoucherDate.SelectedDate = DateTime.Now;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading voucher number: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    txtVoucherNumber.Text = $"REC-{DateTime.Now:yyyyMMddHHmmss}";
                });
            }
        }

        private async Task LoadAllCustomersForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;

                AllCustomersList.Clear();

                var customers = await _dbService.GetCustomersAsync();

                // ✅ إصلاح أداء: كان بيفتح اتصال منفصل بقاعدة البيانات لكل عميل على حدة لجلب رصيده
                // (استعلام واحد لكل عميل × عدد العملاء بالكامل) وده اللي كان بيسبب تجمد الصفحة عند
                // الفتح مع وجود عدد كبير من العملاء. دلوقتي بنجيب أرصدة كل العملاء في استعلام واحد بس.
                var balances = await GetAllCustomersActualBalancesAsync();

                if (customers != null)
                {
                    foreach (var customer in customers)
                    {
                        if (customer != null)
                        {
                            decimal actualBalance = balances.TryGetValue(customer.CustomerID, out decimal bal) ? bal : 0;

                            string customerName = !string.IsNullOrEmpty(customer.CustomerNameAr) ? customer.CustomerNameAr : customer.CustomerName;

                            AllCustomersList.Add(new CustomerSimple
                            {
                                Id = customer.CustomerID,
                                Code = customer.CustomerCode,
                                // ✅ الاسم المعروض بقى اسم العميل فقط بدون الكود قبله (الكود لسه محفوظ في
                                // خاصية Code منفصلة، وبيستخدم في البحث لو المستخدم كتب الكود بدل الاسم)
                                Name = customerName,
                                CurrentBalance = actualBalance
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllCustomersList.Count} عميل لـ AutoComplete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading customers for AutoComplete: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في تحميل العملاء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task LoadAllTreasuriesForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;

                AllTreasuriesList.Clear();

                var treasuries = await _dbService.GetActiveTreasuriesAsync();

                if (treasuries != null)
                {
                    foreach (var treasury in treasuries)
                    {
                        if (treasury != null)
                        {
                            string displayName = $"{treasury.Name} (رصيد: {CurrencyHelper.FormatAmount(treasury.CurrentBalance)})";

                            AllTreasuriesList.Add(new TreasurySimple
                            {
                                Id = treasury.Id,
                                Name = displayName,
                                Code = treasury.Code,
                                CurrentBalance = treasury.CurrentBalance
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllTreasuriesList.Count} خزينة لـ ComboBox");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading treasuries for ComboBox: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في تحميل الخزائن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task LoadAllInvoicesForAutoCompleteAsync()
        {
            try
            {
                if (_dbService == null) return;

                AllInvoicesList.Clear();

                AllInvoicesList.Add(new InvoiceSimple
                {
                    Id = 0,
                    DisplayText = "-- بدون فاتورة --",
                    RemainingAmount = 0,
                    InvoiceNumber = ""
                });

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllInvoicesList.Count} فاتورة لـ AutoComplete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading invoices for AutoComplete: {ex.Message}");
            }
        }

        private async Task LoadCustomerInvoicesForAutoCompleteAsync(int customerId)
        {
            try
            {
                if (_dbService == null) return;

                AllInvoicesList.Clear();

                AllInvoicesList.Add(new InvoiceSimple
                {
                    Id = 0,
                    DisplayText = "-- بدون فاتورة --",
                    RemainingAmount = 0,
                    InvoiceNumber = ""
                });

                var invoices = await _dbService.GetCustomerInvoicesAsync(customerId);

                if (invoices != null)
                {
                    foreach (var invoice in invoices)
                    {
                        if (invoice != null)
                        {
                            string displayText = $"{invoice.InvoiceNumber} - المتبقي: {CurrencyHelper.FormatAmount(invoice.RemainingAmount)}";

                            AllInvoicesList.Add(new InvoiceSimple
                            {
                                Id = invoice.InvoiceID,
                                DisplayText = displayText,
                                RemainingAmount = invoice.RemainingAmount,
                                InvoiceNumber = invoice.InvoiceNumber
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllInvoicesList.Count} فاتورة للعميل {customerId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading invoices for AutoComplete: {ex.Message}");
            }
        }

        private async Task LoadPaymentMethodsAsync()
        {
            try
            {
                AllPaymentMethodsList.Clear();

                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Cash", DisplayName = "💰 نقدي" });
                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Check", DisplayName = "🏦 شيك" });
                AllPaymentMethodsList.Add(new PaymentMethodItem { Value = "Transfer", DisplayName = "💳 تحويل بنكي" });

                System.Diagnostics.Debug.WriteLine($"تم تحميل {AllPaymentMethodsList.Count} طريقة سداد");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading payment methods: {ex.Message}");
            }
        }

        private async Task LoadVouchersAsync()
        {
            try
            {
                if (_dbService == null)
                {
                    System.Diagnostics.Debug.WriteLine("_dbService is null in LoadVouchersAsync");
                    return;
                }

                string filter = "All";

                await Dispatcher.InvokeAsync(() =>
                {
                    if (rbToday != null && rbToday.IsChecked == true)
                    {
                        filter = "Today";
                    }
                    else if (rbThisWeek != null && rbThisWeek.IsChecked == true)
                    {
                        filter = "ThisWeek";
                    }
                    else if (rbThisMonth != null && rbThisMonth.IsChecked == true)
                    {
                        filter = "ThisMonth";
                    }
                });

                if (_vouchersList != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _vouchersList.Clear();
                    });
                }

                var vouchers = await _dbService.GetCustomerPaymentsWithTreasuryAsync(filter);

                decimal totalAmount = 0;

                if (vouchers != null)
                {
                    foreach (var voucher in vouchers)
                    {
                        if (voucher != null)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                _vouchersList.Add(new ReceiptVoucherItem
                                {
                                    PaymentID = voucher.PaymentID,
                                    VoucherNumber = voucher.VoucherNumber,
                                    PaymentDate = voucher.PaymentDate,
                                    CustomerID = voucher.CustomerID,
                                    CustomerName = voucher.CustomerName,
                                    Amount = voucher.Amount,
                                    PaymentMethod = voucher.PaymentMethod,
                                    PaymentMethodDisplay = voucher.PaymentMethod,
                                    TreasuryName = voucher.TreasuryName,
                                    CheckNumber = voucher.CheckNumber,
                                    Description = voucher.Description,
                                    InvoiceNumber = voucher.InvoiceNumber
                                });
                            });

                            totalAmount += voucher.Amount;
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (lblTotalAmount != null)
                    {
                        lblTotalAmount.Text = CurrencyHelper.FormatAmount(totalAmount);
                    }

                    if (lblVouchersCount != null)
                    {
                        lblVouchersCount.Text = _vouchersList.Count.ToString();
                    }

                    if (dgVouchers != null)
                    {
                        dgVouchers.Visibility = _vouchersList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    }

                    if (lblNoData != null)
                    {
                        lblNoData.Visibility = _vouchersList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                });

                System.Diagnostics.Debug.WriteLine($"تم تحميل {_vouchersList.Count} سند قبض");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading vouchers: {ex.Message}");
            }
        }

        #endregion

        #region دوال حساب الرصيد الفعلي وتحديثه

        /// <summary>
        /// يجيب الرصيد الفعلي لكل العملاء دفعة واحدة (استعلام واحد فقط)، بدل ما نفتح اتصال منفصل
        /// بقاعدة البيانات لكل عميل على حدة كما كان يحدث سابقاً في LoadAllCustomersForAutoCompleteAsync.
        /// </summary>
        private async Task<Dictionary<int, decimal>> GetAllCustomersActualBalancesAsync()
        {
            var result = new Dictionary<int, decimal>();

            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT
                            c.CustomerID,
                            c.OpeningBalance +
                            COALESCE((
                                SELECT SUM(DebitAmount)
                                FROM CustomerTransactions
                                WHERE CustomerID = c.CustomerID
                                AND TransactionType = 'Invoice'
                            ), 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount)
                                FROM CustomerTransactions
                                WHERE CustomerID = c.CustomerID
                                AND TransactionType = 'Receipt'
                            ), 0) as ActualBalance
                        FROM Customers c";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int customerId = Convert.ToInt32(reader["CustomerID"]);
                            decimal balance = reader["ActualBalance"] != DBNull.Value ? Convert.ToDecimal(reader["ActualBalance"]) : 0;
                            result[customerId] = balance;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllCustomersActualBalancesAsync Error: {ex.Message}");
            }

            return result;
        }

        private async Task<decimal> GetCustomerActualBalanceAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            COALESCE((SELECT OpeningBalance FROM Customers WHERE CustomerID = @customerId), 0) +
                            COALESCE((
                                SELECT SUM(DebitAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId 
                                AND TransactionType = 'Invoice'
                            ), 0) -
                            COALESCE((
                                SELECT SUM(CreditAmount) 
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId 
                                AND TransactionType = 'Receipt'
                            ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"الرصيد الفعلي للعميل {customerId}: {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerActualBalanceAsync Error: {ex.Message}");
                return 0;
            }
        }

        private async Task UpdateCustomerBalanceInTableAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه
                    // الحركات تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync
                    // وتريجر trig_customer_transaction_from_sales) - نفس الباج اللي كان اتصلح
                    // بالفعل لصيغة الموردين المطابقة في هذا الملف (UpdateSupplierBalanceInTableAsync)
                    // لكن اتفات إصلاحه هنا. النتيجة: كل سند قبض من عميل كان "يُصفِّر" مساهمة كل
                    // فواتيره من رصيده الحالي المعروض فورًا.
                    string updateSql = @"
                        UPDATE Customers 
                        SET CurrentBalance = 
                            COALESCE(OpeningBalance, 0) + COALESCE((
                                SELECT SUM(
                                    CASE 
                                        WHEN TransactionType = 'Invoice' THEN DebitAmount
                                        WHEN TransactionType = 'Receipt' THEN -CreditAmount
                                        WHEN TransactionType = 'Refund' THEN -CreditAmount
                                        ELSE (DebitAmount - CreditAmount)
                                    END
                                )
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId
                            ), 0),
                        ModifiedDate = CURRENT_TIMESTAMP
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل {customerId} في جدول Customers، الصفوف المتأثرة: {rowsAffected}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCustomerBalanceInTableAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال AutoComplete للعملاء

        private void TxtCustomerSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtCustomerPlaceholder != null)
            {
                txtCustomerPlaceholder.Visibility = Visibility.Collapsed;
            }

            if (borderCustomerSearch != null)
            {
                borderCustomerSearch.BorderBrush = (Brush)FindResource("PrimaryColor");
                borderCustomerSearch.BorderThickness = new Thickness(2);
            }
        }

        private void TxtCustomerSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isNavigatingWithArrows)
            {
                if (string.IsNullOrWhiteSpace(txtCustomerSearch.Text))
                {
                    if (txtCustomerPlaceholder != null)
                    {
                        txtCustomerPlaceholder.Visibility = Visibility.Visible;
                    }
                }

                if (borderCustomerSearch != null)
                {
                    borderCustomerSearch.BorderBrush = (Brush)FindResource("BorderLightColor");
                    borderCustomerSearch.BorderThickness = new Thickness(1);
                }

                Task.Delay(200).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (popupCustomers != null && !lstCustomers.IsMouseOver)
                        {
                            popupCustomers.IsOpen = false;
                        }
                    });
                });
            }
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
                }

                if (lstCustomers != null)
                {
                    lstCustomers.ItemsSource = null;
                }

                if (!_isClearing)
                {
                    SelectedCustomerObj = null;

                    if (borderCustomerBalance != null)
                    {
                        borderCustomerBalance.Visibility = Visibility.Collapsed;
                    }

                    if (borderBalanceAfter != null)
                    {
                        borderBalanceAfter.Visibility = Visibility.Collapsed;
                    }

                    if (lblCustomerBalance != null)
                    {
                        lblCustomerBalance.Text = CurrencyHelper.FormatAmount(0);
                    }
                }

                return;
            }

            var filtered = AllCustomersList
                .Where(c => (c.Name != null && c.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                         || (c.Code != null && c.Code.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (filtered.Count > 0)
            {
                if (lstCustomers != null)
                {
                    lstCustomers.ItemsSource = filtered;

                    if (lstCustomers.Items.Count > 0 && lstCustomers.SelectedIndex == -1 && !_isNavigatingWithArrows)
                    {
                        lstCustomers.SelectedIndex = 0;
                    }
                }

                if (popupCustomers != null && !_isNavigatingWithArrows)
                {
                    popupCustomers.IsOpen = true;
                }
            }
            else
            {
                if (popupCustomers != null && !_isNavigatingWithArrows)
                {
                    popupCustomers.IsOpen = false;
                }
            }
        }

        private void LstCustomers_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstCustomers.SelectedItem is CustomerSimple selectedCustomer)
            {
                SelectCustomer(selectedCustomer);
            }
        }

        private void LstCustomers_KeyDown(object sender, KeyEventArgs e)
        {
            if (lstCustomers == null || lstCustomers.Items.Count == 0) return;

            if (e.Key == Key.Enter && lstCustomers.SelectedItem is CustomerSimple selectedCustomer)
            {
                SelectCustomer(selectedCustomer);
                popupCustomers.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupCustomers.IsOpen = false;
                txtCustomerSearch.Focus();
                txtCustomerSearch.CaretIndex = txtCustomerSearch.Text.Length;
                e.Handled = true;
            }
        }

        private void LstCustomers_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (lstCustomers == null || lstCustomers.Items.Count == 0) return;

            if (e.Key == Key.Down && lstCustomers.SelectedIndex >= lstCustomers.Items.Count - 1)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up && lstCustomers.SelectedIndex == 0)
            {
                txtCustomerSearch.Focus();
                txtCustomerSearch.CaretIndex = txtCustomerSearch.Text.Length;
                e.Handled = true;
            }
        }

        private void TxtCustomerSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (popupCustomers == null || lstCustomers == null) return;

            if (e.Key == Key.Down)
            {
                if (popupCustomers.IsOpen && lstCustomers.Items.Count > 0)
                {
                    e.Handled = true;
                    _isNavigatingWithArrows = true;
                    lstCustomers.Focus();
                    if (lstCustomers.SelectedIndex == -1 && lstCustomers.Items.Count > 0)
                    {
                        lstCustomers.SelectedIndex = 0;
                    }
                    if (lstCustomers.SelectedItem != null)
                    {
                        lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                    }
                    _isNavigatingWithArrows = false;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (popupCustomers.IsOpen && lstCustomers.SelectedItem is CustomerSimple selectedCustomer)
                {
                    e.Handled = true;
                    SelectCustomer(selectedCustomer);
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (popupCustomers.IsOpen)
                {
                    popupCustomers.IsOpen = false;
                    e.Handled = true;
                }
            }
        }

        private void TxtCustomerSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                _isNavigatingWithArrows = true;

                string searchText = txtCustomerSearch.Text?.Trim() ?? "";

                var filtered = string.IsNullOrEmpty(searchText)
                    ? AllCustomersList
                    : AllCustomersList.Where(c => c.Name != null &&
                        c.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                lstCustomers.ItemsSource = filtered.Count > 0 ? filtered : AllCustomersList;

                if (lstCustomers.Items.Count > 0)
                {
                    popupCustomers.IsOpen = true;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (popupCustomers.IsOpen)
                        {
                            lstCustomers.UpdateLayout();
                            lstCustomers.Focus();
                            if (lstCustomers.SelectedIndex == -1 && lstCustomers.Items.Count > 0)
                            {
                                lstCustomers.SelectedIndex = 0;
                            }
                            if (lstCustomers.SelectedItem != null)
                            {
                                lstCustomers.ScrollIntoView(lstCustomers.SelectedItem);
                            }
                        }
                        _isNavigatingWithArrows = false;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    _isNavigatingWithArrows = false;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (popupCustomers.IsOpen)
                {
                    popupCustomers.IsOpen = false;
                    e.Handled = true;
                }
            }
        }

        private async void SelectCustomer(CustomerSimple customer)
        {
            if (customer == null) return;

            _isSelectingCustomerFromList = true;

            await Dispatcher.InvokeAsync(() =>
            {
                SelectedCustomerObj = customer;
                txtCustomerSearch.Text = customer.Name;
                txtCustomerSearch.CaretIndex = txtCustomerSearch.Text.Length;
            });

            if (popupCustomers != null)
            {
                popupCustomers.IsOpen = false;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (borderCustomerBalance != null)
                {
                    borderCustomerBalance.Visibility = Visibility.Visible;
                }

                if (borderBalanceAfter != null)
                {
                    borderBalanceAfter.Visibility = Visibility.Visible;
                }

                if (lblCustomerBalance != null)
                {
                    lblCustomerBalance.Text = CurrencyHelper.FormatAmount(customer.CurrentBalance);
                }

                if (lblCustomerBalanceStatus != null && borderBalanceStatus != null)
                {
                    if (customer.CurrentBalance > 0)
                    {
                        lblCustomerBalanceStatus.Text = "مدين (عليه)";
                        borderBalanceStatus.Background = (Brush)FindResource("DangerColor");
                    }
                    else if (customer.CurrentBalance < 0)
                    {
                        lblCustomerBalanceStatus.Text = "دائن (له)";
                        borderBalanceStatus.Background = (Brush)FindResource("SuccessColor");
                    }
                    else
                    {
                        lblCustomerBalanceStatus.Text = "متزن";
                        borderBalanceStatus.Background = (Brush)FindResource("InfoColor");
                    }
                }
            });

            UpdateBalanceAfter();

            await LoadCustomerInvoicesForAutoCompleteAsync(customer.Id);

            _isSelectingCustomerFromList = false;

            System.Diagnostics.Debug.WriteLine($"تم اختيار العميل: {customer.Name} (ID: {customer.Id}, الرصيد: {customer.CurrentBalance})");
        }

        #endregion

        #region دوال AutoComplete للفواتير

        private void TxtInvoiceSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtInvoicePlaceholder != null)
            {
                txtInvoicePlaceholder.Visibility = Visibility.Collapsed;
            }

            if (borderInvoiceSearch != null)
            {
                borderInvoiceSearch.BorderBrush = (Brush)FindResource("PrimaryColor");
                borderInvoiceSearch.BorderThickness = new Thickness(2);
            }
        }

        private void TxtInvoiceSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isNavigatingWithArrowsForInvoices)
            {
                if (string.IsNullOrWhiteSpace(txtInvoiceSearch.Text))
                {
                    if (txtInvoicePlaceholder != null)
                    {
                        txtInvoicePlaceholder.Visibility = Visibility.Visible;
                    }
                }

                if (borderInvoiceSearch != null)
                {
                    borderInvoiceSearch.BorderBrush = (Brush)FindResource("BorderLightColor");
                    borderInvoiceSearch.BorderThickness = new Thickness(1);
                }

                Task.Delay(200).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (popupInvoices != null && !lstInvoices.IsMouseOver)
                        {
                            popupInvoices.IsOpen = false;
                        }
                    });
                });
            }
        }

        private void TxtInvoiceSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingInvoiceFromList) return;

            string searchText = txtInvoiceSearch.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                if (popupInvoices != null)
                {
                    popupInvoices.IsOpen = false;
                }

                if (lstInvoices != null)
                {
                    lstInvoices.ItemsSource = null;
                }

                if (!_isClearing)
                {
                    SelectedInvoiceObj = null;
                }

                return;
            }

            var filtered = AllInvoicesList
                .Where(i => i.DisplayText != null && i.DisplayText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (filtered.Count > 0)
            {
                if (lstInvoices != null)
                {
                    lstInvoices.ItemsSource = filtered;

                    if (lstInvoices.Items.Count > 0 && lstInvoices.SelectedIndex == -1 && !_isNavigatingWithArrowsForInvoices)
                    {
                        lstInvoices.SelectedIndex = 0;
                    }
                }

                if (popupInvoices != null && !_isNavigatingWithArrowsForInvoices)
                {
                    popupInvoices.IsOpen = true;
                }
            }
            else
            {
                if (popupInvoices != null && !_isNavigatingWithArrowsForInvoices)
                {
                    popupInvoices.IsOpen = false;
                }
            }
        }

        private void LstInvoices_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstInvoices.SelectedItem is InvoiceSimple selectedInvoice)
            {
                SelectInvoice(selectedInvoice);
            }
        }

        private void LstInvoices_KeyDown(object sender, KeyEventArgs e)
        {
            if (lstInvoices == null || lstInvoices.Items.Count == 0) return;

            if (e.Key == Key.Enter && lstInvoices.SelectedItem is InvoiceSimple selectedInvoice)
            {
                SelectInvoice(selectedInvoice);
                popupInvoices.IsOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupInvoices.IsOpen = false;
                txtInvoiceSearch.Focus();
                txtInvoiceSearch.CaretIndex = txtInvoiceSearch.Text.Length;
                e.Handled = true;
            }
        }

        private void LstInvoices_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (lstInvoices == null || lstInvoices.Items.Count == 0) return;

            if (e.Key == Key.Down && lstInvoices.SelectedIndex >= lstInvoices.Items.Count - 1)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up && lstInvoices.SelectedIndex == 0)
            {
                txtInvoiceSearch.Focus();
                txtInvoiceSearch.CaretIndex = txtInvoiceSearch.Text.Length;
                e.Handled = true;
            }
        }

        private void TxtInvoiceSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (popupInvoices == null || lstInvoices == null) return;

            if (e.Key == Key.Down)
            {
                if (popupInvoices.IsOpen && lstInvoices.Items.Count > 0)
                {
                    e.Handled = true;
                    _isNavigatingWithArrowsForInvoices = true;
                    lstInvoices.Focus();
                    if (lstInvoices.SelectedIndex == -1 && lstInvoices.Items.Count > 0)
                    {
                        lstInvoices.SelectedIndex = 0;
                    }
                    if (lstInvoices.SelectedItem != null)
                    {
                        lstInvoices.ScrollIntoView(lstInvoices.SelectedItem);
                    }
                    _isNavigatingWithArrowsForInvoices = false;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (popupInvoices.IsOpen && lstInvoices.SelectedItem is InvoiceSimple selectedInvoice)
                {
                    e.Handled = true;
                    SelectInvoice(selectedInvoice);
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (popupInvoices.IsOpen)
                {
                    popupInvoices.IsOpen = false;
                    e.Handled = true;
                }
            }
        }

        private void TxtInvoiceSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                _isNavigatingWithArrowsForInvoices = true;

                string searchText = txtInvoiceSearch.Text?.Trim() ?? "";

                var filtered = string.IsNullOrEmpty(searchText)
                    ? AllInvoicesList
                    : AllInvoicesList.Where(i => i.DisplayText != null &&
                        i.DisplayText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                lstInvoices.ItemsSource = filtered.Count > 0 ? filtered : AllInvoicesList;

                if (lstInvoices.Items.Count > 0)
                {
                    popupInvoices.IsOpen = true;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (popupInvoices.IsOpen)
                        {
                            lstInvoices.UpdateLayout();
                            lstInvoices.Focus();
                            if (lstInvoices.SelectedIndex == -1 && lstInvoices.Items.Count > 0)
                            {
                                lstInvoices.SelectedIndex = 0;
                            }
                            if (lstInvoices.SelectedItem != null)
                            {
                                lstInvoices.ScrollIntoView(lstInvoices.SelectedItem);
                            }
                        }
                        _isNavigatingWithArrowsForInvoices = false;
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    _isNavigatingWithArrowsForInvoices = false;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (popupInvoices.IsOpen)
                {
                    popupInvoices.IsOpen = false;
                    e.Handled = true;
                }
            }
        }

        private void SelectInvoice(InvoiceSimple invoice)
        {
            if (invoice == null) return;

            _isSelectingInvoiceFromList = true;

            Dispatcher.InvokeAsync(() =>
            {
                SelectedInvoiceObj = invoice;
                txtInvoiceSearch.Text = invoice.DisplayText;
                txtInvoiceSearch.CaretIndex = txtInvoiceSearch.Text.Length;
            });

            if (popupInvoices != null)
            {
                popupInvoices.IsOpen = false;
            }

            UpdateBalanceAfter();

            _isSelectingInvoiceFromList = false;

            System.Diagnostics.Debug.WriteLine($"تم اختيار الفاتورة: {invoice.DisplayText} (ID: {invoice.Id})");
        }

        #endregion

        #region دوال ComboBox لطريقة السداد

        private void CboPaymentMethod_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingPaymentMethodFromList) return;
            if (cboPaymentMethod == null) return;

            TextBox textBox = e.OriginalSource as TextBox;
            if (textBox == null) return;

            string searchText = textBox.Text;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                cboPaymentMethod.ItemsSource = AllPaymentMethodsList;
                SelectedPaymentMethodObj = null;
                if (pnlCheckNumber != null)
                {
                    pnlCheckNumber.Visibility = Visibility.Collapsed;
                }
                return;
            }

            var filtered = AllPaymentMethodsList
                .Where(p => p.DisplayName != null && p.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            cboPaymentMethod.ItemsSource = filtered;

            if (filtered.Count == 1 && filtered[0].DisplayName.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                SelectedPaymentMethodObj = filtered[0];
                if (SelectedPaymentMethodObj.Value == "Check")
                {
                    if (pnlCheckNumber != null)
                    {
                        pnlCheckNumber.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (pnlCheckNumber != null)
                    {
                        pnlCheckNumber.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void CboPaymentMethod_DropDownOpened(object sender, EventArgs e)
        {
            if (cboPaymentMethod == null) return;

            string currentText = cboPaymentMethod.Text;

            if (string.IsNullOrWhiteSpace(currentText))
            {
                cboPaymentMethod.ItemsSource = AllPaymentMethodsList;
            }
            else
            {
                var filtered = AllPaymentMethodsList
                    .Where(p => p.DisplayName != null && p.DisplayName.IndexOf(currentText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                cboPaymentMethod.ItemsSource = filtered;
            }
        }

        private void CboPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSelectingPaymentMethodFromList) return;
            if (cboPaymentMethod == null) return;

            if (cboPaymentMethod.SelectedItem is PaymentMethodItem selectedPaymentMethod)
            {
                _isSelectingPaymentMethodFromList = true;

                SelectedPaymentMethodObj = selectedPaymentMethod;
                cboPaymentMethod.Text = selectedPaymentMethod.DisplayName;

                if (selectedPaymentMethod.Value == "Check")
                {
                    if (pnlCheckNumber != null)
                    {
                        pnlCheckNumber.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (pnlCheckNumber != null)
                    {
                        pnlCheckNumber.Visibility = Visibility.Collapsed;
                    }
                    if (txtCheckNumber != null)
                    {
                        txtCheckNumber.Text = "";
                    }
                }

                _isSelectingPaymentMethodFromList = false;

                System.Diagnostics.Debug.WriteLine($"تم اختيار طريقة السداد: {selectedPaymentMethod.DisplayName}");
            }
        }

        private void CboPaymentMethod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (cboPaymentMethod != null && cboPaymentMethod.Items.Count > 0 && cboPaymentMethod.SelectedItem == null)
                {
                    var firstItem = cboPaymentMethod.Items[0] as PaymentMethodItem;
                    if (firstItem != null)
                    {
                        _isSelectingPaymentMethodFromList = true;
                        SelectedPaymentMethodObj = firstItem;
                        cboPaymentMethod.Text = firstItem.DisplayName;
                        if (firstItem.Value == "Check")
                        {
                            if (pnlCheckNumber != null)
                            {
                                pnlCheckNumber.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            if (pnlCheckNumber != null)
                            {
                                pnlCheckNumber.Visibility = Visibility.Collapsed;
                            }
                        }
                        _isSelectingPaymentMethodFromList = false;
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (cboPaymentMethod != null)
                {
                    cboPaymentMethod.IsDropDownOpen = false;
                }
                e.Handled = true;
            }
        }

        private void CboPaymentMethod_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isSelectingPaymentMethodFromList) return;

            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (cboPaymentMethod != null && !cboPaymentMethod.IsDropDownOpen)
                    {
                        if (string.IsNullOrWhiteSpace(cboPaymentMethod.Text))
                        {
                            SelectedPaymentMethodObj = null;
                            if (pnlCheckNumber != null)
                            {
                                pnlCheckNumber.Visibility = Visibility.Collapsed;
                            }
                        }
                    }
                });
            });
        }

        #endregion

        #region دوال ComboBox للخزينة

        private void CboTreasury_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingTreasuryFromList) return;
            if (cboTreasury == null) return;

            TextBox textBox = e.OriginalSource as TextBox;
            if (textBox == null) return;

            string searchText = textBox.Text;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                cboTreasury.ItemsSource = AllTreasuriesList;
                SelectedTreasuryObj = null;
                return;
            }

            var filtered = AllTreasuriesList
                .Where(t => t.Name != null && t.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            cboTreasury.ItemsSource = filtered;

            if (filtered.Count == 1 && filtered[0].Name.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                SelectedTreasuryObj = filtered[0];
            }
        }

        private void CboTreasury_DropDownOpened(object sender, EventArgs e)
        {
            if (cboTreasury == null) return;

            string currentText = cboTreasury.Text;

            if (string.IsNullOrWhiteSpace(currentText))
            {
                cboTreasury.ItemsSource = AllTreasuriesList;
            }
            else
            {
                var filtered = AllTreasuriesList
                    .Where(t => t.Name != null && t.Name.IndexOf(currentText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                cboTreasury.ItemsSource = filtered;
            }
        }

        private void CboTreasury_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSelectingTreasuryFromList) return;
            if (cboTreasury == null) return;

            if (cboTreasury.SelectedItem is TreasurySimple selectedTreasury)
            {
                _isSelectingTreasuryFromList = true;

                SelectedTreasuryObj = selectedTreasury;
                cboTreasury.Text = selectedTreasury.Name;

                _isSelectingTreasuryFromList = false;

                System.Diagnostics.Debug.WriteLine($"تم اختيار الخزينة: {selectedTreasury.Name} (ID: {selectedTreasury.Id})");
            }
        }

        private void CboTreasury_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (cboTreasury != null && cboTreasury.Items.Count > 0 && cboTreasury.SelectedItem == null)
                {
                    var firstItem = cboTreasury.Items[0] as TreasurySimple;
                    if (firstItem != null)
                    {
                        _isSelectingTreasuryFromList = true;
                        SelectedTreasuryObj = firstItem;
                        cboTreasury.Text = firstItem.Name;
                        _isSelectingTreasuryFromList = false;
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (cboTreasury != null)
                {
                    cboTreasury.IsDropDownOpen = false;
                }
                e.Handled = true;
            }
        }

        private void CboTreasury_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isSelectingTreasuryFromList) return;

            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (cboTreasury != null && !cboTreasury.IsDropDownOpen)
                    {
                        if (string.IsNullOrWhiteSpace(cboTreasury.Text))
                        {
                            SelectedTreasuryObj = null;
                        }
                    }
                });
            });
        }

        #endregion

        #region الأحداث الأخرى

        private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateBalanceAfter();
        }

        private void UpdateBalanceAfter()
        {
            try
            {
                if (SelectedCustomerObj != null && lblBalanceAfter != null)
                {
                    decimal currentBalance = SelectedCustomerObj.CurrentBalance;
                    decimal amount = 0;

                    if (txtAmount != null)
                    {
                        decimal.TryParse(txtAmount.Text, out amount);
                    }

                    if (SelectedInvoiceObj != null && SelectedInvoiceObj.Id > 0)
                    {
                        decimal balanceAfter = SelectedInvoiceObj.RemainingAmount - amount;
                        Dispatcher.InvokeAsync(() =>
                        {
                            lblBalanceAfter.Text = CurrencyHelper.FormatAmount(Math.Max(0, balanceAfter));

                            if (balanceAfter < 0)
                            {
                                lblBalanceAfter.Foreground = (Brush)FindResource("DangerColor");
                            }
                            else if (balanceAfter == 0)
                            {
                                lblBalanceAfter.Foreground = (Brush)FindResource("SuccessColor");
                            }
                            else
                            {
                                lblBalanceAfter.Foreground = (Brush)FindResource("WarningColor");
                            }
                        });
                    }
                    else
                    {
                        decimal balanceAfter = currentBalance - amount;
                        Dispatcher.InvokeAsync(() =>
                        {
                            lblBalanceAfter.Text = CurrencyHelper.FormatAmount(balanceAfter);

                            if (balanceAfter < 0)
                            {
                                lblBalanceAfter.Foreground = (Brush)FindResource("WarningColor");
                            }
                            else
                            {
                                lblBalanceAfter.Foreground = (Brush)FindResource("SuccessColor");
                            }
                        });
                    }
                }
                else if (lblBalanceAfter != null)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        lblBalanceAfter.Text = CurrencyHelper.FormatAmount(0);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateBalanceAfter: {ex.Message}");
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
            {
                System.Diagnostics.Debug.WriteLine("عملية حفظ قيد التشغيل بالفعل، تم تجاهل النقرة");
                return;
            }

            _isSaving = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("=== بدء عملية حفظ سند القبض ===");

                if (_dbService == null)
                {
                    System.Diagnostics.Debug.WriteLine("خطأ: _dbService == null");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("خدمة قاعدة البيانات غير متاحة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                if (SelectedCustomerObj == null)
                {
                    System.Diagnostics.Debug.WriteLine("خطأ: لم يتم اختيار عميل");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("الرجاء اختيار عميل عن طريق البحث", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    txtCustomerSearch?.Focus();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"العميل: ID={SelectedCustomerObj.Id}, Name={SelectedCustomerObj.Name}, Balance={SelectedCustomerObj.CurrentBalance}");

                if (txtAmount == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("حدث خطأ في قراءة المبلغ", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"خطأ: المبلغ غير صحيح - قيمة المدخل: '{txtAmount.Text}'");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("المبلغ غير صحيح. يرجى إدخال مبلغ أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    txtAmount.Text = "";
                    txtAmount.Focus();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"المبلغ: {amount}");

                if (SelectedTreasuryObj == null)
                {
                    System.Diagnostics.Debug.WriteLine("خطأ: لم يتم اختيار خزينة");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("الرجاء اختيار الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    cboTreasury.Focus();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"الخزينة: ID={SelectedTreasuryObj.Id}, Name={SelectedTreasuryObj.Name}");

                int treasuryId = SelectedTreasuryObj.Id;

                if (SelectedPaymentMethodObj == null)
                {
                    System.Diagnostics.Debug.WriteLine("خطأ: لم يتم اختيار طريقة سداد");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("الرجاء اختيار طريقة السداد", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    cboPaymentMethod.Focus();
                    return;
                }

                string paymentMethod = SelectedPaymentMethodObj.Value;
                System.Diagnostics.Debug.WriteLine($"طريقة السداد: {paymentMethod} - {SelectedPaymentMethodObj.DisplayName}");

                if (paymentMethod != "Cash" && paymentMethod != "Check" && paymentMethod != "Transfer")
                {
                    System.Diagnostics.Debug.WriteLine($"خطأ: طريقة السداد '{paymentMethod}' غير مدعومة في قاعدة البيانات");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"طريقة السداد '{SelectedPaymentMethodObj.DisplayName}' غير مدعومة. الطرق المدعومة: نقدي, شيك, تحويل بنكي", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                string checkNumber = (paymentMethod == "Check" && txtCheckNumber != null) ? txtCheckNumber.Text : null;
                string description = (txtDescription != null) ? txtDescription.Text : "";
                DateTime voucherDate = (dpVoucherDate != null && dpVoucherDate.SelectedDate.HasValue) ? dpVoucherDate.SelectedDate.Value : DateTime.Now;

                int? invoiceId = null;
                string invoiceNumber = null;

                if (SelectedInvoiceObj != null && SelectedInvoiceObj.Id > 0)
                {
                    invoiceId = SelectedInvoiceObj.Id;
                    invoiceNumber = SelectedInvoiceObj.InvoiceNumber;
                    System.Diagnostics.Debug.WriteLine($"الفاتورة المرتبطة: ID={invoiceId}, Number={invoiceNumber}, Remaining={SelectedInvoiceObj.RemainingAmount}");
                }

                string voucherNumber = (txtVoucherNumber != null) ? txtVoucherNumber.Text : $"REC-{DateTime.Now:yyyyMMddHHmmss}";
                System.Diagnostics.Debug.WriteLine($"رقم السند: {voucherNumber}");

                int currentUserId = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1;
                System.Diagnostics.Debug.WriteLine($"معرف المستخدم: {currentUserId}");

                System.Diagnostics.Debug.WriteLine("محاولة استدعاء SaveCustomerPaymentWithTreasuryAsync...");

                bool success = await _dbService.SaveCustomerPaymentWithTreasuryAsync(
                    voucherNumber,
                    voucherDate,
                    SelectedCustomerObj.Id,
                    amount,
                    paymentMethod,
                    checkNumber,
                    description,
                    invoiceId,
                    invoiceNumber,
                    treasuryId,
                    currentUserId
                );

                System.Diagnostics.Debug.WriteLine($"نتيجة الحفظ: {(success ? "نجاح" : "فشل")}");

                if (success)
                {
                    await UpdateCustomerBalanceInTableAsync(SelectedCustomerObj.Id);

                    string debugInfo = await _dbService.DebugCustomerBalanceAsync(SelectedCustomerObj.Id);
                    System.Diagnostics.Debug.WriteLine(debugInfo);

                    decimal newBalance = await GetCustomerActualBalanceAsync(SelectedCustomerObj.Id);
                    SelectedCustomerObj.CurrentBalance = newBalance;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"تم حفظ سند القبض بنجاح\nرقم السند: {voucherNumber}\nالمبلغ: {CurrencyHelper.FormatAmount(amount)}", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    ClearForm();
                    await LoadVoucherNumberAsync();
                    await LoadAllCustomersForAutoCompleteAsync();
                    await LoadAllTreasuriesForAutoCompleteAsync();
                    await LoadVouchersAsync();
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("حدث خطأ في حفظ سند القبض. الرجاء التحقق من البيانات والمحاولة مرة أخرى.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== تفاصيل الخطأ في BtnSave_Click ===");
                System.Diagnostics.Debug.WriteLine($"نوع الخطأ: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"رسالة الخطأ: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"تتبع المكدس: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"الخطأ الداخلي: {ex.InnerException.Message}");
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في حفظ السند: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            _isClearing = true;

            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (txtAmount != null) txtAmount.Text = "";
                    if (txtDescription != null) txtDescription.Text = "";
                    if (txtCheckNumber != null) txtCheckNumber.Text = "";
                    if (txtCustomerSearch != null) txtCustomerSearch.Text = "";
                    if (txtInvoiceSearch != null) txtInvoiceSearch.Text = "";

                    if (cboPaymentMethod != null)
                    {
                        _isSelectingPaymentMethodFromList = true;
                        cboPaymentMethod.Text = "";
                        cboPaymentMethod.ItemsSource = AllPaymentMethodsList;
                        _isSelectingPaymentMethodFromList = false;
                    }

                    if (cboTreasury != null)
                    {
                        _isSelectingTreasuryFromList = true;
                        cboTreasury.Text = "";
                        cboTreasury.ItemsSource = AllTreasuriesList;
                        _isSelectingTreasuryFromList = false;
                    }

                    if (dpVoucherDate != null) dpVoucherDate.SelectedDate = DateTime.Now;
                    if (pnlCheckNumber != null) pnlCheckNumber.Visibility = Visibility.Collapsed;
                    if (borderCustomerBalance != null) borderCustomerBalance.Visibility = Visibility.Collapsed;
                    if (borderBalanceAfter != null) borderBalanceAfter.Visibility = Visibility.Collapsed;

                    if (txtCustomerPlaceholder != null && string.IsNullOrWhiteSpace(txtCustomerSearch?.Text))
                        txtCustomerPlaceholder.Visibility = Visibility.Visible;
                    if (txtInvoicePlaceholder != null && string.IsNullOrWhiteSpace(txtInvoiceSearch?.Text))
                        txtInvoicePlaceholder.Visibility = Visibility.Visible;
                });

                SelectedCustomerObj = null;
                SelectedInvoiceObj = null;
                SelectedPaymentMethodObj = null;
                SelectedTreasuryObj = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearForm: {ex.Message}");
            }
            finally
            {
                _isClearing = false;
            }
        }

        private async void Filter_Checked(object sender, RoutedEventArgs e)
        {
            await LoadVouchersAsync();
        }

        private async void DeleteVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (_isDeleting)
            {
                System.Diagnostics.Debug.WriteLine("عملية حذف قيد التشغيل بالفعل، تم تجاهل النقرة");
                return;
            }

            Button btn = sender as Button;
            if (btn == null || btn.Tag == null) return;

            if (!int.TryParse(btn.Tag.ToString(), out int paymentId)) return;

            var voucher = _vouchersList?.FirstOrDefault(v => v.PaymentID == paymentId);
            if (voucher == null) return;

            MessageBoxResult result = MessageBoxResult.None;

            await Dispatcher.InvokeAsync(() =>
            {
                result = MessageBox.Show(
                    $"هل أنت متأكد من حذف سند القبض رقم {voucher.VoucherNumber}؟\nالمبلغ: {CurrencyHelper.FormatAmount(voucher.Amount)}\nالعميل: {voucher.CustomerName}",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
            });

            if (result != MessageBoxResult.Yes) return;

            _isDeleting = true;

            try
            {
                if (_dbService == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("خدمة قاعدة البيانات غير متاحة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }

                bool success = await _dbService.DeleteCustomerPaymentWithTreasuryAsync(paymentId);

                if (success)
                {
                    if (voucher.CustomerID > 0)
                    {
                        await UpdateCustomerBalanceInTableAsync(voucher.CustomerID);
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("تم حذف سند القبض بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    await LoadAllCustomersForAutoCompleteAsync();
                    await LoadAllTreasuriesForAutoCompleteAsync();
                    await LoadVouchersAsync();
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("حدث خطأ في حذف سند القبض", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"خطأ في حذف السند: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                _isDeleting = false;
            }
        }

        #endregion

        #region دوال طباعة سند القبض

        /// <summary>
        /// طباعة سند القبض مع معاينة
        /// </summary>
        private async void PrintVoucher_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button btn = sender as Button;
                if (btn == null || btn.Tag == null) return;

                if (!int.TryParse(btn.Tag.ToString(), out int paymentId)) return;

                var voucher = _vouchersList?.FirstOrDefault(v => v.PaymentID == paymentId);
                if (voucher == null)
                {
                    await ShowMessage("لم يتم العثور على بيانات السند", "تنبيه");
                    return;
                }

                var voucherDetails = await GetReceiptVoucherDetailsAsync(paymentId);
                if (voucherDetails == null)
                {
                    await ShowMessage("حدث خطأ في جلب بيانات السند", "خطأ");
                    return;
                }

                FixedDocument fixedDocument = CreateReceiptVoucherPrintDocument(voucherDetails);
                if (fixedDocument != null && fixedDocument.Pages.Count > 0)
                {
                    await ShowReceiptPrintPreviewDialogAsync(fixedDocument, voucherDetails.VoucherNumber);
                }
                else
                {
                    MessageBox.Show("حدث خطأ في إنشاء مستند الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PrintVoucher_Click Error: {ex.Message}");
                await ShowMessage($"خطأ في الطباعة: {ex.Message}", "خطأ");
            }
        }

        /// <summary>
        /// عرض نافذة معاينة الطباعة لسند القبض
        /// </summary>
        private async Task ShowReceiptPrintPreviewDialogAsync(FixedDocument fixedDocument, string voucherNumber)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Window previewWindow = new Window
                {
                    Title = $"معاينة قبل الطباعة - سند قبض {voucherNumber}",
                    Width = 850,
                    Height = 950,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.CanResize,
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    FlowDirection = FlowDirection.RightToLeft
                };

                DocumentViewer documentViewer = new DocumentViewer
                {
                    Document = fixedDocument
                };

                StackPanel toolbar = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    FlowDirection = FlowDirection.RightToLeft
                };

                Button printButton = new Button
                {
                    Content = "🖨️ طباعة",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    Foreground = Brushes.White,
                    Cursor = Cursors.Hand
                };
                printButton.Click += (s, ev) =>
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        printDialog.PrintDocument(fixedDocument.DocumentPaginator, $"سند_قبض_{voucherNumber}");
                        MessageBox.Show("تم إرسال المستند إلى الطابعة بنجاح", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };

                Button closeButton = new Button
                {
                    Content = "✖ إغلاق",
                    Width = 85,
                    Height = 35,
                    Margin = new Thickness(5),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    Cursor = Cursors.Hand
                };
                closeButton.Click += (s, ev) => previewWindow.Close();

                toolbar.Children.Add(printButton);
                toolbar.Children.Add(closeButton);

                DockPanel mainPanel = new DockPanel();
                DockPanel.SetDock(toolbar, Dock.Top);
                mainPanel.Children.Add(toolbar);
                mainPanel.Children.Add(documentViewer);

                previewWindow.Content = mainPanel;
                previewWindow.Owner = Window.GetWindow(this);
                previewWindow.ShowDialog();
            });
        }

        /// <summary>
        /// جلب تفاصيل سند القبض من قاعدة البيانات
        /// </summary>
        private async Task<ReceiptVoucherPrintData> GetReceiptVoucherDetailsAsync(int paymentId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT 
                            rv.VoucherNumber,
                            rv.VoucherDate,
                            rv.Amount,
                            rv.PaymentMethod,
                            rv.CheckNumber,
                            rv.Description,
                            rv.CustomerID,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName,
                            COALESCE(c.CustomerCode, '') as CustomerCode,
                            t.TreasuryNameAr as TreasuryName,
                            t.TreasuryCode as TreasuryCode,
                            u.FullName as CreatedByName
                        FROM ReceiptVouchers rv
                        LEFT JOIN Customers c ON rv.CustomerID = c.CustomerID
                        LEFT JOIN Treasury t ON rv.TreasuryID = t.TreasuryID
                        LEFT JOIN Users u ON rv.CreatedBy = u.UserID
                        WHERE rv.VoucherID = @paymentId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@paymentId", paymentId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new ReceiptVoucherPrintData
                                {
                                    VoucherNumber = reader["VoucherNumber"]?.ToString() ?? "",
                                    VoucherDate = reader["VoucherDate"] != DBNull.Value ? Convert.ToDateTime(reader["VoucherDate"]) : DateTime.Now,
                                    Amount = reader["Amount"] != DBNull.Value ? Convert.ToDecimal(reader["Amount"]) : 0,
                                    PaymentMethod = GetPaymentMethodArabic(reader["PaymentMethod"]?.ToString() ?? "Cash"),
                                    CheckNumber = reader["CheckNumber"]?.ToString() ?? "",
                                    Description = reader["Description"]?.ToString() ?? "",
                                    CustomerID = reader["CustomerID"] != DBNull.Value ? Convert.ToInt32(reader["CustomerID"]) : 0,
                                    CustomerName = reader["CustomerName"]?.ToString() ?? "",
                                    CustomerCode = reader["CustomerCode"]?.ToString() ?? "",
                                    TreasuryName = reader["TreasuryName"]?.ToString() ?? "",
                                    TreasuryCode = reader["TreasuryCode"]?.ToString() ?? "",
                                    CreatedByName = reader["CreatedByName"]?.ToString() ?? "نظام"
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetReceiptVoucherDetailsAsync Error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// تحويل طريقة السداد من الإنجليزية إلى العربية
        /// </summary>
        private string GetPaymentMethodArabic(string method)
        {
            switch (method?.ToLower())
            {
                case "cash": return "نقدي";
                case "check": return "شيك";
                case "transfer": return "تحويل بنكي";
                case "bank": return "تحويل بنكي";
                default: return method ?? "";
            }
        }

        /// <summary>
        /// إنشاء مستند طباعة لسند القبض (من اليمين إلى اليسار)
        /// </summary>
        private FixedDocument CreateReceiptVoucherPrintDocument(ReceiptVoucherPrintData data)
        {
            try
            {
                string companyName = GetCompanyName();
                BitmapImage companyLogo = GetCompanyLogo();

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(680, 900);

                PageContent pageContent = new PageContent();
                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = 680;
                fixedPage.Height = 900;
                fixedPage.Background = Brushes.White;
                fixedPage.FlowDirection = FlowDirection.RightToLeft;
                fixedPage.Language = XmlLanguage.GetLanguage("ar-SA");

                StackPanel printPanel = new StackPanel();
                printPanel.Width = 620;
                printPanel.Margin = new Thickness(30, 30, 30, 30);
                printPanel.Background = Brushes.White;
                printPanel.FlowDirection = FlowDirection.RightToLeft;

                Grid headerGrid = CreateReceiptHeader(companyName, companyLogo);
                printPanel.Children.Add(headerGrid);

                Border separator = new Border
                {
                    Height = 2,
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Margin = new Thickness(0, 10, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                printPanel.Children.Add(separator);

                TextBlock titleBlock = new TextBlock
                {
                    Text = "سند قبض",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                printPanel.Children.Add(titleBlock);

                Grid infoGrid = CreateReceiptInfoGrid(data);
                printPanel.Children.Add(infoGrid);

                Border innerSeparator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Margin = new Thickness(0, 15, 0, 15),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                printPanel.Children.Add(innerSeparator);

                Grid amountGrid = CreateReceiptAmountGrid(data);
                printPanel.Children.Add(amountGrid);

                if (!string.IsNullOrEmpty(data.Description))
                {
                    Border descBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 15, 0, 15),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        BorderThickness = new Thickness(1)
                    };

                    StackPanel descPanel = new StackPanel
                    {
                        FlowDirection = FlowDirection.RightToLeft
                    };

                    descPanel.Children.Add(new TextBlock
                    {
                        Text = "البيان",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        FontFamily = new FontFamily("Cairo"),
                        Margin = new Thickness(0, 0, 0, 5),
                        HorizontalAlignment = HorizontalAlignment.Right
                    });

                    descPanel.Children.Add(new TextBlock
                    {
                        Text = data.Description,
                        FontSize = 12,
                        FontFamily = new FontFamily("Cairo"),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Right
                    });

                    descBorder.Child = descPanel;
                    printPanel.Children.Add(descBorder);
                }

                Grid signatureGrid = CreateReceiptSignatureGrid();
                printPanel.Children.Add(signatureGrid);

                TextBlock footerBlock = new TextBlock
                {
                    Text = $"تم الإنشاء بواسطة: {data.CreatedByName} | تاريخ الطباعة: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontFamily = new FontFamily("Cairo"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                printPanel.Children.Add(footerBlock);

                fixedPage.Children.Add(printPanel);
                pageContent.Child = fixedPage;
                fixedDocument.Pages.Add(pageContent);

                return fixedDocument;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateReceiptVoucherPrintDocument Error: {ex.Message}");
                return null;
            }
        }

        private Grid CreateReceiptHeader(string companyName, BitmapImage logo)
        {
            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(0, 0, 0, 10);
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (logo != null)
            {
                Border logoBorder = new Border
                {
                    Width = 60,
                    Height = 60,
                    CornerRadius = new CornerRadius(30),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(0, 0, 15, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Image logoImage = new Image
                {
                    Source = logo,
                    Width = 50,
                    Height = 50,
                    Stretch = Stretch.UniformToFill
                };

                EllipseGeometry clipGeometry = new EllipseGeometry(new Point(25, 25), 25, 25);
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
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            companyInfoPanel.Children.Add(companyNameBlock);

            TextBlock voucherTypeBlock = new TextBlock
            {
                Text = "سند قبض نقدي",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            companyInfoPanel.Children.Add(voucherTypeBlock);

            Grid.SetColumn(companyInfoPanel, 1);
            headerGrid.Children.Add(companyInfoPanel);

            return headerGrid;
        }

        private Grid CreateReceiptInfoGrid(ReceiptVoucherPrintData data)
        {
            Grid infoGrid = new Grid();
            infoGrid.Margin = new Thickness(0, 0, 0, 15);
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.Margin = new Thickness(0, 0, 0, 10);

            AddReceiptInfoRow(row1, 0, "رقم السند:", data.VoucherNumber);
            AddReceiptInfoRow(row1, 1, "التاريخ:", data.VoucherDate.ToString("yyyy-MM-dd"));
            Grid.SetRow(row1, 0);
            infoGrid.Children.Add(row1);

            Grid row2 = new Grid();
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.Margin = new Thickness(0, 0, 0, 10);

            AddReceiptInfoRow(row2, 0, "العميل:", $"{data.CustomerCode} - {data.CustomerName}");
            AddReceiptInfoRow(row2, 1, "طريقة السداد:", data.PaymentMethod);
            Grid.SetRow(row2, 1);
            infoGrid.Children.Add(row2);

            Grid row3 = new Grid();
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.Margin = new Thickness(0, 0, 0, 10);

            AddReceiptInfoRow(row3, 0, "الخزينة:", $"{data.TreasuryCode} - {data.TreasuryName}");
            string checkInfo = string.IsNullOrEmpty(data.CheckNumber) ? "---" : data.CheckNumber;
            AddReceiptInfoRow(row3, 1, "رقم الشيك:", checkInfo);
            Grid.SetRow(row3, 2);
            infoGrid.Children.Add(row3);

            return infoGrid;
        }

        private void AddReceiptInfoRow(Grid grid, int column, string label, string value)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            Grid innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                FontFamily = new FontFamily("Cairo"),
                TextAlignment = TextAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(labelBlock, 0);
            innerGrid.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                FontFamily = new FontFamily("Cairo"),
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(valueBlock, 1);
            innerGrid.Children.Add(valueBlock);

            cardBorder.Child = innerGrid;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        private Grid CreateReceiptAmountGrid(ReceiptVoucherPrintData data)
        {
            Grid amountGrid = new Grid();
            amountGrid.Margin = new Thickness(0, 10, 0, 10);
            amountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            amountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border amountCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                BorderThickness = new Thickness(1.5)
            };

            StackPanel amountStack = new StackPanel();
            amountStack.HorizontalAlignment = HorizontalAlignment.Center;

            amountStack.Children.Add(new TextBlock
            {
                Text = "المبلغ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            amountStack.Children.Add(new TextBlock
            {
                Text = CurrencyHelper.FormatAmount(data.Amount),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            amountStack.Children.Add(new TextBlock
            {
                Text = "ريال سعودي فقط",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });

            amountCard.Child = amountStack;
            Grid.SetColumn(amountCard, 0);
            amountGrid.Children.Add(amountCard);

            Border emptyCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            Grid.SetColumn(emptyCard, 1);
            amountGrid.Children.Add(emptyCard);

            return amountGrid;
        }

        private Grid CreateReceiptSignatureGrid()
        {
            Grid signatureGrid = new Grid();
            signatureGrid.Margin = new Thickness(0, 20, 0, 0);
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            signatureGrid.FlowDirection = FlowDirection.RightToLeft;

            AddReceiptSignatureCard(signatureGrid, 0, "المستلم", "الاسم والتوقيع");
            AddReceiptSignatureCard(signatureGrid, 1, "الصرف", "الاسم والتوقيع");
            AddReceiptSignatureCard(signatureGrid, 2, "المدير المالي", "الاسم والتوقيع");

            return signatureGrid;
        }

        private void AddReceiptSignatureCard(Grid grid, int column, string title, string subtitle)
        {
            Border cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 15, 12, 15),
                Margin = new Thickness(5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(1)
            };

            StackPanel cardStack = new StackPanel();
            cardStack.HorizontalAlignment = HorizontalAlignment.Center;
            cardStack.FlowDirection = FlowDirection.RightToLeft;

            cardStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            });

            Border signatureLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Width = 120,
                Margin = new Thickness(0, 15, 0, 5)
            };
            cardStack.Children.Add(signatureLine);

            cardStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Cairo"),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            cardBorder.Child = cardStack;
            Grid.SetColumn(cardBorder, column);
            grid.Children.Add(cardBorder);
        }

        /// <summary>
        /// الحصول على اسم الشركة من قاعدة البيانات
        /// </summary>
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

        /// <summary>
        /// الحصول على شعار الشركة
        /// </summary>
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

        /// <summary>
        /// عرض رسالة للمستخدم
        /// </summary>
        private async Task ShowMessage(string message, string title)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK,
                    title == "خطأ" ? MessageBoxImage.Error : MessageBoxImage.Information);
            });
        }

        #endregion
    }

    #region كلاس بيانات سند القبض

    public class ReceiptVoucherItem
    {
        public int PaymentID { get; set; }
        public string VoucherNumber { get; set; }
        public string PaymentDate { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentMethodDisplay { get; set; }
        public string TreasuryName { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
        public string InvoiceNumber { get; set; }
    }

    #endregion

    #region كلاس بيانات سند القبض للطباعة

    public class ReceiptVoucherPrintData
    {
        public string VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string CheckNumber { get; set; }
        public string Description { get; set; }
        public int CustomerID { get; set; }
        public string CustomerName { get; set; }
        public string CustomerCode { get; set; }
        public string TreasuryName { get; set; }
        public string TreasuryCode { get; set; }
        public string CreatedByName { get; set; }
    }

    #endregion
}