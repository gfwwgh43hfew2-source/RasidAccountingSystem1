using System.Windows.Controls;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// شاشة "العملاء" الحقيقية الأولى في هيكل الورشة التجريبي (المهمة 3 من
    /// WORKSHOP_TASKS_CHECKLIST.txt). البيانات هنا Placeholder ثابت بالكامل —
    /// الربط الفعلي بقاعدة البيانات (عبر CustomerService الموجود بالفعل في
    /// المشروع الإنتاجي) هيتم في خطوة لاحقة منفصلة، بنفس فكرة المهمة 13
    /// الخاصة بربط أرقام الـDashboard.
    /// </summary>
    public partial class WorkshopCustomersView : UserControl
    {
        public WorkshopCustomersView()
        {
            InitializeComponent();
        }
    }
}
