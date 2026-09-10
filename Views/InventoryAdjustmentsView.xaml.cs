using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// عرض التسويات الجردية
    /// </summary>
    public partial class InventoryAdjustmentsView : UserControl
    {
        #region المتغيرات الخاصة

        /// <summary>
        /// خدمة التسويات الجردية
        /// </summary>
        private readonly InventoryAdjustmentService _adjustmentService;

        /// <summary>
        /// قائمة جميع التسويات
        /// </summary>
        private List<AdjustmentItem> _allAdjustments;

        /// <summary>
        /// قائمة التسويات بعد تطبيق الفلاتر
        /// </summary>
        private List<AdjustmentItem> _filteredAdjustments;

        #endregion

        #region المنشئ

        /// <summary>
        /// تهيئة عرض التسويات الجردية
        /// </summary>
        public InventoryAdjustmentsView()
        {
            InitializeComponent();
            _adjustmentService = new InventoryAdjustmentService();
            _allAdjustments = new List<AdjustmentItem>();
            _filteredAdjustments = new List<AdjustmentItem>();

            this.Loaded += async (sender, eventArguments) =>
            {
                try
                {
                    // تعيين التواريخ الافتراضية
                    if (dpFromDate != null)
                    {
                        dpFromDate.SelectedDate = DateTime.Now.AddMonths(-1);
                    }

                    if (dpToDate != null)
                    {
                        dpToDate.SelectedDate = DateTime.Now;
                    }

                    await LoadAdjustmentsAsync();
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine($"InventoryAdjustmentsView_Loaded Error: {exception.Message}");
                    MessageBox.Show(
                        $"حدث خطأ أثناء تحميل الصفحة: {exception.Message}",
                        "خطأ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
        }

        #endregion

        #region دوال تحميل البيانات

        /// <summary>
        /// تحميل قائمة التسويات من قاعدة البيانات
        /// </summary>
        private async Task LoadAdjustmentsAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // ============================================================
                // 1️⃣ تحميل البيانات من الخدمة
                // ============================================================
                _allAdjustments = await _adjustmentService.GetAllAdjustmentsAsync();

                // ============================================================
                // 2️⃣ Debug: طباعة عدد التسويات المحملة
                // ============================================================
                System.Diagnostics.Debug.WriteLine($"📊 عدد التسويات المحملة من قاعدة البيانات: {_allAdjustments?.Count ?? 0}");

                if (_allAdjustments != null && _allAdjustments.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("📋 تفاصيل التسويات المحملة:");
                    foreach (var item in _allAdjustments)
                    {
                        System.Diagnostics.Debug.WriteLine($"   - {item.AdjustmentNumber} | الحالة: {item.Status} | التاريخ: {item.AdjustmentDate:yyyy-MM-dd} | المخزن: {item.WarehouseName}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ لا توجد تسويات محملة من قاعدة البيانات!");
                }

                // ============================================================
                // 3️⃣ ترقيم الصفوف
                // ============================================================
                int rowNumber = 1;
                foreach (AdjustmentItem item in _allAdjustments)
                {
                    item.RowNumber = rowNumber;
                    rowNumber = rowNumber + 1;
                }

                // ============================================================
                // 4️⃣ تحميل قائمة المخازن للفلتر
                // ============================================================
                LoadWarehousesFilter();

                // ============================================================
                // 5️⃣ تطبيق الفلاتر
                // ============================================================
                ApplyAllFilters();

                // ============================================================
                // 6️⃣ Debug: طباعة النتيجة النهائية
                // ============================================================
                System.Diagnostics.Debug.WriteLine($"✅ بعد تطبيق الفلاتر: {_filteredAdjustments?.Count ?? 0} تسوية ظاهرة");

                Mouse.OverrideCursor = null;
            }
            catch (Exception exception)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"❌ LoadAdjustmentsAsync Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack Trace: {exception.StackTrace}");
                MessageBox.Show(
                    $"حدث خطأ أثناء تحميل التسويات: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// تحميل قائمة المخازن في فلتر المخزن
        /// </summary>
        private void LoadWarehousesFilter()
        {
            try
            {
                if (_allAdjustments == null || _allAdjustments.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ لا توجد تسويات لتحميل المخازن منها");
                    return;
                }

                // استخراج أسماء المخازن الفريدة
                List<string> warehouses = _allAdjustments
                    .Where(x => x.WarehouseName != null)
                    .Select(x => x.WarehouseName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"🏢 عدد المخازن المستخرجة: {warehouses.Count}");

                // بناء قائمة كاملة تبدأ بـ "جميع المخازن" ثم كل مخزن مستخرج فعلياً
                var warehouseFilterOptions = new List<string> { "جميع المخازن" };

                foreach (string warehouse in warehouses)
                {
                    if (!string.IsNullOrEmpty(warehouse))
                    {
                        warehouseFilterOptions.Add(warehouse);
                        System.Diagnostics.Debug.WriteLine($"   - {warehouse}");
                    }
                }

                cmbWarehouseFilter.ItemsSource = warehouseFilterOptions;
                cmbWarehouseFilter.SelectedItem = warehouseFilterOptions[0];
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ LoadWarehousesFilter Error: {exception.Message}");
            }
        }

        #endregion

        #region دوال الفلتر

        /// <summary>
        /// تطبيق جميع الفلاتر على قائمة التسويات
        /// </summary>
        private void ApplyAllFilters()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 بدء ApplyAllFilters...");

                if (dgAdjustments == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ dgAdjustments == null");
                    return;
                }

                if (_allAdjustments == null || _allAdjustments.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ _allAdjustments فارغ أو null");
                    dgAdjustments.ItemsSource = null;
                    if (lblFilterResult != null)
                    {
                        lblFilterResult.Text = "لا توجد تسويات لعرضها";
                    }
                    if (lblRecordCount != null)
                    {
                        lblRecordCount.Text = "0 تسوية";
                    }
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 عدد التسويات قبل الفلتر: {_allAdjustments.Count}");

                IEnumerable<AdjustmentItem> filtered = _allAdjustments;

                // ============================================
                // فلتر البحث السريع
                // ============================================
                string searchText = txtSearch.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(searchText))
                {
                    string searchLower = searchText.ToLower();
                    filtered = filtered.Where(item =>
                        (item.AdjustmentNumber != null && item.AdjustmentNumber.ToLower().Contains(searchLower)) ||
                        (item.WarehouseName != null && item.WarehouseName.ToLower().Contains(searchLower)) ||
                        (item.Reason != null && item.Reason.ToLower().Contains(searchLower))
                    );
                    System.Diagnostics.Debug.WriteLine($"🔍 فلتر البحث '{searchText}': {filtered.Count()} نتيجة");
                }

                // ============================================
                // فلتر المخزن
                // ============================================
                if (cmbWarehouseFilter.SelectedItem != null)
                {
                    string selectedWarehouse = cmbWarehouseFilter.SelectedItem.ToString();
                    if (selectedWarehouse != "جميع المخازن" && !string.IsNullOrEmpty(selectedWarehouse))
                    {
                        filtered = filtered.Where(item =>
                            item.WarehouseName != null &&
                            item.WarehouseName == selectedWarehouse
                        );
                        System.Diagnostics.Debug.WriteLine($"🏢 فلتر المخزن '{selectedWarehouse}': {filtered.Count()} نتيجة");
                    }
                }

                // ============================================
                // ✅ فلتر الحالة - تم الإصلاح
                // ============================================
                if (cmbStatusFilter.SelectedItem != null)
                {
                    string selectedStatus = cmbStatusFilter.SelectedItem.ToString();

                    // ✅ فقط إذا لم يكن "جميع الحالات" نقوم بالتصفية
                    if (selectedStatus != "جميع الحالات" && selectedStatus != "System.Windows.Controls.ComboBoxItem: جميع الحالات")
                    {
                        string statusValue = string.Empty;
                        if (selectedStatus == "📋 قيد المراجعة" || selectedStatus.Contains("قيد المراجعة"))
                        {
                            statusValue = "Pending";
                        }
                        else if (selectedStatus == "✅ معتمدة" || selectedStatus.Contains("معتمدة"))
                        {
                            statusValue = "Approved";
                        }
                        else if (selectedStatus == "❌ مرفوضة" || selectedStatus.Contains("مرفوضة"))
                        {
                            statusValue = "Rejected";
                        }
                        else
                        {
                            statusValue = selectedStatus;
                        }

                        filtered = filtered.Where(item =>
                            item.Status != null &&
                            item.Status == statusValue
                        );
                        System.Diagnostics.Debug.WriteLine($"📋 فلتر الحالة '{selectedStatus}': {filtered.Count()} نتيجة");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"📋 فلتر الحالة: 'جميع الحالات' - تم تخطي التصفية");
                    }
                }

                // ============================================
                // فلتر التاريخ (من)
                // ============================================
                if (dpFromDate.SelectedDate.HasValue)
                {
                    DateTime fromDate = dpFromDate.SelectedDate.Value.Date;
                    filtered = filtered.Where(item => item.AdjustmentDate.Date >= fromDate);
                    System.Diagnostics.Debug.WriteLine($"📅 فلتر التاريخ (من) {fromDate:yyyy-MM-dd}: {filtered.Count()} نتيجة");
                }

                // ============================================
                // فلتر التاريخ (إلى)
                // ============================================
                if (dpToDate.SelectedDate.HasValue)
                {
                    DateTime toDate = dpToDate.SelectedDate.Value.Date.AddDays(1);
                    filtered = filtered.Where(item => item.AdjustmentDate.Date < toDate);
                    System.Diagnostics.Debug.WriteLine($"📅 فلتر التاريخ (إلى) {toDate:yyyy-MM-dd}: {filtered.Count()} نتيجة");
                }

                // ============================================
                // تطبيق النتائج
                // ============================================
                _filteredAdjustments = filtered.ToList();

                System.Diagnostics.Debug.WriteLine($"✅ النتيجة النهائية: {_filteredAdjustments.Count} تسوية");

                // تحديث الأرقام المسلسلة بعد الفلتر
                int rowNumber = 1;
                foreach (AdjustmentItem item in _filteredAdjustments)
                {
                    item.RowNumber = rowNumber;
                    rowNumber = rowNumber + 1;
                }

                // تحديث مصدر البيانات في DataGrid
                dgAdjustments.ItemsSource = _filteredAdjustments;

                // تحديث عدد النتائج في أعلى الصفحة
                int totalCount = _allAdjustments.Count;
                int filteredCount = _filteredAdjustments.Count;
                if (lblFilterResult != null)
                {
                    lblFilterResult.Text = $"عرض {filteredCount} من إجمالي {totalCount} تسوية";
                    System.Diagnostics.Debug.WriteLine($"📊 lblFilterResult: عرض {filteredCount} من إجمالي {totalCount} تسوية");
                }

                // تحديث عدد النتائج في شريط الحالة السفلي
                if (lblRecordCount != null)
                {
                    lblRecordCount.Text = $"{filteredCount} تسوية";
                }

                System.Diagnostics.Debug.WriteLine("✅ ApplyAllFilters انتهى بنجاح");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ApplyAllFilters Error: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ Stack Trace: {exception.StackTrace}");
                if (lblFilterResult != null)
                {
                    lblFilterResult.Text = "حدث خطأ في تطبيق الفلاتر";
                }
            }
        }

        #endregion

        #region أحداث الفلتر

        /// <summary>
        /// حدث تغيير نص البحث
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs eventArguments)
        {
            // لا نقوم بتطبيق الفلتر تلقائياً، المستخدم يضغط على زر تطبيق
        }

        /// <summary>
        /// حدث تغيير اختيار فلتر المخزن
        /// </summary>
        private void CmbWarehouseFilter_SelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
        {
            // لا نقوم بتطبيق الفلتر تلقائياً، المستخدم يضغط على زر تطبيق
        }

        /// <summary>
        /// حدث تغيير اختيار فلتر الحالة
        /// </summary>
        private void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs eventArguments)
        {
            // لا نقوم بتطبيق الفلتر تلقائياً، المستخدم يضغط على زر تطبيق
        }

        /// <summary>
        /// حدث تغيير التاريخ في DatePicker
        /// </summary>
        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs eventArguments)
        {
            // لا نقوم بتطبيق الفلتر تلقائياً، المستخدم يضغط على زر تطبيق
        }

        /// <summary>
        /// حدث الضغط على زر تطبيق الفلتر
        /// </summary>
        private void BtnApplyFilter_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 الضغط على زر تطبيق الفلتر");
                ApplyAllFilters();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BtnApplyFilter_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء تطبيق الفلاتر: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// حدث الضغط على زر مسح الفلاتر
        /// </summary>
        private void BtnClearFilters_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🧹 الضغط على زر مسح الفلاتر");

                // إعادة تعيين جميع الفلاتر
                txtSearch.Text = string.Empty;
                if (cmbWarehouseFilter.ItemsSource != null)
                {
                    var firstWarehouseOption = cmbWarehouseFilter.ItemsSource.Cast<object>().FirstOrDefault();
                    cmbWarehouseFilter.SelectedItem = firstWarehouseOption;
                }
                cmbStatusFilter.SelectedIndex = 0;
                dpFromDate.SelectedDate = null;
                dpToDate.SelectedDate = null;

                System.Diagnostics.Debug.WriteLine("🧹 تم مسح جميع الفلاتر");

                // تطبيق الفلاتر (الآن جميع الفلاتر فارغة)
                ApplyAllFilters();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BtnClearFilters_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء مسح الفلاتر: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region أحداث التحديث

        /// <summary>
        /// حدث الضغط على زر تحديث البيانات
        /// </summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 الضغط على زر تحديث");
                await LoadAdjustmentsAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BtnRefresh_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء تحديث البيانات: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region أحداث إضافة تسوية جديدة

        /// <summary>
        /// حدث الضغط على زر إضافة تسوية جديدة
        /// </summary>
        private async void BtnAddAdjustment_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("➕ الضغط على زر إضافة تسوية جديدة");

                InventoryAdjustmentDialog adjustmentDialog = new InventoryAdjustmentDialog();
                adjustmentDialog.Owner = Window.GetWindow(this);

                bool? dialogResult = adjustmentDialog.ShowDialog();

                if (dialogResult.HasValue && dialogResult.Value == true)
                {
                    System.Diagnostics.Debug.WriteLine("✅ تم حفظ التسوية، جاري تحديث القائمة");
                    await LoadAdjustmentsAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("❌ تم إلغاء التسوية");
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BtnAddAdjustment_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"خطأ في فتح نافذة التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region أحداث الاعتماد والرفض والعرض

        /// <summary>
        /// حدث الضغط على زر اعتماد التسوية
        /// </summary>
        private async void BtnApprove_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                Button approveButton = sender as Button;

                if (approveButton == null)
                {
                    return;
                }

                int adjustmentId = (int)approveButton.Tag;

                System.Diagnostics.Debug.WriteLine($"✅ الضغط على زر اعتماد التسوية ID: {adjustmentId}");

                MessageBoxResult confirmationResult = MessageBox.Show(
                    "هل أنت متأكد من رغبتك في اعتماد هذه التسوية الجردية؟\n\n" +
                    "سيتم تحديث أرصدة المخازن تلقائياً بعد الاعتماد.",
                    "تأكيد الاعتماد",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmationResult != MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("❌ تم إلغاء اعتماد التسوية");
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;

                bool isSuccess = await _adjustmentService.ApproveAdjustmentAsync(adjustmentId, LoginView.CurrentUserId);

                Mouse.OverrideCursor = null;

                if (isSuccess == true)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ تم اعتماد التسوية ID: {adjustmentId} بنجاح");
                    MessageBox.Show(
                        "تم اعتماد التسوية الجردية بنجاح.\n\n" +
                        "تم تحديث أرصدة المخازن تلقائياً.",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await LoadAdjustmentsAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ فشل اعتماد التسوية ID: {adjustmentId}");
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
                System.Diagnostics.Debug.WriteLine($"❌ BtnApprove_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء اعتماد التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// حدث الضغط على زر رفض التسوية
        /// </summary>
        private async void BtnReject_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                Button rejectButton = sender as Button;

                if (rejectButton == null)
                {
                    return;
                }

                int adjustmentId = (int)rejectButton.Tag;

                System.Diagnostics.Debug.WriteLine($"❌ الضغط على زر رفض التسوية ID: {adjustmentId}");

                MessageBoxResult confirmationResult = MessageBox.Show(
                    "هل أنت متأكد من رغبتك في رفض هذه التسوية الجردية؟\n\n" +
                    "لن يتم تحديث أرصدة المخازن، وستظل كما هي.",
                    "تأكيد الرفض",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmationResult != MessageBoxResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine("❌ تم إلغاء رفض التسوية");
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;

                bool isSuccess = await _adjustmentService.RejectAdjustmentAsync(adjustmentId, LoginView.CurrentUserId);

                Mouse.OverrideCursor = null;

                if (isSuccess == true)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ تم رفض التسوية ID: {adjustmentId} بنجاح");
                    MessageBox.Show(
                        "تم رفض التسوية الجردية بنجاح.\n\n" +
                        "لم يتم تحديث أرصدة المخازن.",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await LoadAdjustmentsAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ فشل رفض التسوية ID: {adjustmentId}");
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
                System.Diagnostics.Debug.WriteLine($"❌ BtnReject_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء رفض التسوية: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// حدث الضغط على زر عرض التفاصيل
        /// </summary>
        private async void BtnView_Click(object sender, RoutedEventArgs eventArguments)
        {
            try
            {
                Button viewButton = sender as Button;

                if (viewButton == null)
                {
                    return;
                }

                int adjustmentId = (int)viewButton.Tag;

                System.Diagnostics.Debug.WriteLine($"👁 الضغط على زر عرض التفاصيل ID: {adjustmentId}");

                InventoryAdjustmentDetailWindow detailWindow = new InventoryAdjustmentDetailWindow(adjustmentId);
                detailWindow.Owner = Window.GetWindow(this);

                bool? dialogResult = detailWindow.ShowDialog();

                if (dialogResult.HasValue && dialogResult.Value == true)
                {
                    System.Diagnostics.Debug.WriteLine("✅ تم تحديث البيانات بعد عرض التفاصيل");
                    await LoadAdjustmentsAsync();
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"❌ BtnView_Click Error: {exception.Message}");
                MessageBox.Show(
                    $"حدث خطأ أثناء عرض التفاصيل: {exception.Message}",
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion
    }
}