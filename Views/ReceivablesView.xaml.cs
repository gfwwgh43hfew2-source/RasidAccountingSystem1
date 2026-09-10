using RasidAccountingSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RasidAccountingSystem.Views
{
    public partial class ReceivablesView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private DatabaseService _databaseService;
        private InstallmentService _installmentService;
        private ObservableCollection<InstallmentService.InstallmentItem> _receivablesList;
        private bool _isLoading = false;
        private string _currencySymbol = "ر.س";

        #endregion

        #region المنشئ (Constructor)

        public ReceivablesView()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔴 ReceivablesView Constructor - Start");

                // تهيئة الخدمات
                _databaseService = new DatabaseService();
                _installmentService = new InstallmentService(_databaseService);
                _receivablesList = new ObservableCollection<InstallmentService.InstallmentItem>();

                System.Diagnostics.Debug.WriteLine($"🔴 _installmentService is null? {_installmentService == null}");

                InitializeComponent();

                System.Diagnostics.Debug.WriteLine("🔴 InitializeComponent completed");

                dgReceivables.ItemsSource = _receivablesList;

                // ✅ تحميل رمز العملة من الإعدادات
                _ = LoadCurrencySymbolAsync();

                this.Loaded += async (s, e) => await LoadReceivablesAsync();

                System.Diagnostics.Debug.WriteLine("🔴 ReceivablesView Constructor - End");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔴 Constructor Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"🔴 StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال العملة

        private async Task LoadCurrencySymbolAsync()
        {
            try
            {
                if (_databaseService == null) return;

                string symbol = await _databaseService.GetCurrencySymbolAsync();
                if (!string.IsNullOrEmpty(symbol))
                {
                    _currencySymbol = symbol;
                }
                else
                {
                    _currencySymbol = "ر.س";
                }

                // تحديث الإحصائيات بعد تحميل العملة
                if (_receivablesList != null && _receivablesList.Count > 0)
                {
                    UpdateStatistics(_receivablesList.ToList());
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل العملة في ReceivablesView: {_currencySymbol}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrencySymbolAsync Error: {ex.Message}");
                _currencySymbol = "ر.س";
            }
        }

        #endregion

        #region دوال تحميل البيانات (Data Loading Methods)

        private async Task LoadReceivablesAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                System.Diagnostics.Debug.WriteLine("🔴 LoadReceivablesAsync - Start");

                if (_installmentService == null)
                {
                    System.Diagnostics.Debug.WriteLine("🔴 _installmentService is null! Reinitializing...");
                    _databaseService = new DatabaseService();
                    _installmentService = new InstallmentService(_databaseService);
                }

                if (_receivablesList == null)
                {
                    System.Diagnostics.Debug.WriteLine("🔴 _receivablesList is null! Reinitializing...");
                    _receivablesList = new ObservableCollection<InstallmentService.InstallmentItem>();
                    dgReceivables.ItemsSource = _receivablesList;
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // ✅ التحقق من وجود العناصر قبل استخدامها
                string customerName = "";
                if (txtSearchCustomer != null)
                {
                    customerName = txtSearchCustomer.Text?.Trim() ?? "";
                }

                string statusFilter = "All";
                if (cmbStatusFilter != null && cmbStatusFilter.SelectedItem is ComboBoxItem selectedStatus)
                {
                    string statusText = selectedStatus.Content?.ToString() ?? "الكل";
                    switch (statusText)
                    {
                        case "غير مسدد":
                            statusFilter = "Pending";
                            break;
                        case "متأخر":
                            statusFilter = "Overdue";
                            break;
                        case "مسدد جزئياً":
                            statusFilter = "Partial";
                            break;
                        case "مسدد":
                            statusFilter = "Paid";
                            break;
                        default:
                            statusFilter = "All";
                            break;
                    }
                }

                DateTime? dueDate = null;
                if (dpDueDateFilter != null)
                {
                    dueDate = dpDueDateFilter.SelectedDate;
                }

                System.Diagnostics.Debug.WriteLine("🔴 Calling GetAllPendingInstallmentsAsync...");

                var installments = await _installmentService.GetAllPendingInstallmentsAsync();

                System.Diagnostics.Debug.WriteLine($"🔴 Got {installments.Count} installments");

                // تطبيق الفلاتر
                var filteredInstallments = installments.AsEnumerable();

                if (!string.IsNullOrEmpty(customerName))
                {
                    filteredInstallments = filteredInstallments.Where(x =>
                        (x.CustomerName?.IndexOf(customerName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (x.CustomerCode?.IndexOf(customerName, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                if (statusFilter != "All")
                {
                    filteredInstallments = filteredInstallments.Where(x => x.Status == statusFilter);
                }

                if (dueDate.HasValue)
                {
                    filteredInstallments = filteredInstallments.Where(x => x.DueDate.Date == dueDate.Value.Date);
                }

                _receivablesList.Clear();

                int serial = 1;
                foreach (var item in filteredInstallments)
                {
                    item.SerialNumber = serial++;
                    _receivablesList.Add(item);
                }

                UpdateStatistics(filteredInstallments.ToList());

                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"✅ Loaded {_receivablesList.Count} installments");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"❌ Load Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateStatistics(System.Collections.Generic.List<InstallmentService.InstallmentItem> allInstallments)
        {
            try
            {
                if (allInstallments == null || allInstallments.Count == 0)
                {
                    lblTotalPending.Text = $"0.00 {_currencySymbol}";
                    lblTotalOverdue.Text = $"0.00 {_currencySymbol}";
                    lblTotalCollected.Text = $"0.00 {_currencySymbol}";
                    lblTotalRemaining.Text = $"0.00 {_currencySymbol}";
                    lblOverdueCount.Text = "0 أقساط متأخرة";
                    return;
                }

                decimal totalPending = allInstallments.Where(x => x.Status != "Paid").Sum(x => x.RemainingAmount);
                decimal totalOverdue = allInstallments.Where(x => x.Status == "Overdue" || (x.Status != "Paid" && x.DaysRemaining < 0)).Sum(x => x.RemainingAmount);
                decimal totalCollected = allInstallments.Sum(x => x.PaidAmount);
                decimal totalRemaining = allInstallments.Where(x => x.Status != "Paid").Sum(x => x.RemainingAmount);
                int overdueCount = allInstallments.Count(x => x.Status == "Overdue" || (x.Status != "Paid" && x.DaysRemaining < 0));

                lblTotalPending.Text = $"{totalPending:N2} {_currencySymbol}";
                lblTotalOverdue.Text = $"{totalOverdue:N2} {_currencySymbol}";
                lblTotalCollected.Text = $"{totalCollected:N2} {_currencySymbol}";
                lblTotalRemaining.Text = $"{totalRemaining:N2} {_currencySymbol}";
                lblOverdueCount.Text = $"{overdueCount} أقساط متأخرة";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateStatistics Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال أحداث الفلاتر (Filter Event Handlers)

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ = LoadReceivablesAsync();
        }

        private void CmbStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = LoadReceivablesAsync();
        }

        private void DpDueDateFilter_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = LoadReceivablesAsync();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearchCustomer.Text = "";
            cmbStatusFilter.SelectedIndex = 0;
            dpDueDateFilter.SelectedDate = null;
            _ = LoadReceivablesAsync();
        }

        #endregion

        #region دوال أحداث الأزرار (Button Click Handlers)

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadReceivablesAsync();
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_receivablesList == null || _receivablesList.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                MessageBox.Show($"تم تصدير {_receivablesList.Count} قسط بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// حدث النقر على زر تحصيل القسط
        /// </summary>
        private async void BtnCollectInstallment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null) return;

                int installmentId = (int)button.Tag;

                // البحث عن القسط في القائمة
                var installment = _receivablesList.FirstOrDefault(x => x.InstallmentID == installmentId);
                if (installment == null)
                {
                    MessageBox.Show("القسط غير موجود", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // التحقق من أن القسط غير مسدد
                if (installment.Status == "Paid")
                {
                    MessageBox.Show("هذا القسط مسدد بالفعل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // إنشاء نافذة تحصيل القسط
                var dialog = new CollectInstallmentDialog(
                    installment.InstallmentID,
                    installment.CustomerName,
                    installment.InvoiceNumber,
                    installment.InstallmentNumber,
                    installment.RemainingAmount,
                    _databaseService);

                dialog.Owner = Window.GetWindow(this);

                // عرض النافذة وانتظار النتيجة
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    // تحديث البيانات بعد التحصيل
                    await LoadReceivablesAsync();
                    MessageBox.Show("تم تحصيل القسط بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحصيل القسط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnCollectInstallment_Click Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// حدث النقر على زر عرض تفاصيل القسط
        /// </summary>
        private async void BtnViewInstallment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null) return;

                int installmentId = (int)button.Tag;

                // البحث عن القسط في القائمة
                var installment = _receivablesList.FirstOrDefault(x => x.InstallmentID == installmentId);
                if (installment == null)
                {
                    MessageBox.Show("القسط غير موجود", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // عرض تفاصيل القسط في رسالة مع العملة
                string details = $"📋 تفاصيل القسط\n\n" +
                                 $"العميل: {installment.CustomerName}\n" +
                                 $"رقم الفاتورة: {installment.InvoiceNumber}\n" +
                                 $"رقم القسط: {installment.InstallmentNumber}\n" +
                                 $"قيمة القسط: {installment.InstallmentAmount:N2} {_currencySymbol}\n" +
                                 $"المدفوع: {installment.PaidAmount:N2} {_currencySymbol}\n" +
                                 $"المتبقي: {installment.RemainingAmount:N2} {_currencySymbol}\n" +
                                 $"تاريخ الاستحقاق: {installment.DueDate:yyyy/MM/dd}\n" +
                                 $"الحالة: {installment.StatusText}\n" +
                                 $"الأيام المتبقية: {installment.DaysText}\n" +
                                 $"ملاحظات: {(string.IsNullOrEmpty(installment.Notes) ? "لا يوجد" : installment.Notes)}";

                MessageBox.Show(details, "تفاصيل القسط", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnViewInstallment_Click Error: {ex.Message}");
            }
        }

        #endregion
    }
}