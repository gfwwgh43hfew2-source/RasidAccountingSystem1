using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class InstallmentDialog : Window
    {
        #region المتغيرات

        private readonly DatabaseService _databaseService;
        private readonly int _customerId;
        private readonly string _customerName;
        private readonly string _invoiceNumber;
        private readonly decimal _totalAmount;
        private readonly DateTime _invoiceDate;
        private decimal _paidUpfront = 0;
        private string _currencySymbol = "ر.س";

        public ObservableCollection<InstallmentItem> Installments { get; set; }

        public class InstallmentItem : INotifyPropertyChanged
        {
            public int SerialNumber { get; set; }
            public decimal Amount { get; set; }
            public int DueDays { get; set; }
            public DateTime DueDate { get; set; }
            public string Notes { get; set; }

            public string FormattedAmount => Amount.ToString("N2");
            public string FormattedDueDate => DueDate.ToString("yyyy/MM/dd");

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public decimal TotalInstallmentAmount => Installments?.Sum(x => x.Amount) ?? 0;
        public int InstallmentCount => Installments?.Count ?? 0;

        #endregion

        #region المنشئ

        public InstallmentDialog(int customerId, string customerName, string invoiceNumber, decimal totalAmount, DatabaseService dbService, DateTime? invoiceDate = null)
        {
            InitializeComponent();

            _databaseService = dbService;
            _customerId = customerId;
            _customerName = customerName;
            _invoiceNumber = invoiceNumber;
            _totalAmount = totalAmount;
            // ✅ نستخدم تاريخ الفاتورة كأساس لحساب مواعيد استحقاق الأقساط
            // (وليس تاريخ اليوم الفعلي على الجهاز)
            _invoiceDate = invoiceDate ?? DateTime.Now;

            Installments = new ObservableCollection<InstallmentItem>();
            dgInstallments.ItemsSource = Installments;

            // عرض معلومات الفاتورة (سيتم تحديثه بعد تحميل العملة)
            txtInvoiceInfo.Text = $"الفاتورة: {invoiceNumber} | العميل: {customerName} | الإجمالي: {totalAmount:N2} {_currencySymbol}";
            txtTotalAmount.Text = $"{totalAmount:N2} {_currencySymbol}";
            txtRemaining.Text = $"{totalAmount:N2} {_currencySymbol}";

            UpdateCounts();

            // ✅ تحميل رمز العملة من الإعدادات (بعد التهيئة)
            _ = LoadCurrencySymbolAsync();
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
                // تحديث معلومات الفاتورة
                txtInvoiceInfo.Text = $"الفاتورة: {_invoiceNumber} | العميل: {_customerName} | الإجمالي: {_totalAmount:N2} {_currencySymbol}";
                txtTotalAmount.Text = $"{_totalAmount:N2} {_currencySymbol}";
                txtPaidUpfront.Text = $"{_paidUpfront:N2} {_currencySymbol}";

                // تحديث المبلغ المتبقي
                decimal remaining = _totalAmount - _paidUpfront - TotalInstallmentAmount;
                txtRemaining.Text = $"{remaining:N2} {_currencySymbol}";

                // تحديث إجمالي الأقساط
                txtTotalInstallments.Text = $" (الإجمالي: {TotalInstallmentAmount:N2} {_currencySymbol})";

                // تحديث عدد الأقساط
                txtInstallmentCount.Text = InstallmentCount.ToString();

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث العملة إلى: {_currencySymbol}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCurrencyDisplay Error: {ex.Message}");
            }
        }

        #endregion

        #region الأحداث

        private void BtnAddInstallment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(txtInstallmentAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("الرجاء إدخال قيمة قسط صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtDueDays.Text, out int dueDays) || dueDays <= 0)
                {
                    MessageBox.Show("الرجاء إدخال مدة قسط صحيحة (أيام)", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // التحقق من عدم تجاوز المبلغ المتبقي
                decimal remaining = _totalAmount - _paidUpfront - TotalInstallmentAmount;
                if (amount > remaining)
                {
                    MessageBox.Show($"المبلغ المتبقي هو {remaining:N2} {_currencySymbol} فقط", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime dueDate = _invoiceDate.AddDays(dueDays);

                Installments.Add(new InstallmentItem
                {
                    SerialNumber = Installments.Count + 1,
                    Amount = amount,
                    DueDays = dueDays,
                    DueDate = dueDate,
                    Notes = ""
                });

                // تحديث الحقول
                UpdateCounts();
                UpdateRemaining();

                // مسح الحقول
                txtInstallmentAmount.Text = "0.00";
                txtDueDays.Text = "30";

                System.Diagnostics.Debug.WriteLine($"✅ تم إضافة قسط بقيمة {amount} لمدة {dueDays} يوم");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnAddInstallment_Click Error: {ex.Message}");
            }
        }

        private void BtnAutoDistribute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // التحقق من وجود مبلغ متبقي
                decimal remaining = _totalAmount - _paidUpfront - TotalInstallmentAmount;
                if (remaining <= 0)
                {
                    MessageBox.Show("لا يوجد مبلغ متبقي للتوزيع", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // نافذة إدخال عدد الأقساط
                string input = GetUserInputImproved(
                    $"المبلغ المتبقي: {remaining:N2} {_currencySymbol}",
                    "توزيع تلقائي للأقساط",
                    "3"
                );

                if (string.IsNullOrEmpty(input)) return;

                if (!int.TryParse(input, out int count) || count <= 0)
                {
                    MessageBox.Show("الرجاء إدخال عدد صحيح موجب", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // منع عدد الأقساط الكبير جداً
                if (count > 100)
                {
                    MessageBox.Show("الحد الأقصى للأقساط هو 100 قسط", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                decimal installmentAmount = Math.Round(remaining / count, 2);
                decimal totalDistributed = 0;

                // مسح الأقساط الحالية
                Installments.Clear();

                for (int i = 0; i < count; i++)
                {
                    decimal amount = (i == count - 1) ? remaining - totalDistributed : installmentAmount;
                    totalDistributed += amount;

                    int dueDays = 30 * (i + 1); // كل 30 يوم

                    Installments.Add(new InstallmentItem
                    {
                        SerialNumber = i + 1,
                        Amount = amount,
                        DueDays = dueDays,
                        DueDate = _invoiceDate.AddDays(dueDays),
                        Notes = ""
                    });
                }

                UpdateCounts();
                UpdateRemaining();

                MessageBox.Show($"تم توزيع {remaining:N2} {_currencySymbol} على {count} أقساط", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Diagnostics.Debug.WriteLine($"✅ تم التوزيع التلقائي: {count} أقساط بقيمة {installmentAmount} لكل قسط");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnAutoDistribute_Click Error: {ex.Message}");
            }
        }

        // دالة محسنة لـ InputBox بحجم أكبر وتصميم أفضل
        private string GetUserInputImproved(string message, string title, string defaultValue)
        {
            var inputWindow = new Window
            {
                Title = title,
                Width = 480,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FlowDirection = FlowDirection.RightToLeft,
                WindowStyle = WindowStyle.SingleBorderWindow
            };

            var grid = new Grid();
            grid.Margin = new Thickness(25);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = message,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 16,
                Height = 42,
                Margin = new Thickness(0, 0, 0, 20),
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0)
            };
            textBox.Focus();
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var okButton = new Button
            {
                Content = "موافق",
                Width = 110,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 110,
                Height = 42,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            string result = null;

            okButton.Click += (s, ev) =>
            {
                result = textBox.Text;
                inputWindow.DialogResult = true;
                inputWindow.Close();
            };

            cancelButton.Click += (s, ev) =>
            {
                inputWindow.DialogResult = false;
                inputWindow.Close();
            };

            textBox.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Enter)
                {
                    result = textBox.Text;
                    inputWindow.DialogResult = true;
                    inputWindow.Close();
                }
                if (ev.Key == System.Windows.Input.Key.Escape)
                {
                    inputWindow.DialogResult = false;
                    inputWindow.Close();
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            inputWindow.Content = grid;
            inputWindow.Owner = this;

            bool? dialogResult = inputWindow.ShowDialog();

            return dialogResult == true ? result : null;
        }

        private void BtnRemoveInstallment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is InstallmentItem item)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"هل أنت متأكد من حذف القسط رقم {item.SerialNumber} بقيمة {item.FormattedAmount}؟",
                        "تأكيد الحذف",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Installments.Remove(item);
                        UpdateSerialNumbers();
                        UpdateCounts();
                        UpdateRemaining();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnRemoveInstallment_Click Error: {ex.Message}");
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // التحقق من أن مجموع الأقساط يساوي المبلغ المتبقي
                decimal remaining = _totalAmount - _paidUpfront;
                decimal totalInstallments = TotalInstallmentAmount;

                if (Installments.Count == 0)
                {
                    MessageBox.Show("الرجاء إضافة قسط واحد على الأقل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (Math.Abs(totalInstallments - remaining) > 0.01m)
                {
                    MessageBox.Show($"مجموع الأقساط ({totalInstallments:N2} {_currencySymbol}) لا يساوي المبلغ المتبقي ({remaining:N2} {_currencySymbol})",
                                    "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"BtnConfirm_Click Error: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region دوال مساعدة

        private void UpdateCounts()
        {
            txtInstallmentCount.Text = InstallmentCount.ToString();
            txtTotalInstallments.Text = $" (الإجمالي: {TotalInstallmentAmount:N2} {_currencySymbol})";
        }

        private void UpdateRemaining()
        {
            decimal remaining = _totalAmount - _paidUpfront - TotalInstallmentAmount;
            txtRemaining.Text = $"{remaining:N2} {_currencySymbol}";
            txtRemaining.Foreground = remaining >= 0 ?
                new SolidColorBrush(Color.FromRgb(215, 119, 6)) :
                new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }

        private void UpdateSerialNumbers()
        {
            for (int i = 0; i < Installments.Count; i++)
            {
                Installments[i].SerialNumber = i + 1;
            }
        }

        public decimal GetPaidUpfront() => _paidUpfront;

        public System.Collections.Generic.List<InstallmentService.InstallmentInput> GetInstallments()
        {
            return Installments.Select(x => new InstallmentService.InstallmentInput
            {
                Amount = x.Amount,
                DueDays = x.DueDays,
                Notes = x.Notes
            }).ToList();
        }

        #endregion
    }
}