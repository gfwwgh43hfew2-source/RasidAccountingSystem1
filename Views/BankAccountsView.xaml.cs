using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Media;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class BankAccountsView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private readonly DatabaseService _dbService;
        private readonly BankService _bankService;
        private readonly PermissionService _permissionService;
        private bool _isLoading = false;
        private int _selectedBankAccountId = 0; // 0 = عرض كل الحسابات

        #endregion

        #region الخصائص العامة (Public Properties)

        public ObservableCollection<BankAccountCardItem> BankAccountsList { get; set; }
        public ObservableCollection<BankTransactionDisplayItem> TransactionsList { get; set; }

        #endregion

        #region المنشئ (Constructor)

        public BankAccountsView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _bankService = new BankService(_dbService);
                _permissionService = new PermissionService(_dbService);

                BankAccountsList = new ObservableCollection<BankAccountCardItem>();
                TransactionsList = new ObservableCollection<BankTransactionDisplayItem>();

                icBankAccountsList.ItemsSource = BankAccountsList;
                dgTransactions.ItemsSource = TransactionsList;

                this.Loaded += async (s, e) => await OnViewLoadedAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BankAccountsView Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال التحميل الأساسية (Initialization Methods)

        private async Task OnViewLoadedAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                UpdateCurrencySymbol();
                await LoadBankAccountsAsync();
                await LoadFilterComboAsync();
                await LoadTransactionsAsync();

                btnAddBankAccount.Click += BtnAddBankAccount_Click;
                btnDeposit.Click += BtnDeposit_Click;
                btnWithdraw.Click += BtnWithdraw_Click;
                btnRefresh.Click += async (s, e) => await RefreshAllAsync();
                cmbBankFilter.SelectionChanged += CmbBankFilter_SelectionChanged;

                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"OnViewLoadedAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task RefreshAllAsync()
        {
            await LoadBankAccountsAsync();
            await LoadFilterComboAsync();
            await LoadTransactionsAsync();
        }

        private void UpdateCurrencySymbol()
        {
            try
            {
                string symbol = CurrencyHelper.GetCurrencySymbol();
                lblCurrencySymbol.Text = string.IsNullOrEmpty(symbol) ? "ر.س" : symbol;
            }
            catch
            {
                lblCurrencySymbol.Text = "ر.س";
            }
        }

        private async Task LoadBankAccountsAsync()
        {
            var accounts = await _bankService.GetBankAccountsAsync(activeOnly: true);

            BankAccountsList.Clear();
            decimal total = 0;

            foreach (var acc in accounts)
            {
                total += acc.CurrentBalance;
                BankAccountsList.Add(new BankAccountCardItem
                {
                    Id = acc.BankAccountID,
                    BankNameAr = acc.BankNameAr,
                    BankCode = acc.BankCode,
                    AccountNumberDisplay = $"رقم الحساب: {acc.AccountNumber}",
                    IBANDisplay = string.IsNullOrEmpty(acc.IBAN) ? "" : $"IBAN: {acc.IBAN}",
                    IBANVisibility = string.IsNullOrEmpty(acc.IBAN) ? Visibility.Collapsed : Visibility.Visible,
                    Balance = acc.CurrentBalance,
                    BalanceColor = acc.CurrentBalance >= 0
                        ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                        : new SolidColorBrush(Color.FromRgb(239, 68, 68))
                });
            }

            lblTotalBalance.Text = CurrencyHelper.FormatAmount(total);
            lblAccountsCount.Text = $"{BankAccountsList.Count} حساب";
            lblNoAccounts.Visibility = BankAccountsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task LoadFilterComboAsync()
        {
            var accounts = await _bankService.GetBankAccountsAsync(activeOnly: true);

            int? previouslySelected = _selectedBankAccountId;

            cmbBankFilter.SelectionChanged -= CmbBankFilter_SelectionChanged;
            cmbBankFilter.Items.Clear();

            var allItem = new ComboBoxItem { Content = "كل الحسابات", Tag = 0 };
            cmbBankFilter.Items.Add(allItem);

            ComboBoxItem itemToSelect = allItem;

            foreach (var acc in accounts)
            {
                var item = new ComboBoxItem { Content = acc.BankNameAr, Tag = acc.BankAccountID };
                cmbBankFilter.Items.Add(item);
                if (previouslySelected == acc.BankAccountID)
                    itemToSelect = item;
            }

            cmbBankFilter.SelectedItem = itemToSelect;
            cmbBankFilter.SelectionChanged += CmbBankFilter_SelectionChanged;
        }

        private async void CmbBankFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbBankFilter.SelectedItem is ComboBoxItem item && item.Tag is int id)
            {
                _selectedBankAccountId = id;
                await LoadTransactionsAsync();
            }
        }

        private async Task LoadTransactionsAsync()
        {
            var transactions = await _bankService.GetBankTransactionsAsync(_selectedBankAccountId);
            var accounts = await _bankService.GetBankAccountsAsync(activeOnly: false);

            TransactionsList.Clear();
            decimal totalDeposits = 0;
            decimal totalWithdrawals = 0;
            int serial = 1;

            foreach (var tx in transactions)
            {
                var account = accounts.FirstOrDefault(a => a.BankAccountID == tx.BankAccountID);
                decimal depositAmount = tx.TransactionType == "Deposit" ? tx.Amount : 0;
                decimal withdrawalAmount = tx.TransactionType == "Withdrawal" ? tx.Amount : 0;

                totalDeposits += depositAmount;
                totalWithdrawals += withdrawalAmount;

                TransactionsList.Add(new BankTransactionDisplayItem
                {
                    SerialNumber = serial++,
                    TransactionId = tx.TransactionID,
                    TransactionDateDisplay = tx.TransactionDate.ToString("yyyy-MM-dd"),
                    BankAccountName = account?.BankNameAr ?? "",
                    Description = tx.Description,
                    FormattedDeposit = depositAmount > 0 ? CurrencyHelper.FormatAmount(depositAmount) : "",
                    FormattedWithdrawal = withdrawalAmount > 0 ? CurrencyHelper.FormatAmount(withdrawalAmount) : "",
                    FormattedBalanceAfter = CurrencyHelper.FormatAmount(tx.BalanceAfter)
                });
            }

            lblTotalDeposits.Text = CurrencyHelper.FormatAmount(totalDeposits);
            lblTotalWithdrawals.Text = CurrencyHelper.FormatAmount(totalWithdrawals);
            lblNetAmount.Text = CurrencyHelper.FormatAmount(totalDeposits - totalWithdrawals);
        }

        #endregion

        #region إضافة / تعديل / حذف حساب بنكي

        private async void BtnAddBankAccount_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new BankAccountDialog(_bankService);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async void EditBankAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            var account = await _bankService.GetBankAccountByIdAsync(id);
            if (account == null)
            {
                MessageBox.Show("لم يتم العثور على الحساب البنكي", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new BankAccountDialog(_bankService, account);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async void DeleteBankAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            if (!await _permissionService.CheckAndWarnAsync("delete_bank_account")) return;

            var account = await _bankService.GetBankAccountByIdAsync(id);
            if (account == null) return;

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من حذف الحساب البنكي «{account.BankNameAr}»؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var result = await _bankService.DeleteBankAccountAsync(id);
            if (result.Success)
            {
                MessageBox.Show(result.Message, "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshAllAsync();
            }
            else
            {
                MessageBox.Show(result.Message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region إيداع / سحب / عرض الحركات

        private async void BtnDeposit_Click(object sender, RoutedEventArgs e)
        {
            await OpenTransactionDialogAsync("Deposit");
        }

        private async void BtnWithdraw_Click(object sender, RoutedEventArgs e)
        {
            await OpenTransactionDialogAsync("Withdrawal");
        }

        private async Task OpenTransactionDialogAsync(string transactionType)
        {
            if (BankAccountsList.Count == 0)
            {
                MessageBox.Show("لا توجد حسابات بنكية. يرجى إضافة حساب بنكي أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BankAccountItem selectedAccount;

            if (_selectedBankAccountId > 0)
            {
                selectedAccount = await _bankService.GetBankAccountByIdAsync(_selectedBankAccountId);
            }
            else if (BankAccountsList.Count == 1)
            {
                selectedAccount = await _bankService.GetBankAccountByIdAsync(BankAccountsList.First().Id);
            }
            else
            {
                // أكثر من حساب ولا يوجد فلتر محدد - نطلب من المستخدم الاختيار عبر نافذة بسيطة
                selectedAccount = await PromptForBankAccountAsync();
                if (selectedAccount == null) return;
            }

            if (selectedAccount == null)
            {
                MessageBox.Show("يرجى اختيار حساب بنكي أولاً من قائمة الفلترة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new BankTransactionDialog(_bankService, selectedAccount, transactionType);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async Task<BankAccountItem> PromptForBankAccountAsync()
        {
            var accounts = await _bankService.GetBankAccountsAsync(activeOnly: true);

            var window = new Window
            {
                Title = "اختيار الحساب البنكي",
                Width = 380,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Segoe UI")
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "اختر الحساب البنكي:", FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

            var combo = new ComboBox { Height = 38, ItemsSource = accounts, DisplayMemberPath = "BankNameAr", Margin = new Thickness(0, 0, 0, 20) };
            if (accounts.Count > 0) combo.SelectedIndex = 0;
            panel.Children.Add(combo);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            var okBtn = new Button { Content = "متابعة", Width = 100, Height = 36, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new Button { Content = "إلغاء", Width = 100, Height = 36 };
            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonPanel);

            window.Content = panel;

            BankAccountItem selected = null;
            okBtn.Click += (s, e) => { selected = combo.SelectedItem as BankAccountItem; window.DialogResult = true; };
            cancelBtn.Click += (s, e) => window.DialogResult = false;

            ShowDialog(window);

            return selected;
        }

        private void ViewTransactions_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            _selectedBankAccountId = id;

            foreach (ComboBoxItem item in cmbBankFilter.Items)
            {
                if (item.Tag is int tagId && tagId == id)
                {
                    cmbBankFilter.SelectedItem = item;
                    break;
                }
            }
        }

        private async void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int transactionId)) return;

            var confirm = MessageBox.Show(
                "هل أنت متأكد من حذف هذه الحركة؟ سيتم إعادة احتساب رصيد الحساب تلقائياً.",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var result = await _bankService.DeleteTransactionAsync(transactionId);
            if (result.Success)
            {
                await RefreshAllAsync();
            }
            else
            {
                MessageBox.Show(result.Message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region أدوات مساعدة (Helpers)

        private void ShowDialog(Window dialog)
        {
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                BlurEffect blurEffect = new BlurEffect { Radius = 8, KernelType = KernelType.Gaussian };
                mainWindow.Effect = blurEffect;
                mainWindow.IsEnabled = false;
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();
                mainWindow.Effect = null;
                mainWindow.IsEnabled = true;
                mainWindow.Activate();
                mainWindow.Focus();
            }
            else
            {
                dialog.ShowDialog();
            }
        }

        #endregion
    }

    #region نماذج العرض (Display Models)

    public class BankAccountCardItem
    {
        public int Id { get; set; }
        public string BankNameAr { get; set; }
        public string BankCode { get; set; }
        public string AccountNumberDisplay { get; set; }
        public string IBANDisplay { get; set; }
        public Visibility IBANVisibility { get; set; }
        public decimal Balance { get; set; }
        public Brush BalanceColor { get; set; }
        public string FormattedBalance => CurrencyHelper.FormatAmount(Balance);
    }

    public class BankTransactionDisplayItem
    {
        public int SerialNumber { get; set; }
        public int TransactionId { get; set; }
        public string TransactionDateDisplay { get; set; }
        public string BankAccountName { get; set; }
        public string Description { get; set; }
        public string FormattedDeposit { get; set; }
        public string FormattedWithdrawal { get; set; }
        public string FormattedBalanceAfter { get; set; }
    }

    #endregion
}
