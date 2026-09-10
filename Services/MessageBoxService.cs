using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// أنواع رسائل MessageBox
    /// </summary>
    public enum MessageBoxType
    {
        /// <summary>
        /// رسالة نجاح (لون فيروزي)
        /// </summary>
        Success,

        /// <summary>
        /// رسالة خطأ (لون وردي/أحمر)
        /// </summary>
        Error,

        /// <summary>
        /// رسالة تحذير (لون برتقالي/ذهبي)
        /// </summary>
        Warning,

        /// <summary>
        /// رسالة معلومات (لون أزرق سماوي)
        /// </summary>
        Info,

        /// <summary>
        /// رسالة استفسار (لون بنفسجي)
        /// </summary>
        Question
    }

    /// <summary>
    /// خدمة عرض رسائل MessageBox بتصميم احترافي بحجم مناسب وحواف ناعمة
    /// </summary>
    public static class MessageBoxService
    {
        /// <summary>
        /// عرض رسالة MessageBox بتصميم مخصص
        /// </summary>
        public static void Show(string message, string title, MessageBoxType type = MessageBoxType.Info)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 380,
                Height = 240,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true
            };

            // ==================== ألوان مختلفة ومميزة ====================

            Brush accentColor;
            Brush iconBackgroundColor;
            Brush mainBackgroundColor;
            Brush messageTextColor;
            string icon;

            switch (type)
            {
                case MessageBoxType.Success:
                    accentColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    iconBackgroundColor = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                    mainBackgroundColor = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    messageTextColor = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                    icon = "✓";
                    break;

                case MessageBoxType.Error:
                    accentColor = new SolidColorBrush(Color.FromRgb(244, 63, 94));
                    iconBackgroundColor = new SolidColorBrush(Color.FromRgb(255, 228, 230));
                    mainBackgroundColor = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                    messageTextColor = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                    icon = "✕";
                    break;

                case MessageBoxType.Warning:
                    accentColor = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    iconBackgroundColor = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                    mainBackgroundColor = new SolidColorBrush(Color.FromRgb(255, 251, 235));
                    messageTextColor = new SolidColorBrush(Color.FromRgb(113, 63, 18));
                    icon = "!";
                    break;

                case MessageBoxType.Question:
                    accentColor = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                    iconBackgroundColor = new SolidColorBrush(Color.FromRgb(237, 233, 254));
                    mainBackgroundColor = new SolidColorBrush(Color.FromRgb(245, 243, 255));
                    messageTextColor = new SolidColorBrush(Color.FromRgb(46, 16, 101));
                    icon = "?";
                    break;

                default:
                    accentColor = new SolidColorBrush(Color.FromRgb(14, 165, 233));
                    iconBackgroundColor = new SolidColorBrush(Color.FromRgb(224, 242, 254));
                    mainBackgroundColor = new SolidColorBrush(Color.FromRgb(240, 249, 255));
                    messageTextColor = new SolidColorBrush(Color.FromRgb(7, 89, 133));
                    icon = "ⓘ";
                    break;
            }

            // ==================== الإطار الرئيسي بحواف ناعمة ====================

            var mainBorder = new Border
            {
                Background = mainBackgroundColor,
                CornerRadius = new CornerRadius(12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    Color = Colors.Black
                }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ==================== الشريط العلوي الملون بحواف ناعمة ====================

            var topBar = new Border
            {
                Background = accentColor,
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Height = 6
            };
            mainGrid.Children.Add(topBar);
            Grid.SetRow(topBar, 0);

            // ==================== المحتوى الرئيسي ====================

            var contentGrid = new Grid();
            contentGrid.Margin = new Thickness(20, 15, 20, 15);
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // صف الأيقونة والعنوان
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.Margin = new Thickness(0, 0, 0, 12);

            // دائرة الأيقونة
            var iconBorderCircle = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Background = iconBackgroundColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 12, 0)
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = accentColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorderCircle.Child = iconText;

            // نص العنوان
            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.FindResource("TextPrimaryColor"),
                FontFamily = (FontFamily)Application.Current.FindResource("ArabicFont"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };

            topRow.Children.Add(iconBorderCircle);
            Grid.SetColumn(iconBorderCircle, 0);
            topRow.Children.Add(titleText);
            Grid.SetColumn(titleText, 1);

            contentGrid.Children.Add(topRow);
            Grid.SetRow(topRow, 0);

            // نص الرسالة
            var messageText = new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = messageTextColor,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = (FontFamily)Application.Current.FindResource("ArabicFont"),
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 20)
            };
            contentGrid.Children.Add(messageText);
            Grid.SetRow(messageText, 1);

            mainGrid.Children.Add(contentGrid);
            Grid.SetRow(contentGrid, 1);

            // ==================== زر الإجراء ====================

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // زر "حسناً"
            var okButton = new Button
            {
                Content = "حسناً",
                Width = 100,
                Height = 34,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                FontFamily = (FontFamily)Application.Current.FindResource("ArabicFont"),
                Background = accentColor,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            okButton.Click += (sender, e) => dialog.Close();

            var okButtonBorder = new Border
            {
                Background = accentColor,
                CornerRadius = new CornerRadius(8),
                Child = okButton
            };

            buttonPanel.Children.Add(okButtonBorder);

            // تأثير التمرير
            okButton.MouseEnter += (sender, e) =>
            {
                okButton.Background = Brushes.Transparent;
                if (accentColor is SolidColorBrush solidBrush)
                {
                    Color color = solidBrush.Color;
                    okButtonBorder.Background = new SolidColorBrush(Color.FromRgb(
                        (byte)(color.R * 0.85),
                        (byte)(color.G * 0.85),
                        (byte)(color.B * 0.85)
                    ));
                }
            };

            okButton.MouseLeave += (sender, e) =>
            {
                okButton.Background = Brushes.Transparent;
                okButtonBorder.Background = accentColor;
            };

            mainGrid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);

            mainBorder.Child = mainGrid;
            dialog.Content = mainBorder;

            // دعم لوحة المفاتيح
            dialog.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    dialog.Close();
                }
            };

            dialog.ShowDialog();
        }

        /// <summary>
        /// عرض رسالة نجاح
        /// </summary>
        public static void ShowSuccess(string message, string title = "تم بنجاح")
        {
            Show(message, title, MessageBoxType.Success);
        }

        /// <summary>
        /// عرض رسالة خطأ
        /// </summary>
        public static void ShowError(string message, string title = "خطأ")
        {
            Show(message, title, MessageBoxType.Error);
        }

        /// <summary>
        /// عرض رسالة تحذير
        /// </summary>
        public static void ShowWarning(string message, string title = "تنبيه")
        {
            Show(message, title, MessageBoxType.Warning);
        }

        /// <summary>
        /// عرض رسالة معلومات
        /// </summary>
        public static void ShowInfo(string message, string title = "معلومة")
        {
            Show(message, title, MessageBoxType.Info);
        }

        /// <summary>
        /// عرض رسالة استفسار
        /// </summary>
        public static void ShowQuestion(string message, string title = "استفسار")
        {
            Show(message, title, MessageBoxType.Question);
        }
    }
}