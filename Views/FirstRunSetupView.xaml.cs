using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class FirstRunSetupView : Window
    {
        private readonly DatabaseService _dbService;
        private bool isPasswordVisible = false;
        private TextBox txtPasswordVisible;

        public FirstRunSetupView()
        {
            InitializeComponent();
            _dbService = new DatabaseService();

            btnCreate.Click += BtnCreate_Click;
            btnCancel.Click += (s, ev) => this.Close();
            btnTogglePassword.Click += BtnTogglePassword_Click;

            txtUsername.KeyDown += (s, ev) => { if (ev.Key == Key.Enter) txtPassword.Focus(); };
            txtPassword.KeyDown += (s, ev) => { if (ev.Key == Key.Enter) BtnCreate_Click(s, ev); };
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (txtPasswordVisible == null)
            {
                txtPasswordVisible = new TextBox
                {
                    FontFamily = txtPassword.FontFamily,
                    FontSize = txtPassword.FontSize,
                    Height = txtPassword.Height,
                    Padding = txtPassword.Padding,
                    VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
                    HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                    Background = txtPassword.Background,
                    BorderBrush = txtPassword.BorderBrush,
                    BorderThickness = txtPassword.BorderThickness,
                    Visibility = Visibility.Collapsed,
                    Margin = txtPassword.Margin
                };

                txtPasswordVisible.KeyDown += (s, ev) =>
                {
                    if (ev.Key == Key.Enter)
                        BtnCreate_Click(s, ev);
                };

                Grid parentGrid = txtPassword.Parent as Grid;
                if (parentGrid != null)
                {
                    int columnIndex = Grid.GetColumn(txtPassword);
                    int rowIndex = Grid.GetRow(txtPassword);
                    Grid.SetColumn(txtPasswordVisible, columnIndex);
                    Grid.SetRow(txtPasswordVisible, rowIndex);
                    parentGrid.Children.Add(txtPasswordVisible);
                }
            }

            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = txtPassword.Password;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Collapsed;
                togglePasswordIcon.Text = "🙈";
                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            else
            {
                txtPassword.Password = txtPasswordVisible.Text;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                togglePasswordIcon.Text = "👁";
                txtPassword.Focus();
            }
        }

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = isPasswordVisible ? txtPasswordVisible.Text : txtPassword.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowError("يرجى إدخال اسم المستخدم");
                txtUsername.Focus();
                return;
            }

            if (username.Length < 3)
            {
                ShowError("اسم المستخدم يجب أن يكون 3 أحرف على الأقل");
                txtUsername.Focus();
                txtUsername.SelectAll();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("يرجى إدخال كلمة المرور");
                if (isPasswordVisible)
                    txtPasswordVisible.Focus();
                else
                    txtPassword.Focus();
                return;
            }

            if (password.Length < 6)
            {
                ShowError("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
                if (isPasswordVisible)
                    txtPasswordVisible.Focus();
                else
                    txtPassword.Focus();
                return;
            }

            try
            {
                btnCreate.IsEnabled = false;
                btnCreate.Content = "جاري الإنشاء...";
                this.Cursor = Cursors.Wait;

                bool userExists = await _dbService.UserExistsAsync(username);
                if (userExists)
                {
                    ShowError("اسم المستخدم موجود مسبقاً");
                    txtUsername.Focus();
                    return;
                }

                bool success = await _dbService.CreateFirstUserAsync(username, password, username);

                if (success)
                {
                    MessageBox.Show(
                        $"✅ تم إنشاء حساب المدير بنجاح!\n\nاسم المستخدم: {username}",
                        "نجاح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    ShowError("فشل إنشاء الحساب. يرجى المحاولة مرة أخرى");
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطأ: {ex.Message}");
            }
            finally
            {
                btnCreate.IsEnabled = true;
                btnCreate.Content = "إنشاء حساب";
                this.Cursor = null;
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }
    }
}