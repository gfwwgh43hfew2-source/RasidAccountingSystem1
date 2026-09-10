using System;
using System.Windows;
using System.Windows.Media;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// نافذة تسجيل حركة إيداع أو سحب على حساب بنكي معيّن.
    /// </summary>
    public partial class BankTransactionDialog : Window
    {
        private readonly BankService _bankService;
        private readonly BankAccountItem _bankAccount;
        private readonly string _transactionType; // "Deposit" أو "Withdrawal"

        public bool SavedSuccessfully { get; private set; }

        public BankTransactionDialog(BankService bankService, BankAccountItem bankAccount, string transactionType)
        {
            InitializeComponent();

            _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
            _bankAccount = bankAccount ?? throw new ArgumentNullException(nameof(bankAccount));
            _transactionType = transactionType;

            dpTransactionDate.SelectedDate = DateTime.Now;
            lblBankAccountName.Text = $"{_bankAccount.BankNameAr} - {_bankAccount.AccountNumber}";
            lblCurrentBalance.Text = $"الرصيد الحالي: {CurrencyHelper.FormatAmount(_bankAccount.CurrentBalance)}";

            if (_transactionType == "Deposit")
            {
                Title = "إيداع بنكي";
                lblDialogTitle.Text = "إيداع بنكي";
                lblHeaderIcon.Text = "💰";
                var color = (Color)ColorConverter.ConvertFromString("#10B981");
                borderHeader.Background = new SolidColorBrush(color);
                btnSave.Background = new SolidColorBrush(color);
            }
            else
            {
                Title = "سحب بنكي";
                lblDialogTitle.Text = "سحب بنكي";
                lblHeaderIcon.Text = "💸";
                var color = (Color)ColorConverter.ConvertFromString("#EF4444");
                borderHeader.Background = new SolidColorBrush(color);
                btnSave.Background = new SolidColorBrush(color);
            }

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => this.DialogResult = false;

            this.Loaded += (s, e) => txtAmount.Focus();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(txtAmount.Text.Trim(), out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("يرجى إدخال مبلغ صحيح أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dpTransactionDate.SelectedDate == null)
                {
                    MessageBox.Show("يرجى اختيار تاريخ الحركة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_transactionType == "Withdrawal" && amount > _bankAccount.CurrentBalance)
                {
                    var confirm = MessageBox.Show(
                        $"المبلغ المطلوب سحبه ({CurrencyHelper.FormatAmount(amount)}) أكبر من الرصيد الحالي ({CurrencyHelper.FormatAmount(_bankAccount.CurrentBalance)}).\nلن تتمكن من إتمام هذه العملية. هل تريد المتابعة على أي حال والمحاولة؟",
                        "تنبيه",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirm != MessageBoxResult.Yes)
                        return;
                }

                btnSave.IsEnabled = false;

                var result = await _bankService.AddTransactionAsync(
                    _bankAccount.BankAccountID,
                    _transactionType,
                    amount,
                    dpTransactionDate.SelectedDate.Value,
                    string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim(),
                    string.IsNullOrWhiteSpace(txtReferenceNumber.Text) ? null : txtReferenceNumber.Text.Trim(),
                    LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : (int?)null);

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    SavedSuccessfully = true;
                    this.DialogResult = true;
                }
                else
                {
                    btnSave.IsEnabled = true;
                    MessageBox.Show(result.Message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                btnSave.IsEnabled = true;
                MessageBox.Show($"حدث خطأ أثناء الحفظ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
