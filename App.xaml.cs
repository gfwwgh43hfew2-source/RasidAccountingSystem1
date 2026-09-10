using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;
using RasidAccountingSystem.Views;

namespace RasidAccountingSystem
{
    public partial class App : Application
    {
        private DatabaseService _databaseService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ============================================================
            // ⚠️ كتلة مؤقتة لمعاينة هوية "الورشة" فقط (WorkshopTheme/Sidebar/
            // WorkshopMainWindow) — راجع PROJECT_PROGRESS.txt (نهاية الخطوة 3).
            // بتفتح DesignPreviewWindow مباشرة وترجع فورًا، من غير ما تلمس
            // قاعدة البيانات ولا تسجيل الدخول ولا أي كود تاني تحتها في الدالة.
            //
            // للرجوع لتشغيل البرنامج الطبيعي: احذف هذه الكتلة بالكامل (أو
            // علّقها بـ /* */) وشغّل تاني — باقي الدالة يرجع يشتغل زي ما كان
            // من غير أي تعديل إضافي مطلوب.
            // ============================================================
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var previewWindow = new RasidAccountingSystem.Views.DesignPreviewWindow();
            this.MainWindow = previewWindow;
            previewWindow.Show();
            return;
            // ============================================================
            // نهاية الكتلة المؤقتة — الكود الطبيعي تحت هنا لم يتغيّر إطلاقًا
            // ============================================================

            // ✅ تهيئة نظام تسجيل الأخطاء أولاً، قبل أي شيء آخر، حتى لو حصل خطأ فادح
            // من اللحظة الأولى نقدر نسجله ونعرف سببه لاحقاً بدل ما يختفي بلا أثر.
            Logger.Initialize();
            Logger.LogInfo("بدء تشغيل البرنامج", "App.OnStartup");

            // ✅ ربط معالجات الأخطاء الشاملة (Global Exception Handlers).
            // من غير الكود ده، أي خطأ غير متوقع في أي شاشة كان بيقفل البرنامج فوراً بدون أي رسالة
            // مفيدة للمستخدم ولا أي أثر مسجَّل يساعدنا نعرف حصل إيه بعد كده.
            RegisterGlobalExceptionHandlers();

            // ✅ ربط تشفير قاعدة البيانات عند إغلاق البرنامج بشكل طبيعي (راجع DatabaseEncryptionHelper
            // لتفاصيل هذا القرار المعماري وأسباب اختيار هذا الأسلوب تحديداً)
            this.Exit += App_Exit;

            // منع التطبيق من الإغلاق التلقائي عند إغلاق النافذة الأولى
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _databaseService = new DatabaseService();
                string databasePath = _databaseService.DatabasePath;
                string appDataPath = Path.GetDirectoryName(databasePath);

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                // ✅ فك تشفير قاعدة البيانات (لو كانت مشفَّرة من إغلاق سابق) قبل أي اتصال بها.
                // آمنة تماماً: لو فشلت أو لم توجد نسخة مشفَّرة، تكمل بشكل طبيعي ولا توقف التشغيل.
                DatabaseEncryptionHelper.PrepareDatabaseForUse(databasePath);

                _databaseService.CreateAllDatabaseTables();
                Logger.LogInfo("تم التأكد من هيكل قاعدة البيانات بنجاح", "App.OnStartup");

                // ✅ إصلاح تلقائي لمرة واحدة لباج قديم كان يضاعف رصيد العميل عند إنشاء فاتورة بيع.
                // هذه الدالة آمنة تماماً: تتحقق أولاً هل تم تنفيذ الإصلاح من قبل، ولا تفعل شيئاً لو تم بالفعل.
                try
                {
                            var repairResult = _databaseService.RepairDuplicateSalesInvoiceCustomerBalancesIfNeeded();
                            if (repairResult.WasNeeded)
                            {
                                string customersList = string.Join("، ", repairResult.AffectedCustomerNames);

                                Logger.LogWarning(
                                    $"تم إصلاح باج مضاعفة رصيد العملاء تلقائياً - حركات محذوفة: {repairResult.DuplicateRowsRemoved}، " +
                                    $"عملاء تم تصحيحهم: {repairResult.CustomersRecalculated} ({customersList})",
                                    "App.OnStartup.BalanceRepair");

                                // ✅ الإصلاح يتم تلقائياً وبصمت دون إزعاج المستخدم برسالة منبثقة،
                                // طالما أن الحسابات تتم الآن بصورة طبيعية. يبقى التفصيل مسجلاً في
                                // ملف السجلات (Logger) فقط للرجوع إليه عند الحاجة.
                            }
                }
                catch (Exception repairEx)
                {
                    Logger.LogError("فشل تنفيذ إصلاح أرصدة العملاء التلقائي عند بدء التشغيل", repairEx, "App.OnStartup.BalanceRepair");
                    MessageBox.Show(
                        $"تعذّر تنفيذ إصلاح أرصدة العملاء التلقائي: {repairEx.Message}\n\nيمكنك المتابعة باستخدام البرنامج بشكل طبيعي، ويُنصح بإبلاغ الدعم الفني.",
                        "تنبيه",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                bool hasAnyUser = _databaseService.HasAnyUser();

                if (!hasAnyUser)
                {
                    // فتح نافذة إنشاء المستخدم الأول
                    FirstRunSetupView firstRunView = new FirstRunSetupView();
                    bool? result = firstRunView.ShowDialog();

                    // بعد إغلاقها، تحقق مما إذا تم إنشاء مستخدم بنجاح
                    if (result == true && _databaseService.HasAnyUser())
                    {
                        Logger.LogInfo("تم إنشاء أول مستخدم للنظام بنجاح", "App.OnStartup");
                        OpenLoginView();
                    }
                    else
                    {
                        Logger.LogInfo("تم إلغاء إعداد أول مستخدم - سيتم إغلاق البرنامج", "App.OnStartup");
                        Shutdown(); // إذا لم يتم إنشاء مستخدم أو تم الإلغاء
                    }
                }
                else
                {
                    OpenLoginView();
                }
            }
            catch (Exception ex)
            {
                Logger.LogCritical("فشل فادح أثناء تهيئة النظام عند بدء التشغيل", ex, "App.OnStartup");

                MessageBox.Show(
                    $"خطأ في تهيئة النظام: {ex.Message}\n\nتم تسجيل تفاصيل الخطأ في ملف السجل لمراجعته مع الدعم الفني.",
                    "خطأ فادح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// يُستدعى تلقائياً عند إغلاق البرنامج بشكل طبيعي (سواء عبر زر الإغلاق أو Shutdown()).
        /// يشفِّر قاعدة البيانات على القرص قبل الخروج فعلياً. راجع DatabaseEncryptionHelper
        /// لتفاصيل الآلية الكاملة وضمانات عدم فقد أي بيانات حتى لو فشلت خطوة التشفير.
        /// </summary>
        private void App_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                if (_databaseService != null)
                {
                    DatabaseEncryptionHelper.SecureDatabaseOnExit(_databaseService.DatabasePath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ تشفير قاعدة البيانات عند الإغلاق (App_Exit)", ex, "App.Exit");
            }
        }

        /// <summary>
        /// ربط كل مصادر الأخطاء غير المتوقعة الممكنة في تطبيق WPF بنظام التسجيل، حتى لا يختفي
        /// أي Crash بصمت، وحتى نعرض للمستخدم رسالة واضحة بدل شاشة "توقف البرنامج عن العمل"
        /// الافتراضية من ويندوز التي لا تحمل أي معلومة مفيدة.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            // 1) أي خطأ غير متوقع يحدث على خيط واجهة المستخدم (UI Thread) - الأكثر شيوعاً
            this.DispatcherUnhandledException += (sender, args) =>
            {
                Logger.LogCritical("خطأ غير متوقع على خيط الواجهة (DispatcherUnhandledException)", args.Exception, "App.Global");

                try
                {
                    MessageBox.Show(
                        $"حدث خطأ غير متوقع:\n\n{args.Exception.Message}\n\n" +
                        $"تم تسجيل تفاصيل الخطأ تلقائياً في ملف السجل. يمكنك متابعة استخدام البرنامج، " +
                        $"لكن يُفضّل حفظ عملك الحالي وإعادة تشغيل البرنامج، وإبلاغ الدعم الفني بالتفاصيل لو تكررت المشكلة.",
                        "حدث خطأ غير متوقع",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch
                {
                    // لو فشل عرض الرسالة نفسها لأي سبب، نتجاهل ونكمل - الأهم إن الخطأ اتسجل بالفعل
                }

                // ✅ نمنع إغلاق البرنامج بالكامل (Handled = true) عشان المستخدم ميفاجئش بإغلاق
                // مفاجئ يفقده بيانات شغالة، طالما إن الخطأ لم يكن في نقطة حرجة تمنع الاستمرار.
                args.Handled = true;
            };

            // 2) أي خطأ غير متوقع في أي خيط آخر غير خيط الواجهة (Background Thread)
            // هذا النوع لا يمكن "التعامل معه ومنع الإغلاق" لأن AppDomain بيتقفل عادةً بعده،
            // لكن على الأقل نضمن تسجيله بالكامل في ملف السجل قبل الإغلاق.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception ex = args.ExceptionObject as Exception;
                Logger.LogCritical(
                    $"خطأ غير متوقع على مستوى AppDomain (IsTerminating: {args.IsTerminating})",
                    ex,
                    "App.Global");

                if (args.IsTerminating)
                {
                    try
                    {
                        MessageBox.Show(
                            $"حدث خطأ فادح أدى لإغلاق البرنامج:\n\n{ex?.Message}\n\n" +
                            $"تم تسجيل التفاصيل الكاملة في ملف السجل. برجاء إبلاغ الدعم الفني بهذه المشكلة.",
                            "خطأ فادح",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    catch
                    {
                        // تجاهل فشل عرض الرسالة أثناء الإغلاق الفادح
                    }
                }
            };

            // 3) أخطاء غير متابَعة داخل مهام Task غير متزامنة (async/await) لم يتم انتظارها بشكل صحيح.
            // بدون هذا المعالج، هذا النوع من الأخطاء كان يختفي تماماً بصمت في الخلفية.
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.LogError("خطأ غير متابَع في مهمة غير متزامنة (UnobservedTaskException)", args.Exception, "App.Global");
                args.SetObserved();
            };
        }

        private void OpenLoginView()
        {
            // تغيير وضع الإغلاق إلى الإغلاق عند إغلاق النافذة الرئيسية (LoginView)
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;

            LoginView loginView = new LoginView(_databaseService);
            this.MainWindow = loginView;
            loginView.Show();
        }

        public DatabaseService GetDatabaseService()
        {
            return _databaseService;
        }
    }
}
