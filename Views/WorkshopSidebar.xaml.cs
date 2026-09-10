using System;
using System.Windows;
using System.Windows.Controls;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// القائمة الجانبية الجديدة لبرنامج "الورشة" — يوزركنترول مستقل بعد.
    ///
    /// [تحديث] كل زرار تنقّل دلوقتي بيطلق SectionSelected بنص العنوان
    /// اللي ضغط عليه المستخدم (زي "العملاء"، "السيارات"...). المسؤولية
    /// عن "عمل إيه" بالضغطة دي (تبديل المحتوى) بقت على الطرف اللي بيستخدم
    /// الكنترول (WorkshopMainWindow حاليًا) — الـSidebar نفسه لسه ملوش
    /// أي فكرة عن الشاشات ولا محتواها، وده مقصود (فصل مسؤوليات).
    ///
    /// [تحديث] الشكل المرئي "Active" بقى بيتبدّل فعليًا وقت التشغيل:
    /// عند كل ضغطة، الزرار القديم اللي كان نشط بيرجع لـSidebarItemStyle
    /// العادي، والزرار الجديد المضغوط بياخد SidebarItemActiveStyle.
    /// "الرئيسية" (HomeButton) هي الحالة الابتدائية النشطة زي ما كانت.
    /// </summary>
    public partial class WorkshopSidebar : UserControl
    {
        /// <summary>يتطلق عند الضغط على أي قسم تنقّل، وبيبعت نص عنوان القسم.</summary>
        public event Action<string> SectionSelected;

        /// <summary>الزرار النشط حاليًا (اللي شكله مميّز بلون التمييز).</summary>
        private Button _activeButton;

        public WorkshopSidebar()
        {
            InitializeComponent();
            _activeButton = HomeButton;
        }

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string sectionName)
            {
                SetActiveButton(button);
                SectionSelected?.Invoke(sectionName);
            }
        }

        /// <summary>
        /// يبدّل شكل الزرار المضغوط لـ"نشط" ويرجّع الزرار القديم لشكله
        /// العادي. زرار تسجيل الخروج مش من ضمن أزرار الأقسام (Click بتاعه
        /// مش متربوط بـSidebarButton_Click)، فمش بيتأثر بالتبديل ده.
        /// </summary>
        private void SetActiveButton(Button newActiveButton)
        {
            if (_activeButton == newActiveButton)
            {
                return;
            }

            if (_activeButton != null)
            {
                _activeButton.Style = (Style)FindResource("SidebarItemStyle");
            }

            newActiveButton.Style = (Style)FindResource("SidebarItemActiveStyle");
            _activeButton = newActiveButton;
        }
    }
}

