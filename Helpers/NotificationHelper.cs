using System;
using System.Threading.Tasks;
using System.Windows;
using RasidAccountingSystem.Views;

namespace RasidAccountingSystem.Helpers
{
    /// <summary>
    /// كلاس مساعد لتحديث التنبيهات من أي مكان في البرنامج
    /// </summary>
    public static class NotificationHelper
    {
        /// <summary>
        /// تحديث تنبيهات الأقساط فوراً
        /// </summary>
        public static async Task RefreshInstallmentNotificationsAsync()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    await mainWindow.RefreshInstallmentNotificationsAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ MainWindow غير متاح لتحديث التنبيهات");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshInstallmentNotificationsAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// عرض تنبيه نجاح
        /// </summary>
        public static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ShowSuccessNotification(title, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowSuccessNotification Error: {ex.Message}");
            }
        }

        /// <summary>
        /// عرض تنبيه تحذير
        /// </summary>
        public static void ShowWarningNotification(string title, string message)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ShowWarningNotification(title, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowWarningNotification Error: {ex.Message}");
            }
        }

        /// <summary>
        /// عرض تنبيه خطأ
        /// </summary>
        public static void ShowErrorNotification(string title, string message)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ShowErrorNotification(title, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowErrorNotification Error: {ex.Message}");
            }
        }

        /// <summary>
        /// عرض تنبيه معلومات
        /// </summary>
        public static void ShowInfoNotification(string title, string message)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ShowInfoNotification(title, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowInfoNotification Error: {ex.Message}");
            }
        }
    }
}