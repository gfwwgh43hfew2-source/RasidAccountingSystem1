using System.Windows.Controls;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// شاشة "استلام سيارة" في هيكل الورشة التجريبي (المهمة 6 من
    /// WORKSHOP_TASKS_CHECKLIST.txt). فورم Placeholder بالكامل — كل الحقول
    /// نصوص ثابتة قابلة للتعديل شكليًا فقط (بدون حفظ فعلي أو Binding)،
    /// وخيارات فحص الحالة الخارجية (RadioButton لكل جانب من السيارة) شغالة
    /// بصريًا لأنها عناصر WPF عادية، لكن قيمتها مش متخزّنة في أي مكان بعد.
    /// الربط الفعلي بـCustomerService/CarService والحفظ الحقيقي في قاعدة
    /// البيانات هيتم في خطوة لاحقة منفصلة.
    /// </summary>
    public partial class WorkshopCarIntakeView : UserControl
    {
        public WorkshopCarIntakeView()
        {
            InitializeComponent();
        }
    }
}
