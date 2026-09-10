using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// نافذة تسجيل الدخول إلى نظام رصيد المحاسبي المتكامل
    /// </summary>
    public partial class LoginView : Window
    {
        // ==================== المتغيرات الخاصة ====================
        private DatabaseService _dbService;
        private bool isDragging = false;
        private Point lastCursor;
        private bool isPasswordVisible = false;

        // المتغيرات العامة لتخزين بيانات المستخدم الحالي
        public static int CurrentUserId { get; set; }
        public static string CurrentUsername { get; set; }
        public static string CurrentUserRole { get; set; }
        public static string CurrentUserFullName { get; set; }
        public static string CurrentUserEmail { get; set; }
        public static string CurrentUserPhone { get; set; }

        // عناصر التحكم
        private TextBox txtPasswordVisible;

        // ==================== ثوابت التخزين في Registry ====================
        private const string REGISTRY_PATH = @"SOFTWARE\RasidAccountingSystem";
        private const string LICENSE_KEY_KEY = "LicenseKey";
        private const string LICENSE_TYPE_KEY = "LicenseType";
        private const string ACTIVATION_DATE_KEY = "ActivationDate";
        private const string EXPIRY_DATE_KEY = "ExpiryDate";
        private const string IS_ACTIVATED_KEY = "IsActivated";
        private const string FIRST_RUN_DATE_KEY = "FirstRunDate";

        // ==================== رقم الدعم الفني ====================
        private const string SUPPORT_PHONE = "+201010422057";

        // ==================== كلاس معلومات الترخيص ====================
        public class LicenseInfo
        {
            public string LicenseKey { get; set; }
            public string LicenseType { get; set; }
            public DateTime ActivationDate { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public bool IsActivated { get; set; }
            public int DaysRemaining { get; set; }
            public bool IsExpired { get; set; }
            public bool IsPermanent { get; set; }

            /// <summary>
            /// عرض نوع الترخيص بشكل نصي مفهوم
            /// </summary>
            public string LicenseTypeDisplay
            {
                get
                {
                    switch (LicenseType)
                    {
                        case "30_DAY":
                            return "ترخيص شهري (30 يوم)";
                        case "90_DAY":
                            return "ترخيص 3 أشهر (90 يوم)";
                        case "ANNUAL":
                            return "ترخيص سنوي (365 يوم)";
                        case "LIFETIME":
                            return "ترخيص دائم";
                        default:
                            return LicenseType ?? "نوع ترخيص غير معروف";
                    }
                }
            }

            /// <summary>
            /// عرض حالة الترخيص بشكل نصي مفهوم
            /// </summary>
            public string StatusMessage
            {
                get
                {
                    if (IsPermanent)
                    {
                        return "✅ ترخيص دائم - لا ينتهي أبداً";
                    }
                    if (IsExpired)
                    {
                        return "❌ انتهت صلاحية الترخيص";
                    }
                    return $"⏰ متبقي {DaysRemaining} يوم";
                }
            }

            /// <summary>
            /// الحصول على عدد الأيام المتبقية كنص منسق
            /// </summary>
            public string GetRemainingDaysText()
            {
                if (IsPermanent)
                {
                    return "غير محدد (ترخيص دائم)";
                }
                if (IsExpired)
                {
                    return "انتهت الصلاحية";
                }
                return $"{DaysRemaining} يوم متبقي";
            }
        }

        // ==================== كلاس نتيجة التحقق من الترخيص ====================
        public class LicenseValidationResult
        {
            public bool IsValid { get; set; }
            public bool RequiresActivation { get; set; }
            public bool IsExpired { get; set; }
            public string Message { get; set; }
            public LicenseInfo LicenseInfo { get; set; }

            public LicenseValidationResult()
            {
                IsValid = false;
                RequiresActivation = true;
                IsExpired = false;
                Message = string.Empty;
                LicenseInfo = null;
            }
        }

        // ==================== المنشئات ====================
        public LoginView()
        {
            InitializeComponent();
            var dbService = new DatabaseService();
            InitializeLoginView(dbService);
        }

        public LoginView(DatabaseService dbService)
        {
            InitializeComponent();
            InitializeLoginView(dbService);
        }

        // ==================== دالة تهيئة واجهة تسجيل الدخول ====================
        /// <summary>
        /// تهيئة واجهة تسجيل الدخول بالكامل
        /// </summary>
        /// <param name="dbService">خدمة قاعدة البيانات المستخدمة للتفاعل مع البيانات</param>
        private void InitializeLoginView(DatabaseService dbService)
        {
            // ==================== تعيين خدمة قاعدة البيانات ====================
            _dbService = dbService;

            // ==================== تحميل الصورة في المجسم ثلاثي الأبعاد ====================
            LoadLogoImageInto3D();

            // ==================== حدث تحميل النافذة ====================
            this.Loaded += async (s, e) =>
            {
                try
                {
                    // ==================== التحقق من الترخيص ====================
                    bool canProceed = await CheckLicenseAndProceed();

                    if (!canProceed)
                    {
                        Application.Current.Shutdown();
                        return;
                    }

                    // ==================== تهيئة عناصر كلمة المرور ====================
                    InitializePasswordControls();

                    // ==================== تحميل قائمة المستخدمين ====================
                    await LoadUsersAsync();

                    // ==================== تشغيل الدوران ثلاثي الأبعاد ====================
                    Start3DRotation();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"حدث خطأ أثناء تحميل النافذة: {ex.Message}\n\n" +
                        $"يرجى إعادة تشغيل البرنامج أو الاتصال بالدعم الفني.\n" +
                        $"📞 {SUPPORT_PHONE}",
                        "خطأ في التحميل",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Application.Current.Shutdown();
                }
            };

            // ==================== أحداث سحب النافذة ====================
            this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
            this.MouseMove += Window_MouseMove;
            this.MouseLeftButtonUp += Window_MouseLeftButtonUp;

            // ==================== أحداث الأزرار ====================
            btnClose.Click += (s, e) => this.Close();
            btnTogglePassword.Click += BtnTogglePassword_Click;
            btnLogin.Click += BtnLogin_Click;
            btnCreateAccount.Click += BtnCreateAccount_Click;
            btnForgotPassword.Click += BtnForgotPassword_Click;

            // ==================== حدث الضغط على Enter ====================
            txtPasswordHidden.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    BtnLogin_Click(s, e);
                    e.Handled = true;
                }
            };

            // ==================== التركيز الافتراضي ====================
            this.Focus();
            txtPasswordHidden.Focus();

            // ==================== تسجيل في سجل التتبع ====================
            System.Diagnostics.Debug.WriteLine("✅ تم تهيئة واجهة تسجيل الدخول بنجاح");
        }

        // ==================== تحميل الصورة في المجسم ثلاثي الأبعاد ====================
        /// <summary>
        /// تحميل الصورة في المجسم ثلاثي الأبعاد (الوجه الأمامي والخلفي)
        /// </summary>
        private void LoadLogoImageInto3D()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string logoPath = null;

                // ==================== قائمة المسارات المحتملة للصورة ====================
                // المسار الأول مضمون الوجود دائماً بعد البناء (نفس ملف الشعار المستخدم في
                // القائمة الجانبية بشاشة البرنامج الرئيسية)، والباقي مسارات احتياطية إضافية
                string[] possiblePaths = new string[]
                {
                    System.IO.Path.Combine(baseDirectory, "assets", "icons", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "Assets", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "..", "Assets", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "Resources", "logo.png"),
                    System.IO.Path.Combine(baseDirectory, "Images", "logo.png"),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.png"),
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RasidAccountingSystem",
                        "logo.png"
                    ),
                    System.IO.Path.Combine(baseDirectory, "..", "..", "..", "Assets", "logo.png")
                };

                // ==================== البحث عن الصورة في المسارات ====================
                foreach (string path in possiblePaths)
                {
                    if (System.IO.File.Exists(path))
                    {
                        logoPath = path;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(logoPath))
                {
                    // ====== تحميل الصورة ======
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    // ====== تعيين الصورة للوجه الأمامي ======
                    LogoImageBrush.ImageSource = bitmap;

                    // ====== تعيين نفس الصورة للوجه الخلفي ======
                    LogoBackImageBrush.ImageSource = bitmap;

                    System.Diagnostics.Debug.WriteLine($"✅ تم تحميل الصورة 3D من: {logoPath}");
                }
                else
                {
                    // ====== استخدام تدرج لوني في حالة عدم وجود الصورة ======
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1A2980"), 0));
                    gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#26D0CE"), 1));

                    LogoImageBrush.ImageSource = null;
                    LogoMaterial.Brush = gradientBrush;

                    // ====== نفس التدرج للخلف ======
                    LogoBackImageBrush.ImageSource = null;
                    LogoBackMaterial.Brush = gradientBrush;

                    System.Diagnostics.Debug.WriteLine("⚠️ لم يتم العثور على الصورة، استخدام تدرج لوني");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تحميل الصورة 3D: {ex.Message}");
            }
        }

        // ==================== تشغيل الدوران ثلاثي الأبعاد ====================
        /// <summary>
        /// تشغيل حركة الدوران ثلاثي الأبعاد للمجسم
        /// </summary>
        private void Start3DRotation()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🌀 بدء الدوران ثلاثي الأبعاد...");

                // ==================== دوران حول محور Y (الدوران الرئيسي) ====================
                var rotationAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(10),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                LogoAxisRotation3D.BeginAnimation(AxisAngleRotation3D.AngleProperty, rotationAnimation);

                // ==================== إمالة خفيفة حول محور X ====================
                var tiltAnimation = new DoubleAnimation
                {
                    From = -5,
                    To = 5,
                    Duration = TimeSpan.FromSeconds(3),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                LogoTiltAxisRotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, tiltAnimation);

                // ==================== دوران الحلقة الضوئية ====================
                var ringAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(5),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                RingRotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, ringAnimation);

                System.Diagnostics.Debug.WriteLine("✅ تم تشغيل الدوران ثلاثي الأبعاد بنجاح");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ خطأ في تشغيل الدوران 3D: {ex.Message}");
            }
        }

        // ==================== التحقق من الترخيص والمتابعة ====================
        /// <summary>
        /// التحقق من الترخيص والمتابعة (بدون رسائل تحذير)
        /// </summary>
        private async Task<bool> CheckLicenseAndProceed()
        {
            try
            {
                if (IsInGracePeriod())
                {
                    return true;
                }

                var license = GetStoredLicense();

                if (license == null || !license.IsActivated)
                {
                    return await ShowLicenseActivationDialog();
                }

                if (license.IsPermanent)
                {
                    return true;
                }

                if (license.ExpiryDate.HasValue)
                {
                    if (DateTime.Now.Date > license.ExpiryDate.Value.Date)
                    {
                        MessageBox.Show(
                            $"❌ انتهت صلاحية الترخيص ({license.LicenseTypeDisplay})\n\n" +
                            $"تاريخ الانتهاء: {license.ExpiryDate.Value:yyyy-MM-dd}\n\n" +
                            $"للحصول على ترخيص جديد، يرجى التواصل مع الدعم الفني:\n" +
                            $"📞 {SUPPORT_PHONE}\n\n" +
                            $"يرجى إدخال الترخيص الجديد للمتابعة.",
                            "انتهت صلاحية الترخيص",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        ClearLicense();
                        return await ShowLicenseActivationDialog();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"خطأ في التحقق من الترخيص: {ex.Message}\n\nللحصول على المساعدة: {SUPPORT_PHONE}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        // ==================== التحقق من فترة السماح ====================
        private bool IsInGracePeriod()
        {
            try
            {
                DateTime firstRunDate = GetFirstRunDate();
                DateTime currentDate = DateTime.Now.Date;
                int daysPassed = (currentDate - firstRunDate).Days;
                return daysPassed < 15;
            }
            catch
            {
                return true;
            }
        }

        // ==================== الحصول على تاريخ أول تشغيل ====================
        private DateTime GetFirstRunDate()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key != null)
                    {
                        string savedDate = key.GetValue(FIRST_RUN_DATE_KEY) as string;
                        if (!string.IsNullOrEmpty(savedDate))
                        {
                            return DateTime.Parse(savedDate);
                        }
                    }
                }

                DateTime firstRunDate = DateTime.Now.Date;
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    key.SetValue(FIRST_RUN_DATE_KEY, firstRunDate.ToString("yyyy-MM-dd"));
                }

                return firstRunDate;
            }
            catch
            {
                return DateTime.Now.Date;
            }
        }

        // ==================== الحصول على الترخيص المخزن ====================
        private LicenseInfo GetStoredLicense()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    string isActivated = key.GetValue(IS_ACTIVATED_KEY) as string;
                    if (isActivated != "True")
                    {
                        return null;
                    }

                    var license = new LicenseInfo
                    {
                        LicenseKey = key.GetValue(LICENSE_KEY_KEY) as string,
                        LicenseType = key.GetValue(LICENSE_TYPE_KEY) as string,
                        IsActivated = true
                    };

                    string activationDateStr = key.GetValue(ACTIVATION_DATE_KEY) as string;
                    if (!string.IsNullOrEmpty(activationDateStr))
                    {
                        license.ActivationDate = DateTime.Parse(activationDateStr);
                    }

                    string expiryDateStr = key.GetValue(EXPIRY_DATE_KEY) as string;
                    if (!string.IsNullOrEmpty(expiryDateStr))
                    {
                        license.ExpiryDate = DateTime.Parse(expiryDateStr);
                    }

                    license.IsPermanent = license.LicenseType == "LIFETIME";

                    if (!license.IsPermanent && license.ExpiryDate.HasValue)
                    {
                        license.DaysRemaining = (int)(license.ExpiryDate.Value.Date - DateTime.Now.Date).TotalDays;
                        license.IsExpired = license.DaysRemaining < 0;
                    }

                    return license;
                }
            }
            catch
            {
                return null;
            }
        }

        // ==================== تخزين الترخيص ====================
        private void SaveLicense(string licenseKey, string licenseType, DateTime activationDate, DateTime? expiryDate)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    key.SetValue(LICENSE_KEY_KEY, licenseKey);
                    key.SetValue(LICENSE_TYPE_KEY, licenseType);
                    key.SetValue(ACTIVATION_DATE_KEY, activationDate.ToString("yyyy-MM-dd"));
                    key.SetValue(IS_ACTIVATED_KEY, "True");

                    if (expiryDate.HasValue)
                    {
                        key.SetValue(EXPIRY_DATE_KEY, expiryDate.Value.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        key.DeleteValue(EXPIRY_DATE_KEY, false);
                    }
                }
            }
            catch
            {
                // تجاهل أخطاء الحفظ
            }
        }

        // ==================== حذف الترخيص ====================
        private void ClearLicense()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
                {
                    key.DeleteValue(LICENSE_KEY_KEY, false);
                    key.DeleteValue(LICENSE_TYPE_KEY, false);
                    key.DeleteValue(ACTIVATION_DATE_KEY, false);
                    key.DeleteValue(EXPIRY_DATE_KEY, false);
                    key.SetValue(IS_ACTIVATED_KEY, "False");
                }
            }
            catch
            {
                // تجاهل أخطاء الحذف
            }
        }

        // ==================== التحقق من صحة كود الترخيص ====================
        private bool ValidateLicenseKey(string licenseKey, out string licenseType, out int days)
        {
            licenseType = string.Empty;
            days = 0;

            try
            {
                DateTime now = DateTime.Now;
                long baseValue = now.Day * now.Month * now.Year * now.Year;

                if (licenseKey == $"{baseValue * 2}khgg")
                {
                    licenseType = "30_DAY";
                    days = 30;
                    return true;
                }
                else if (licenseKey == $"{baseValue * 4}wdsa")
                {
                    licenseType = "90_DAY";
                    days = 90;
                    return true;
                }
                else if (licenseKey == $"{baseValue * 6}dfdsf")
                {
                    licenseType = "ANNUAL";
                    days = 365;
                    return true;
                }
                else if (licenseKey == $"{baseValue * 8}vfsda")
                {
                    licenseType = "LIFETIME";
                    days = 99999;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ==================== عرض نافذة تفعيل الترخيص ====================
        private async Task<bool> ShowLicenseActivationDialog()
        {
            bool isActivated = false;
            int attempts = 0;
            const int maxAttempts = 3;

            while (!isActivated && attempts < maxAttempts)
            {
                attempts++;

                var activationWindow = new Window
                {
                    Title = "تفعيل البرنامج - نظام رصيد المحاسبي",
                    Width = 500,
                    Height = 380,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Owner = this
                };

                var grid = new Grid();
                grid.Margin = new Thickness(20);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var titleLabel = new Label
                {
                    Content = "🔐 تفعيل الترخيص",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(titleLabel, 0);
                grid.Children.Add(titleLabel);

                var messageLabel = new Label
                {
                    Content = attempts == 1 ?
                        "يرجى إدخال كود الترخيص لتفعيل البرنامج:" :
                        $"❌ كود الترخيص غير صحيح. تبقت لك {maxAttempts - attempts + 1} محاولة.",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10),
                    Foreground = attempts > 1 ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Black
                };
                Grid.SetRow(messageLabel, 1);
                grid.Children.Add(messageLabel);

                var licenseKeyBox = new TextBox
                {
                    Width = 320,
                    Height = 40,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(licenseKeyBox, 2);
                grid.Children.Add(licenseKeyBox);

                var supportPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var supportLabel = new Label
                {
                    Content = "📞 للدعم الفني والتواصل:",
                    FontSize = 11,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var phoneLabel = new Label
                {
                    Content = SUPPORT_PHONE,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.Green,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = Cursors.Hand
                };

                phoneLabel.MouseLeftButtonDown += (s, e) =>
                {
                    Clipboard.SetText(SUPPORT_PHONE);
                    MessageBox.Show("تم نسخ رقم الدعم الفني إلى الحافظة", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
                };

                supportPanel.Children.Add(supportLabel);
                supportPanel.Children.Add(phoneLabel);
                Grid.SetRow(supportPanel, 3);
                grid.Children.Add(supportPanel);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var activateButton = new Button
                {
                    Content = "✅ تفعيل",
                    Width = 120,
                    Height = 40,
                    Margin = new Thickness(5),
                    Background = System.Windows.Media.Brushes.Green,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Cursor = Cursors.Hand
                };

                var cancelButton = new Button
                {
                    Content = "❌ إلغاء",
                    Width = 120,
                    Height = 40,
                    Margin = new Thickness(5),
                    Cursor = Cursors.Hand
                };

                buttonPanel.Children.Add(activateButton);
                buttonPanel.Children.Add(cancelButton);
                Grid.SetRow(buttonPanel, 4);
                grid.Children.Add(buttonPanel);

                var noteLabel = new Label
                {
                    Content = "ملاحظة: كود الترخيص صالح للاستخدام على جهاز واحد فقط",
                    FontSize = 9,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(noteLabel, 5);
                grid.Children.Add(noteLabel);

                var spacer = new Label
                {
                    Height = 10
                };
                Grid.SetRow(spacer, 6);
                grid.Children.Add(spacer);

                activationWindow.Content = grid;

                bool dialogResult = false;

                activateButton.Click += (s, e) =>
                {
                    string licenseKey = licenseKeyBox.Text.Trim();
                    string licenseType;
                    int days;

                    if (ValidateLicenseKey(licenseKey, out licenseType, out days))
                    {
                        DateTime activationDate = DateTime.Now;
                        DateTime? expiryDate = licenseType != "LIFETIME" ? activationDate.AddDays(days) : (DateTime?)null;

                        SaveLicense(licenseKey, licenseType, activationDate, expiryDate);

                        string message = licenseType == "LIFETIME" ?
                            "✅ تم تفعيل الترخيص الدائم بنجاح!" :
                            $"✅ تم تفعيل الترخيص بنجاح!\nنوع الترخيص: {GetLicenseTypeDisplay(licenseType)}\nتاريخ الانتهاء: {expiryDate:yyyy-MM-dd}";

                        MessageBox.Show(message, "تم التفعيل", MessageBoxButton.OK, MessageBoxImage.Information);

                        dialogResult = true;
                        activationWindow.Close();
                    }
                    else
                    {
                        messageLabel.Content = $"❌ كود الترخيص غير صحيح. تبقت لك {maxAttempts - attempts} محاولة.";
                        messageLabel.Foreground = System.Windows.Media.Brushes.Red;
                        licenseKeyBox.Clear();
                        licenseKeyBox.Focus();
                    }
                };

                cancelButton.Click += (s, e) =>
                {
                    if (attempts < maxAttempts)
                    {
                        var result = MessageBox.Show(
                            "هل تريد إلغاء التفعيل؟\n\nسيتم إغلاق البرنامج.",
                            "تأكيد الإلغاء",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            dialogResult = false;
                            activationWindow.Close();
                        }
                    }
                    else
                    {
                        dialogResult = false;
                        activationWindow.Close();
                    }
                };

                licenseKeyBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        activateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                };

                activationWindow.ShowDialog();

                if (dialogResult)
                {
                    isActivated = true;
                }
                else if (attempts >= maxAttempts)
                {
                    MessageBox.Show(
                        $"لقد استنفدت جميع المحاولات ({maxAttempts}).\n\nللحصول على المساعدة، يرجى التواصل مع الدعم الفني:\n📞 {SUPPORT_PHONE}",
                        "فشل التفعيل",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }

            return isActivated;
        }

        // ==================== عرض نوع الترخيص بشكل نصي ====================
        private string GetLicenseTypeDisplay(string licenseType)
        {
            switch (licenseType)
            {
                case "30_DAY":
                    return "ترخيص شهري (30 يوم)";
                case "90_DAY":
                    return "ترخيص 3 أشهر (90 يوم)";
                case "ANNUAL":
                    return "ترخيص سنوي (365 يوم)";
                case "LIFETIME":
                    return "ترخيص دائم";
                default:
                    return licenseType ?? "نوع ترخيص غير معروف";
            }
        }

        // ==================== تهيئة عناصر التحكم الخاصة بكلمة المرور ====================
        private void InitializePasswordControls()
        {
            txtPasswordVisible = new TextBox
            {
                FontFamily = txtPasswordHidden.FontFamily,
                FontSize = txtPasswordHidden.FontSize,
                Height = txtPasswordHidden.Height,
                Width = txtPasswordHidden.Width,
                Padding = txtPasswordHidden.Padding,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = txtPasswordHidden.Background,
                BorderBrush = txtPasswordHidden.BorderBrush,
                BorderThickness = txtPasswordHidden.BorderThickness,
                Visibility = Visibility.Collapsed,
                TextAlignment = TextAlignment.Center
            };

            txtPasswordVisible.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    BtnLogin_Click(s, e);
                }
            };

            Grid parentGrid = txtPasswordHidden.Parent as Grid;
            if (parentGrid != null)
            {
                int columnIndex = Grid.GetColumn(txtPasswordHidden);
                int rowIndex = Grid.GetRow(txtPasswordHidden);

                Grid.SetColumn(txtPasswordVisible, columnIndex);
                Grid.SetRow(txtPasswordVisible, rowIndex);

                parentGrid.Children.Add(txtPasswordVisible);
            }
        }

        // ==================== إظهار/إخفاء كلمة المرور ====================
        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (txtPasswordVisible == null)
            {
                return;
            }

            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = txtPasswordHidden.Password;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPasswordHidden.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = "🙈";

                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            else
            {
                txtPasswordHidden.Password = txtPasswordVisible.Text;
                txtPasswordHidden.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = "👁";

                txtPasswordHidden.Focus();
            }
        }

        // ==================== أحداث سحب النافذة ====================
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                isDragging = true;
                lastCursor = e.GetPosition(this);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                this.Left += e.GetPosition(this).X - lastCursor.X;
                this.Top += e.GetPosition(this).Y - lastCursor.Y;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        // ==================== تحميل قائمة المستخدمين ====================
        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _dbService.GetActiveUsernamesAsync();

                cmbUsername.Items.Clear();
                foreach (var user in users)
                {
                    cmbUsername.Items.Add(user);
                }

                if (cmbUsername.Items.Count > 0)
                {
                    cmbUsername.SelectedIndex = 0;
                    cmbUsername.Visibility = Visibility.Visible;
                    txtPasswordHidden.Visibility = Visibility.Visible;
                    btnLogin.Visibility = Visibility.Visible;
                    btnCreateAccount.Visibility = Visibility.Visible;
                    btnForgotPassword.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطأ في تحميل المستخدمين: {ex.Message}");
            }
        }

        // ==================== معالج حدث تسجيل الدخول ====================
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string selectedUsername = cmbUsername.SelectedItem?.ToString() ?? string.Empty;

            string password = string.Empty;
            if (isPasswordVisible && txtPasswordVisible != null && txtPasswordVisible.Visibility == Visibility.Visible)
            {
                password = txtPasswordVisible.Text;
            }
            else
            {
                password = txtPasswordHidden.Password;
            }

            if (string.IsNullOrEmpty(selectedUsername))
            {
                ShowError("يرجى اختيار اسم المستخدم");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("يرجى إدخال كلمة المرور");
                return;
            }

            try
            {
                btnLogin.IsEnabled = false;
                btnLogin.Content = "جاري التحقق...";
                this.Cursor = Cursors.Wait;

                var user = await _dbService.ValidateUserAsync(selectedUsername, password);

                if (user != null)
                {
                    CurrentUserId = user.UserID;
                    CurrentUsername = user.Username;
                    CurrentUserRole = user.UserRole;
                    CurrentUserFullName = user.FullName;
                    CurrentUserEmail = user.Email;
                    CurrentUserPhone = user.Phone;

                    await _dbService.UpdateLastLoginAsync(CurrentUserId);

                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    Application.Current.MainWindow = mainWindow;
                    this.Close();
                }
                else
                {
                    ShowError("اسم المستخدم أو كلمة المرور غير صحيحة");
                    txtPasswordHidden.Clear();
                    if (txtPasswordVisible != null)
                    {
                        txtPasswordVisible.Clear();
                    }
                    txtPasswordHidden.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطأ: {ex.Message}");
            }
            finally
            {
                btnLogin.IsEnabled = true;
                btnLogin.Content = "🔑  دخول";
                this.Cursor = null;
            }
        }

        // ==================== معالج حدث إنشاء حساب جديد ====================
        private void BtnCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            var createAccountDialog = new CreateAccountView(_dbService);
            createAccountDialog.Owner = this;

            if (createAccountDialog.ShowDialog() == true)
            {
                _ = LoadUsersAsync();
            }
        }

        // ==================== معالج حدث نسيت كلمة المرور ====================
        private void BtnForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                $"لإعادة تعيين كلمة المرور، يرجى التواصل مع الدعم الفني:\n📞 {SUPPORT_PHONE}",
                "نسيت كلمة المرور",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ==================== عرض رسالة خطأ ====================
        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        // ==================== حدث إضافي لزر الدخول ====================
        private void btnLogin_Click_1(object sender, RoutedEventArgs e)
        {
            // هذا الحدث متروك للتوسع المستقبلي
        }
    }
}