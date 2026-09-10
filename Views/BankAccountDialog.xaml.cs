using System;
using System.Windows;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// نافذة إضافة أو تعديل حساب بنكي.
    /// يُستخدم نفس الكلاس للحالتين: مرّر حساب موجود للتعديل، أو اتركه فارغاً للإضافة.
    /// </summary>
    public partial class BankAccountDialog : Window
    {
        private readonly BankService _bankService;
        private readonly BankAccountItem _existingAccount; // null = إضافة جديد

        /// <summary>
        /// يُضبط بعد نجاح الحفظ حتى تقرأه الشاشة الرئيسية وتعيد تحميل البيانات
        /// </summary>
        public bool SavedSuccessfully { get; private set; }

        public BankAccountDialog(BankService bankService, BankAccountItem existingAccount = null)
        {
            InitializeComponent();

            _bankService = bankService ?? throw new ArgumentNullException(nameof(bankService));
            _existingAccount = existingAccount;

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => this.DialogResult = false;

            if (_existingAccount != null)
            {
                lblDialogTitle.Text = "تعديل حساب بنكي";
                Title = "تعديل حساب بنكي";
                FillFormFromExisting();
            }
            else
            {
                lblDialogTitle.Text = "إضافة حساب بنكي جديد";
                Title = "إضافة حساب بنكي جديد";
                this.Loaded += async (s, e) => txtBankCode.Text = await _bankService.GenerateUniqueBankCodeAsync();
            }
        }

        private void FillFormFromExisting()
        {
            txtBankCode.Text = _existingAccount.BankCode;
            txtBankCode.IsEnabled = false; // لا يُسمح بتعديل الكود بعد الإنشاء
            txtBankNameAr.Text = _existingAccount.BankNameAr;
            txtBankNameEn.Text = _existingAccount.BankNameEn;
            txtAccountNumber.Text = _existingAccount.AccountNumber;
            txtIBAN.Text = _existingAccount.IBAN;
            txtBranchName.Text = _existingAccount.BranchName;
            txtBranchCode.Text = _existingAccount.BranchCode;
            txtSwiftCode.Text = _existingAccount.SwiftCode;
            txtContactPerson.Text = _existingAccount.ContactPerson;
            txtPhone.Text = _existingAccount.Phone;
            txtEmail.Text = _existingAccount.Email;
            txtNotes.Text = _existingAccount.Notes;

            // في التعديل لا يمكن تغيير الرصيد مباشرة - فقط عبر حركات الإيداع/السحب
            lblOpeningBalance.Text = "الرصيد الحالي (للعرض فقط)";
            txtOpeningBalance.Text = _existingAccount.CurrentBalance.ToString("0.##");
            txtOpeningBalance.IsEnabled = false;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string bankCode = txtBankCode.Text.Trim();
                string bankNameAr = txtBankNameAr.Text.Trim();
                string accountNumber = txtAccountNumber.Text.Trim();

                if (string.IsNullOrEmpty(bankCode) || string.IsNullOrEmpty(bankNameAr) || string.IsNullOrEmpty(accountNumber))
                {
                    MessageBox.Show("يرجى تعبئة الحقول الإلزامية: كود الحساب، اسم البنك، رقم الحساب", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool codeExists = await _bankService.BankCodeExistsAsync(bankCode, _existingAccount?.BankAccountID ?? 0);
                if (codeExists)
                {
                    MessageBox.Show("كود الحساب مستخدم بالفعل لحساب بنكي آخر، يرجى اختيار كود مختلف", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var account = new BankAccountItem
                {
                    BankAccountID = _existingAccount?.BankAccountID ?? 0,
                    BankCode = bankCode,
                    BankNameAr = bankNameAr,
                    BankNameEn = string.IsNullOrWhiteSpace(txtBankNameEn.Text) ? null : txtBankNameEn.Text.Trim(),
                    AccountNumber = accountNumber,
                    IBAN = string.IsNullOrWhiteSpace(txtIBAN.Text) ? null : txtIBAN.Text.Trim(),
                    BranchName = string.IsNullOrWhiteSpace(txtBranchName.Text) ? null : txtBranchName.Text.Trim(),
                    BranchCode = string.IsNullOrWhiteSpace(txtBranchCode.Text) ? null : txtBranchCode.Text.Trim(),
                    SwiftCode = string.IsNullOrWhiteSpace(txtSwiftCode.Text) ? null : txtSwiftCode.Text.Trim(),
                    Currency = "SAR",
                    ContactPerson = string.IsNullOrWhiteSpace(txtContactPerson.Text) ? null : txtContactPerson.Text.Trim(),
                    Phone = string.IsNullOrWhiteSpace(txtPhone.Text) ? null : txtPhone.Text.Trim(),
                    Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? null : txtEmail.Text.Trim(),
                    Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
                };

                btnSave.IsEnabled = false;

                if (_existingAccount == null)
                {
                    decimal openingBalance = 0;
                    if (!string.IsNullOrWhiteSpace(txtOpeningBalance.Text) && !decimal.TryParse(txtOpeningBalance.Text.Trim(), out openingBalance))
                    {
                        MessageBox.Show("الرصيد الافتتاحي يجب أن يكون رقماً صحيحاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        btnSave.IsEnabled = true;
                        return;
                    }

                    if (openingBalance < 0)
                    {
                        MessageBox.Show("لا يمكن أن يكون الرصيد الافتتاحي بالسالب", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        btnSave.IsEnabled = true;
                        return;
                    }

                    await _bankService.AddBankAccountAsync(account, openingBalance, LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : (int?)null);
                    MessageBox.Show("تم إضافة الحساب البنكي بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _bankService.UpdateBankAccountAsync(account);
                    MessageBox.Show("تم تعديل بيانات الحساب البنكي بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                SavedSuccessfully = true;
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                btnSave.IsEnabled = true;
                MessageBox.Show($"حدث خطأ أثناء الحفظ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
