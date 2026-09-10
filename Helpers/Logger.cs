using System;
using System.IO;
using System.Text;
using System.Threading;
using RasidAccountingSystem.Views;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// مستويات خطورة الرسالة المسجَّلة
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// نظام تسجيل أخطاء بسيط يكتب في ملفات نصية على القرص، بحيث لو حصلت مشكلة عند العميل
    /// (Crash، خطأ غير متوقع، فشل عملية حفظ...) يبقى فيه أثر مكتوب يوضح بالظبط إيه اللي حصل
    /// وإمتى، بدل الاعتماد على Debug.WriteLine اللي بيظهر بس لو البرنامج شغال جوه Visual Studio
    /// ويختفي فور إغلاق البرنامج - يعني عملياً غير موجود عند العميل النهائي.
    ///
    /// مكان حفظ ملفات السجل:
    ///   %LocalAppData%\RasidERP\Logs\RasidLog_yyyy-MM-dd.txt
    ///
    /// كل يوم بيتعمله ملف منفصل، وبيتم تلقائياً حذف أي ملفات سجل أقدم من 30 يوم عشان
    /// السجلات ما تكبرش وتاخد مساحة على جهاز العميل من غير داعي.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObject = new object();
        private static string _logDirectory;
        private static bool _initialized = false;
        private const int RetentionDays = 30;

        /// <summary>
        /// تهيئة نظام التسجيل. تُستدعى مرة واحدة فقط عند بدء تشغيل البرنامج (من App.xaml.cs).
        /// آمنة الاستدعاء أكثر من مرة (لن تكرر التهيئة أو الحذف).
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lockObject)
            {
                if (_initialized) return;

                try
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    _logDirectory = Path.Combine(appDataPath, "RasidERP", "Logs");

                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }

                    CleanupOldLogFiles();
                    _initialized = true;
                }
                catch
                {
                    // لو فشلت التهيئة (مثلاً مفيش صلاحية كتابة)، البرنامج لازم يكمل شغله عادي
                    // بدون تسجيل، بدل ما يقف بسبب مشكلة في نظام التسجيل نفسه
                    _initialized = false;
                }
            }
        }

        /// <summary>
        /// تسجيل رسالة معلوماتية عادية (بداية/نهاية عملية مهمة، إلخ)
        /// </summary>
        public static void LogInfo(string message, string source = null)
        {
            Write(LogLevel.Info, message, null, source);
        }

        /// <summary>
        /// تسجيل تحذير (حالة غير طبيعية لكنها ليست خطأ متوقف)
        /// </summary>
        public static void LogWarning(string message, string source = null)
        {
            Write(LogLevel.Warning, message, null, source);
        }

        /// <summary>
        /// تسجيل خطأ مع تفاصيل الاستثناء الكاملة (Message + StackTrace)
        /// </summary>
        public static void LogError(string message, Exception ex = null, string source = null)
        {
            Write(LogLevel.Error, message, ex, source);
        }

        /// <summary>
        /// تسجيل خطأ فادح غير متوقع (Crash / Unhandled Exception)
        /// </summary>
        public static void LogCritical(string message, Exception ex = null, string source = null)
        {
            Write(LogLevel.Critical, message, ex, source);
        }

        private static void Write(LogLevel level, string message, Exception ex, string source)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                    if (!_initialized) return;
                }

                string fileName = $"RasidLog_{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_logDirectory, fileName);

                var sb = new StringBuilder();
                sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}]");

                if (!string.IsNullOrEmpty(source))
                {
                    sb.Append($" [{source}]");
                }

                sb.Append($" [User: {SafeCurrentUsername()}]");
                sb.AppendLine();
                sb.AppendLine($"    الرسالة: {message}");

                if (ex != null)
                {
                    sb.AppendLine($"    نوع الاستثناء: {ex.GetType().FullName}");
                    sb.AppendLine($"    تفاصيل: {ex.Message}");
                    sb.AppendLine($"    Stack Trace:");
                    sb.AppendLine(ex.StackTrace ?? "(غير متوفر)");

                    Exception inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null && depth <= 5)
                    {
                        sb.AppendLine($"    --- الاستثناء الداخلي ({depth}): {inner.GetType().Name}: {inner.Message}");
                        inner = inner.InnerException;
                        depth++;
                    }
                }

                sb.AppendLine(new string('-', 80));

                lock (_lockObject)
                {
                    File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // فشل التسجيل نفسه لا يجب أن يوقف تشغيل البرنامج أو يرمي استثناء جديد
            }
        }

        private static string SafeCurrentUsername()
        {
            try
            {
                return LoginView.CurrentUsername ?? "غير مسجل دخول";
            }
            catch
            {
                return "غير معروف";
            }
        }

        private static void CleanupOldLogFiles()
        {
            try
            {
                if (!Directory.Exists(_logDirectory)) return;

                DateTime cutoffDate = DateTime.Now.AddDays(-RetentionDays);

                foreach (string file in Directory.GetFiles(_logDirectory, "RasidLog_*.txt"))
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.LastWriteTime < cutoffDate)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // تجاهل فشل حذف ملف واحد ومتابعة الباقي
                    }
                }
            }
            catch
            {
                // تجاهل أي خطأ أثناء التنظيف - ليس عملية حرجة
            }
        }

        /// <summary>
        /// مسار مجلد السجلات الحالي، يُستخدم مثلاً لو أردنا إضافة زر "فتح مجلد السجلات" في الإعدادات
        /// </summary>
        public static string GetLogDirectory()
        {
            if (!_initialized) Initialize();
            return _logDirectory;
        }
    }
}
