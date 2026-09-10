using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// عنصر بسيط لعرض عميل أو مورد في صندوق الاقتراحات الذكي داخل نافذة الشيك
    /// </summary>
    public class PartySimpleItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// نافذة إضافة أو تعديل شيك (وارد من عميل أو صادر لمورد)
    /// </summary>
    public partial class CheckDialog : Window
    {
        private readonly CheckService _checkService;
        private readonly DatabaseService _dbService;
        private readonly CheckItem _existingCheck; // null = إضافة جديد

        private List<PartySimpleItem> _customersList = new List<PartySimpleItem>();
        private List<PartySimpleItem> _suppliersList = new List<PartySimpleItem>();

        public bool SavedSuccessfully { get; private set; }

        public CheckDialog(CheckService checkService, DatabaseService dbService, CheckItem existingCheck = null)
        {
            InitializeComponent();

            _checkService = checkService ?? throw new ArgumentNullException(nameof(checkService));
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
            _existingCheck = existingCheck;

            dpCheckDate.SelectedDate = DateTime.Now;

            rbReceivable.Checked += (s, e) => SwitchPartyList();
            rbPayable.Checked += (s, e) => SwitchPartyList();

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => this.DialogResult = false;

            this.Loaded += async (s, e) => await LoadPartiesAsync();

            if (_existingCheck != null)
            {
                lblDialogTitle.Text = "تعديل بيانات شيك";
                Title = "تعديل بيانات شيك";
                FillFormFromExisting();
            }
            else
            {
                lblDialogTitle.Text = "إضافة شيك جديد";
                Title = "إضافة شيك جديد";
            }
        }

        private async System.Threading.Tasks.Task LoadPartiesAsync()
        {
            try
            {
                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    using (var cmd = new SQLiteCommand(
                        "SELECT CustomerID, COALESCE(CustomerNameAr, CustomerName) FROM Customers WHERE IsActive = 1 ORDER BY CustomerNameAr", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _customersList.Add(new PartySimpleItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                            });
                        }
                    }

                    using (var cmd = new SQLiteCommand(
                        "SELECT SupplierID, COALESCE(SupplierNameAr, SupplierName) FROM Suppliers WHERE IsActive = 1 ORDER BY SupplierNameAr", connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _suppliersList.Add(new PartySimpleItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                            });
                        }
                    }
                }

                SwitchPartyList();

                if (_existingCheck != null)
                {
                    var list = _existingCheck.CheckType == "Receivable" ? _customersList : _suppliersList;
                    int? partyId = _existingCheck.CheckType == "Receivable" ? _existingCheck.CustomerID : _existingCheck.SupplierID;

                    if (partyId.HasValue)
                    {
                        cmbParty.SelectedItem = list.Find(p => p.Id == partyId.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء تحميل قوائم العملاء والموردين: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchPartyList()
        {
            bool isReceivable = rbReceivable.IsChecked == true;

            lblPartyLabel.Text = isReceivable ? "العميل" : "المورد";
            cmbParty.Placeholder = isReceivable ? "اكتب اسم العميل..." : "اكتب اسم المورد...";
            cmbParty.ItemsSource = isReceivable ? _customersList : _suppliersList;
            cmbParty.ClearSelection();
        }

        private void FillFormFromExisting()
        {
            rbReceivable.IsChecked = _existingCheck.CheckType == "Receivable";
            rbPayable.IsChecked = _existingCheck.CheckType == "Payable";
            rbReceivable.IsEnabled = false; // لا يُسمح بتغيير نوع الشيك بعد إنشائه
            rbPayable.IsEnabled = false;

            txtCheckNumber.Text = _existingCheck.CheckNumber;
            txtAmount.Text = _existingCheck.Amount.ToString("0.##");
            dpCheckDate.SelectedDate = _existingCheck.CheckDate;
            dpDueDate.SelectedDate = _existingCheck.DueDate;
            txtBankName.Text = _existingCheck.BankName;
            txtAccountNumber.Text = _existingCheck.AccountNumber;
            txtNotes.Text = _existingCheck.Notes;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string checkNumber = txtCheckNumber.Text.Trim();
                string bankName = txtBankName.Text.Trim();

                if (string.IsNullOrEmpty(checkNumber) || string.IsNullOrEmpty(bankName))
                {
                    MessageBox.Show("يرجى تعبئة الحقول الإلزامية: رقم الشيك واسم البنك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(txtAmount.Text.Trim(), out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("يرجى إدخال قيمة صحيحة أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dpCheckDate.SelectedDate == null)
                {
                    MessageBox.Show("يرجى اختيار تاريخ الشيك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedParty = cmbParty.SelectedItem as PartySimpleItem;
                bool isReceivable = rbReceivable.IsChecked == true;

                var check = new CheckItem
                {
                    CheckID = _existingCheck?.CheckID ?? 0,
                    CheckNumber = checkNumber,
                    CheckDate = dpCheckDate.SelectedDate.Value,
                    CheckType = isReceivable ? "Receivable" : "Payable",
                    Amount = amount,
                    BankName = bankName,
                    AccountNumber = string.IsNullOrWhiteSpace(txtAccountNumber.Text) ? null : txtAccountNumber.Text.Trim(),
                    DueDate = dpDueDate.SelectedDate,
                    CustomerID = isReceivable ? selectedParty?.Id : null,
                    SupplierID = !isReceivable ? selectedParty?.Id : null,
                    PayerName = isReceivable ? selectedParty?.Name : null,
                    PayeeName = !isReceivable ? selectedParty?.Name : null,
                    Notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim()
                };

                btnSave.IsEnabled = false;

                if (_existingCheck == null)
                {
                    var result = await _checkService.AddCheckAsync(check, LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 0);
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
                else
                {
                    var result = await _checkService.UpdateCheckAsync(check);
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
            }
            catch (Exception ex)
            {
                btnSave.IsEnabled = true;
                MessageBox.Show($"حدث خطأ أثناء الحفظ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
