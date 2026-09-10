using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class AuditLogView : UserControl
    {
        private readonly DatabaseService _dbService;
        private readonly PermissionService _permissionService;

        private string _selectedTableFilter = null;
        private string _selectedActionFilter = null;

        public ObservableCollection<AuditLogDisplayItem> LogItems { get; set; }

        public AuditLogView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _permissionService = new PermissionService(_dbService);

                LogItems = new ObservableCollection<AuditLogDisplayItem>();
                dgAuditLog.ItemsSource = LogItems;

                SetupFilters();

                this.Loaded += async (s, e) =>
                {
                    if (!await _permissionService.CheckAndWarnAsync("view_audit_log"))
                    {
                        lblNoResults.Text = "ليس لديك صلاحية عرض سجل التعديلات";
                        lblNoResults.Visibility = Visibility.Visible;
                        return;
                    }

                    await LoadLogAsync();
                };

                btnRefresh.Click += async (s, e) => await LoadLogAsync();
                cmbTableFilter.SelectionChanged += async (s, e) => await OnFilterChangedAsync();
                cmbActionFilter.SelectionChanged += async (s, e) => await OnFilterChangedAsync();
                txtSearch.TextChanged += async (s, e) => await LoadLogAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تهيئة صفحة سجل التعديلات", ex, "AuditLogView");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFilters()
        {
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "كل الجداول", Tag = null });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "المستخدمون", Tag = "Users" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "العملاء", Tag = "Customers" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "الموردون", Tag = "Suppliers" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "حركات العملاء", Tag = "CustomerTransactions" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "حركات الموردين", Tag = "SupplierTransactions" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "فواتير البيع", Tag = "SalesInvoices" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "فواتير المشتريات", Tag = "PurchaseInvoices" });
            cmbTableFilter.Items.Add(new ComboBoxItem { Content = "الشيكات", Tag = "Checks" });
            cmbTableFilter.SelectedIndex = 0;

            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "كل الأحداث", Tag = null });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "حذف", Tag = "Deleted" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "إلغاء", Tag = "Voided" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "تغيير صلاحية", Tag = "RoleChanged" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "تفعيل/تعطيل حساب", Tag = "ActiveStatusChanged" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "تغيير كلمة مرور", Tag = "PasswordChanged" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "تعديل رصيد أول المدة", Tag = "OpeningBalanceChanged" });
            cmbActionFilter.Items.Add(new ComboBoxItem { Content = "تعديل حد الائتمان", Tag = "CreditLimitChanged" });
            cmbActionFilter.SelectedIndex = 0;
        }

        private async Task OnFilterChangedAsync()
        {
            _selectedTableFilter = (cmbTableFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            _selectedActionFilter = (cmbActionFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            await LoadLogAsync();
        }

        private async Task LoadLogAsync()
        {
            try
            {
                LogItems.Clear();

                using (var connection = new SQLiteConnection(_dbService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT a.LogID, a.TableName, a.RecordID, a.ActionType, a.NewValue, a.UserID, a.CreatedDate,
                               u.FullName AS ChangedByName
                        FROM AuditLog a
                        LEFT JOIN Users u ON a.UserID = u.UserID
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(_selectedTableFilter))
                        sql += " AND a.TableName = @tableName";

                    if (!string.IsNullOrEmpty(_selectedActionFilter))
                        sql += " AND a.ActionType = @action";

                    if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                        sql += " AND a.NewValue LIKE @search";

                    sql += " ORDER BY a.CreatedDate DESC LIMIT 500";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(_selectedTableFilter))
                            cmd.Parameters.AddWithValue("@tableName", _selectedTableFilter);

                        if (!string.IsNullOrEmpty(_selectedActionFilter))
                            cmd.Parameters.AddWithValue("@action", _selectedActionFilter);

                        if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                            cmd.Parameters.AddWithValue("@search", $"%{txtSearch.Text.Trim()}%");

                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                LogItems.Add(AuditLogDisplayItem.FromReader(reader));
                            }
                        }
                    }
                }

                lblNoResults.Visibility = LogItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                dgAuditLog.Visibility = LogItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل سجل التعديلات", ex, "AuditLogView");
                MessageBox.Show($"حدث خطأ أثناء تحميل السجل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// نموذج عرض حدث واحد من سجل التعديلات، مع ترجمة أسماء الجداول والأحداث للعربية وألوان مناسبة
    /// </summary>
    public class AuditLogDisplayItem
    {
        public int AuditId { get; set; }
        public string TableNameDisplay { get; set; }
        public string ActionDisplay { get; set; }
        public string Summary { get; set; }
        public string ChangedByName { get; set; }
        public string ChangedDateDisplay { get; set; }
        public Brush ActionBackground { get; set; }
        public Brush ActionForeground { get; set; }

        public static AuditLogDisplayItem FromReader(SQLiteDataReader reader)
        {
            string tableName = reader["TableName"]?.ToString() ?? "";
            string action = reader["ActionType"]?.ToString() ?? "";
            var (bg, fg, actionText) = GetActionVisual(action);

            DateTime changedDate = reader["CreatedDate"] == DBNull.Value
                ? DateTime.Now
                : Convert.ToDateTime(reader["CreatedDate"]);

            return new AuditLogDisplayItem
            {
                AuditId = Convert.ToInt32(reader["LogID"]),
                TableNameDisplay = TranslateTableName(tableName),
                ActionDisplay = actionText,
                Summary = reader["NewValue"] == DBNull.Value ? "" : reader["NewValue"].ToString(),
                ChangedByName = reader["ChangedByName"] == DBNull.Value ? "النظام تلقائياً" : reader["ChangedByName"].ToString(),
                ChangedDateDisplay = changedDate.ToString("yyyy-MM-dd HH:mm"),
                ActionBackground = bg,
                ActionForeground = fg
            };
        }

        private static string TranslateTableName(string tableName)
        {
            switch (tableName)
            {
                case "Users": return "المستخدمون";
                case "Customers": return "العملاء";
                case "Suppliers": return "الموردون";
                case "CustomerTransactions": return "حركات العملاء";
                case "SupplierTransactions": return "حركات الموردين";
                case "SalesInvoices": return "فواتير البيع";
                case "PurchaseInvoices": return "فواتير المشتريات";
                case "Checks": return "الشيكات";
                default: return tableName;
            }
        }

        private static (Brush Background, Brush Foreground, string Text) GetActionVisual(string action)
        {
            switch (action)
            {
                case "Deleted":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)), new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)), "🗑️ حذف");
                case "Voided":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)), new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)), "🚫 إلغاء");
                case "RoleChanged":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)), new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)), "🔑 تغيير صلاحية");
                case "ActiveStatusChanged":
                    return (new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)), new SolidColorBrush(Color.FromRgb(0x02, 0x64, 0x94)), "👤 تفعيل/تعطيل");
                case "PasswordChanged":
                    return (new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)), new SolidColorBrush(Color.FromRgb(0x02, 0x64, 0x94)), "🔒 كلمة مرور");
                case "OpeningBalanceChanged":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)), new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)), "💰 رصيد أول المدة");
                case "CreditLimitChanged":
                    return (new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)), new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)), "💳 حد ائتمان");
                default:
                    return (new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), action);
            }
        }
    }
}
