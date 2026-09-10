using RasidAccountingSystem.Services;
using RasidAccountingSystem.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RasidAccountingSystem.Views
{
    public partial class CollectInstallmentDialog : Window
    {
        #region Private Fields

        private readonly DatabaseService _databaseService;
        private readonly int _installmentId;
        private readonly decimal _remainingAmount;
        private readonly string _customerName;
        private readonly string _invoiceNumber;
        private readonly int _installmentNumber;
        private string _currencySymbol = "ر.س";

        #endregion

        #region Constructor

        public CollectInstallmentDialog(int installmentId, string customerName, string invoiceNumber, int installmentNumber, decimal remainingAmount, DatabaseService dbService)
        {
            try
            {
                InitializeComponent();

                _databaseService = dbService;
                _installmentId = installmentId;
                _remainingAmount = remainingAmount;
                _customerName = customerName;
                _invoiceNumber = invoiceNumber;
                _installmentNumber = installmentNumber;

                // عرض معلومات القسط (سيتم تحديثه بعد تحميل العملة)
                txtInvoiceInfo.Text = $"العميل: {customerName} | الفاتورة: {invoiceNumber} | القسط: {installmentNumber}";
                lblRemainingAmount.Text = $"{remainingAmount:N2} {_currencySymbol}";
                txtAmount.Text = remainingAmount.ToString("N2");

                // تحميل الخزائن عند فتح النافذة
                _ = LoadTreasuriesAsync();

                // تحميل رمز العملة من الإعدادات
                _ = LoadCurrencySymbolAsync();

                // تعيين حدث KeyDown لحقل المبلغ للسماح بالضغط على Enter
                txtAmount.KeyDown += TxtAmount_KeyDown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CollectInstallmentDialog Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة نافذة تحصيل القسط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // تحديث جميع النصوص التي تعرض العملة
                UpdateCurrencyDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrencySymbolAsync Error: {ex.Message}");
                _currencySymbol = "ر.س";
            }
        }

        private void UpdateCurrencyDisplay()
        {
            try
            {
                // تحديث المبلغ المتبقي
                lblRemainingAmount.Text = $"{_remainingAmount:N2} {_currencySymbol}";

                // تحديث نص معلومات العميل (إذا كان يحتوي على عملة)
                txtInvoiceInfo.Text = $"العميل: {_customerName} | الفاتورة: {_invoiceNumber} | القسط: {_installmentNumber}";

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث العملة في CollectInstallmentDialog إلى: {_currencySymbol}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCurrencyDisplay Error: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void TxtAmount_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    BtnConfirm_Click(sender, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TxtAmount_KeyDown Error: {ex.Message}");
            }
        }

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // التحقق من صحة المبلغ
                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("الرجاء إدخال مبلغ صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtAmount.Focus();
                    txtAmount.SelectAll();
                    return;
                }

                if (amount > _remainingAmount)
                {
                    MessageBox.Show($"المبلغ المدفوع ({amount:N2} {_currencySymbol}) أكبر من المبلغ المتبقي ({_remainingAmount:N2} {_currencySymbol})", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtAmount.Focus();
                    txtAmount.SelectAll();
                    return;
                }

                // التحقق من اختيار الخزينة
                if (cmbTreasury.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cmbTreasury.Focus();
                    return;
                }

                // التحقق من اختيار طريقة الدفع
                if (cmbPaymentMethod.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار طريقة الدفع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    cmbPaymentMethod.Focus();
                    return;
                }

                // الحصول على معرف الخزينة
                int treasuryId = 0;
                var selectedTreasury = cmbTreasury.SelectedItem as TreasuryBasicItem;
                if (selectedTreasury != null)
                {
                    treasuryId = selectedTreasury.Id;
                }
                else
                {
                    // محاولة الحصول على المعرف من خلال SelectedValue
                    try
                    {
                        treasuryId = (int)cmbTreasury.SelectedValue;
                    }
                    catch
                    {
                        MessageBox.Show("خطأ في قراءة بيانات الخزينة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (treasuryId <= 0)
                {
                    MessageBox.Show("معرف الخزينة غير صالح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // الحصول على طريقة الدفع
                string paymentMethod = ((ComboBoxItem)cmbPaymentMethod.SelectedItem).Content.ToString();

                // تحويل طريقة الدفع إلى الإنجليزية
                string paymentMethodEnglish;
                switch (paymentMethod)
                {
                    case "نقدي":
                        paymentMethodEnglish = "Cash";
                        break;
                    case "تحويل بنكي":
                        paymentMethodEnglish = "Transfer";
                        break;
                    case "شيك":
                        paymentMethodEnglish = "Check";
                        break;
                    default:
                        paymentMethodEnglish = "Cash";
                        break;
                }

                int userId = LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1;

                // إنشاء خدمة الأقساط وتنفيذ عملية التحصيل
                var installmentService = new InstallmentService(_databaseService);

                bool result = await installmentService.CollectInstallmentAsync(
                    _installmentId,
                    amount,
                    DateTime.Now,
                    paymentMethodEnglish,
                    treasuryId,
                    userId,
                    txtNotes.Text
                );

                if (result)
                {
                    // ✅ عرض رسالة نجاح
                    MessageBox.Show($"تم تحصيل مبلغ {amount:N2} بنجاح", "تم التحصيل", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ✅ تحديث تنبيهات الأقساط فوراً بعد التحصيل
                    await Helpers.NotificationHelper.RefreshInstallmentNotificationsAsync();

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("حدث خطأ أثناء تحصيل القسط", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحصيل القسط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnConfirm_Click Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnCancel_Click Error: {ex.Message}");
                DialogResult = false;
                Close();
            }
        }

        #endregion

        #region Data Loading Methods

        private async Task LoadTreasuriesAsync()
        {
            try
            {
                // استخدام DatabaseExecutor لتحميل الخزائن مع إعادة المحاولة التلقائية
                var treasuries = await DatabaseExecutor.ExecuteNonTransactionAsync<List<TreasuryBasicItem>>(_databaseService, async (connection) =>
                {
                    string sql = "SELECT TreasuryID, TreasuryCode, TreasuryNameAr, CurrentBalance FROM Treasury WHERE IsActive = 1 ORDER BY TreasuryNameAr";
                    var result = new List<TreasuryBasicItem>();

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new TreasuryBasicItem
                            {
                                Id = reader.GetInt32(0),
                                Code = reader.GetString(1),
                                Name = reader.GetString(2),
                                CurrentBalance = reader.GetDecimal(3)
                            });
                        }
                    }
                    return result;
                }, CancellationToken.None, 3);

                // تعيين مصدر البيانات للـ ComboBox
                cmbTreasury.ItemsSource = treasuries;

                // اختيار أول خزينة بشكل افتراضي
                if (treasuries.Count > 0)
                {
                    cmbTreasury.SelectedItem = treasuries[0];
                }
                else
                {
                    MessageBox.Show("لا توجد خزينة نشطة في النظام.\nالرجاء إنشاء خزينة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                System.Diagnostics.Debug.WriteLine($"تم تحميل {treasuries.Count} خزينة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuriesAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل الخزائن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    #region TreasuryBasicItem Class

    /// <summary>
    /// كلاس بيانات أساسية للخزينة - نسخة مكررة للاستخدام في هذا الديالوج
    /// </summary>
    public class TreasuryBasicItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal CurrentBalance { get; set; }

        public override string ToString()
        {
            return $"{Name} (الرصيد: {CurrentBalance:N2} {CurrencyHelper.GetCurrencySymbol()})";
        }
    }

    #endregion
}