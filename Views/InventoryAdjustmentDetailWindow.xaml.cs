using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class InventoryAdjustmentDetailWindow : Window
    {
        #region المتغيرات الخاصة

        private readonly int _adjustmentId;
        private readonly InventoryAdjustmentService _adjustmentService;
        private AdjustmentDetail _adjustmentData;

        #endregion

        #region المنشئ

        public InventoryAdjustmentDetailWindow(int adjustmentId)
        {
            InitializeComponent();
            _adjustmentId = adjustmentId;
            _adjustmentService = new InventoryAdjustmentService();

            this.Loaded += async (sender, eventArguments) => await LoadAdjustmentDetailsAsync();
        }

        #endregion

        #region دوال تحميل البيانات

        private async Task LoadAdjustmentDetailsAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                _adjustmentData = await _adjustmentService.GetAdjustmentByIdAsync(_adjustmentId);

                if (_adjustmentData == null)
                {
                    MessageBox.Show(
                        "لم يتم العثور على التسوية المطلوبة.\n\n" +
                        "قد تكون تم حذفها أو أن رقمها غير صحيح.",
                        "خطأ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Close();
                    return;
                }

                lblAdjustmentNumber.Text = $"رقم: {_adjustmentData.AdjustmentNumber}";
                lblWarehouse.Text = _adjustmentData.WarehouseName;
                lblDate.Text = _adjustmentData.AdjustmentDate.ToString("yyyy-MM-dd HH:mm");

                UpdateStatusDisplay();

                if (_adjustmentData.Items != null && _adjustmentData.Items.Count > 0)
                {
                    dgItems.ItemsSource = _adjustmentData.Items;

                    // ✅ عرض عدد الأصناف مع تفاصيل الوحدات
                    int totalItems = _adjustmentData.Items.Count;
                    int itemsWithUnits = _adjustmentData.Items.Count(i => !string.IsNullOrEmpty(i.UnitName) && i.UnitName != "وحدة");

                    if (itemsWithUnits > 0)
                    {
                        lblItemsCount.Text = $"عدد الأصناف: {totalItems} (منها {itemsWithUnits} بوحدات متعددة)";
                    }
                    else
                    {
                        lblItemsCount.Text = $"عدد الأصناف: {totalItems}";
                    }
                }
                else
                {
                    lblItemsCount.Text = "عدد الأصناف: 0";
                }

                bool isPending = _adjustmentData.Status == "Pending";
                btnApprove.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;
                btnReject.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;

                Mouse.OverrideCursor = null;
            }
            catch (Exception exception)
            {
                Mouse.OverrideCursor = null;

                MessageBox.Show(
                    $"حدث خطأ أثناء تحميل تفاصيل التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateStatusDisplay()
        {
            switch (_adjustmentData.Status)
            {
                case "Pending":
                    StatusBorder.Background = (Brush)FindResource("WarningColor");
                    StatusBorder.Opacity = 0.15;
                    lblStatus.Text = "📋 قيد المراجعة";
                    lblStatus.Foreground = (Brush)FindResource("WarningColor");
                    break;

                case "Approved":
                    StatusBorder.Background = (Brush)FindResource("SuccessColor");
                    StatusBorder.Opacity = 0.15;
                    lblStatus.Text = "✅ معتمدة";
                    lblStatus.Foreground = (Brush)FindResource("SuccessColor");
                    break;

                case "Rejected":
                    StatusBorder.Background = (Brush)FindResource("DangerColor");
                    StatusBorder.Opacity = 0.15;
                    lblStatus.Text = "❌ مرفوضة";
                    lblStatus.Foreground = (Brush)FindResource("DangerColor");
                    break;

                default:
                    StatusBorder.Background = (Brush)FindResource("TextSecondary");
                    StatusBorder.Opacity = 0.15;
                    lblStatus.Text = "⚠️ غير معروفة";
                    lblStatus.Foreground = (Brush)FindResource("TextSecondary");
                    break;
            }
        }

        #endregion

        #region أحداث الأزرار

        private async void BtnApprove_Click(object sender, RoutedEventArgs eventArguments)
        {
            MessageBoxResult confirmationResult = MessageBox.Show(
                "هل أنت متأكد من رغبتك في اعتماد هذه التسوية الجردية؟\n\n" +
                "سيتم تحديث أرصدة المخازن تلقائياً بعد الاعتماد.\n" +
                "هذا الإجراء لا يمكن التراجع عنه.",
                "تأكيد الاعتماد",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmationResult != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                btnApprove.IsEnabled = false;
                btnReject.IsEnabled = false;

                bool isSuccess = await _adjustmentService.ApproveAdjustmentAsync(_adjustmentId, LoginView.CurrentUserId);

                Mouse.OverrideCursor = null;
                btnApprove.IsEnabled = true;
                btnReject.IsEnabled = true;

                if (isSuccess)
                {
                    MessageBox.Show(
                        "تم اعتماد التسوية الجردية بنجاح.\n\n" +
                        "تم تحديث أرصدة المخازن تلقائياً.",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "حدث خطأ أثناء محاولة اعتماد التسوية.\n\n" +
                        "يرجى التأكد من أن التسوية لا تزال في حالة 'قيد المراجعة'.",
                        "خطأ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception exception)
            {
                Mouse.OverrideCursor = null;
                btnApprove.IsEnabled = true;
                btnReject.IsEnabled = true;

                MessageBox.Show(
                    $"حدث خطأ أثناء اعتماد التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void BtnReject_Click(object sender, RoutedEventArgs eventArguments)
        {
            MessageBoxResult confirmationResult = MessageBox.Show(
                "هل أنت متأكد من رغبتك في رفض هذه التسوية الجردية؟\n\n" +
                "لن يتم تحديث أرصدة المخازن، وستظل كما هي.\n" +
                "هذا الإجراء لا يمكن التراجع عنه.",
                "تأكيد الرفض",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmationResult != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                btnApprove.IsEnabled = false;
                btnReject.IsEnabled = false;

                bool isSuccess = await _adjustmentService.RejectAdjustmentAsync(_adjustmentId, LoginView.CurrentUserId);

                Mouse.OverrideCursor = null;
                btnApprove.IsEnabled = true;
                btnReject.IsEnabled = true;

                if (isSuccess)
                {
                    MessageBox.Show(
                        "تم رفض التسوية الجردية بنجاح.\n\n" +
                        "لم يتم تحديث أرصدة المخازن.",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "حدث خطأ أثناء محاولة رفض التسوية.\n\n" +
                        "يرجى التأكد من أن التسوية لا تزال في حالة 'قيد المراجعة'.",
                        "خطأ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception exception)
            {
                Mouse.OverrideCursor = null;
                btnApprove.IsEnabled = true;
                btnReject.IsEnabled = true;

                MessageBox.Show(
                    $"حدث خطأ أثناء رفض التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs eventArguments)
        {
            DialogResult = false;
            Close();
        }

        #endregion
    }
}