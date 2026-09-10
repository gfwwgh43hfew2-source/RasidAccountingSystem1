using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// صندوق اقتراحات ذكي (بحث لحظي أثناء الكتابة) يُستخدم كبديل مباشر لعنصر ComboBox العادي
    /// في أي مكان بالبرنامج يحتوي على قائمة طويلة نسبياً (عملاء، موردين، منتجات، مخازن، حسابات...).
    ///
    /// نفس فكرة وسلوك حقل البحث عن العميل الموجود أصلاً في شاشة فاتورة المبيعات، لكن مبني هنا
    /// كعنصر تحكم عام قابل لإعادة الاستخدام في أي شاشة بسطرين فقط من XAML، بدلاً من إعادة كتابة
    /// نفس منطق TextBox + Popup + ListBox يدوياً في كل شاشة على حدة.
    ///
    /// طريقة الاستخدام (مطابقة تقريباً لاستخدام ComboBox العادي):
    ///   &lt;local:SmartSearchComboBox ItemsSource="{Binding Products}"
    ///                                DisplayMemberPath="Name"
    ///                                SelectedValuePath="Id"
    ///                                Placeholder="اختر منتج..."
    ///                                SelectionChanged="OnProductSelectionChanged"/&gt;
    /// </summary>
    public partial class SmartSearchComboBox : UserControl
    {
        #region الخصائص الاعتمادية (Dependency Properties)

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(SmartSearchComboBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
            nameof(DisplayMemberPath), typeof(string), typeof(SmartSearchComboBox), new PropertyMetadata(""));

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public static readonly DependencyProperty SelectedValuePathProperty = DependencyProperty.Register(
            nameof(SelectedValuePath), typeof(string), typeof(SmartSearchComboBox), new PropertyMetadata(""));

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SmartSearchComboBox),
            new PropertyMetadata("اكتب للبحث...", OnPlaceholderChanged));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(SmartSearchComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        /// <summary>
        /// القيمة المستخلصة من العنصر المحدد عبر SelectedValuePath، تماماً مثل ComboBox.SelectedValue
        /// </summary>
        public object SelectedValue => SelectedItem == null ? null : GetPropertyValue(SelectedItem, SelectedValuePath);

        public static readonly DependencyProperty IsEnabledSearchProperty = DependencyProperty.Register(
            nameof(IsReadOnlySelection), typeof(bool), typeof(SmartSearchComboBox), new PropertyMetadata(false));

        /// <summary>
        /// عند true، يُمنع المستخدم من الكتابة الحرة والاختيار فقط يكون من القائمة المعروضة
        /// (سلوك مطابق لـ IsEditable=false في ComboBox العادي). القيمة الافتراضية false تسمح
        /// بالبحث الحر أثناء الكتابة، وهو السلوك المطلوب في أغلب الاستخدامات.
        /// </summary>
        public bool IsReadOnlySelection
        {
            get => (bool)GetValue(IsEnabledSearchProperty);
            set => SetValue(IsEnabledSearchProperty, value);
        }

        #endregion

        #region الأحداث العامة (Public Events)

        /// <summary>
        /// يُطلَق عند تغيّر العنصر المحدد، تماماً مثل ComboBox.SelectionChanged، بحيث يمكن استبدال
        /// أي ComboBox بهذا العنصر دون تعديل منطق معالجة الحدث في الشاشات الموجودة.
        /// </summary>
        public event EventHandler SelectionChanged;

        #endregion

        #region المتغيرات الداخلية

        private List<object> _allItems = new List<object>();
        private bool _suppressTextChanged = false;
        private int _highlightedIndex = -1;

        #endregion

        public SmartSearchComboBox()
        {
            InitializeComponent();

            lblPlaceholder.Text = Placeholder;
            SetPlaceholderVisual();

            txtSearch.GotFocus += TxtSearch_GotFocus;
            txtSearch.TextChanged += TxtSearch_TextChanged;
            txtSearch.PreviewKeyDown += TxtSearch_PreviewKeyDown;
            txtSearch.LostFocus += TxtSearch_LostFocus;

            lstSuggestions.PreviewMouseLeftButtonUp += LstSuggestions_PreviewMouseLeftButtonUp;
            lstSuggestions.SelectionChanged += LstSuggestions_SelectionChanged;
        }

        #region ItemsSource / Placeholder Callbacks

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartSearchComboBox control)
            {
                control.RefreshItemsList();
            }
        }

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartSearchComboBox control)
            {
                control.lblPlaceholder.Text = e.NewValue?.ToString() ?? "";
                control.SetPlaceholderVisual();
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SmartSearchComboBox control)
            {
                control._suppressTextChanged = true;
                control.txtSearch.Text = e.NewValue == null ? "" : control.GetDisplayText(e.NewValue);
                control._suppressTextChanged = false;

                control.SetPlaceholderVisual();
                control.SelectionChanged?.Invoke(control, EventArgs.Empty);
            }
        }

        private void RefreshItemsList()
        {
            // ملاحظة: العنصر يأخذ "لقطة" (Snapshot) من القائمة وقت إسناد ItemsSource، وليس مربوطاً
            // بشكل حي بأحداث INotifyCollectionChanged. يعني لو أضفت/حذفت عنصراً من نفس القائمة
            // المربوطة بدون إعادة إسناد الخاصية ItemsSource من جديد، لن يظهر التغيير تلقائياً.
            // هذا يتوافق تماماً مع كل الاستخدامات الحالية في المشروع (كل شاشة تُعيد تحميل القائمة
            // بالكامل عبر ItemsSource = newList في كل مرة)، لكن يجب الانتباه له عند أي استخدام
            // مستقبلي يعتمد على التحديث الحي لعناصر نفس القائمة.
            _allItems = ItemsSource == null
                ? new List<object>()
                : ItemsSource.Cast<object>().ToList();

            if (SelectedItem == null) return;

            if (_allItems.Contains(SelectedItem))
            {
                // نفس المرجع (Reference) لسه موجود بالقائمة الجديدة، مفيش داعي لأي حاجة
                return;
            }

            // العنصر المحدد سابقاً هو نسخة (Object) مختلفة عن القائمة الجديدة (الحالة الشائعة: الشاشة
            // بتعمل ItemsSource = قائمة جديدة كل مرة بعد إعادة تحميل البيانات من قاعدة البيانات).
            // بدل ما نمسح الاختيار مباشرة، نحاول نلاقي العنصر "المطابق" في القائمة الجديدة عن طريق
            // قيمة SelectedValuePath (زي Id) لو متاحة، عشان الاختيار ميختفيش من غير سبب حقيقي.
            if (!string.IsNullOrEmpty(SelectedValuePath))
            {
                object oldValue = GetPropertyValue(SelectedItem, SelectedValuePath);
                if (oldValue != null)
                {
                    object match = _allItems.FirstOrDefault(item => Equals(GetPropertyValue(item, SelectedValuePath), oldValue));
                    if (match != null)
                    {
                        SelectedItem = match;
                        return;
                    }
                }
            }

            // العنصر فعلاً لم يعد موجوداً بالقائمة الجديدة (اتحذف مثلاً) → نمسح الاختيار فعلياً
            SelectedItem = null;
        }

        private void SetPlaceholderVisual()
        {
            lblPlaceholder.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region منطق البحث والعرض

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            ShowFilteredSuggestions(_suppressTextChanged ? "" : txtSearch.Text);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetPlaceholderVisual();

            if (_suppressTextChanged) return;

            ShowFilteredSuggestions(txtSearch.Text);
        }

        private void ShowFilteredSuggestions(string searchText)
        {
            List<object> filtered;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                filtered = _allItems;
            }
            else
            {
                filtered = _allItems
                    .Where(item => GetDisplayText(item).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            lstSuggestions.ItemsSource = filtered.Select(GetDisplayText).ToList();
            lstSuggestions.Tag = filtered; // نحتفظ بالعناصر الأصلية مرتبطة بنفس ترتيب النصوص المعروضة

            _highlightedIndex = -1;
            lstSuggestions.SelectedIndex = -1;

            bool hasResults = filtered.Count > 0;
            lstSuggestions.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
            lblNoResults.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;

            if (!suggestionsPopup.IsOpen)
            {
                suggestionsPopup.IsOpen = true;
            }
        }

        private void TxtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var currentList = lstSuggestions.Tag as List<object>;
            int count = currentList?.Count ?? 0;

            if (e.Key == Key.Down)
            {
                if (!suggestionsPopup.IsOpen)
                {
                    ShowFilteredSuggestions(txtSearch.Text);
                }
                else if (count > 0)
                {
                    _highlightedIndex = Math.Min(_highlightedIndex + 1, count - 1);
                    lstSuggestions.SelectedIndex = _highlightedIndex;
                    lstSuggestions.ScrollIntoView(lstSuggestions.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (count > 0)
                {
                    _highlightedIndex = Math.Max(_highlightedIndex - 1, 0);
                    lstSuggestions.SelectedIndex = _highlightedIndex;
                    lstSuggestions.ScrollIntoView(lstSuggestions.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (suggestionsPopup.IsOpen && _highlightedIndex >= 0 && _highlightedIndex < count)
                {
                    CommitSelection(currentList[_highlightedIndex]);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                suggestionsPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void LstSuggestions_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (lstSuggestions.SelectedItem == null) return;

            var currentList = lstSuggestions.Tag as List<object>;
            int index = lstSuggestions.SelectedIndex;

            if (currentList != null && index >= 0 && index < currentList.Count)
            {
                CommitSelection(currentList[index]);
            }
        }

        private void LstSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _highlightedIndex = lstSuggestions.SelectedIndex;
        }

        private void CommitSelection(object item)
        {
            SelectedItem = item; // سيؤدي هذا تلقائياً لتحديث النص وإغلاق القائمة عبر OnSelectedItemChanged
            suggestionsPopup.IsOpen = false;
            Keyboard.ClearFocus();
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            // تأخير بسيط حتى تُتاح الفرصة لحدث النقر على عنصر بالقائمة أن يُنفَّذ أولاً
            Dispatcher.BeginInvoke(new Action(() =>
            {
                suggestionsPopup.IsOpen = false;

                // لو المستخدم كتب نصاً حراً ولم يختر عنصراً حقيقياً منه، نرجع للعنصر المحدد سابقاً
                // (أو نص فارغ لو لم يكن هناك تحديد)، بنفس سلوك ComboBox العادي غير القابل للتحرير الحر
                string expectedText = SelectedItem == null ? "" : GetDisplayText(SelectedItem);

                if (txtSearch.Text != expectedText)
                {
                    _suppressTextChanged = true;
                    txtSearch.Text = expectedText;
                    _suppressTextChanged = false;
                }

                SetPlaceholderVisual();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion

        #region أدوات مساعدة عامة (Reflection Helpers)

        private string GetDisplayText(object item)
        {
            if (item == null) return "";

            if (string.IsNullOrEmpty(DisplayMemberPath))
            {
                return item.ToString();
            }

            object value = GetPropertyValue(item, DisplayMemberPath);
            return value?.ToString() ?? "";
        }

        private object GetPropertyValue(object item, string propertyPath)
        {
            if (item == null || string.IsNullOrEmpty(propertyPath)) return null;

            try
            {
                PropertyInfo prop = item.GetType().GetProperty(propertyPath, BindingFlags.Public | BindingFlags.Instance);
                return prop?.GetValue(item);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region دوال عامة إضافية (Public Helpers)

        /// <summary>
        /// إعادة ضبط التحديد الحالي (مسح الاختيار والنص)، مفيدة عند إعادة تعيين نموذج الإدخال
        /// </summary>
        public void ClearSelection()
        {
            SelectedItem = null;
        }

        /// <summary>
        /// تركيز فعلي على مربع البحث الداخلي (وليس على العنصر ككل)، حتى يتوافق سلوك استدعاء
        /// .Focus() هنا مع التوقع المعتاد من ComboBox العادي في باقي أنحاء المشروع.
        /// </summary>
        public new bool Focus()
        {
            return txtSearch.Focus();
        }

        #endregion
    }
}
