using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// شاشة إدارة صلاحيات المستخدمين (متاحة لمدير النظام فقط).
    /// لكل صلاحية ثلاث حالات ممكنة تظهر كـ "شارة" قابلة للنقر للتبديل بينها بالدور:
    ///   افتراضي (حسب دور المستخدم) → مسموح صراحةً → ممنوع صراحةً → رجوع للافتراضي...
    /// </summary>
    public partial class PermissionsManagementView : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly PermissionService _permissionService;

        private UserSimpleItem _selectedUser;
        private System.Collections.Generic.Dictionary<string, bool> _explicitPermissions;

        public ObservableCollection<PermissionDisplayItem> PermissionItems { get; set; }

        public PermissionsManagementView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _permissionService = new PermissionService(_dbService);

                PermissionItems = new ObservableCollection<PermissionDisplayItem>();
                icPermissions.ItemsSource = PermissionItems;

                this.Loaded += async (s, e) => await CheckAccessAndLoadAsync();

                cmbUser.SelectionChanged += async (s, e) => await OnUserSelectedAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تهيئة صفحة إدارة الصلاحيات", ex, "PermissionsManagementView");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task CheckAccessAndLoadAsync()
        {
            // هذه الشاشة حساسة جداً (تتحكم في صلاحيات كل المستخدمين) - مقصورة على مدير النظام فقط
            if (!PermissionService.IsAdminRole(LoginView.CurrentUserRole))
            {
                MessageBox.Show(
                    "هذه الشاشة مخصصة لمدير النظام فقط.",
                    "غير مصرَّح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                lblNoUserSelected.Text = "ليس لديك صلاحية الوصول لهذه الشاشة";
                return;
            }

            var users = await _permissionService.GetAllUsersAsync();
            cmbUser.ItemsSource = users;
        }

        private async System.Threading.Tasks.Task OnUserSelectedAsync()
        {
            _selectedUser = cmbUser.SelectedItem as UserSimpleItem;

            if (_selectedUser == null)
            {
                borderUserInfo.Visibility = Visibility.Collapsed;
                icPermissions.Visibility = Visibility.Collapsed;
                lblNoUserSelected.Visibility = Visibility.Visible;
                return;
            }

            lblNoUserSelected.Visibility = Visibility.Collapsed;
            borderUserInfo.Visibility = Visibility.Visible;
            lblSelectedUserInfo.Text = $"{_selectedUser.FullName} ({_selectedUser.Username}) - {_selectedUser.UserRole}";

            bool isAdminUser = PermissionService.IsAdminRole(_selectedUser.UserRole);
            lblAdminNotice.Visibility = isAdminUser ? Visibility.Visible : Visibility.Collapsed;
            icPermissions.Visibility = isAdminUser ? Visibility.Collapsed : Visibility.Visible;

            if (isAdminUser) return;

            _explicitPermissions = await _permissionService.GetExplicitUserPermissionsAsync(_selectedUser.UserId);

            PermissionItems.Clear();
            foreach (var permission in PermissionService.GetPermissionCatalog())
            {
                PermissionItems.Add(BuildDisplayItem(permission));
            }
        }

        private PermissionDisplayItem BuildDisplayItem(PermissionCatalogItem permission)
        {
            bool? explicitValue = _explicitPermissions.ContainsKey(permission.PermissionKey)
                ? _explicitPermissions[permission.PermissionKey]
                : (bool?)null;

            return PermissionDisplayItem.Build(permission, explicitValue, _selectedUser.UserRole);
        }

        private async void PermissionPill_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border border) || !(border.Tag is string permissionKey) || _selectedUser == null)
                return;

            try
            {
                bool? currentExplicit = _explicitPermissions.ContainsKey(permissionKey)
                    ? _explicitPermissions[permissionKey]
                    : (bool?)null;

                int grantedBy = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 0;

                // دورة التبديل: بلا قرار صريح → مسموح صراحةً → ممنوع صراحةً → رجوع لبلا قرار صريح
                if (currentExplicit == null)
                {
                    await _permissionService.SetUserPermissionAsync(_selectedUser.UserId, permissionKey, true, grantedBy);
                    _explicitPermissions[permissionKey] = true;
                }
                else if (currentExplicit == true)
                {
                    await _permissionService.SetUserPermissionAsync(_selectedUser.UserId, permissionKey, false, grantedBy);
                    _explicitPermissions[permissionKey] = false;
                }
                else
                {
                    await _permissionService.ResetUserPermissionAsync(_selectedUser.UserId, permissionKey);
                    _explicitPermissions.Remove(permissionKey);
                }

                // إعادة رسم كل الصلاحيات بالحالة المحدَّثة
                var catalog = PermissionService.GetPermissionCatalog();
                PermissionItems.Clear();
                foreach (var permission in catalog)
                {
                    PermissionItems.Add(BuildDisplayItem(permission));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تبديل حالة الصلاحية", ex, "PermissionsManagementView");
                MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// نموذج عرض صلاحية واحدة لمستخدم محدَّد، موضحاً حالتها الحالية (افتراضي/مسموح صراحةً/ممنوع صراحةً)
    /// </summary>
    public class PermissionDisplayItem
    {
        public string PermissionKey { get; set; }
        public string NameAr { get; set; }
        public string CategoryAr { get; set; }
        public string StatusText { get; set; }
        public Brush StatusBackground { get; set; }
        public Brush StatusForeground { get; set; }

        public static PermissionDisplayItem Build(PermissionCatalogItem permission, bool? explicitValue, string userRole)
        {
            string statusText;
            Brush bg, fg;

            if (explicitValue == true)
            {
                statusText = "✅ مسموح صراحةً";
                bg = new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5));
                fg = new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46));
            }
            else if (explicitValue == false)
            {
                statusText = "🚫 ممنوع صراحةً";
                bg = new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2));
                fg = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B));
            }
            else
            {
                bool defaultAllowed = userRole != "مستخدم عادي";
                statusText = defaultAllowed ? "⚪ افتراضي (مسموح)" : "⚪ افتراضي (ممنوع)";
                bg = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
                fg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            }

            return new PermissionDisplayItem
            {
                PermissionKey = permission.PermissionKey,
                NameAr = permission.NameAr,
                CategoryAr = permission.CategoryAr,
                StatusText = statusText,
                StatusBackground = bg,
                StatusForeground = fg
            };
        }
    }
}
