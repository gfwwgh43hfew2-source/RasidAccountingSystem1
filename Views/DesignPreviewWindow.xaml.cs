using System.Windows;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// نافذة معاينة معزولة لهوية "الورشة" (Carbon &amp; Ignition).
    /// لا تعتمد على أي خدمة إنتاجية (DatabaseService, LicenseService...)
    /// عن قصد، عشان تفتح فورًا للمعاينة البصرية بس بدون أي متطلبات جانبية.
    /// </summary>
    public partial class DesignPreviewWindow : Window
    {
        public DesignPreviewWindow()
        {
            InitializeComponent();
        }

        private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
        {
            var window = new WorkshopMainWindow();
            window.Show();
        }

        private void OpenSidebarPreview_Click(object sender, RoutedEventArgs e)
        {
            var sidebarWindow = new Window
            {
                Title = "معاينة القائمة الجانبية — الورشة",
                Width = 300,
                Height = 700,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new WorkshopSidebar()
            };
            sidebarWindow.Show();
        }
    }
}
