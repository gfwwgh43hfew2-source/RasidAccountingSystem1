using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class ChecksView : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly CheckService _checkService;
        private readonly PermissionService _permissionService;

        private string _selectedTypeFilter = null;   // null = الكل
        private string _selectedStatusFilter = null; // null = الكل

        public ObservableCollection<CheckDisplayItem> ChecksList { get; set; }

        public ChecksView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _checkService = new CheckService(_dbService);
                _permissionService = new PermissionService(_dbService);

                ChecksList = new ObservableCollection<CheckDisplayItem>();
                dgChecks.ItemsSource = ChecksList;

                SetupFilters();

                this.Loaded += async (s, e) => await RefreshAllAsync();

                btnAddCheck.Click += BtnAddCheck_Click;
                btnRefresh.Click += async (s, e) => await RefreshAllAsync();
                cmbTypeFilter.SelectionChanged += async (s, e) => await OnFilterChangedAsync();
                cmbStatusFilter.SelectionChanged += async (s, e) => await OnFilterChangedAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تهيئة صفحة الشيكات", ex, "ChecksView");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region الفلاتر والتحميل

        private void SetupFilters()
        {
            cmbTypeFilter.Items.Add(new ComboBoxItem { Content = "كل الأنواع", Tag = null });
            cmbTypeFilter.Items.Add(new ComboBoxItem { Content = "📥 واردة", Tag = "Receivable" });
            cmbTypeFilter.Items.Add(new ComboBoxItem { Content = "📤 صادرة", Tag = "Payable" });
            cmbTypeFilter.SelectedIndex = 0;

            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "كل الحالات", Tag = null });
            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "مستلم / مُصدر", Tag = "Received" });
            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "تم الإيداع", Tag = "Deposited" });
            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "تم التحصيل / الصرف", Tag = "Cleared" });
            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "مرتد", Tag = "Bounced" });
            cmbStatusFilter.Items.Add(new ComboBoxItem { Content = "ملغى", Tag = "Cancelled" });
            cmbStatusFilter.SelectedIndex = 0;
        }

        private async Task OnFilterChangedAsync()
        {
            _selectedTypeFilter = (cmbTypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            _selectedStatusFilter = (cmbStatusFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            await LoadChecksAsync();
        }

        private async Task RefreshAllAsync()
        {
            await LoadSummaryAsync();
            await LoadChecksAsync();
        }

        private async Task LoadSummaryAsync()
        {
            var summary = await _checkService.GetChecksSummaryAsync();

            lblReceivablePending.Text = CurrencyHelper.FormatAmount(summary.TotalReceivablePending);
            lblPayablePending.Text = CurrencyHelper.FormatAmount(summary.TotalPayablePending);
            lblDueSoon.Text = summary.DueSoonCount.ToString();
            lblBounced.Text = summary.BouncedCount.ToString();
        }

        private async Task LoadChecksAsync()
        {
            var checks = await _checkService.GetChecksAsync(_selectedTypeFilter, _selectedStatusFilter, includeVoided: false);

            ChecksList.Clear();

            foreach (var c in checks)
            {
                ChecksList.Add(CheckDisplayItem.FromCheckItem(c));
            }

            lblNoChecks.Visibility = ChecksList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            dgChecks.Visibility = ChecksList.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region الإضافة / التعديل / تغيير الحالة / الإلغاء

        private async void BtnAddCheck_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CheckDialog(_checkService, _dbService);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async void EditCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            var check = await _checkService.GetCheckByIdAsync(id);
            if (check == null)
            {
                MessageBox.Show("لم يتم العثور على الشيك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new CheckDialog(_checkService, _dbService, check);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            var check = await _checkService.GetCheckByIdAsync(id);
            if (check == null)
            {
                MessageBox.Show("لم يتم العثور على الشيك", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validNext = _checkService.GetValidNextStatuses(check.Status, check.CheckType);
            if (validNext.Count == 0)
            {
                MessageBox.Show("هذا الشيك في حالة نهائية ولا يمكن تغيير حالته بعد الآن", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CheckStatusDialog(_checkService, check);
            ShowDialog(dialog);

            if (dialog.SavedSuccessfully)
                await RefreshAllAsync();
        }

        private async void VoidCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            if (!await _permissionService.CheckAndWarnAsync("void_check")) return;

            var check = await _checkService.GetCheckByIdAsync(id);
            if (check == null) return;

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من إلغاء الشيك رقم «{check.CheckNumber}»؟\nلن يظهر بعد ذلك في القوائم أو الملخصات، لكن سجله يبقى محفوظاً.",
                "تأكيد الإلغاء",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            int voidBy = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 0;
            var result = await _checkService.VoidCheckAsync(id, "ألغاه المستخدم من شاشة الشيكات", voidBy);

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

    /// <summary>
    /// نموذج عرض الشيك في الجدول، مع الألوان والنصوص الجاهزة للعرض حسب الحالة والنوع
    /// </summary>
    public class CheckDisplayItem
    {
        public int CheckId { get; set; }
        public string CheckNumber { get; set; }
        public string TypeDisplay { get; set; }
        public string PartyName { get; set; }
        public string BankName { get; set; }
        public string DueDateDisplay { get; set; }
        public string FormattedAmount { get; set; }
        public string StatusDisplay { get; set; }
        public Brush StatusBackground { get; set; }
        public Brush StatusForeground { get; set; }

        public static CheckDisplayItem FromCheckItem(CheckItem c)
        {
            var (bg, fg, text) = GetStatusVisual(c.Status);

            return new CheckDisplayItem
            {
                CheckId = c.CheckID,
                CheckNumber = c.CheckNumber,
                TypeDisplay = c.CheckType == "Receivable" ? "📥 واردة" : "📤 صادرة",
                PartyName = string.IsNullOrEmpty(c.PartyName) ? "-" : c.PartyName,
                BankName = c.BankName,
                DueDateDisplay = c.DueDate.HasValue ? c.DueDate.Value.ToString("yyyy-MM-dd") : "-",
                FormattedAmount = CurrencyHelper.FormatAmount(c.Amount),
                StatusDisplay = text,
                StatusBackground = bg,
                StatusForeground = fg
            };
        }

        private static (Brush Background, Brush Foreground, string Text) GetStatusVisual(string status)
        {
            switch (status)
            {
                case "Received":
                    return (new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)), new SolidColorBrush(Color.FromRgb(0x02, 0x64, 0x94)), "📥 مستلم");
                case "Issued":
                    return (new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)), new SolidColorBrush(Color.FromRgb(0x02, 0x64, 0x94)), "📤 مُصدر");
                case "Deposited":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)), new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)), "🏦 تم الإيداع");
                case "Cleared":
                    return (new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5)), new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46)), "✅ تم التحصيل");
                case "Bounced":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)), new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)), "↩️ مرتد");
                case "Cancelled":
                    return (new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), "🚫 ملغى");
                default:
                    return (new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), status);
            }
        }
    }
}
