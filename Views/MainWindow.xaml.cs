using Microsoft.Win32;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Models;
using RasidAccountingSystem.Services;
using RasidAccountingSystem.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace RasidAccountingSystem.Views
{
    public partial class MainWindow : Window
    {
        #region المتغيرات الخاصة

        private bool isSidebarCollapsed = false;
        private bool isAccountsMenuExpanded = false;
        private bool isCustodyMenuExpanded = false;
        private bool isHrMenuExpanded = false;
        private bool isInventoryMenuExpanded = false;
        private bool isSalesMenuExpanded = false;
        private bool isPurchaseMenuExpanded = false;
        private bool isSuppliersMenuExpanded = false;
        private bool isCustomersMenuExpanded = false;
        private bool isProductsMenuExpanded = false;
        private bool isReportsMenuExpanded = false;
        private bool isMaximized = false;
        private double normalWidth = 1200;
        private double normalHeight = 700;
        private double normalTop = 0;
        private double normalLeft = 0;
        private DatabaseService _dbService;
        private BackupService _backupService;
        private ImageSource _logoImageSource;
        private Timer _notificationTimer;
        private Timer _backupTimer;
        private int _unreadCount = 0;
        private bool _isUpdatingPopup = false;
        private ObservableCollection<NotificationModel> _notifications;
        private int _nextNotificationId = 1;
        private System.Timers.Timer _installmentNotificationTimer;

        private Image _arrowAccounts;
        private Image _arrowCustody;
        private Image _arrowHr;
        private Image _arrowInventory;
        private Image _arrowSales;
        private Image _arrowPurchase;
        private Image _arrowSuppliers;
        private Image _arrowCustomers;
        private Image _arrowProducts;
        private Image _arrowReports;

        private Button _btnInventoryAdjustment;

        #endregion

        #region المنشئ

        public MainWindow()
        {
            InitializeComponent();

            this.btnToggleSidebar.Click += (sender, e) =>
            {
                if (SidebarPanel.Width == 240)
                {
                    SidebarPanel.Width = 60;
                    LogoText.Visibility = Visibility.Collapsed;

                    LogoBorder.Width = 40;
                    LogoBorder.Height = 40;
                    LogoBorder.Margin = new Thickness(0, 15, 0, 10);
                    LogoBorder.Clip = new EllipseGeometry(new Point(20, 20), 20, 20);
                    LogoImage.Width = 40;
                    LogoImage.Height = 40;
                    LogoImage.Stretch = Stretch.UniformToFill;

                    AccountsSubMenu.Visibility = Visibility.Collapsed;
                    CustodySubMenu.Visibility = Visibility.Collapsed;
                    HrSubMenu.Visibility = Visibility.Collapsed;
                    InventorySubMenu.Visibility = Visibility.Collapsed;
                    SalesSubMenu.Visibility = Visibility.Collapsed;
                    PurchaseSubMenu.Visibility = Visibility.Collapsed;
                    SuppliersSubMenu.Visibility = Visibility.Collapsed;
                    CustomersSubMenu.Visibility = Visibility.Collapsed;
                    ProductsSubMenu.Visibility = Visibility.Collapsed;
                    ReportsSubMenu.Visibility = Visibility.Collapsed;

                    isAccountsMenuExpanded = false;
                    isCustodyMenuExpanded = false;
                    isHrMenuExpanded = false;
                    isInventoryMenuExpanded = false;
                    isSalesMenuExpanded = false;
                    isPurchaseMenuExpanded = false;
                    isSuppliersMenuExpanded = false;
                    isCustomersMenuExpanded = false;
                    isProductsMenuExpanded = false;
                    isReportsMenuExpanded = false;

                    this.RotateArrow(_arrowAccounts, false);
                    this.RotateArrow(_arrowCustody, false);
                    this.RotateArrow(_arrowHr, false);
                    this.RotateArrow(_arrowInventory, false);
                    this.RotateArrow(_arrowSales, false);
                    this.RotateArrow(_arrowPurchase, false);
                    this.RotateArrow(_arrowSuppliers, false);
                    this.RotateArrow(_arrowCustomers, false);
                    this.RotateArrow(_arrowProducts, false);
                    this.RotateArrow(_arrowReports, false);

                    btnLogout.Style = (Style)FindResource("LogoutButtonStyleCollapsed");

                    isSidebarCollapsed = true;
                }
                else
                {
                    SidebarPanel.Width = 240;
                    LogoText.Visibility = Visibility.Visible;

                    LogoBorder.Width = 60;
                    LogoBorder.Height = 60;
                    LogoBorder.Margin = new Thickness(0, 8, 0, 8);
                    LogoBorder.Clip = new EllipseGeometry(new Point(30, 30), 30, 30);
                    LogoImage.Width = 60;
                    LogoImage.Height = 60;
                    LogoImage.Stretch = Stretch.UniformToFill;

                    btnLogout.Style = (Style)FindResource("LogoutButtonStyle");

                    isSidebarCollapsed = false;
                }

                SidebarPanel.InvalidateVisual();
                LogoBorder.InvalidateVisual();
            };

            _dbService = new DatabaseService();
            _backupService = new BackupService(_dbService);
            _notifications = new ObservableCollection<NotificationModel>();
            CurrencyHelper.ConnectionString = _dbService.GetConnectionString();
            CurrencyHelper.LoadCurrencySettings();
            this.DisplayUserInfo();
            this.LoadCompanyLogo();
            this.AttachEventHandlers();
            this.MakeWindowDraggable();
            this.LoadDashboardPage();
            this.InitializeNotifications();
            this.StartNotificationTimer();
            this.StartInstallmentNotificationTimer();
            this.StartBackupTimer();
        }

        #endregion

        #region دوال عرض معلومات المستخدم

        private void DisplayUserInfo()
        {
            if (LoginView.CurrentUserId > 0)
            {
                if (lblUsername != null)
                {
                    lblUsername.Text = LoginView.CurrentUsername;
                }
                if (lblWelcome != null)
                {
                    lblWelcome.Text = "مرحباً";
                }
            }
        }

        #endregion

        #region دوال إدارة الشعار

        private void LoadDefaultLogo()
        {
            try
            {
                // ملاحظة: نفس الشعار الظاهر في شاشة تسجيل الدخول (assets/logo.png)، حتى تكون
                // الصورة موحّدة بين شاشة الدخول والقائمة الجانبية. يتم البحث في عدة مسارات
                // محتملة لضمان إيجاد الملف بغض النظر عن طريقة النشر (نفس أسلوب LoginView).
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(baseDirectory, "assets", "icons", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "assets", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "Assets", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "logo.png"),
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RasidAccountingSystem",
                        "logo.png")
                };

                string defaultLogoPath = possiblePaths.FirstOrDefault(File.Exists);

                if (!string.IsNullOrEmpty(defaultLogoPath))
                {
                    LogoImage.Source = this.LoadHighQualityImage(defaultLogoPath, 140);
                    _logoImageSource = LogoImage.Source;
                }
                else
                {
                    this.SetFallbackLogo();
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل الشعار الافتراضي: {exception.Message}");
                this.SetFallbackLogo();
            }
        }

        private void LoadCompanyLogo()
        {
            try
            {
                string databasePath = _dbService.DatabasePath;
                if (File.Exists(databasePath))
                {
                    using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                    {
                        connection.Open();
                        string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='CompanySettings'";
                        using (var checkCommand = new SQLiteCommand(checkTableQuery, connection))
                        {
                            object result = checkCommand.ExecuteScalar();
                            if (result != null)
                            {
                                string selectQuery = "SELECT LogoPath FROM CompanySettings LIMIT 1";
                                using (var selectCommand = new SQLiteCommand(selectQuery, connection))
                                {
                                    object logoPathObject = selectCommand.ExecuteScalar();
                                    if (logoPathObject != null)
                                    {
                                        string logoPath = logoPathObject.ToString();
                                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                                        {
                                            this.UpdateCompanyLogo(logoPath);
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                this.LoadDefaultLogo();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل شعار الشركة: {exception.Message}");
                this.LoadDefaultLogo();
            }
        }

        public void UpdateCompanyLogo(string logoPath)
        {
            try
            {
                if (_logoImageSource != null)
                {
                    _logoImageSource = null;
                }
                LogoImage.Source = null;
                if (string.IsNullOrEmpty(logoPath) || !File.Exists(logoPath))
                {
                    this.LoadDefaultLogo();
                    return;
                }
                LogoImage.Source = this.LoadHighQualityImage(logoPath, 140);
                _logoImageSource = LogoImage.Source;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحديث شعار الشركة: {exception.Message}");
                this.LoadDefaultLogo();
            }
        }

        private BitmapImage LoadHighQualityImage(string imagePath, int decodeSize)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = decodeSize;
            bitmap.DecodePixelHeight = decodeSize;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void SetFallbackLogo()
        {
            try
            {
                LogoImage.Visibility = Visibility.Collapsed;
                Border parentBorder = LogoImage.Parent as Border;
                if (parentBorder != null)
                {
                    parentBorder.Child = null;
                    parentBorder.Child = LogoImage;
                    TextBlock fallbackText = new TextBlock();
                    fallbackText.Text = "🏦";
                    fallbackText.FontSize = 32;
                    fallbackText.FontWeight = FontWeights.Bold;
                    fallbackText.Foreground = (Brush)FindResource("PrimaryColor");
                    fallbackText.HorizontalAlignment = HorizontalAlignment.Center;
                    fallbackText.VerticalAlignment = VerticalAlignment.Center;
                    parentBorder.Child = fallbackText;
                    LogoImage.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تعيين الشعار البديل: {exception.Message}");
            }
        }

        #endregion

        #region دوال إدارة التنبيهات

        private void InitializeNotifications()
        {
            btnNotifications.ApplyTemplate();
            NotificationsList.ItemsSource = _notifications;
            this.UpdateNotificationBadge();
            this.UpdateEmptyPanelVisibility();
            this.AddSampleNotifications();
        }

        private void AddSampleNotifications()
        {
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = "مرحباً بك في النظام المحاسبي",
                Message = "نرحب بانضمامك إلى النظام المحاسبي المتكامل",
                Time = DateTime.Now,
                Category = NotificationCategory.Info,
                IsRead = false
            });
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = "تحديث النظام",
                Message = "تم تحديث النظام إلى الإصدار الأحدث",
                Time = DateTime.Now.AddMinutes(-5),
                Category = NotificationCategory.Success,
                IsRead = false
            });
        }

        private void AddNotification(NotificationModel notification)
        {
            Dispatcher.Invoke(() =>
            {
                _notifications.Insert(0, notification);
                if (!notification.IsRead)
                {
                    _unreadCount++;
                    this.UpdateNotificationBadge();
                }
                this.UpdateEmptyPanelVisibility();
            });
        }

        public void ShowSuccessNotification(string title, string message)
        {
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = title,
                Message = message,
                Time = DateTime.Now,
                Category = NotificationCategory.Success,
                IsRead = false
            });
        }

        public void ShowWarningNotification(string title, string message)
        {
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = title,
                Message = message,
                Time = DateTime.Now,
                Category = NotificationCategory.Warning,
                IsRead = false
            });
        }

        public void ShowErrorNotification(string title, string message)
        {
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = title,
                Message = message,
                Time = DateTime.Now,
                Category = NotificationCategory.Error,
                IsRead = false
            });
        }

        public void ShowInfoNotification(string title, string message)
        {
            this.AddNotification(new NotificationModel
            {
                Id = _nextNotificationId++,
                Title = title,
                Message = message,
                Time = DateTime.Now,
                Category = NotificationCategory.Info,
                IsRead = false
            });
        }

        private void MarkAllNotificationsAsRead()
        {
            foreach (var notification in _notifications)
            {
                if (!notification.IsRead)
                {
                    notification.IsRead = true;
                }
            }
            _unreadCount = 0;
            this.UpdateNotificationBadge();
            var updatedList = new ObservableCollection<NotificationModel>(_notifications);
            _notifications.Clear();
            foreach (var item in updatedList)
            {
                _notifications.Add(item);
            }
        }

        private void ClearAllNotifications()
        {
            _notifications.Clear();
            _unreadCount = 0;
            _nextNotificationId = 1;
            this.UpdateNotificationBadge();
            this.UpdateEmptyPanelVisibility();
        }

        private void UpdateNotificationBadge()
        {
            Dispatcher.Invoke(() =>
            {
                if (_unreadCount > 0)
                {
                    NotificationBadge.Visibility = Visibility.Visible;
                    NotificationCount.Text = _unreadCount.ToString();
                    NotificationBadge.ToolTip = $"{_unreadCount} تنبيه غير مقروء";

                    if (_unreadCount >= 10 && _unreadCount < 100)
                    {
                        NotificationBadge.MinWidth = 20;
                        NotificationBadge.Height = 20;
                        NotificationBadge.CornerRadius = new CornerRadius(10);
                        NotificationCount.FontSize = 10;
                    }
                    else if (_unreadCount >= 100)
                    {
                        NotificationBadge.MinWidth = 24;
                        NotificationBadge.Height = 20;
                        NotificationBadge.CornerRadius = new CornerRadius(10);
                        NotificationCount.FontSize = 9;
                    }
                    else
                    {
                        NotificationBadge.MinWidth = 16;
                        NotificationBadge.Height = 16;
                        NotificationBadge.CornerRadius = new CornerRadius(8);
                        NotificationCount.FontSize = 9;
                    }
                }
                else
                {
                    NotificationBadge.Visibility = Visibility.Collapsed;
                    NotificationCount.Text = "0";
                }
            });
        }

        private void UpdateEmptyPanelVisibility()
        {
            Dispatcher.Invoke(() =>
            {
                bool hasNotifications = _notifications.Count > 0;
                NotificationsList.Visibility = hasNotifications ? Visibility.Visible : Visibility.Collapsed;
                EmptyNotificationsPanel.Visibility = hasNotifications ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private void StartNotificationTimer()
        {
            _notificationTimer = new Timer(this.CheckForPeriodicNotifications, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// بدء مؤقت الجدولة التلقائية للنسخ الاحتياطي. يفحص كل 30 دقيقة هل حان وقت نسخة احتياطية
        /// جديدة حسب الإعدادات المختارة (يومي/أسبوعي/شهري)، وينفذها تلقائياً بصمت في الخلفية
        /// بدون أي تدخل من المستخدم، حتى لو ظل البرنامج مفتوحاً لأيام متواصلة بدون إغلاق.
        /// </summary>
        private void StartBackupTimer()
        {
            _backupTimer = new Timer(this.CheckForScheduledBackup, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        }

        private async void CheckForScheduledBackup(object state)
        {
            try
            {
                var result = await _backupService.RunScheduledBackupIfDueAsync();

                if (result.Success)
                {
                    Logger.LogInfo($"تم تنفيذ نسخة احتياطية مجدولة تلقائياً بنجاح: {result.BackupFilePath}", "MainWindow");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ فحص/تنفيذ النسخة الاحتياطية المجدولة", ex, "MainWindow");
            }
        }

        private void CheckForPeriodicNotifications(object state)
        {
            Dispatcher.Invoke(() =>
            {
                DateTime now = DateTime.Now;
                if (now.Hour == 9 && now.Minute < 5)
                {
                    this.AddNotification(new NotificationModel
                    {
                        Id = _nextNotificationId++,
                        Title = "بداية يوم جديد",
                        Message = "نتمنى لك يوماً موفقاً في العمل",
                        Time = DateTime.Now,
                        Category = NotificationCategory.Info,
                        IsRead = false
                    });
                }
                if (now.Day == 25)
                {
                    this.AddNotification(new NotificationModel
                    {
                        Id = _nextNotificationId++,
                        Title = "نهاية الشهر",
                        Message = "يقترب موعد إغلاق الشهر المالي الحالي",
                        Time = DateTime.Now,
                        Category = NotificationCategory.Warning,
                        IsRead = false
                    });
                }
            });
        }

        #endregion

        #region دوال تنبيهات الأقساط

        private async Task CheckInstallmentNotificationsAsync()
        {
            try
            {
                var installmentService = new InstallmentService(_dbService);

                var upcomingInstallments = await installmentService.GetUpcomingInstallmentsAsync(7);
                var overdueInstallments = await installmentService.GetOverdueInstallmentsAsync();

                var oldInstallmentNotifications = _notifications
                    .Where(n => n.Title.Contains("قسط") ||
                               n.Title.Contains("المستحقات") ||
                               n.Title.Contains("لا توجد أقساط"))
                    .ToList();

                foreach (var oldNotification in oldInstallmentNotifications)
                {
                    _notifications.Remove(oldNotification);
                }

                _unreadCount = _notifications.Count(n => !n.IsRead);
                this.UpdateNotificationBadge();

                foreach (var installment in overdueInstallments)
                {
                    string title = $"🔴 قسط متأخر - {installment.CustomerName}";
                    string message = $"القسط رقم {installment.InstallmentNumber} من الفاتورة {installment.InvoiceNumber} متأخر منذ {installment.DaysText}";
                    this.ShowErrorNotification(title, message);
                }

                var urgentInstallments = upcomingInstallments.Where(i => i.DaysRemaining <= 3 && i.DaysRemaining >= 0).ToList();
                foreach (var installment in urgentInstallments)
                {
                    string title = $"⚠️ قسط مستحق خلال 3 أيام - {installment.CustomerName}";
                    string message = $"القسط رقم {installment.InstallmentNumber} من الفاتورة {installment.InvoiceNumber} مستحق خلال {installment.DaysText}";
                    this.ShowWarningNotification(title, message);
                }

                var normalInstallments = upcomingInstallments.Where(i => i.DaysRemaining > 3 && i.DaysRemaining <= 7).ToList();
                foreach (var installment in normalInstallments)
                {
                    string title = $"📅 قسط مستحق قريباً - {installment.CustomerName}";
                    string message = $"القسط رقم {installment.InstallmentNumber} من الفاتورة {installment.InvoiceNumber} مستحق خلال {installment.DaysText}";
                    this.ShowInfoNotification(title, message);
                }

                int totalPending = upcomingInstallments.Count + overdueInstallments.Count;
                if (totalPending > 0)
                {
                    string statusMessage = $"📊 لديك {totalPending} قسط مستحق خلال الأيام القادمة";
                    if (overdueInstallments.Count > 0)
                    {
                        statusMessage += $" (منها {overdueInstallments.Count} متأخر)";
                    }
                    this.ShowInfoNotification("📊 ملخص الأقساط المستحقة", statusMessage);
                }
                else
                {
                    this.AddNotification(new NotificationModel
                    {
                        Id = _nextNotificationId++,
                        Title = "✅ لا توجد أقساط مستحقة",
                        Message = "جميع الأقساط مسددة أو لا توجد أقساط قريبة من الاستحقاق",
                        Time = DateTime.Now,
                        Category = NotificationCategory.Success,
                        IsRead = false
                    });
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم فحص تنبيهات الأقساط: {upcomingInstallments.Count} قسط قريب, {overdueInstallments.Count} قسط متأخر");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckInstallmentNotificationsAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void StartInstallmentNotificationTimer()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(5000);
                    await CheckInstallmentNotificationsAsync();
                }));

                _installmentNotificationTimer = new System.Timers.Timer(6 * 60 * 60 * 1000);
                _installmentNotificationTimer.Elapsed += async (sender, e) =>
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            await CheckInstallmentNotificationsAsync();
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"InstallmentNotificationTimer Elapsed Error: {ex.Message}");
                    }
                };
                _installmentNotificationTimer.AutoReset = true;
                _installmentNotificationTimer.Start();

                System.Diagnostics.Debug.WriteLine("✅ تم بدء مؤقت تنبيهات الأقساط (كل 6 ساعات)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartInstallmentNotificationTimer Error: {ex.Message}");
            }
        }

        public async Task RefreshInstallmentNotificationsAsync()
        {
            try
            {
                await CheckInstallmentNotificationsAsync();
                System.Diagnostics.Debug.WriteLine("✅ تم تحديث تنبيهات الأقساط يدوياً");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshInstallmentNotificationsAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال مساعدة للأسهم

        private Image GetArrowImageFromButton(Button button)
        {
            if (button.Template.FindName("arrowIcon", button) is Image arrowImage)
            {
                return arrowImage;
            }
            return null;
        }

        private void RotateArrow(Image arrow, bool isExpanded)
        {
            if (arrow == null) return;
            var transform = new RotateTransform();
            arrow.RenderTransform = transform;
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            if (isExpanded)
            {
                transform.Angle = 180;
            }
            else
            {
                transform.Angle = 0;
            }
        }

        #endregion

        #region دوال النسخ الاحتياطي عند الإغلاق

        private BackupSettings LoadBackupSettingsFromDatabase()
        {
            try
            {
                var settings = new BackupSettings();
                string databasePath = _dbService.DatabasePath;

                using (var connection = new SQLiteConnection($"Data Source={databasePath};Version=3;"))
                {
                    connection.Open();

                    string checkTableQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name='BackupSettings'";

                    using (var checkCmd = new SQLiteCommand(checkTableQuery, connection))
                    {
                        object result = checkCmd.ExecuteScalar();

                        if (result == null)
                        {
                            return settings;
                        }
                    }

                    string sql = "SELECT SettingKey, SettingValue FROM BackupSettings";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader.GetString(0);
                            string value = reader.GetString(1);

                            switch (key)
                            {
                                case "AutoBackupEnabled":
                                    settings.AutoBackupEnabled = value == "True";
                                    break;

                                case "BackupPath":
                                    settings.BackupPath = value;
                                    break;

                                case "BackupFrequency":
                                    settings.BackupFrequency = value;
                                    break;

                                case "BackupRetention":
                                    if (int.TryParse(value, out int retention))
                                    {
                                        settings.BackupRetention = retention;
                                    }
                                    break;
                            }
                        }
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadBackupSettingsFromDatabase Error: {ex.Message}");
                return new BackupSettings();
            }
        }

        // ملاحظة: تم استبدال الدالة القديمة PerformBackup (كانت تنسخ ملف القاعدة مباشرة بـ File.Copy)
        // بالكامل بخدمة RasidAccountingSystem.Services.BackupService، التي تستخدم أمر SQLite الآمن
        // (VACUUM INTO) لضمان نسخة احتياطية متسقة وكاملة حتى مع تفعيل وضع WAL. راجع BtnClose_Click
        // وCheckForScheduledBackup لمعرفة كيفية استخدامها.

        // ملاحظة: تم نقل منطق تنظيف النسخ الاحتياطية القديمة إلى BackupService.CleanupOldBackups
        // ضمن نفس عملية إنشاء النسخة الاحتياطية الجديدة (VACUUM INTO)، فلا داعي لدالة منفصلة هنا.

        private async Task<bool> ShowBackupConfirmationDialog()
        {
            var result = MessageBox.Show(
                "هل تريد إنشاء نسخة احتياطية قبل إغلاق البرنامج؟\n\n" +
                "توصي الشركة بعمل نسخة احتياطية بشكل دوري لحماية بياناتك",
                "نسخ احتياطي قبل الإغلاق",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Title = "حفظ النسخة الاحتياطية";
                saveFileDialog.Filter = "SQLite files (*.sqlite)|*.sqlite|All files (*.*)|*.*";
                saveFileDialog.FileName = $"RasidBackup_{DateTime.Now:yyyyMMdd_HHmmss}.sqlite";

                if (saveFileDialog.ShowDialog() == true)
                {
                    string sourcePath = _dbService.DatabasePath;

                    if (File.Exists(sourcePath))
                    {
                        try
                        {
                            File.Copy(sourcePath, saveFileDialog.FileName, true);
                            MessageBox.Show("تم إنشاء النسخة الاحتياطية بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"خطأ في إنشاء النسخة الاحتياطية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                            return false;
                        }
                    }
                    else
                    {
                        MessageBox.Show("قاعدة البيانات غير موجودة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else if (result == MessageBoxResult.No)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region دوال تحميل الصفحات

        private void LoadDashboardPage()
        {
            try
            {
                var dashboardView = new DashboardView();
                MainFrame.Content = dashboardView;
                lblPageTitle.Text = "لوحة التحكم";
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل لوحة التحكم: {exception.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "مرحباً بك في نظام راصد المحاسبي",
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("PrimaryColor"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadSettingsPage()
        {
            try
            {
                var settingsView = new SettingsView(_dbService, this);
                MainFrame.Content = settingsView;
                lblPageTitle.Text = "الإعدادات";
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل الإعدادات: {exception.Message}");
                MessageBox.Show($"خطأ في تحميل الإعدادات: {exception.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAuditLogPage()
        {
            try
            {
                var auditLogView = new AuditLogView();
                MainFrame.Content = auditLogView;
                lblPageTitle.Text = "سجل التعديلات";
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل صفحة سجل التعديلات", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل سجل التعديلات: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل سجل التعديلات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPermissionsPage()
        {
            try
            {
                var permissionsView = new PermissionsManagementView();
                MainFrame.Content = permissionsView;
                lblPageTitle.Text = "إدارة الصلاحيات";
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل صفحة إدارة الصلاحيات", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة إدارة الصلاحيات: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل صفحة إدارة الصلاحيات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadEmployeesPage()
        {
            try
            {
                var employeesView = new EmployeesView();
                MainFrame.Content = employeesView;
                lblPageTitle.Text = "إدارة الموظفين";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة الموظفين: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل صفحة الموظفين: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProductsPage()
        {
            try
            {
                var productsView = new ProductsView();
                MainFrame.Content = productsView;
                lblPageTitle.Text = "المنتجات";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة المنتجات: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة المنتجات",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadAddCustomerPage()
        {
            try
            {
                var customersView = new CustomersView();
                MainFrame.Content = customersView;
                lblPageTitle.Text = "إضافة عميل / قائمة العملاء";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة إضافة عميل: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة إضافة عميل: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة إضافة عميل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReceiptVouchersPage()
        {
            try
            {
                var receiptVouchersView = new ReceiptVouchersView();
                MainFrame.Content = receiptVouchersView;
                lblPageTitle.Text = "سندات القبض";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة سندات القبض: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة سندات القبض: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
            }
        }

        private void LoadPaymentVouchersPage()
        {
            try
            {
                var paymentVouchersView = new PaymentVouchersView();
                MainFrame.Content = paymentVouchersView;
                lblPageTitle.Text = "سندات الصرف / المدفوعات";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة سندات الصرف: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة سندات الصرف: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
            }
        }

        private void LoadCustodyPage()
        {
            try
            {
                var custodyView = new CustodyView();
                MainFrame.Content = custodyView;
                lblPageTitle.Text = "إدارة العهد";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة إدارة العهد: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة إدارة العهد: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة إدارة العهد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAddSupplierPage()
        {
            try
            {
                var suppliersView = new SuppliersView();
                MainFrame.Content = suppliersView;
                lblPageTitle.Text = "إضافة مورد / قائمة الموردين";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة إضافة مورد: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة إضافة مورد: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة إضافة مورد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSupplierStatementPage()
        {
            try
            {
                var supplierStatementView = new SupplierStatementView();
                MainFrame.Content = supplierStatementView;
                lblPageTitle.Text = "كشف حساب المورد";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة كشف حساب المورد: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة كشف حساب المورد: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة كشف حساب المورد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSuppliersBalancePage()
        {
            try
            {
                var suppliersBalanceView = new SuppliersBalanceView();
                MainFrame.Content = suppliersBalanceView;
                lblPageTitle.Text = "ميزان الموردين";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة ميزان الموردين: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة ميزان الموردين: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة ميزان الموردين: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCustomerStatementPage()
        {
            try
            {
                var customerStatementView = new CustomerStatementView();
                MainFrame.Content = customerStatementView;
                lblPageTitle.Text = "كشف حساب العميل";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة كشف حساب العميل: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة كشف حساب العميل: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة كشف حساب العميل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCustomersBalancePage()
        {
            try
            {
                var customersBalanceView = new CustomersBalanceView();
                MainFrame.Content = customersBalanceView;
                lblPageTitle.Text = "ميزان العملاء";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة ميزان العملاء: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة ميزان العملاء: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة ميزان العملاء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReceivablesPage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔴 بدء تحميل صفحة المستحقات...");

                var receivablesView = new ReceivablesView();
                System.Diagnostics.Debug.WriteLine("🔴 تم إنشاء ReceivablesView بنجاح");

                MainFrame.Content = receivablesView;
                System.Diagnostics.Debug.WriteLine("🔴 تم تعيين المحتوى في MainFrame");

                lblPageTitle.Text = "📋 المستحقات";
                System.Diagnostics.Debug.WriteLine("🔴 تم تحديث عنوان الصفحة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔴 خطأ في تحميل صفحة المستحقات: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"🔴 StackTrace: {ex.StackTrace}");

                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة المستحقات:\n{ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة المستحقات:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStoreManagementPage()
        {
            try
            {
                var storesView = new StoresView();
                MainFrame.Content = storesView;
                lblPageTitle.Text = "إدارة المخازن";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة إدارة المخازن: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة إدارة المخازن",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MessageBox.Show($"خطأ في تحميل صفحة إدارة المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStoreBalancePage()
        {
            try
            {
                var storeBalanceView = new StoresBalanceView();
                MainFrame.Content = storeBalanceView;
                lblPageTitle.Text = "أرصدة المخازن";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة أرصدة المخازن: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة أرصدة المخازن",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MessageBox.Show($"خطأ في تحميل صفحة أرصدة المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadItemCardPage()
        {
            try
            {
                var itemCardView = new ItemCardView();
                MainFrame.Content = itemCardView;
                lblPageTitle.Text = "كارتة صنف";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة كارتة صنف: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة كارتة صنف",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MessageBox.Show($"خطأ في تحميل صفحة كارتة صنف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadInventoryAdjustmentPage()
        {
            try
            {
                var inventoryAdjustmentView = new InventoryAdjustmentsView();
                MainFrame.Content = inventoryAdjustmentView;
                lblPageTitle.Text = "التسويات الجردية";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة التسويات الجردية: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة التسويات الجردية",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MessageBox.Show($"خطأ في تحميل صفحة التسويات الجردية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPurchaseInvoicePage()
        {
            try
            {
                var purchaseInvoiceView = new PurchaseInvoiceView();
                MainFrame.Content = purchaseInvoiceView;
                lblPageTitle.Text = "فاتورة مشتريات";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة فاتورة المشتريات: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة فاتورة المشتريات",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadSalesInvoicePage()
        {
            try
            {
                var salesInvoiceView = new SalesInvoiceView();
                MainFrame.Content = salesInvoiceView;
                lblPageTitle.Text = "فاتورة بيع";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة فاتورة البيع: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة فاتورة البيع",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MessageBox.Show($"خطأ في تحميل صفحة فاتورة البيع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSalesReturnInvoicePage()
        {
            try
            {
                var salesReturnView = new SalesReturnInvoiceView();
                MainFrame.Content = salesReturnView;
                lblPageTitle.Text = "مرتجع بيع";
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل صفحة مرتجع البيع", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة مرتجع البيع: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة مرتجع البيع",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadPurchaseReturnInvoicePage()
        {
            try
            {
                var purchaseReturnView = new PurchaseReturnInvoiceView();
                MainFrame.Content = purchaseReturnView;
                lblPageTitle.Text = "مرتجع مشتريات";
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل صفحة مرتجع المشتريات", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة مرتجع المشتريات: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة مرتجع المشتريات",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadTreasuryPage()
        {
            try
            {
                var treasuryView = new TreasuryView();
                MainFrame.Content = treasuryView;
                lblPageTitle.Text = "الخزينة";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة الخزينة: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة الخزينة",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadBankPage()
        {
            try
            {
                var bankAccountsView = new BankAccountsView();
                MainFrame.Content = bankAccountsView;
                lblPageTitle.Text = "الحسابات البنكية";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة البنك: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة الحسابات البنكية",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadChecksPage()
        {
            try
            {
                var checksView = new ChecksView();
                MainFrame.Content = checksView;
                lblPageTitle.Text = "الشيكات";
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل صفحة الشيكات", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة الشيكات: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = "حدث خطأ في تحميل صفحة الشيكات",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
        }

        private void LoadProfitLossPage()
        {
            try
            {
                var profitLossView = new ProfitLossView();
                MainFrame.Content = profitLossView;
                lblPageTitle.Text = "قائمة الأرباح والخسائر";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل صفحة الأرباح والخسائر: {ex.Message}");
                MainFrame.Content = new TextBlock
                {
                    Text = $"حدث خطأ في تحميل صفحة الأرباح والخسائر: {ex.Message}",
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20)
                };
                MessageBox.Show($"خطأ في تحميل صفحة الأرباح والخسائر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region ربط الأحداث

        private void AttachEventHandlers()
        {
            btnSettingsHeader.Click += BtnSettingsHeader_Click;
            btnMinimize.Click += BtnMinimize_Click;
            btnMaximize.Click += BtnMaximize_Click;
            btnClose.Click += BtnClose_Click;
            btnLogout.Click += BtnLogout_Click;
            btnDashboard.Click += BtnDashboard_Click;
            btnSettings.Click += BtnSettings_Click;
            btnAuditLog.Click += (s, e) => LoadAuditLogPage();
            btnPermissions.Click += (s, e) => LoadPermissionsPage();
            btnProfitLoss.Click += BtnProfitLoss_Click;

            btnAccounts.Click += BtnAccounts_Click;
            btnCustodyGroup.Click += BtnCustodyGroup_Click;
            btnHrGroup.Click += BtnHrGroup_Click;
            btnInventory.Click += BtnInventory_Click;
            btnSalesGroup.Click += BtnSalesGroup_Click;
            btnPurchaseGroup.Click += BtnPurchaseGroup_Click;
            btnSuppliersGroup.Click += BtnSuppliersGroup_Click;
            btnCustomersGroup.Click += BtnCustomersGroup_Click;
            btnProductsGroup.Click += BtnProductsGroup_Click;
            btnReportsGroup.Click += BtnReportsGroup_Click;

            btnTreasury.Click += BtnTreasury_Click;
            btnReceiptVouchers.Click += BtnReceiptVouchers_Click;
            btnPaymentVoucher.Click += BtnPaymentVoucher_Click;
            btnBank.Click += BtnBank_Click;
            btnChecks.Click += BtnChecks_Click;
            btnCustody.Click += BtnCustody_Click;
            btnEmployees.Click += BtnEmployees_Click;

            btnWarehouse.Click += (s, e) => LoadStoreManagementPage();
            btnStoreBalance.Click += (s, e) => LoadStoreBalancePage();
            btnItemCard.Click += (s, e) => LoadItemCardPage();
            btnInventoryAdjustment.Click += (s, e) => LoadInventoryAdjustmentPage();

            btnSalesInvoice.Click += (s, e) => LoadSalesInvoicePage();
            btnSalesReturnInvoice.Click += (s, e) => LoadSalesReturnInvoicePage();

            btnPurchaseInvoice.Click += (s, e) => LoadPurchaseInvoicePage();
            btnPurchaseReturnInvoice.Click += (s, e) => LoadPurchaseReturnInvoicePage();

            btnSuppliers.Click += (s, e) => LoadAddSupplierPage();
            btnSuppliersBalance.Click += (s, e) => LoadSuppliersBalancePage();
            btnSupplierStatement.Click += (s, e) => LoadSupplierStatementPage();

            btnCustomers.Click += (s, e) => LoadAddCustomerPage();
            btnCustomersBalance.Click += (s, e) => LoadCustomersBalancePage();
            btnCustomerStatement.Click += (s, e) => LoadCustomerStatementPage();

            btnReceivables.Click += (s, e) => LoadReceivablesPage();

            btnProducts.Click += (s, e) => LoadProductsPage();

            this.Loaded += (s, e) =>
            {
                _arrowAccounts = GetArrowImageFromButton(btnAccounts);
                _arrowCustody = GetArrowImageFromButton(btnCustodyGroup);
                _arrowHr = GetArrowImageFromButton(btnHrGroup);
                _arrowInventory = GetArrowImageFromButton(btnInventory);
                _arrowSales = GetArrowImageFromButton(btnSalesGroup);
                _arrowPurchase = GetArrowImageFromButton(btnPurchaseGroup);
                _arrowSuppliers = GetArrowImageFromButton(btnSuppliersGroup);
                _arrowCustomers = GetArrowImageFromButton(btnCustomersGroup);
                _arrowProducts = GetArrowImageFromButton(btnProductsGroup);
                _arrowReports = GetArrowImageFromButton(btnReportsGroup);
                _btnInventoryAdjustment = btnInventoryAdjustment;
            };
        }

        private void MakeWindowDraggable()
        {
            MouseLeftButtonDown += Window_MouseLeftButtonDown;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        #endregion

        #region أحداث أزرار القائمة الرئيسية

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            LoadDashboardPage();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            LoadSettingsPage();
        }

        private void BtnSettingsHeader_Click(object sender, RoutedEventArgs e)
        {
            LoadSettingsPage();
        }

        private void BtnTreasury_Click(object sender, RoutedEventArgs e)
        {
            LoadTreasuryPage();
        }

        private void BtnReceiptVouchers_Click(object sender, RoutedEventArgs e)
        {
            LoadReceiptVouchersPage();
        }

        private void BtnPaymentVoucher_Click(object sender, RoutedEventArgs e)
        {
            LoadPaymentVouchersPage();
        }

        private void BtnBank_Click(object sender, RoutedEventArgs e)
        {
            LoadBankPage();
        }

        private void BtnChecks_Click(object sender, RoutedEventArgs e)
        {
            LoadChecksPage();
        }

        private void BtnCustody_Click(object sender, RoutedEventArgs e)
        {
            LoadCustodyPage();
        }

        private void BtnEmployees_Click(object sender, RoutedEventArgs e)
        {
            LoadEmployeesPage();
        }

        private void BtnProfitLoss_Click(object sender, RoutedEventArgs e)
        {
            LoadProfitLossPage();
        }

        #endregion

        #region أحداث أزرار المجموعات

        private void BtnAccounts_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isAccountsMenuExpanded = !isAccountsMenuExpanded;
            AccountsSubMenu.Visibility = isAccountsMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowAccounts, isAccountsMenuExpanded);
            if (isAccountsMenuExpanded) CloseOtherMenus("Accounts");
        }

        private void BtnCustodyGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isCustodyMenuExpanded = !isCustodyMenuExpanded;
            CustodySubMenu.Visibility = isCustodyMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowCustody, isCustodyMenuExpanded);
            if (isCustodyMenuExpanded) CloseOtherMenus("Custody");
        }

        private void BtnHrGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isHrMenuExpanded = !isHrMenuExpanded;
            HrSubMenu.Visibility = isHrMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowHr, isHrMenuExpanded);
            if (isHrMenuExpanded) CloseOtherMenus("Hr");
        }

        private void BtnInventory_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isInventoryMenuExpanded = !isInventoryMenuExpanded;
            InventorySubMenu.Visibility = isInventoryMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowInventory, isInventoryMenuExpanded);
            if (isInventoryMenuExpanded) CloseOtherMenus("Inventory");
        }

        private void BtnSalesGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isSalesMenuExpanded = !isSalesMenuExpanded;
            SalesSubMenu.Visibility = isSalesMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowSales, isSalesMenuExpanded);
            if (isSalesMenuExpanded) CloseOtherMenus("Sales");
        }

        private void BtnPurchaseGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isPurchaseMenuExpanded = !isPurchaseMenuExpanded;
            PurchaseSubMenu.Visibility = isPurchaseMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowPurchase, isPurchaseMenuExpanded);
            if (isPurchaseMenuExpanded) CloseOtherMenus("Purchase");
        }

        private void BtnSuppliersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isSuppliersMenuExpanded = !isSuppliersMenuExpanded;
            SuppliersSubMenu.Visibility = isSuppliersMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowSuppliers, isSuppliersMenuExpanded);
            if (isSuppliersMenuExpanded) CloseOtherMenus("Suppliers");
        }

        private void BtnCustomersGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isCustomersMenuExpanded = !isCustomersMenuExpanded;
            CustomersSubMenu.Visibility = isCustomersMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowCustomers, isCustomersMenuExpanded);
            if (isCustomersMenuExpanded) CloseOtherMenus("Customers");
        }

        private void BtnProductsGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isProductsMenuExpanded = !isProductsMenuExpanded;
            ProductsSubMenu.Visibility = isProductsMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowProducts, isProductsMenuExpanded);
            if (isProductsMenuExpanded) CloseOtherMenus("Products");
        }

        private void BtnReportsGroup_Click(object sender, RoutedEventArgs e)
        {
            if (isSidebarCollapsed) return;
            isReportsMenuExpanded = !isReportsMenuExpanded;
            ReportsSubMenu.Visibility = isReportsMenuExpanded ? Visibility.Visible : Visibility.Collapsed;
            RotateArrow(_arrowReports, isReportsMenuExpanded);
            if (isReportsMenuExpanded) CloseOtherMenus("Reports");
        }

        private void CloseOtherMenus(string currentMenu)
        {
            if (currentMenu != "Accounts" && isAccountsMenuExpanded)
            {
                isAccountsMenuExpanded = false;
                AccountsSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowAccounts, false);
            }
            if (currentMenu != "Custody" && isCustodyMenuExpanded)
            {
                isCustodyMenuExpanded = false;
                CustodySubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowCustody, false);
            }
            if (currentMenu != "Hr" && isHrMenuExpanded)
            {
                isHrMenuExpanded = false;
                HrSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowHr, false);
            }
            if (currentMenu != "Inventory" && isInventoryMenuExpanded)
            {
                isInventoryMenuExpanded = false;
                InventorySubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowInventory, false);
            }
            if (currentMenu != "Sales" && isSalesMenuExpanded)
            {
                isSalesMenuExpanded = false;
                SalesSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowSales, false);
            }
            if (currentMenu != "Purchase" && isPurchaseMenuExpanded)
            {
                isPurchaseMenuExpanded = false;
                PurchaseSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowPurchase, false);
            }
            if (currentMenu != "Suppliers" && isSuppliersMenuExpanded)
            {
                isSuppliersMenuExpanded = false;
                SuppliersSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowSuppliers, false);
            }
            if (currentMenu != "Customers" && isCustomersMenuExpanded)
            {
                isCustomersMenuExpanded = false;
                CustomersSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowCustomers, false);
            }
            if (currentMenu != "Products" && isProductsMenuExpanded)
            {
                isProductsMenuExpanded = false;
                ProductsSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowProducts, false);
            }
            if (currentMenu != "Reports" && isReportsMenuExpanded)
            {
                isReportsMenuExpanded = false;
                ReportsSubMenu.Visibility = Visibility.Collapsed;
                RotateArrow(_arrowReports, false);
            }
        }

        #endregion

        #region أحداث التنبيهات

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingPopup) return;
            _isUpdatingPopup = true;
            try
            {
                btnNotifications.ApplyTemplate();
                if (NotificationsPopup.IsOpen)
                {
                    NotificationsPopup.IsOpen = false;
                    btnNotifications.IsChecked = false;
                }
                else
                {
                    NotificationsPopup.IsOpen = true;
                    btnNotifications.IsChecked = true;
                    UpdateEmptyPanelVisibility();
                }
            }
            finally
            {
                Task.Delay(200).ContinueWith(t => { _isUpdatingPopup = false; }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void CloseNotification_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int notificationId)
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    if (!notification.IsRead && _unreadCount > 0)
                    {
                        _unreadCount--;
                    }
                    _notifications.Remove(notification);
                    UpdateNotificationBadge();
                    UpdateEmptyPanelVisibility();
                }
            }
        }

        private void BtnMarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            MarkAllNotificationsAsRead();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("هل أنت متأكد من رغبتك في مسح جميع التنبيهات؟", "تأكيد المسح", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ClearAllNotifications();
            }
        }

        private void BtnViewAllNotifications_Click(object sender, RoutedEventArgs e)
        {
            NotificationsPopup.IsOpen = false;
            btnNotifications.IsChecked = false;

            Window notificationsDialog = new Window();
            notificationsDialog.Title = "جميع التنبيهات";
            notificationsDialog.Width = 500;
            notificationsDialog.Height = 600;
            notificationsDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            notificationsDialog.Owner = this;
            notificationsDialog.WindowStyle = WindowStyle.SingleBorderWindow;
            notificationsDialog.ResizeMode = ResizeMode.CanResize;

            ItemsControl itemsControl = new ItemsControl();
            itemsControl.ItemsSource = _notifications;
            itemsControl.ItemTemplate = (DataTemplate)FindResource("NotificationItemTemplate");
            itemsControl.Margin = new Thickness(5);

            ScrollViewer scrollViewer = new ScrollViewer();
            scrollViewer.Content = itemsControl;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            notificationsDialog.Content = scrollViewer;
            notificationsDialog.ShowDialog();
        }

        #endregion

        #region أزرار التحكم في النافذة

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (isMaximized)
            {
                WindowState = WindowState.Normal;
                Width = normalWidth;
                Height = normalHeight;
                Top = normalTop;
                Left = normalLeft;
                isMaximized = false;
                btnMaximize.Content = "□";
            }
            else
            {
                normalWidth = Width;
                normalHeight = Height;
                normalTop = Top;
                normalLeft = Left;

                Rect workingArea = SystemParameters.WorkArea;
                Top = workingArea.Top;
                Left = workingArea.Left;
                Width = workingArea.Width;
                Height = workingArea.Height;
                WindowState = WindowState.Normal;
                isMaximized = true;
                btnMaximize.Content = "❐";
            }
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BackupSettings backupSettings = LoadBackupSettingsFromDatabase();
                bool shouldClose = false;

                if (backupSettings.AutoBackupEnabled && !string.IsNullOrEmpty(backupSettings.BackupPath))
                {
                    await _backupService.CreateBackupNowAsync(backupSettings.BackupPath, backupSettings.BackupRetention);
                    shouldClose = true;
                }
                else
                {
                    shouldClose = await ShowBackupConfirmationDialog();
                }

                if (shouldClose)
                {
                    if (_notificationTimer != null)
                    {
                        _notificationTimer.Dispose();
                    }
                    if (_backupTimer != null)
                    {
                        _backupTimer.Dispose();
                    }
                    if (_installmentNotificationTimer != null)
                    {
                        _installmentNotificationTimer.Stop();
                        _installmentNotificationTimer.Dispose();
                    }
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ إغلاق البرنامج / النسخة الاحتياطية عند الإغلاق", ex, "MainWindow");
                System.Diagnostics.Debug.WriteLine($"BtnClose_Click Error: {ex.Message}");
                Application.Current.Shutdown();
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            Logout();
        }

        private void Logout()
        {
            if (MessageBox.Show("هل أنت متأكد من رغبتك في تسجيل الخروج؟", "تأكيد تسجيل الخروج", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (_notificationTimer != null)
                {
                    _notificationTimer.Dispose();
                }
                if (_backupTimer != null)
                {
                    _backupTimer.Dispose();
                }
                if (_installmentNotificationTimer != null)
                {
                    _installmentNotificationTimer.Stop();
                    _installmentNotificationTimer.Dispose();
                }

                LoginView.CurrentUserId = 0;
                LoginView.CurrentUsername = null;
                LoginView.CurrentUserRole = null;
                LoginView.CurrentUserFullName = null;
                LoginView.CurrentUserEmail = null;
                LoginView.CurrentUserPhone = null;

                this.Close();

                LoginView loginWindow = new LoginView();
                loginWindow.Show();
                Application.Current.MainWindow = loginWindow;
            }
        }

        #endregion
    }

    #region نموذج بيانات التنبيه

    public enum NotificationCategory
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationModel : INotifyPropertyChanged
    {
        private int _id;
        private string _title;
        private string _message;
        private DateTime _time;
        private NotificationCategory _category;
        private bool _isRead;

        public int Id
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public DateTime Time
        {
            get { return _time; }
            set
            {
                _time = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimeDisplay));
            }
        }

        public string TimeDisplay
        {
            get
            {
                TimeSpan diff = DateTime.Now - Time;
                if (diff.TotalMinutes < 1) return "الآن";
                if (diff.TotalMinutes < 60) return $"منذ {Math.Floor(diff.TotalMinutes)} دقيقة";
                if (diff.TotalHours < 24) return $"منذ {Math.Floor(diff.TotalHours)} ساعة";
                if (diff.TotalDays < 7) return $"منذ {Math.Floor(diff.TotalDays)} يوم";
                return Time.ToString("dd/MM/yyyy");
            }
        }

        public NotificationCategory Category
        {
            get { return _category; }
            set
            {
                _category = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconBackground));
                OnPropertyChanged(nameof(CategoryName));
                OnPropertyChanged(nameof(CategoryColor));
            }
        }

        public string Icon
        {
            get
            {
                switch (Category)
                {
                    case NotificationCategory.Success: return "✓";
                    case NotificationCategory.Warning: return "⚠";
                    case NotificationCategory.Error: return "✕";
                    default: return "ℹ";
                }
            }
        }

        public Brush IconBackground
        {
            get
            {
                switch (Category)
                {
                    case NotificationCategory.Success: return new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    case NotificationCategory.Warning: return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    case NotificationCategory.Error: return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    default: return new SolidColorBrush(Color.FromRgb(59, 130, 246));
                }
            }
        }

        public string CategoryName
        {
            get
            {
                switch (Category)
                {
                    case NotificationCategory.Success: return "تم بنجاح";
                    case NotificationCategory.Warning: return "تنبيه";
                    case NotificationCategory.Error: return "خطأ";
                    default: return "معلومة";
                }
            }
        }

        public Brush CategoryColor
        {
            get
            {
                switch (Category)
                {
                    case NotificationCategory.Success: return new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    case NotificationCategory.Warning: return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    case NotificationCategory.Error: return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    default: return new SolidColorBrush(Color.FromRgb(59, 130, 246));
                }
            }
        }

        public bool IsRead
        {
            get { return _isRead; }
            set
            {
                _isRead = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}