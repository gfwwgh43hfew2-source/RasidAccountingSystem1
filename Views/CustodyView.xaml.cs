using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// صفحة إدارة العهد - تحتوي على عرض العهود وإضافة عمليات الصرف والتسوية
    /// </summary>
    public partial class CustodyView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private string _connectionString;
        private ObservableCollection<EmployeeItem> _employeesList;

        #endregion

        #region الخصائص العامة

        /// <summary>
        /// قائمة العهود المعروضة في البطاقات
        /// </summary>
        public ObservableCollection<CustodyCardItem> CustodyList { get; set; }

        /// <summary>
        /// قائمة عمليات العهدة المعروضة في الجدول
        /// </summary>
        public ObservableCollection<CustodyTransactionItem> TransactionsList { get; set; }

        private int _selectedCustodyId = 0;

        #endregion

        #region المنشئ

        public CustodyView()
        {
            try
            {
                InitializeComponent();

                _dbService = new DatabaseService();
                _connectionString = _dbService.GetConnectionString();

                CustodyList = new ObservableCollection<CustodyCardItem>();
                TransactionsList = new ObservableCollection<CustodyTransactionItem>();
                _employeesList = new ObservableCollection<EmployeeItem>();

                icCustodyList.ItemsSource = CustodyList;
                dgTransactions.ItemsSource = TransactionsList;

                LoadCustodyData();
                LoadCustodyFilter();

                // ربط الأحداث
                btnAddCustody.Click += BtnAddCustody_Click;
                btnAddExpense.Click += BtnAddExpense_Click;
                btnSettle.Click += BtnSettle_Click;
                btnRefresh.Click += (s, e) => { LoadCustodyData(); LoadTransactions(_selectedCustodyId); };

                cmbStatusFilter.SelectionChanged += (s, e) => LoadCustodyData();
                cmbEmployeeFilter.SelectionChanged += (s, e) => LoadCustodyData();

                // تحميل قائمة الموظفين للفلتر
                _ = LoadEmployeesFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CustodyView Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة صفحة العهد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال تحميل البيانات

        private async void LoadCustodyData()
        {
            try
            {
                CustodyList.Clear();

                string statusFilter = "All";
                if (cmbStatusFilter.SelectedItem is ComboBoxItem selectedStatus && selectedStatus.Tag != null)
                {
                    statusFilter = selectedStatus.Tag.ToString();
                }

                int employeeId = 0;
                if (cmbEmployeeFilter.SelectedItem is ComboBoxItem selectedEmployee && selectedEmployee.Tag is int id)
                {
                    employeeId = id;
                }

                var custodyItems = await _dbService.GetCustodyListAsync(statusFilter, employeeId);

                int totalCount = 0;
                decimal totalAmount = 0;
                decimal totalRemaining = 0;
                decimal totalSettled = 0;
                int activeCount = 0;

                foreach (var item in custodyItems)
                {
                    CustodyList.Add(new CustodyCardItem
                    {
                        Id = item.CustodyID,
                        Number = item.CustodyNumber,
                        EmployeeName = item.EmployeeName,
                        EmployeeCode = item.EmployeeCode,
                        Amount = item.Amount,
                        RemainingAmount = item.RemainingAmount,
                        Status = item.Status,
                        StatusColor = item.StatusColor,
                        StatusText = item.StatusText,
                        CustodyDate = item.CustodyDate
                    });

                    totalCount++;
                    totalAmount += item.Amount;
                    totalRemaining += item.RemainingAmount;
                    totalSettled += item.SettledAmount;
                    if (item.Status == "Active") activeCount++;
                }

                // تحديث الإحصائيات
                var lblTotalCustodyControl = FindName("lblTotalCustody") as TextBlock;
                var lblTotalRemainingControl = FindName("lblTotalRemaining") as TextBlock;
                var lblTotalSettledControl = FindName("lblTotalSettled") as TextBlock;
                var lblActiveCountControl = FindName("lblActiveCount") as TextBlock;
                var lblHeaderTotalCountControl = FindName("lblHeaderTotalCount") as TextBlock;

                if (lblTotalCustodyControl != null) lblTotalCustodyControl.Text = totalAmount.ToString("N2");
                if (lblTotalRemainingControl != null) lblTotalRemainingControl.Text = totalRemaining.ToString("N2");
                if (lblTotalSettledControl != null) lblTotalSettledControl.Text = totalSettled.ToString("N2");
                if (lblActiveCountControl != null) lblActiveCountControl.Text = activeCount.ToString();
                if (lblHeaderTotalCountControl != null) lblHeaderTotalCountControl.Text = totalCount.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading custody data: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل بيانات العهد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadEmployeesFilter()
        {
            try
            {
                var cmbEmployeeFilterControl = FindName("cmbEmployeeFilter") as SmartSearchComboBox;
                if (cmbEmployeeFilterControl == null) return;

                var employees = await _dbService.GetEmployeesAsync();
                _employeesList.Clear();

                var employeeFilterItems = new List<ComboBoxItem>();
                employeeFilterItems.Add(new ComboBoxItem { Content = "جميع الموظفين", Tag = 0 });

                foreach (var emp in employees)
                {
                    _employeesList.Add(emp);
                    employeeFilterItems.Add(new ComboBoxItem { Content = emp.DisplayName, Tag = emp.EmployeeID });
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    cmbEmployeeFilterControl.ItemsSource = employeeFilterItems;
                });

                if (employeeFilterItems.Count > 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        cmbEmployeeFilterControl.SelectedItem = employeeFilterItems[0];
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading employees: {ex.Message}");
            }
        }

        private void LoadCustodyFilter()
        {
            try
            {
                var cmbStatusFilterControl = FindName("cmbStatusFilter") as ComboBox;
                if (cmbStatusFilterControl == null) return;

                cmbStatusFilterControl.Items.Clear();
                cmbStatusFilterControl.Items.Add(new ComboBoxItem { Content = "الجميع", Tag = "All" });
                cmbStatusFilterControl.Items.Add(new ComboBoxItem { Content = "نشطة", Tag = "Active" });
                cmbStatusFilterControl.Items.Add(new ComboBoxItem { Content = "مسددة", Tag = "Settled" });
                cmbStatusFilterControl.Items.Add(new ComboBoxItem { Content = "ملغية", Tag = "Cancelled" });
                cmbStatusFilterControl.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCustodyFilter Error: {ex.Message}");
            }
        }

        private async void LoadTransactions(int custodyId)
        {
            try
            {
                _selectedCustodyId = custodyId;
                TransactionsList.Clear();

                var dgTransactionsControl = FindName("dgTransactions") as DataGrid;
                var lblNoDataControl = FindName("lblNoData") as TextBlock;

                if (custodyId > 0)
                {
                    var transactions = await _dbService.GetCustodyTransactionsAsync(custodyId);
                    foreach (var trans in transactions)
                    {
                        TransactionsList.Add(trans);
                    }

                    if (dgTransactionsControl != null)
                        dgTransactionsControl.Visibility = TransactionsList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    if (lblNoDataControl != null)
                        lblNoDataControl.Visibility = TransactionsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    if (dgTransactionsControl != null) dgTransactionsControl.Visibility = Visibility.Collapsed;
                    if (lblNoDataControl != null) lblNoDataControl.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading transactions: {ex.Message}");
            }
        }

        #endregion

        #region دوال عرض النوافذ المنبثقة

        private void ShowDialog(Window dialog)
        {
            try
            {
                var mainWindow = Window.GetWindow(this);
                if (mainWindow != null)
                {
                    var blurEffect = new BlurEffect { Radius = 8, KernelType = KernelType.Gaussian };
                    mainWindow.Effect = blurEffect;
                    mainWindow.IsEnabled = false;

                    dialog.Owner = mainWindow;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.ShowDialog();

                    mainWindow.Effect = null;
                    mainWindow.IsEnabled = true;
                    mainWindow.Activate();
                }
                else
                {
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowDialog Error: {ex.Message}");
            }
        }

        private Window CreateDialogWindow(string title, double width, double height, UIElement content, Brush accentColor = null, Brush backgroundColor = null)
        {
            try
            {
                if (accentColor == null) accentColor = (Brush)FindResource("CustodyPrimaryColor");
                if (backgroundColor == null) backgroundColor = (Brush)FindResource("CustodySurfaceColor");

                var dialog = new Window
                {
                    Title = title,
                    Width = width,
                    Height = height,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainBorder = new Border
                {
                    Background = backgroundColor,
                    CornerRadius = new CornerRadius(16),
                    Effect = new DropShadowEffect { BlurRadius = 25, ShadowDepth = 0, Opacity = 0.2, Color = Colors.Black }
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var topBar = new Border { Background = accentColor, CornerRadius = new CornerRadius(16, 16, 0, 0), Height = 8 };
                mainGrid.Children.Add(topBar);
                Grid.SetRow(topBar, 0);

                var contentBorder = new Border { Child = content, Margin = new Thickness(0, 0, 0, 0) };
                mainGrid.Children.Add(contentBorder);
                Grid.SetRow(contentBorder, 1);

                mainBorder.Child = mainGrid;
                dialog.Content = mainBorder;

                return dialog;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateDialogWindow Error: {ex.Message}");
                return new Window { Title = title, Width = width, Height = height };
            }
        }

        #endregion

        #region أحداث أزرار البطاقات

        private void ViewTransactions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button != null && button.Tag != null)
                {
                    int custodyId = (int)button.Tag;
                    LoadTransactions(custodyId);

                    var scrollViewer = FindChild<ScrollViewer>(this, "MainScrollViewer");
                    scrollViewer?.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewTransactions_Click Error: {ex.Message}");
            }
        }

        private void EditCustody_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button != null && button.Tag != null)
                {
                    int custodyId = (int)button.Tag;
                    EditCustodyDialog(custodyId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditCustody_Click Error: {ex.Message}");
            }
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            try
            {
                T child = null;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var childObj = VisualTreeHelper.GetChild(parent, i);
                    if (childObj is T && ((T)childObj).Name == childName)
                    {
                        child = (T)childObj;
                        break;
                    }
                    child = FindChild<T>(childObj, childName);
                    if (child != null) break;
                }
                return child;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindChild Error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region إنشاء عهدة جديدة

        private void BtnAddCustody_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_employeesList.Count == 0)
                {
                    MessageBox.Show("لا يوجد موظفين. يرجى إضافة موظفين أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var grid = CreateCustodyFormGrid();
                var dialog = CreateDialogWindow("إنشاء عهدة جديدة", 500, 650, grid, (Brush)FindResource("CustodyPrimaryColor"), new SolidColorBrush(Color.FromRgb(245, 243, 255)));

                Button saveBtn = null;
                Button cancelBtn = null;

                saveBtn = FindButton(grid, "SaveButton");
                cancelBtn = FindButton(grid, "CancelButton");

                if (saveBtn != null)
                {
                    saveBtn.Click += async (s, ev) =>
                    {
                        await SaveNewCustody(grid, dialog);
                    };
                }

                if (cancelBtn != null)
                {
                    cancelBtn.Click += (s, ev) => dialog.Close();
                }

                ShowDialog(dialog);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnAddCustody_Click Error: {ex.Message}");
                MessageBox.Show($"خطأ في فتح نافذة إنشاء العهدة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Grid CreateCustodyFormGrid()
        {
            try
            {
                var grid = new Grid { Margin = new Thickness(25) };
                for (int i = 0; i < 15; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                int row = 0;

                // العنوان
                var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                titlePanel.Children.Add(new TextBlock { Text = "📋", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "إنشاء عهدة جديدة",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("CustodyPrimaryColor"),
                    FontFamily = (FontFamily)FindResource("ArabicFont")
                });
                Grid.SetRow(titlePanel, row++);
                grid.Children.Add(titlePanel);

                // خط فاصل
                var line = new Border { BorderBrush = (Brush)FindResource("CustodyBorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(line, row++);
                grid.Children.Add(line);

                // الموظف
                var employeeLabel = CreateLabel("الموظف", row);
                grid.Children.Add(employeeLabel);

                var employeeCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Name = "EmployeeCombo" };
                foreach (var emp in _employeesList)
                    employeeCombo.Items.Add(emp.DisplayName);
                if (employeeCombo.Items.Count > 0) employeeCombo.SelectedIndex = 0;
                Grid.SetRow(employeeCombo, row + 1);
                grid.Children.Add(employeeCombo);
                row += 2;

                // المبلغ
                var amountLabel = CreateLabel("المبلغ", row);
                grid.Children.Add(amountLabel);

                var amountBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(amountBox, row + 1);
                grid.Children.Add(amountBox);
                row += 2;

                // نوع العهدة
                var typeLabel = CreateLabel("نوع العهدة", row);
                grid.Children.Add(typeLabel);

                var typeCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                typeCombo.Items.Add("نقدي");
                typeCombo.Items.Add("عيني");
                typeCombo.Items.Add("مختلط");
                typeCombo.SelectedIndex = 0;
                Grid.SetRow(typeCombo, row + 1);
                grid.Children.Add(typeCombo);
                row += 2;

                // الخزينة
                var treasuryLabel = CreateLabel("الخزينة المصدرة", row);
                grid.Children.Add(treasuryLabel);

                var treasuryCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                _ = LoadTreasuriesToCombo(treasuryCombo);
                Grid.SetRow(treasuryCombo, row + 1);
                grid.Children.Add(treasuryCombo);
                row += 2;

                // تاريخ الاسترداد المتوقع
                var expectedLabel = CreateLabel("تاريخ الاسترداد المتوقع (اختياري)", row);
                grid.Children.Add(expectedLabel);

                var expectedDatePicker = new DatePicker { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(expectedDatePicker, row + 1);
                grid.Children.Add(expectedDatePicker);
                row += 2;

                // الوصف
                var descLabel = CreateLabel("الوصف", row);
                grid.Children.Add(descLabel);

                var descBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20) };
                Grid.SetRow(descBox, row + 1);
                grid.Children.Add(descBox);
                row += 2;

                // الأزرار
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
                var saveBtn = new Button { Content = "إنشاء", Width = 110, Height = 40, Margin = new Thickness(0, 0, 12, 0), Name = "SaveButton" };
                var cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Name = "CancelButton" };
                buttonPanel.Children.Add(saveBtn);
                buttonPanel.Children.Add(cancelBtn);
                Grid.SetRow(buttonPanel, row);
                grid.Children.Add(buttonPanel);

                return grid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateCustodyFormGrid Error: {ex.Message}");
                return new Grid { Margin = new Thickness(25) };
            }
        }

        private TextBlock CreateLabel(string text, int row)
        {
            try
            {
                var label = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("CustodyTextPrimary"),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                Grid.SetRow(label, row);
                return label;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateLabel Error: {ex.Message}");
                return new TextBlock { Text = text };
            }
        }

        private Button FindButton(DependencyObject parent, string name)
        {
            try
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is Button button && button.Name == name)
                        return button;
                    var result = FindButton(child, name);
                    if (result != null)
                        return result;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindButton Error: {ex.Message}");
                return null;
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, int index = 0) where T : DependencyObject
        {
            try
            {
                int count = 0;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T tChild)
                    {
                        if (count == index)
                            return tChild;
                        count++;
                    }
                    var result = FindVisualChild<T>(child, index - count);
                    if (result != null)
                        return result;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindVisualChild Error: {ex.Message}");
                return null;
            }
        }

        private async Task LoadTreasuriesToCombo(ComboBox combo)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    combo.Items.Clear();
                    combo.Items.Add("بدون خزينة");

                    // ✅ استخدام DatabaseExecutor لتحميل الخزائن مع إعادة المحاولة التلقائية
                    var treasuries = await DatabaseExecutor.ExecuteNonTransactionAsync<List<TreasuryBasicItem>>(_dbService, async (connection) =>
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

                    foreach (var treasury in treasuries)
                    {
                        combo.Items.Add(new ComboBoxItem { Content = treasury.Name, Tag = treasury.Id });
                    }

                    if (combo.Items.Count > 0)
                        combo.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading treasuries: {ex.Message}");
                }
            });
        }

        private async Task SaveNewCustody(Grid grid, Window dialog)
        {
            try
            {
                var employeeCombo = FindVisualChild<ComboBox>(grid);
                var amountBox = FindVisualChild<TextBox>(grid);
                var typeCombo = FindVisualChild<ComboBox>(grid, 1);
                var treasuryCombo = FindVisualChild<ComboBox>(grid, 2);
                var expectedDatePicker = FindVisualChild<DatePicker>(grid);
                var descBox = FindVisualChild<TextBox>(grid, 1);

                if (employeeCombo == null || employeeCombo.SelectedIndex < 0 || employeeCombo.SelectedIndex >= _employeesList.Count)
                {
                    MessageBox.Show("يرجى اختيار موظف", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(amountBox?.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("المبلغ غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = _employeesList[employeeCombo.SelectedIndex].EmployeeID;
                string type = "Cash";
                string selectedType = typeCombo?.SelectedItem?.ToString() ?? "نقدي";
                if (selectedType == "عيني") type = "Asset";
                else if (selectedType == "مختلط") type = "Mixed";

                int treasuryId = 0;
                if (treasuryCombo?.SelectedItem is ComboBoxItem treasuryItem && treasuryItem.Tag is int tid)
                    treasuryId = tid;

                var custody = new CustodyItem
                {
                    EmployeeID = employeeId,
                    Amount = amount,
                    CustodyType = type,
                    TreasuryID = treasuryId,
                    ExpectedReturnDate = expectedDatePicker?.SelectedDate,
                    Description = descBox?.Text ?? "",
                    CustodyDate = DateTime.Now
                };

                bool result = await _dbService.CreateCustodyAsync(custody, LoginView.CurrentUserId);

                if (result)
                {
                    MessageBox.Show("تم إنشاء العهدة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadEmployeesFilter();
                    LoadCustodyData();
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("فشل في إنشاء العهدة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveNewCustody Error: {ex.Message}");
                MessageBox.Show($"خطأ في إنشاء العهدة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region إضافة صرف للعهدة

        private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedCustodyId == 0)
                {
                    MessageBox.Show("يرجى اختيار عهدة أولاً من البطاقات", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var custody = CustodyList.FirstOrDefault(c => c.Id == _selectedCustodyId);
                if (custody == null)
                {
                    MessageBox.Show("العهدة غير موجودة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (custody.Status != "Active" && custody.Status != "PartiallySettled")
                {
                    MessageBox.Show("لا يمكن إضافة صرف لعهدة مسددة أو ملغية", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var grid = CreateExpenseFormGrid(custody);
                var dialog = CreateDialogWindow("صرف من العهدة", 480, 550, grid, (Brush)FindResource("CustodyWarningColor"), new SolidColorBrush(Color.FromRgb(255, 237, 213)));

                Button saveBtn = FindButton(grid, "SaveButton");
                Button cancelBtn = FindButton(grid, "CancelButton");

                if (saveBtn != null)
                {
                    saveBtn.Click += async (s, ev) =>
                    {
                        await SaveExpense(grid, dialog);
                    };
                }

                if (cancelBtn != null)
                {
                    cancelBtn.Click += (s, ev) => dialog.Close();
                }

                ShowDialog(dialog);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnAddExpense_Click Error: {ex.Message}");
                MessageBox.Show($"خطأ في فتح نافذة الصرف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Grid CreateExpenseFormGrid(CustodyCardItem custody)
        {
            try
            {
                var grid = new Grid { Margin = new Thickness(25) };
                for (int i = 0; i < 12; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                int row = 0;

                var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                titlePanel.Children.Add(new TextBlock { Text = "💰", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "صرف من العهدة",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("CustodyWarningColor"),
                    FontFamily = (FontFamily)FindResource("ArabicFont")
                });
                Grid.SetRow(titlePanel, row++);
                grid.Children.Add(titlePanel);

                var line = new Border { BorderBrush = (Brush)FindResource("CustodyBorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(line, row++);
                grid.Children.Add(line);

                // معلومات العهدة
                var infoPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 243, 255)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var infoGrid = new Grid();
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                infoGrid.RowDefinitions.Add(new RowDefinition());
                infoGrid.RowDefinitions.Add(new RowDefinition());

                var numberLabel = new TextBlock { Text = "رقم العهدة:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 5) };
                infoGrid.Children.Add(numberLabel);
                Grid.SetRow(numberLabel, 0);
                Grid.SetColumn(numberLabel, 0);

                var numberValue = new TextBlock { Text = custody.Number, Margin = new Thickness(0, 0, 0, 5) };
                infoGrid.Children.Add(numberValue);
                Grid.SetRow(numberValue, 0);
                Grid.SetColumn(numberValue, 1);

                var employeeLabel = new TextBlock { Text = "الموظف:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                infoGrid.Children.Add(employeeLabel);
                Grid.SetRow(employeeLabel, 1);
                Grid.SetColumn(employeeLabel, 0);

                var employeeValue = new TextBlock { Text = custody.EmployeeName };
                infoGrid.Children.Add(employeeValue);
                Grid.SetRow(employeeValue, 1);
                Grid.SetColumn(employeeValue, 1);

                infoPanel.Child = infoGrid;
                Grid.SetRow(infoPanel, row++);
                grid.Children.Add(infoPanel);

                // المبلغ المتبقي
                var remainingPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var remainingText = new TextBlock
                {
                    Text = $"المبلغ المتبقي: {custody.FormattedRemaining}",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center
                };
                remainingPanel.Child = remainingText;
                Grid.SetRow(remainingPanel, row++);
                grid.Children.Add(remainingPanel);

                // المبلغ المصروف
                var amountLabel = CreateLabel("المبلغ المصروف", row);
                grid.Children.Add(amountLabel);

                var amountBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(amountBox, row + 1);
                grid.Children.Add(amountBox);
                row += 2;

                // نوع المصروف
                var expenseLabel = CreateLabel("نوع المصروف", row);
                grid.Children.Add(expenseLabel);

                var expenseCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                expenseCombo.Items.Add("مشتريات");
                expenseCombo.Items.Add("نقل");
                expenseCombo.Items.Add("إيجار");
                expenseCombo.Items.Add("مقصف");
                expenseCombo.Items.Add("أخرى");
                expenseCombo.SelectedIndex = 0;
                Grid.SetRow(expenseCombo, row + 1);
                grid.Children.Add(expenseCombo);
                row += 2;

                // رقم الإيصال
                var receiptLabel = CreateLabel("رقم الإيصال (اختياري)", row);
                grid.Children.Add(receiptLabel);

                var receiptBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(receiptBox, row + 1);
                grid.Children.Add(receiptBox);
                row += 2;

                // الوصف
                var descLabel = CreateLabel("الوصف", row);
                grid.Children.Add(descLabel);

                var descBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20) };
                Grid.SetRow(descBox, row + 1);
                grid.Children.Add(descBox);
                row += 2;

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
                var saveBtn = new Button { Content = "صرف", Width = 110, Height = 40, Margin = new Thickness(0, 0, 12, 0), Name = "SaveButton" };
                var cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Name = "CancelButton" };
                buttonPanel.Children.Add(saveBtn);
                buttonPanel.Children.Add(cancelBtn);
                Grid.SetRow(buttonPanel, row);
                grid.Children.Add(buttonPanel);

                return grid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateExpenseFormGrid Error: {ex.Message}");
                return new Grid { Margin = new Thickness(25) };
            }
        }

        private async Task SaveExpense(Grid grid, Window dialog)
        {
            try
            {
                var amountBox = FindVisualChild<TextBox>(grid);
                var expenseCombo = FindVisualChild<ComboBox>(grid);
                var receiptBox = FindVisualChild<TextBox>(grid, 1);
                var descBox = FindVisualChild<TextBox>(grid, 2);

                if (!decimal.TryParse(amountBox?.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("المبلغ غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var custody = CustodyList.FirstOrDefault(c => c.Id == _selectedCustodyId);
                if (custody != null && amount > custody.RemainingAmount)
                {
                    MessageBox.Show($"المبلغ المصروف أكبر من المبلغ المتبقي ({custody.FormattedRemaining})", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string expenseType = expenseCombo?.SelectedItem?.ToString() ?? "";
                string receiptNumber = receiptBox?.Text ?? "";
                string description = descBox?.Text ?? "";

                bool result = await _dbService.AddCustodyExpenseAsync(_selectedCustodyId, amount, expenseType, description, receiptNumber, LoginView.CurrentUserId);

                if (result)
                {
                    MessageBox.Show("تم إضافة الصرف بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadCustodyData();
                    LoadTransactions(_selectedCustodyId);
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("فشل في إضافة الصرف", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveExpense Error: {ex.Message}");
                MessageBox.Show($"خطأ في إضافة الصرف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region تسوية عهدة

        private void BtnSettle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedCustodyId == 0)
                {
                    MessageBox.Show("يرجى اختيار عهدة أولاً من البطاقات", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var custody = CustodyList.FirstOrDefault(c => c.Id == _selectedCustodyId);
                if (custody == null)
                {
                    MessageBox.Show("العهدة غير موجودة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (custody.Status == "Settled")
                {
                    MessageBox.Show("العهدة مسددة بالفعل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (custody.Status == "Cancelled")
                {
                    MessageBox.Show("لا يمكن تسوية عهدة ملغية", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var grid = CreateSettlementFormGrid(custody);
                var dialog = CreateDialogWindow("تسوية العهدة", 450, 450, grid, (Brush)FindResource("CustodySuccessColor"), new SolidColorBrush(Color.FromRgb(204, 251, 241)));

                Button saveBtn = FindButton(grid, "SaveButton");
                Button cancelBtn = FindButton(grid, "CancelButton");

                if (saveBtn != null)
                {
                    saveBtn.Click += async (s, ev) =>
                    {
                        await SaveSettlement(grid, dialog);
                    };
                }

                if (cancelBtn != null)
                {
                    cancelBtn.Click += (s, ev) => dialog.Close();
                }

                ShowDialog(dialog);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BtnSettle_Click Error: {ex.Message}");
                MessageBox.Show($"خطأ في فتح نافذة التسوية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Grid CreateSettlementFormGrid(CustodyCardItem custody)
        {
            try
            {
                var grid = new Grid { Margin = new Thickness(25) };
                for (int i = 0; i < 10; i++)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                int row = 0;

                var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                titlePanel.Children.Add(new TextBlock { Text = "✅", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = "تسوية العهدة",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("CustodySuccessColor"),
                    FontFamily = (FontFamily)FindResource("ArabicFont")
                });
                Grid.SetRow(titlePanel, row++);
                grid.Children.Add(titlePanel);

                var line = new Border { BorderBrush = (Brush)FindResource("CustodyBorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(line, row++);
                grid.Children.Add(line);

                // معلومات العهدة
                var infoPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 243, 255)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var infoGrid = new Grid();
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
                infoGrid.RowDefinitions.Add(new RowDefinition());
                infoGrid.RowDefinitions.Add(new RowDefinition());
                infoGrid.RowDefinitions.Add(new RowDefinition());

                var numberLabel = new TextBlock { Text = "رقم العهدة:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 5) };
                infoGrid.Children.Add(numberLabel);
                Grid.SetRow(numberLabel, 0);
                Grid.SetColumn(numberLabel, 0);

                var numberValue = new TextBlock { Text = custody.Number, Margin = new Thickness(0, 0, 0, 5) };
                infoGrid.Children.Add(numberValue);
                Grid.SetRow(numberValue, 0);
                Grid.SetColumn(numberValue, 1);

                var employeeLabel = new TextBlock { Text = "الموظف:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                infoGrid.Children.Add(employeeLabel);
                Grid.SetRow(employeeLabel, 1);
                Grid.SetColumn(employeeLabel, 0);

                var employeeValue = new TextBlock { Text = custody.EmployeeName };
                infoGrid.Children.Add(employeeValue);
                Grid.SetRow(employeeValue, 1);
                Grid.SetColumn(employeeValue, 1);

                var remainingLabel = new TextBlock { Text = "المبلغ المتبقي:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                infoGrid.Children.Add(remainingLabel);
                Grid.SetRow(remainingLabel, 2);
                Grid.SetColumn(remainingLabel, 0);

                var remainingValue = new TextBlock
                {
                    Text = custody.FormattedRemaining,
                    Foreground = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                    FontWeight = FontWeights.Bold
                };
                infoGrid.Children.Add(remainingValue);
                Grid.SetRow(remainingValue, 2);
                Grid.SetColumn(remainingValue, 1);

                infoPanel.Child = infoGrid;
                Grid.SetRow(infoPanel, row++);
                grid.Children.Add(infoPanel);

                // مبلغ التسوية
                var amountLabel = CreateLabel("مبلغ التسوية", row);
                grid.Children.Add(amountLabel);

                var amountBox = new TextBox
                {
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 15),
                    Text = custody.RemainingAmount.ToString("F2")
                };
                Grid.SetRow(amountBox, row + 1);
                grid.Children.Add(amountBox);
                row += 2;

                // ملاحظات
                var notesLabel = CreateLabel("ملاحظات (اختياري)", row);
                grid.Children.Add(notesLabel);

                var notesBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20) };
                Grid.SetRow(notesBox, row + 1);
                grid.Children.Add(notesBox);
                row += 2;

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
                var saveBtn = new Button { Content = "تسوية", Width = 110, Height = 40, Margin = new Thickness(0, 0, 12, 0), Name = "SaveButton" };
                var cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Name = "CancelButton" };
                buttonPanel.Children.Add(saveBtn);
                buttonPanel.Children.Add(cancelBtn);
                Grid.SetRow(buttonPanel, row);
                grid.Children.Add(buttonPanel);

                return grid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateSettlementFormGrid Error: {ex.Message}");
                return new Grid { Margin = new Thickness(25) };
            }
        }

        private async Task SaveSettlement(Grid grid, Window dialog)
        {
            try
            {
                var amountBox = FindVisualChild<TextBox>(grid);
                var notesBox = FindVisualChild<TextBox>(grid, 1);

                if (!decimal.TryParse(amountBox?.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("مبلغ التسوية غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var custody = CustodyList.FirstOrDefault(c => c.Id == _selectedCustodyId);
                if (custody != null && amount > custody.RemainingAmount)
                {
                    MessageBox.Show($"مبلغ التسوية أكبر من المبلغ المتبقي ({custody.FormattedRemaining})", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string notes = notesBox?.Text ?? "";

                bool result = await _dbService.SettleCustodyAsync(_selectedCustodyId, amount, notes, LoginView.CurrentUserId);

                if (result)
                {
                    MessageBox.Show("تم تسوية العهدة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadCustodyData();
                    LoadTransactions(_selectedCustodyId);
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("فشل في تسوية العهدة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSettlement Error: {ex.Message}");
                MessageBox.Show($"خطأ في تسوية العهدة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region تعديل عهدة

        private async void EditCustodyDialog(int custodyId)
        {
            try
            {
                var custody = CustodyList.FirstOrDefault(c => c.Id == custodyId);
                if (custody == null) return;

                MessageBox.Show("جاري تطوير ميزة تعديل العهدة...", "قريباً", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditCustodyDialog Error: {ex.Message}");
            }
        }

        #endregion
    }

    #region الكلاسات المساعدة

    /// <summary>
    /// كلاس يمثل بيانات العهدة في بطاقة العرض
    /// </summary>
    public class CustodyCardItem : INotifyPropertyChanged
    {
        private int _id;
        private string _number;
        private string _employeeName;
        private string _employeeCode;
        private decimal _amount;
        private decimal _remainingAmount;
        private string _status;
        private string _statusColor;
        private string _statusText;
        private DateTime _custodyDate;

        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Number
        {
            get => _number;
            set
            {
                if (_number != value)
                {
                    _number = value;
                    OnPropertyChanged(nameof(Number));
                }
            }
        }

        public string EmployeeName
        {
            get => _employeeName;
            set
            {
                if (_employeeName != value)
                {
                    _employeeName = value;
                    OnPropertyChanged(nameof(EmployeeName));
                }
            }
        }

        public string EmployeeCode
        {
            get => _employeeCode;
            set
            {
                if (_employeeCode != value)
                {
                    _employeeCode = value;
                    OnPropertyChanged(nameof(EmployeeCode));
                }
            }
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    OnPropertyChanged(nameof(Amount));
                    OnPropertyChanged(nameof(FormattedAmount));
                    OnPropertyChanged(nameof(CompletionPercentage));
                }
            }
        }

        public decimal RemainingAmount
        {
            get => _remainingAmount;
            set
            {
                if (_remainingAmount != value)
                {
                    _remainingAmount = value;
                    OnPropertyChanged(nameof(RemainingAmount));
                    OnPropertyChanged(nameof(FormattedRemaining));
                    OnPropertyChanged(nameof(CompletionPercentage));
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public DateTime CustodyDate
        {
            get => _custodyDate;
            set
            {
                if (_custodyDate != value)
                {
                    _custodyDate = value;
                    OnPropertyChanged(nameof(CustodyDate));
                    OnPropertyChanged(nameof(FormattedDate));
                }
            }
        }

        public string FormattedAmount => $"{Amount:N2}";
        public string FormattedRemaining => $"{RemainingAmount:N2}";
        public string FormattedDate => CustodyDate.ToString("yyyy-MM-dd");
        public decimal CompletionPercentage => Amount > 0 ? ((Amount - RemainingAmount) / Amount) * 100 : 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// كلاس بيانات أساسية للخزينة - للاستخدام في النوافذ المنبثقة
    /// </summary>
    

    #endregion
}