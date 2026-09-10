using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace RasidAccountingSystem.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private FixedDocument _document;
        private double _currentZoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double MaxZoom = 3.0;
        private const double MinZoom = 0.5;
        private DispatcherTimer _updateTimer;

        public PrintPreviewWindow(FixedDocument document)
        {
            InitializeComponent();
            _document = document;
            docViewer.Document = document;

            // ضبط العرض ليناسب عرض الصفحة
            docViewer.FitToWidth();
            _currentZoom = docViewer.Zoom;

            // عرض معلومات الصفحات
            UpdatePageInfo();
            UpdateZoomInfo();

            // استخدام مؤقت لتحديث المعلومات بشكل دوري
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += (s, e) =>
            {
                UpdatePageInfo();
                UpdateZoomInfo();
            };
            _updateTimer.Start();

            // تحديث المعلومات عند تغيير الصفحة عن طريق أزرار DocumentViewer
            this.Loaded += (s, e) =>
            {
                UpdatePageInfo();
                UpdateZoomInfo();
            };

            // عرض عدد الصفحات في شريط الحالة
            int pageCount = document.Pages.Count;
            lblStatus.Text = $"عدد الصفحات: {pageCount} | يمكنك استخدام أزرار التكبير والتصغير للمعاينة";

            this.Title = $"معاينة قبل الطباعة - كشف حساب المورد ({pageCount} صفحات)";
        }

        private void UpdatePageInfo()
        {
            try
            {
                // الحصول على رقم الصفحة الحالية من DocumentViewer
                // باستخدام Reflection أو الاعتماد على DocumentPaginator
                int currentPage = 1;
                int pageCount = _document.Pages.Count;

                // محاولة الحصول على الصفحة الحالية من MasterPageNumber
                try
                {
                    var masterPageNumberProperty = docViewer.GetType().GetProperty("MasterPageNumber");
                    if (masterPageNumberProperty != null)
                    {
                        currentPage = (int)masterPageNumberProperty.GetValue(docViewer) + 1;
                    }
                }
                catch
                {
                    currentPage = 1;
                }

                lblPageInfo.Text = $"صفحة {currentPage} من {pageCount}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحديث معلومات الصفحة: {ex.Message}");
                lblPageInfo.Text = "صفحة 1 من 1";
            }
        }

        private void UpdateZoomInfo()
        {
            try
            {
                double zoomPercentage = docViewer.Zoom * 100;
                lblZoomInfo.Text = $"{zoomPercentage:F0}%";
                _currentZoom = docViewer.Zoom;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحديث معلومات التكبير: {ex.Message}");
                lblZoomInfo.Text = "100%";
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintDocument(_document.DocumentPaginator, "كشف حساب المورد");
                    MessageBox.Show("تم إرسال المستند إلى الطابعة", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentZoom + ZoomStep <= MaxZoom)
                {
                    _currentZoom += ZoomStep;
                    docViewer.Zoom = _currentZoom;
                    UpdateZoomInfo();
                    lblStatus.Text = $"نسبة التكبير: {(_currentZoom * 100):F0}%";
                }
                else
                {
                    lblStatus.Text = "لقد وصلت إلى أقصى حد للتكبير";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في التكبير: {ex.Message}");
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentZoom - ZoomStep >= MinZoom)
                {
                    _currentZoom -= ZoomStep;
                    docViewer.Zoom = _currentZoom;
                    UpdateZoomInfo();
                    lblStatus.Text = $"نسبة التكبير: {(_currentZoom * 100):F0}%";
                }
                else
                {
                    lblStatus.Text = "لقد وصلت إلى أقصى حد للتصغير";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في التصغير: {ex.Message}");
            }
        }

        private void BtnFitToWidth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                docViewer.FitToWidth();
                _currentZoom = docViewer.Zoom;
                UpdateZoomInfo();
                lblStatus.Text = "تم ضبط العرض ليناسب حجم النافذة";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في ضبط العرض: {ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer = null;
            }
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer = null;
            }
            base.OnClosed(e);
        }
    }
}