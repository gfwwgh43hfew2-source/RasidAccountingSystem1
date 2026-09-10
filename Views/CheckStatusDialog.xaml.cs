using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// نافذة تغيير حالة شيك موجود (مثال: من "Deposited" إلى "Cleared" أو "Bounced")،
    /// مع إظهار الحقول الإضافية المناسبة لكل حالة (تاريخ الإيداع، تاريخ التحصيل، سبب الارتداد)
    /// </summary>
    public partial class CheckStatusDialog : Window
    {
        private readonly CheckService _checkService;
        private readonly CheckItem _check;
        private readonly Dictionary<string, RadioButton> _statusRadios = new Dictionary<string, RadioButton>();

        public bool SavedSuccessfully { get; private set; }

        private static readonly Dictionary<string, string> StatusDisplayNames = new Dictionary<string, string>
        {
            { "Received", "📥 مستلم" },
            { "Issued", "📤 مُصدر" },
            { "Deposited", "🏦 تم الإيداع بالبنك" },
            { "Cleared", "✅ تم التحصيل / الصرف" },
            { "Bounced", "↩️ ارتد الشيك" },
            { "Cancelled", "🚫 إلغاء الشيك" }
        };

        public CheckStatusDialog(CheckService checkService, CheckItem check)
        {
            InitializeComponent();

            _checkService = checkService ?? throw new ArgumentNullException(nameof(checkService));
            _check = check ?? throw new ArgumentNullException(nameof(check));

            dpDepositDate.SelectedDate = DateTime.Now;
            dpClearanceDate.SelectedDate = DateTime.Now;

            lblCheckInfo.Text = $"شيك رقم {_check.CheckNumber}  -  {CurrencyHelper.FormatAmount(_check.Amount)}  -  الحالة الحالية: {GetDisplayName(_check.Status)}";

            BuildStatusOptions();

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => this.DialogResult = false;
        }

        private string GetDisplayName(string status)
        {
            return StatusDisplayNames.TryGetValue(status, out string display) ? display : status;
        }

        private void BuildStatusOptions()
        {
            var validStatuses = _checkService.GetValidNextStatuses(_check.Status, _check.CheckType);

            if (validStatuses.Count == 0)
            {
                panelStatusOptions.Children.Add(new TextBlock
                {
                    Text = "لا توجد حالات تالية متاحة لهذا الشيك (الحالة الحالية نهائية)",
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap
                });
                btnSave.IsEnabled = false;
                return;
            }

            bool first = true;
            foreach (var status in validStatuses)
            {
                var radio = new RadioButton
                {
                    Content = GetDisplayName(status),
                    GroupName = "NewStatus",
                    Style = (Style)FindResource("StatusRadio"),
                    Tag = status,
                    IsChecked = first
                };
                radio.Checked += (s, e) => OnStatusSelectionChanged(status);

                _statusRadios[status] = radio;
                panelStatusOptions.Children.Add(radio);
                first = false;
            }

            // إظهار الحقول المناسبة للخيار الأول المحدد افتراضياً
            OnStatusSelectionChanged(validStatuses[0]);
        }

        private void OnStatusSelectionChanged(string newStatus)
        {
            panelDepositDate.Visibility = newStatus == "Deposited" ? Visibility.Visible : Visibility.Collapsed;
            panelClearanceDate.Visibility = newStatus == "Cleared" ? Visibility.Visible : Visibility.Collapsed;
            panelBounceReason.Visibility = newStatus == "Bounced" ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetSelectedStatus()
        {
            foreach (var kvp in _statusRadios)
            {
                if (kvp.Value.IsChecked == true)
                    return kvp.Key;
            }
            return null;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newStatus = GetSelectedStatus();

                if (string.IsNullOrEmpty(newStatus))
                {
                    MessageBox.Show("يرجى اختيار الحالة الجديدة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newStatus == "Bounced" && string.IsNullOrWhiteSpace(txtBounceReason.Text))
                {
                    MessageBox.Show("يرجى إدخال سبب ارتداد الشيك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show(
                    $"هل أنت متأكد من تغيير حالة الشيك رقم {_check.CheckNumber} إلى «{GetDisplayName(newStatus)}»؟",
                    "تأكيد",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                btnSave.IsEnabled = false;

                DateTime? depositDate = newStatus == "Deposited" ? dpDepositDate.SelectedDate : (DateTime?)null;
                DateTime? clearanceDate = newStatus == "Cleared" ? dpClearanceDate.SelectedDate : (DateTime?)null;
                string bounceReason = newStatus == "Bounced" ? txtBounceReason.Text.Trim() : null;

                int modifiedBy = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 0;

                var result = await _checkService.UpdateCheckStatusAsync(
                    _check.CheckID, newStatus, depositDate, clearanceDate, bounceReason, modifiedBy);

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
