using System.Windows;
using Wpf.Ui.Appearance;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// الهيكل العام التجريبي لبرنامج "الورشة" (Sidebar + Header + Dashboard).
    /// نافذة منفصلة تمامًا عن MainWindow.xaml الإنتاجي — لا تستبدله بعد.
    ///
    /// [تحديث] بقى فيه تنقّل أساسي: الضغط على "الرئيسية" في الـSidebar
    /// بيرجّع الـDashboard، والضغط على أي قسم تاني (لسه مالوش شاشة حقيقية
    /// مبنية) بيظهر مكانه رسالة عامة "لسه مش جاهزة" باسم القسم المضغوط.
    /// كل قسم جديد هيُبنى في خطوة منفصلة تالية هيستبدل هنا التحقق من
    /// اسمه بعرض شاشته الحقيقية بدل رسالة "مش جاهزة".
    /// </summary>
    public partial class WorkshopMainWindow : Window
    {
        public WorkshopMainWindow()
        {
            InitializeComponent();

            // [WPF-UI Integration — مهمة 2] App.xaml الإنتاجي مفيهوش دمج
            // WPF-UI (بقرار مقصود، راجع WPFUI_INTEGRATION_CHECKLIST.txt)،
            // فلازم نستدعي Apply هنا يدويًا عشان موارد الثيم (ThemesDictionary/
            // ControlsDictionary) تتفعّل فعليًا على النافذة دي بس.
            ApplicationThemeManager.Apply(this);

            Sidebar.SectionSelected += OnSidebarSectionSelected;
        }

        private void OnSidebarSectionSelected(string sectionName)
        {
            if (sectionName == "الرئيسية")
            {
                DashboardView.Visibility = Visibility.Visible;
                NotReadyView.Visibility = Visibility.Collapsed;
            }
            else
            {
                NotReadySectionNameText.Text = sectionName;
                DashboardView.Visibility = Visibility.Collapsed;
                NotReadyView.Visibility = Visibility.Visible;
            }
        }
    }
}
