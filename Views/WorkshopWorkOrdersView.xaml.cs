using System.Windows.Controls;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// شاشة "أوامر الشغل" الحقيقية في هيكل الورشة التجريبي (المهمة 5 من
    /// WORKSHOP_TASKS_CHECKLIST.txt). البيانات هنا Placeholder ثابت بالكامل،
    /// وبتستخدم مجموعة شارات الحالة الملوّنة الكاملة الموجودة أصلًا في
    /// WorkshopTheme.xaml. الربط الفعلي بقاعدة البيانات هيتم في خطوة لاحقة
    /// منفصلة، بنفس نمط WorkshopCustomersView.xaml وWorkshopCarsView.xaml.
    /// </summary>
    public partial class WorkshopWorkOrdersView : UserControl
    {
        public WorkshopWorkOrdersView()
        {
            InitializeComponent();
        }
    }
}
