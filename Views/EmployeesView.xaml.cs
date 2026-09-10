using RasidAccountingSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RasidAccountingSystem.Views
{
    /// <summary>
    /// صفحة إدارة الموظفين - عرض وإضافة وتعديل وحذف الموظفين
    /// </summary>
    public partial class EmployeesView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private string _connectionString;

        #endregion

        #region الخصائص العامة

        /// <summary>
        /// قائمة الموظفين المعروضة في الجدول
        /// </summary>
        public ObservableCollection<EmployeeItem> EmployeesList { get; set; }

        private EmployeeItem _selectedEmployee;

        #endregion

        #region المنشئ

        public EmployeesView()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            _connectionString = _dbService.GetConnectionString();

            EmployeesList = new ObservableCollection<EmployeeItem>();
            dgEmployees.ItemsSource = EmployeesList;

            LoadEmployees();
            LoadCustodyStatistics();
            AttachEventHandlers();

            // تحرير الموارد عند إغلاق الصفحة
            this.Unloaded += (s, e) =>
            {
                _dbService = null;
                _connectionString = null;
                EmployeesList?.Clear();
            };
        }

        #endregion

        #region دوال تحميل البيانات

        private async void LoadEmployees(string searchText = "")
        {
            try
            {
                EmployeesList.Clear();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT EmployeeID, EmployeeCode, EmployeeNameAr, EmployeeNameEn, 
                               Department, Position, Phone, Email, IsActive, CreatedDate
                        FROM Employees 
                        WHERE IsActive = 1";

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        sql += " AND (EmployeeCode LIKE @search OR EmployeeNameAr LIKE @search OR EmployeeNameEn LIKE @search OR Department LIKE @search OR Phone LIKE @search)";
                    }

                    sql += " ORDER BY EmployeeNameAr";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                EmployeesList.Add(new EmployeeItem
                                {
                                    EmployeeID = reader.GetInt32(0),
                                    EmployeeCode = reader.GetString(1),
                                    EmployeeNameAr = reader.GetString(2),
                                    EmployeeNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Department = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Position = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Phone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    Email = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    IsActive = reader.GetInt32(8) == 1
                                });
                            }
                        }
                    }
                }

                UpdateStatistics();
                LoadCustodyStatistics();

                // إظهار أو إخفاء رسالة عدم وجود بيانات
                if (dgEmployees.Items.Count == 0)
                {
                    dgEmployees.Visibility = Visibility.Collapsed;
                    NoDataBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    dgEmployees.Visibility = Visibility.Visible;
                    NoDataBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل الموظفين: {ex.Message}");
                MessageBoxService.ShowError($"خطأ في تحميل الموظفين: {ex.Message}", "خطأ");
            }
        }

        private void UpdateStatistics()
        {
            int totalCount = EmployeesList.Count;
            int activeCount = EmployeesList.Count(e => e.IsActive);

            lblTotalCount.Text = totalCount.ToString();
            lblActiveCount.Text = activeCount.ToString();
            lblHeaderTotalCount.Text = totalCount.ToString();
        }

        private async void LoadCustodyStatistics()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // عدد العهود النشطة
                    string activeCustodySql = "SELECT COUNT(*) FROM Custody WHERE Status = 'Active' AND IsActive = 1";
                    using (var cmd = new SQLiteCommand(activeCustodySql, connection))
                    {
                        long count = (long)await cmd.ExecuteScalarAsync();
                        lblActiveCustodyCount.Text = count.ToString();
                    }

                    // إجمالي العهود
                    string totalCustodySql = "SELECT COUNT(*) FROM Custody WHERE IsActive = 1";
                    using (var cmd = new SQLiteCommand(totalCustodySql, connection))
                    {
                        long count = (long)await cmd.ExecuteScalarAsync();
                        lblTotalCustodyCount.Text = count.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل إحصائيات العهود: {ex.Message}");
                lblActiveCustodyCount.Text = "0";
                lblTotalCustodyCount.Text = "0";
            }
        }

        #endregion

        #region ربط الأحداث

        private void AttachEventHandlers()
        {
            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnDelete.Click += BtnDelete_Click;
            btnRefresh.Click += (s, e) => { LoadEmployees(); LoadCustodyStatistics(); };
            txtSearch.KeyDown += TxtSearch_KeyDown;
            btnSearch.Click += BtnSearch_Click;
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoadEmployees(txtSearch.Text);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadEmployees(txtSearch.Text);
        }

        #endregion

        #region دوال العرض المنبثق

        private void ShowDialog(Window dialog)
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

        private Window CreateDialogWindow(string title, double width, double height, UIElement content, Brush accentColor = null, Brush backgroundColor = null)
        {
            if (accentColor == null) accentColor = (Brush)FindResource("PrimaryColor");
            if (backgroundColor == null) backgroundColor = (Brush)FindResource("SurfaceColor");

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

        #endregion

        #region دوال مساعدة للبحث عن المكونات

        private Button FindButton(DependencyObject parent, string name)
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

        private T FindVisualChild<T>(DependencyObject parent, int index = 0) where T : DependencyObject
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

        #endregion

        #region إضافة موظف جديد

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var grid = CreateEmployeeFormGrid(null);
            var dialog = CreateDialogWindow("إضافة موظف جديد", 550, 650, grid, (Brush)FindResource("SuccessColor"), new SolidColorBrush(Color.FromRgb(220, 252, 231)));

            Button saveBtn = FindButton(grid, "SaveButton");
            Button cancelBtn = FindButton(grid, "CancelButton");

            if (saveBtn != null)
            {
                saveBtn.Click += async (s, ev) =>
                {
                    await SaveEmployee(grid, dialog, null);
                };
            }

            if (cancelBtn != null)
            {
                cancelBtn.Click += (s, ev) => dialog.Close();
            }

            ShowDialog(dialog);
        }

        private async Task<string> GenerateEmployeeCodeAsync()
        {
            return await _dbService.GenerateUniqueEmployeeCodeAsync();
        }

        private Grid CreateEmployeeFormGrid(EmployeeItem employee)
        {
            var grid = new Grid { Margin = new Thickness(25) };
            for (int i = 0; i < 20; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            int row = 0;
            bool isEdit = employee != null;

            // العنوان
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = isEdit ? "✏️" : "👤", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            titlePanel.Children.Add(new TextBlock
            {
                Text = isEdit ? "تعديل موظف" : "إضافة موظف جديد",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource(isEdit ? "WarningColor" : "SuccessColor"),
                FontFamily = (FontFamily)FindResource("ArabicFont")
            });
            Grid.SetRow(titlePanel, row++);
            grid.Children.Add(titlePanel);

            // خط فاصل
            var line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(line, row++);
            grid.Children.Add(line);

            // كود الموظف
            var codeLabel = CreateLabel("كود الموظف", row);
            grid.Children.Add(codeLabel);

            var codeBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.EmployeeCode : ""
            };

            if (!isEdit)
            {
                _ = Task.Run(async () =>
                {
                    var code = await GenerateEmployeeCodeAsync();
                    Dispatcher.Invoke(() => codeBox.Text = code);
                });
            }

            Grid.SetRow(codeBox, row + 1);
            grid.Children.Add(codeBox);
            row += 2;

            // الاسم بالعربية
            var nameArLabel = CreateLabel("الاسم بالعربية", row);
            grid.Children.Add(nameArLabel);

            var nameArBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.EmployeeNameAr : ""
            };
            Grid.SetRow(nameArBox, row + 1);
            grid.Children.Add(nameArBox);
            row += 2;

            // الاسم بالإنجليزية
            var nameEnLabel = CreateLabel("الاسم بالإنجليزية (اختياري)", row);
            grid.Children.Add(nameEnLabel);

            var nameEnBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.EmployeeNameEn : ""
            };
            Grid.SetRow(nameEnBox, row + 1);
            grid.Children.Add(nameEnBox);
            row += 2;

            // القسم
            var deptLabel = CreateLabel("القسم", row);
            grid.Children.Add(deptLabel);

            var deptBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.Department : ""
            };
            Grid.SetRow(deptBox, row + 1);
            grid.Children.Add(deptBox);
            row += 2;

            // المنصب
            var positionLabel = CreateLabel("المنصب", row);
            grid.Children.Add(positionLabel);

            var positionBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.Position : ""
            };
            Grid.SetRow(positionBox, row + 1);
            grid.Children.Add(positionBox);
            row += 2;

            // رقم الجوال
            var phoneLabel = CreateLabel("رقم الجوال", row);
            grid.Children.Add(phoneLabel);

            var phoneBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 15),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.Phone : ""
            };
            Grid.SetRow(phoneBox, row + 1);
            grid.Children.Add(phoneBox);
            row += 2;

            // البريد الإلكتروني
            var emailLabel = CreateLabel("البريد الإلكتروني (اختياري)", row);
            grid.Children.Add(emailLabel);

            var emailBox = new TextBox
            {
                Height = 40,
                Margin = new Thickness(0, 0, 0, 20),
                Style = (Style)FindResource("ModernTextBox"),
                Text = isEdit ? employee.Email : ""
            };
            Grid.SetRow(emailBox, row + 1);
            grid.Children.Add(emailBox);
            row += 2;

            // الأزرار
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            var saveBtn = new Button
            {
                Content = isEdit ? "تحديث" : "حفظ",
                Width = 110,
                Height = 40,
                Style = (Style)FindResource(isEdit ? "WarningButton" : "SuccessButton"),
                Margin = new Thickness(0, 0, 12, 0),
                Name = "SaveButton"
            };
            var cancelBtn = new Button
            {
                Content = "إلغاء",
                Width = 110,
                Height = 40,
                Style = (Style)FindResource("SecondaryButton"),
                Name = "CancelButton"
            };
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, row);
            grid.Children.Add(buttonPanel);

            return grid;
        }

        private TextBlock CreateLabel(string text, int row)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(label, row);
            return label;
        }

        private async Task SaveEmployee(Grid grid, Window dialog, EmployeeItem existingEmployee)
        {
            var codeBox = FindVisualChild<TextBox>(grid, 0);
            var nameArBox = FindVisualChild<TextBox>(grid, 1);
            var nameEnBox = FindVisualChild<TextBox>(grid, 2);
            var deptBox = FindVisualChild<TextBox>(grid, 3);
            var positionBox = FindVisualChild<TextBox>(grid, 4);
            var phoneBox = FindVisualChild<TextBox>(grid, 5);
            var emailBox = FindVisualChild<TextBox>(grid, 6);

            if (string.IsNullOrWhiteSpace(nameArBox?.Text))
            {
                MessageBoxService.ShowWarning("الاسم بالعربية مطلوب", "تنبيه");
                return;
            }

            bool isEdit = existingEmployee != null;

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    if (isEdit)
                    {
                        string sql = @"
                            UPDATE Employees SET 
                                EmployeeCode = @code,
                                EmployeeNameAr = @nameAr,
                                EmployeeNameEn = @nameEn,
                                Department = @dept,
                                Position = @position,
                                Phone = @phone,
                                Email = @email,
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE EmployeeID = @id";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", codeBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@nameAr", nameArBox.Text);
                            cmd.Parameters.AddWithValue("@nameEn", nameEnBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@dept", deptBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@position", positionBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@phone", phoneBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@email", emailBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@id", existingEmployee.EmployeeID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        MessageBoxService.ShowSuccess("تم تحديث بيانات الموظف بنجاح", "تم");
                    }
                    else
                    {
                        string employeeCode = codeBox?.Text;
                        if (string.IsNullOrEmpty(employeeCode))
                        {
                            employeeCode = await GenerateEmployeeCodeAsync();
                        }

                        string sql = @"
                            INSERT INTO Employees (EmployeeCode, EmployeeNameAr, EmployeeNameEn, Department, Position, Phone, Email, IsActive, CreatedDate, CreatedBy)
                            VALUES (@code, @nameAr, @nameEn, @dept, @position, @phone, @email, 1, CURRENT_TIMESTAMP, @createdBy)";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", employeeCode);
                            cmd.Parameters.AddWithValue("@nameAr", nameArBox.Text);
                            cmd.Parameters.AddWithValue("@nameEn", nameEnBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@dept", deptBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@position", positionBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@phone", phoneBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@email", emailBox?.Text ?? "");
                            cmd.Parameters.AddWithValue("@createdBy", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        MessageBoxService.ShowSuccess("تم إضافة الموظف بنجاح", "تم");
                    }
                }

                LoadEmployees();
                dialog.Close();
            }
            catch (Exception ex)
            {
                MessageBoxService.ShowError($"خطأ: {ex.Message}", "خطأ");
            }
        }

        #endregion

        #region تعديل موظف

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem == null)
            {
                MessageBoxService.ShowWarning("الرجاء تحديد موظف أولاً", "تنبيه");
                return;
            }

            _selectedEmployee = dgEmployees.SelectedItem as EmployeeItem;

            var grid = CreateEmployeeFormGrid(_selectedEmployee);
            var dialog = CreateDialogWindow("تعديل موظف", 550, 650, grid, (Brush)FindResource("WarningColor"), new SolidColorBrush(Color.FromRgb(254, 243, 199)));

            Button saveBtn = FindButton(grid, "SaveButton");
            Button cancelBtn = FindButton(grid, "CancelButton");

            if (saveBtn != null)
            {
                saveBtn.Click += async (s, ev) =>
                {
                    await SaveEmployee(grid, dialog, _selectedEmployee);
                };
            }

            if (cancelBtn != null)
            {
                cancelBtn.Click += (s, ev) => dialog.Close();
            }

            ShowDialog(dialog);
        }

        #endregion

        #region حذف موظف

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem == null)
            {
                MessageBoxService.ShowWarning("الرجاء تحديد موظف أولاً", "تنبيه");
                return;
            }

            _selectedEmployee = dgEmployees.SelectedItem as EmployeeItem;

            MessageBoxResult result = MessageBox.Show(
                $"هل أنت متأكد من حذف الموظف '{_selectedEmployee.EmployeeNameAr}'؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = await _dbService.DeleteEmployeeAsync(_selectedEmployee.EmployeeID);

                    if (deleted)
                    {
                        MessageBoxService.ShowSuccess("تم حذف الموظف بنجاح", "تم");
                        LoadEmployees();
                    }
                    else
                    {
                        MessageBoxService.ShowWarning("لا يمكن حذف الموظف لأنه مرتبط بعهدات نشطة. قم بتسوية العهود أولاً.", "تنبيه");
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxService.ShowError($"خطأ في حذف الموظف: {ex.Message}", "خطأ");
                }
            }
        }

        #endregion

        #region أزرار الإجراءات في الجدول

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int employeeId)
            {
                var employee = EmployeesList.FirstOrDefault(emp => emp.EmployeeID == employeeId);
                if (employee != null)
                {
                    _selectedEmployee = employee;
                    BtnEdit_Click(sender, e);
                }
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int employeeId)
            {
                var employee = EmployeesList.FirstOrDefault(emp => emp.EmployeeID == employeeId);
                if (employee != null)
                {
                    _selectedEmployee = employee;
                    BtnDelete_Click(sender, e);
                }
            }
        }

        #endregion
    }
}