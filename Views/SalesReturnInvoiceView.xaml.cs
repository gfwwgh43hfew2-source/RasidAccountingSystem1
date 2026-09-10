using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// عنصر صف واحد في شبكة أصناف المرتجع، مع تحقق فوري من عدم تجاوز الكمية المتاحة للإرجاع
    /// وحساب تلقائي لإجمالي السطر عند تغيير كمية الإرجاع
    /// </summary>
    public class ReturnLineItemViewModel : INotifyPropertyChanged
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string Unit { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal AlreadyReturnedQuantity { get; set; }
        public decimal MaxReturnableQuantity { get; set; }
        public decimal UnitPrice { get; set; }

        private decimal _returnQuantity;
        public decimal ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                decimal clamped = value;

                if (clamped < 0) clamped = 0;
                if (clamped > MaxReturnableQuantity) clamped = MaxReturnableQuantity;

                if (_returnQuantity != clamped)
                {
                    _returnQuantity = clamped;
                    OnPropertyChanged(nameof(ReturnQuantity));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => ReturnQuantity * UnitPrice;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class SalesReturnInvoiceView : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly InvoiceService _invoiceService;

        private int _currentInvoiceId;
        private int _currentCustomerId;
        private int _currentStoreId;

        private List<InvoiceService.ReturnInvoiceListItem> _allInvoices = new List<InvoiceService.ReturnInvoiceListItem>();
        private bool _isSelectingInvoiceFromList;
        private bool _suppressNextLostFocusClose;

        public ObservableCollection<ReturnLineItemViewModel> Items { get; set; }

        public SalesReturnInvoiceView()
        {
            InitializeComponent();

            _dbService = new DatabaseService();
            _invoiceService = new InvoiceService(_dbService);

            Items = new ObservableCollection<ReturnLineItemViewModel>();
            dgItems.ItemsSource = Items;

            btnSearch.Click += BtnSearch_Click;
            btnSave.Click += BtnSave_Click;
            dgItems.CellEditEnding += (s, e) => this.Dispatcher.BeginInvoke(new Action(RecalculateTotals));

            txtInvoiceNumber.TextChanged += TxtInvoiceNumber_TextChanged;
            txtInvoiceNumber.GotFocus += TxtInvoiceNumber_GotFocus;
            txtInvoiceNumber.LostFocus += TxtInvoiceNumber_LostFocus;
            txtInvoiceNumber.PreviewKeyDown += TxtInvoiceNumber_PreviewKeyDown;

            lstInvoices.MouseUp += LstInvoices_MouseUp;
            lstInvoices.PreviewKeyDown += LstInvoices_PreviewKeyDown;

            this.Loaded += SalesReturnInvoiceView_Loaded;
        }

        private async void SalesReturnInvoiceView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _allInvoices = await _invoiceService.GetRecentSalesInvoicesForReturnAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل قائمة فواتير البيع لاقتراحات المرتجع", ex, "SalesReturnInvoiceView");
            }
        }

        #region اقتراحات البحث عن الفاتورة (بدلاً من كتابة الرقم كاملاً من الذاكرة)

        private bool _isNavigatingInvoicesWithArrows;

        private void TxtInvoiceNumber_GotFocus(object sender, RoutedEventArgs e)
        {
            txtInvoicePlaceholder.Visibility = Visibility.Collapsed;
            ShowInvoiceSuggestions(txtInvoiceNumber.Text?.Trim());
        }

        private void TxtInvoiceNumber_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInvoiceNumber.Text))
                txtInvoicePlaceholder.Visibility = Visibility.Visible;

            if (_suppressNextLostFocusClose)
            {
                _suppressNextLostFocusClose = false;
                return;
            }

            if (_isNavigatingInvoicesWithArrows) return;

            Task.Delay(180).ContinueWith(_ => Dispatcher.Invoke(() =>
            {
                if (!lstInvoices.IsMouseOver) popupInvoices.IsOpen = false;
            }));
        }

        private void TxtInvoiceNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSelectingInvoiceFromList) return;
            ShowInvoiceSuggestions(txtInvoiceNumber.Text?.Trim());
        }

        private void ShowInvoiceSuggestions(string searchText)
        {
            List<InvoiceService.ReturnInvoiceListItem> filtered;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                // لا يوجد نص بعد: اعرض أحدث الفواتير مباشرةً حتى يستعرضها المستخدم دون كتابة أي شيء
                filtered = _allInvoices.Take(30).ToList();
            }
            else
            {
                filtered = _allInvoices.Where(i =>
                    (i.InvoiceNumber?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.PartyName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i.InvoiceDate.ToString("yyyy-MM-dd").IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                ).Take(30).ToList();
            }

            lstInvoices.ItemsSource = filtered;
            popupInvoices.IsOpen = filtered.Count > 0;
        }

        private void TxtInvoiceNumber_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && popupInvoices.IsOpen && lstInvoices.Items.Count > 0)
            {
                _isNavigatingInvoicesWithArrows = true;
                lstInvoices.Focus();
                lstInvoices.SelectedIndex = 0;
                _isNavigatingInvoicesWithArrows = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (popupInvoices.IsOpen && lstInvoices.SelectedItem is InvoiceService.ReturnInvoiceListItem selected)
                {
                    SelectInvoiceFromList(selected);
                }
                else
                {
                    popupInvoices.IsOpen = false;
                    BtnSearch_Click(sender, e);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupInvoices.IsOpen = false;
            }
        }

        private void LstInvoices_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lstInvoices.SelectedItem is InvoiceService.ReturnInvoiceListItem selected)
                SelectInvoiceFromList(selected);
        }

        private void LstInvoices_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && lstInvoices.SelectedItem is InvoiceService.ReturnInvoiceListItem selected)
            {
                SelectInvoiceFromList(selected);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupInvoices.IsOpen = false;
                txtInvoiceNumber.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && lstInvoices.SelectedIndex == 0)
            {
                txtInvoiceNumber.Focus();
                e.Handled = true;
            }
        }

        private void SelectInvoiceFromList(InvoiceService.ReturnInvoiceListItem invoice)
        {
            _isSelectingInvoiceFromList = true;
            _suppressNextLostFocusClose = true;

            txtInvoiceNumber.Text = invoice.InvoiceNumber;
            popupInvoices.IsOpen = false;

            _isSelectingInvoiceFromList = false;

            // تحميل الفاتورة المختارة مباشرة دون الحاجة لضغط زر البحث مرة أخرى
            BtnSearch_Click(this, new RoutedEventArgs());
        }

        #endregion

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string invoiceNumber = txtInvoiceNumber.Text?.Trim();

                if (string.IsNullOrEmpty(invoiceNumber))
                {
                    MessageBox.Show("يرجى إدخال رقم فاتورة البيع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnSearch.IsEnabled = false;
                ResetForm();

                var result = await _invoiceService.GetSalesInvoiceForReturnAsync(invoiceNumber);

                btnSearch.IsEnabled = true;

                if (!result.Found)
                {
                    MessageBox.Show(result.Message, "لم يتم العثور على الفاتورة", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _currentInvoiceId = result.InvoiceID;
                _currentCustomerId = result.PartyID;
                _currentStoreId = result.StoreID;

                lblInvoiceInfo.Text = $"العميل: {result.PartyName}   |   التاريخ: {result.InvoiceDate:yyyy-MM-dd}   |   المخزن: {result.StoreName}";

                foreach (var item in result.Items)
                {
                    var vm = new ReturnLineItemViewModel
                    {
                        ProductID = item.ProductID,
                        ProductName = item.ProductName,
                        Unit = item.Unit,
                        OriginalQuantity = item.OriginalQuantity,
                        AlreadyReturnedQuantity = item.AlreadyReturnedQuantity,
                        MaxReturnableQuantity = item.MaxReturnableQuantity,
                        UnitPrice = item.UnitPrice
                    };
                    vm.PropertyChanged += (s, args) => RecalculateTotals();
                    Items.Add(vm);
                }

                bool hasReturnableItems = Items.Any(i => i.MaxReturnableQuantity > 0);

                if (!hasReturnableItems)
                {
                    lblNoInvoice.Text = "تم إرجاع كل أصناف هذه الفاتورة بالكامل من قبل";
                    lblNoInvoice.Visibility = Visibility.Visible;
                    dgItems.Visibility = Visibility.Collapsed;
                }
                else
                {
                    lblNoInvoice.Visibility = Visibility.Collapsed;
                    dgItems.Visibility = Visibility.Visible;
                    btnSave.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                btnSearch.IsEnabled = true;
                Logger.LogError("فشل البحث عن فاتورة بيع للمرتجع", ex, "SalesReturnInvoiceView");
                MessageBox.Show($"حدث خطأ أثناء البحث: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecalculateTotals()
        {
            decimal total = Items.Sum(i => i.LineTotal);
            lblTotalAmount.Text = total.ToString("N2");
        }

        private void ResetForm()
        {
            Items.Clear();
            lblInvoiceInfo.Text = "";
            lblTotalAmount.Text = "0.00";
            dgItems.Visibility = Visibility.Collapsed;
            lblNoInvoice.Text = "ابحث برقم فاتورة بيع صحيحة أو اختر من القائمة لعرض أصنافها هنا";
            lblNoInvoice.Visibility = Visibility.Visible;
            btnSave.IsEnabled = false;
            txtNotes.Text = "";
            _currentInvoiceId = 0;
            _currentCustomerId = 0;
            _currentStoreId = 0;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var itemsToReturn = Items.Where(i => i.ReturnQuantity > 0).ToList();

                if (itemsToReturn.Count == 0)
                {
                    MessageBox.Show("يرجى إدخال كمية إرجاع لصنف واحد على الأقل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                decimal total = itemsToReturn.Sum(i => i.LineTotal);

                var confirm = MessageBox.Show(
                    $"سيتم إرجاع {itemsToReturn.Count} صنف بقيمة إجمالية {total:N2}، وسيُخصم هذا المبلغ من رصيد العميل وتُعاد الكميات للمخزون.\n\nهل تريد المتابعة؟",
                    "تأكيد حفظ المرتجع",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                btnSave.IsEnabled = false;

                var returnItems = itemsToReturn.Select(i => new InvoiceService.SalesInvoiceItemClass
                {
                    ProductID = i.ProductID,
                    ProductName = i.ProductName,
                    Quantity = i.ReturnQuantity,
                    QuantityInBaseUnit = i.ReturnQuantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = 0,
                    DiscountAmount = 0,
                    TotalAmount = i.LineTotal
                }).ToList();

                int createdBy = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 0;

                var result = await _invoiceService.SaveSalesReturnInvoiceAsync(
                    _currentInvoiceId,
                    _currentCustomerId,
                    _currentStoreId,
                    DateTime.Now,
                    returnItems,
                    txtNotes.Text?.Trim(),
                    createdBy);

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtInvoiceNumber.Text = "";
                    ResetForm();
                }
                else
                {
                    btnSave.IsEnabled = true;
                    MessageBox.Show(result.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                btnSave.IsEnabled = true;
                Logger.LogError("فشل حفظ مرتجع البيع من الواجهة", ex, "SalesReturnInvoiceView");
                MessageBox.Show($"حدث خطأ أثناء الحفظ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
