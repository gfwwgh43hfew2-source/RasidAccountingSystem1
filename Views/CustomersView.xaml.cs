using Microsoft.Win32;
using OfficeOpenXml;
using RasidAccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Path = System.IO.Path;

namespace RasidAccountingSystem.Views
{
    public partial class CustomersView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _dbService;
        private string _connectionString;
        private ObservableCollection<CustomerItem> _customersList;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalCustomersCount = 0;
        private CancellationTokenSource _searchCts;
        private bool _isHeaderCheckBoxUpdating = false;

        // ألوان العملاء (نيلي/أزرق)
        private readonly Color CustomerAccentColor = Color.FromRgb(79, 70, 229);
        private readonly Color CustomerAccentLightColor = Color.FromRgb(238, 242, 255);

        #endregion

        #region المنشئ

        public CustomersView()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeEvents();
            Loaded += async (s, e) => await LoadAllData();
        }

        #endregion

        #region دوال التهيئة الأساسية

        private void InitializeDatabase()
        {
            _dbService = new DatabaseService();
            _connectionString = _dbService.GetConnectionString();
            _customersList = new ObservableCollection<CustomerItem>();
            dgCustomers.ItemsSource = _customersList;
        }

        private void InitializeEvents()
        {
            btnAddCustomer.Click += async (s, e) => await OpenAddCustomerDialog();
            btnRefresh.Click += async (s, e) => await RefreshDataAsync();
            btnImportExcel.Click += async (s, e) => await ImportFromExcel();
            btnPrevPage.Click += async (s, e) =>
            {
                if (_currentPage > 1)
                {
                    _currentPage--;
                    await LoadCustomers();
                }
            };
            btnNextPage.Click += async (s, e) =>
            {
                if (_currentPage < _totalPages)
                {
                    _currentPage++;
                    await LoadCustomers();
                }
            };

            txtSearch.TextChanged += OnSearchTextChanged;
            btnBulkDelete.Click += async (s, e) => await BulkDelete();
        }

        #endregion

        #region دوال إدارة التحديد الجماعي (Bulk Selection)

        private void UpdateBulkBar()
        {
            int selectedCount = 0;
            foreach (var item in _customersList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            BulkText.Text = $"{selectedCount} محدد";
            BulkBar.Visibility = selectedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HeaderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isHeaderCheckBoxUpdating) return;
            _isHeaderCheckBoxUpdating = true;

            foreach (var item in _customersList)
            {
                item.IsSelected = true;
            }

            _isHeaderCheckBoxUpdating = false;
            UpdateBulkBar();
        }

        private void HeaderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isHeaderCheckBoxUpdating) return;
            _isHeaderCheckBoxUpdating = true;

            foreach (var item in _customersList)
            {
                item.IsSelected = false;
            }

            _isHeaderCheckBoxUpdating = false;
            UpdateBulkBar();
        }

        private void UpdateHeaderCheckBoxState()
        {
            if (_isHeaderCheckBoxUpdating) return;

            int selectedCount = 0;
            foreach (var item in _customersList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            var headerCheckBox = FindHeaderCheckBox();
            if (headerCheckBox != null)
            {
                _isHeaderCheckBoxUpdating = true;

                if (selectedCount == 0)
                {
                    headerCheckBox.IsChecked = false;
                }
                else if (selectedCount == _customersList.Count)
                {
                    headerCheckBox.IsChecked = true;
                }
                else
                {
                    headerCheckBox.IsChecked = null;
                }

                _isHeaderCheckBoxUpdating = false;
            }
        }

        private CheckBox FindHeaderCheckBox()
        {
            if (dgCustomers.Columns.Count > 0)
            {
                var column = dgCustomers.Columns[0];
                if (column is DataGridTemplateColumn templateColumn)
                {
                    var headerTemplate = templateColumn.HeaderTemplate;
                    if (headerTemplate != null)
                    {
                        var content = headerTemplate.LoadContent();
                        if (content is FrameworkElement element)
                        {
                            return element.FindName("HeaderCheckBox") as CheckBox;
                        }
                    }
                }
            }
            return null;
        }

        #endregion

        #region دوال البحث والترقيم (Search & Pagination)

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(300, _searchCts.Token);
                _currentPage = 1;
                await LoadCustomers(txtSearch.Text.Trim());
            }
            catch (OperationCanceledException)
            {
                // تم إلغاء العملية بشكل طبيعي
            }
        }

        #endregion

        #region دوال تحديث الرصيد (Balance Update Methods)

        /// <summary>
        /// تحديث رصيد جميع العملاء بناءً على الحركات المسجلة في جدول CustomerTransactions مع إضافة رصيد أول المدة
        /// </summary>
        private async Task UpdateAllCustomersBalance()
        {
            try
            {
                await DatabaseExecutor.ExecuteVoidAsync(_dbService, async (connection) =>
                {
                    string updateSql = @"
                        UPDATE Customers SET 
                            CurrentBalance = COALESCE((
                                SELECT OpeningBalance + SUM(CreditAmount) - SUM(DebitAmount)
                                FROM CustomerTransactions 
                                WHERE CustomerID = Customers.CustomerID
                            ), OpeningBalance)
                        WHERE CustomerID IN (SELECT CustomerID FROM Customers)";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد {rowsAffected} عميل في جدول العملاء");
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateAllCustomersBalance Error: {ex.Message}");
            }
        }

        /// <summary>
        /// تحديث رصيد عميل فردي بناءً على الحركات المسجلة مع إضافة رصيد أول المدة
        /// </summary>
        private async Task UpdateSingleCustomerBalance(int customerId)
        {
            try
            {
                await DatabaseExecutor.ExecuteVoidAsync(_dbService, async (connection) =>
                {
                    string updateSql = @"
                        UPDATE Customers SET 
                            CurrentBalance = COALESCE((
                                SELECT OpeningBalance + SUM(CreditAmount) - SUM(DebitAmount)
                                FROM CustomerTransactions 
                                WHERE CustomerID = @customerId
                            ), OpeningBalance)
                        WHERE CustomerID = @customerId";

                    using (var cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        await cmd.ExecuteNonQueryAsync();
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل {customerId}");
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSingleCustomerBalance Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال الحذف الجماعي (Bulk Delete) - حذف فعلي

        private async Task BulkDelete()
        {
            int selectedCount = 0;
            foreach (var item in _customersList)
            {
                if (item.IsSelected)
                    selectedCount++;
            }

            if (selectedCount == 0)
            {
                MessageBox.Show("الرجاء تحديد العملاء الذين تريد حذفهم", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                $"هل أنت متأكد من الحذف النهائي لـ {selectedCount} عميل؟\n\nتحذير: هذا الإجراء لا يمكن التراجع عنه!",
                "تأكيد الحذف النهائي",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int deletedCount = 0;
                string cannotDeleteList = "";

                try
                {
                    await DatabaseExecutor.ExecuteNonTransactionAsync<int>(_dbService, async (connection) =>
                    {
                        int count = 0;
                        string cannotDelete = "";

                        using (var transaction = connection.BeginTransaction())
                        {
                            foreach (var item in _customersList)
                            {
                                if (item.IsSelected)
                                {
                                    // التحقق من وجود معاملات مرتبطة بهذا العميل
                                    string checkTransactionsSql = "SELECT COUNT(*) FROM CustomerTransactions WHERE CustomerID = @id";
                                    using (var checkCmd = new SQLiteCommand(checkTransactionsSql, connection, transaction))
                                    {
                                        checkCmd.Parameters.AddWithValue("@id", item.CustomerID);
                                        long transactionCount = (long)await checkCmd.ExecuteScalarAsync();

                                        if (transactionCount > 0)
                                        {
                                            cannotDelete += $"• {item.CustomerCode} - {item.CustomerNameAr} (مرتبط بـ {transactionCount} حركة)\n";
                                            continue;
                                        }
                                    }

                                    // حذف فعلي للعميل
                                    string deleteSql = "DELETE FROM Customers WHERE CustomerID = @id";
                                    using (var deleteCmd = new SQLiteCommand(deleteSql, connection, transaction))
                                    {
                                        deleteCmd.Parameters.AddWithValue("@id", item.CustomerID);
                                        await deleteCmd.ExecuteNonQueryAsync();
                                        count++;
                                    }
                                }
                            }
                            transaction.Commit();
                        }

                        deletedCount = count;
                        cannotDeleteList = cannotDelete;
                        return count;
                    }, CancellationToken.None, 3);

                    await RefreshDataAsync();

                    foreach (var item in _customersList)
                    {
                        item.IsSelected = false;
                    }

                    UpdateBulkBar();

                    string message = $"تم حذف {deletedCount} عميل نهائياً بنجاح.";
                    if (!string.IsNullOrEmpty(cannotDeleteList))
                    {
                        message += $"\n\nلا يمكن حذف العملاء التالية أسماؤهم لارتباطهم بمعاملات محاسبية:\n{cannotDeleteList}";
                    }

                    MessageBox.Show(message, "نتيجة الحذف", MessageBoxButton.OK,
                        string.IsNullOrEmpty(cannotDeleteList) ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في الحذف الجماعي: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"BulkDelete Error: {ex.Message}");
                }
            }
        }

        #endregion

        #region دوال تحميل البيانات من قاعدة البيانات

        private async Task LoadAllData()
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            await UpdateAllCustomersBalance();
            await LoadCustomers();
            await ShowSuccessNotification("تم تحديث البيانات بنجاح");
        }

        private async Task LoadCustomers(string searchText = "")
        {
            try
            {
                _customersList.Clear();

                // ✅ استخدام DatabaseExecutor لجلب البيانات
                var result = await DatabaseExecutor.ExecuteNonTransactionAsync<(List<CustomerItem> Customers, int TotalCount, int TotalPages)>(_dbService, async (connection) =>
                {
                    var customers = new List<CustomerItem>();
                    int totalCount = 0;
                    int totalPages = 1;

                    // حساب العدد الإجمالي للعملاء
                    string countSql = "SELECT COUNT(*) FROM Customers";
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        countSql += " WHERE (CustomerCode LIKE @search OR CustomerNameAr LIKE @search OR CustomerName LIKE @search OR Phone LIKE @search OR Mobile LIKE @search)";
                    }

                    using (var countCmd = new SQLiteCommand(countSql, connection))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            countCmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                        }
                        totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                        totalPages = (int)Math.Ceiling((double)totalCount / _pageSize);
                        if (totalPages == 0) totalPages = 1;
                    }

                    // جلب بيانات العملاء مع ترقيم الصفحات
                    string sql = @"
                        SELECT 
                            CustomerID, CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                            AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                            Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                            ContactPerson, ContactPersonPhone, Notes, CreatedDate
                        FROM Customers";

                    if (!string.IsNullOrEmpty(searchText))
                    {
                        sql += " WHERE (CustomerCode LIKE @search OR CustomerNameAr LIKE @search OR CustomerName LIKE @search OR Phone LIKE @search OR Mobile LIKE @search)";
                    }

                    sql += " ORDER BY CustomerCode LIMIT @limit OFFSET @offset";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                        {
                            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
                        }
                        cmd.Parameters.AddWithValue("@limit", _pageSize);
                        cmd.Parameters.AddWithValue("@offset", (_currentPage - 1) * _pageSize);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var customer = new CustomerItem
                                {
                                    CustomerID = reader.GetInt32(0),
                                    CustomerCode = reader.GetString(1),
                                    CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CustomerNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CustomerNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                    OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                    CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                    CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                    PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                    Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                    Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                    TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                    CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                    ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                    ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                    Notes = reader.IsDBNull(20) ? "" : reader.GetString(20),
                                    CreatedDate = reader.IsDBNull(21) ? DateTime.Now : reader.GetDateTime(21)
                                };
                                customers.Add(customer);
                            }
                        }
                    }

                    return (customers, totalCount, totalPages);
                }, CancellationToken.None, 3);

                // تحديث القائمة والبيانات
                foreach (var customer in result.Customers)
                {
                    _customersList.Add(customer);
                }

                _totalCustomersCount = result.TotalCount;
                _totalPages = result.TotalPages;
                lblPageInfo.Text = $"{_currentPage} / {_totalPages}";

                // تحديث واجهة المستخدم
                lblTotalCustomers.Text = _totalCustomersCount.ToString("N0");
                lblTotalBadge.Text = _totalCustomersCount.ToString("N0");

                int startRecord = (_currentPage - 1) * _pageSize + 1;
                int endRecord = Math.Min(_currentPage * _pageSize, _totalCustomersCount);
                lblPaginationInfo.Text = endRecord > 0 ? $"عرض {startRecord} إلى {endRecord} من أصل {_totalCustomersCount:N0} عميل" : "لا توجد عملاء";

                if (_customersList.Count == 0)
                {
                    dgCustomers.Visibility = Visibility.Collapsed;
                    lblNoData.Visibility = Visibility.Visible;
                }
                else
                {
                    dgCustomers.Visibility = Visibility.Visible;
                    lblNoData.Visibility = Visibility.Collapsed;
                }

                UpdateBulkBar();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل العملاء: {ex.Message}");
                dgCustomers.Visibility = Visibility.Collapsed;
                lblNoData.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region دوال استيراد البيانات من Excel

        private async Task ImportFromExcel()
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var openFileDialog = new OpenFileDialog
                {
                    Title = "اختر ملف Excel للعملاء",
                    Filter = "Excel Files|*.xlsx;*.xls",
                    DefaultExt = ".xlsx",
                    FileName = ""
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                string filePath = openFileDialog.FileName;
                int insertedCount = 0;
                int errorCount = 0;
                string errors = "";
                string insertedDetails = "";

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension.Rows;

                    if (rowCount < 2)
                    {
                        MessageBox.Show("الملف لا يحتوي على بيانات. تأكد من أن الصف الأول يحتوي على العناوين.", "تحذير", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var progressWindow = new Window
                    {
                        Width = 400,
                        Height = 150,
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Background = Brushes.Transparent,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this),
                        ShowInTaskbar = false
                    };

                    var progressBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                        CornerRadius = new CornerRadius(12),
                        Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Opacity = 0.2, Color = Colors.Black },
                        Padding = new Thickness(20)
                    };

                    var progressGrid = new Grid();
                    progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    progressGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var progressText = new TextBlock
                    {
                        Text = "جاري استيراد البيانات...",
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 15),
                        FontFamily = new FontFamily("Cairo")
                    };
                    Grid.SetRow(progressText, 0);

                    var progressBar = new ProgressBar
                    {
                        Height = 8,
                        Foreground = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
                        Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                        IsIndeterminate = true
                    };
                    Grid.SetRow(progressBar, 1);

                    progressGrid.Children.Add(progressText);
                    progressGrid.Children.Add(progressBar);
                    progressBorder.Child = progressGrid;
                    progressWindow.Content = progressBorder;

                    progressWindow.Show();

                    await Task.Run(async () =>
                    {
                        // ✅ استخدام DatabaseExecutor داخل Task.Run
                        await DatabaseExecutor.ExecuteNonTransactionAsync<int>(_dbService, async (connection) =>
                        {
                            int count = 0;
                            int errCount = 0;
                            string errList = "";
                            string insertedList = "";

                            for (int row = 2; row <= rowCount; row++)
                            {
                                try
                                {
                                    string code = GetCellValue(worksheet, row, 1);
                                    string nameAr = GetCellValue(worksheet, row, 2);
                                    string nameEn = GetCellValue(worksheet, row, 3);
                                    string phone = GetCellValue(worksheet, row, 4);
                                    string mobile = GetCellValue(worksheet, row, 5);
                                    string email = GetCellValue(worksheet, row, 6);
                                    string address = GetCellValue(worksheet, row, 7);
                                    decimal openingBalance = GetCellDecimal(worksheet, row, 8);
                                    decimal creditLimit = GetCellDecimal(worksheet, row, 9);
                                    string taxNumber = GetCellValue(worksheet, row, 10);
                                    string commercialRegister = GetCellValue(worksheet, row, 11);
                                    string contactPerson = GetCellValue(worksheet, row, 12);
                                    string contactPersonPhone = GetCellValue(worksheet, row, 13);

                                    if (string.IsNullOrEmpty(code))
                                    {
                                        errCount++;
                                        errList += $"⚠️ الصف {row}: كود العميل مطلوب\n";
                                        continue;
                                    }

                                    if (string.IsNullOrEmpty(nameAr))
                                    {
                                        errCount++;
                                        errList += $"⚠️ الصف {row}: اسم العميل مطلوب\n";
                                        continue;
                                    }

                                    string checkSql = "SELECT COUNT(*) FROM Customers WHERE CustomerCode = @code";
                                    using (var checkCmd = new SQLiteCommand(checkSql, connection))
                                    {
                                        checkCmd.Parameters.AddWithValue("@code", code);
                                        long exists = (long)await checkCmd.ExecuteScalarAsync();

                                        if (exists > 0)
                                        {
                                            errCount++;
                                            errList += $"❌ الصف {row}: كود العميل '{code}' موجود مسبقاً\n";
                                            continue;
                                        }
                                    }

                                    string insertSql = @"
                                        INSERT INTO Customers (
                                            CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                                            Phone, Mobile, Email, Address, OpeningBalance, CurrentBalance,
                                            CreditLimit, TaxNumber, CommercialRegister,
                                            ContactPerson, ContactPersonPhone, CreatedDate
                                        ) VALUES (
                                            @code, @nameAr, @nameAr, @nameEn,
                                            @phone, @mobile, @email, @address, @openingBalance, @openingBalance,
                                            @creditLimit, @taxNumber, @commercialRegister,
                                            @contactPerson, @contactPersonPhone, CURRENT_TIMESTAMP
                                        )";

                                    using (var cmd = new SQLiteCommand(insertSql, connection))
                                    {
                                        cmd.Parameters.AddWithValue("@code", code);
                                        cmd.Parameters.AddWithValue("@nameAr", nameAr);
                                        cmd.Parameters.AddWithValue("@nameEn", nameEn ?? "");
                                        cmd.Parameters.AddWithValue("@phone", phone ?? "");
                                        cmd.Parameters.AddWithValue("@mobile", mobile ?? "");
                                        cmd.Parameters.AddWithValue("@email", email ?? "");
                                        cmd.Parameters.AddWithValue("@address", address ?? "");
                                        cmd.Parameters.AddWithValue("@openingBalance", openingBalance);
                                        cmd.Parameters.AddWithValue("@creditLimit", creditLimit);
                                        cmd.Parameters.AddWithValue("@taxNumber", taxNumber ?? "");
                                        cmd.Parameters.AddWithValue("@commercialRegister", commercialRegister ?? "");
                                        cmd.Parameters.AddWithValue("@contactPerson", contactPerson ?? "");
                                        cmd.Parameters.AddWithValue("@contactPersonPhone", contactPersonPhone ?? "");

                                        await cmd.ExecuteNonQueryAsync();
                                        count++;
                                        insertedList += $"➕ الصف {row}: {code} - {nameAr}\n";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errCount++;
                                    errList += $"❌ الصف {row}: {ex.Message}\n";
                                }
                            }

                            insertedCount = count;
                            errorCount = errCount;
                            errors = errList;
                            insertedDetails = insertedList;
                            return count;
                        }, CancellationToken.None, 3);
                    });

                    progressWindow.Close();

                    string message = $"✅ تم استيراد {insertedCount} عميل بنجاح.\n\n{insertedDetails}";
                    if (errorCount > 0)
                    {
                        message += $"\n\n❌ فشل استيراد {errorCount} عميل:\n{errors}";
                    }

                    if (insertedCount == 0 && errorCount == 0)
                    {
                        message = "لم يتم العثور على بيانات للاستيراد.";
                    }

                    MessageBox.Show(message, "نتيجة استيراد العملاء", MessageBoxButton.OK,
                        errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                    if (insertedCount > 0)
                    {
                        await RefreshDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في استيراد البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ImportFromExcel Error: {ex.Message}");
            }
        }

        private string GetCellValue(ExcelWorksheet worksheet, int row, int col)
        {
            try
            {
                var cell = worksheet.Cells[row, col];
                if (cell == null || cell.Value == null)
                    return "";

                return cell.Value.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        private decimal GetCellDecimal(ExcelWorksheet worksheet, int row, int col)
        {
            try
            {
                var cell = worksheet.Cells[row, col];
                if (cell == null || cell.Value == null)
                    return 0;

                if (decimal.TryParse(cell.Value.ToString(), out decimal result))
                    return result;

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region دوال فتح وإدارة الديالوجات (Dialog Management)

        private async Task OpenAddCustomerDialog()
        {
            try
            {
                string generatedCode = await GenerateUniqueCustomerCodeAsync();
                var dialog = new Window
                {
                    Width = 600,
                    Height = 650,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Topmost = false
                };

                var content = CreateModernCustomerDialog(false, dialog, generatedCode);
                dialog.Content = content;

                ShowDialog(dialog);

                if (dialog.DialogResult == true)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في فتح نافذة إضافة عميل: {ex.Message}");
                MessageBox.Show("حدث خطأ أثناء فتح نافذة إضافة عميل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenEditCustomerDialog(int customerId)
        {
            try
            {
                CustomerItem customerToEdit = null;
                foreach (var c in _customersList)
                {
                    if (c.CustomerID == customerId)
                    {
                        customerToEdit = c;
                        break;
                    }
                }

                if (customerToEdit == null)
                {
                    customerToEdit = await GetCustomerByIdAsync(customerId);
                    if (customerToEdit == null)
                    {
                        MessageBox.Show("لم يتم العثور على العميل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var dialog = new Window
                {
                    Width = 600,
                    Height = 650,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Topmost = false
                };

                var content = CreateModernCustomerDialog(true, dialog, null);
                dialog.Content = content;

                FillModernCustomerDialog(content, customerToEdit);

                ShowDialog(dialog);

                if (dialog.DialogResult == true)
                {
                    await RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في فتح نافذة تعديل عميل: {ex.Message}");
                MessageBox.Show("حدث خطأ أثناء فتح نافذة تعديل عميل", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDialog(Window dialog)
        {
            var mainWindow = Window.GetWindow(this);

            if (mainWindow != null)
            {
                var blurEffect = new BlurEffect
                {
                    Radius = 8,
                    KernelType = KernelType.Gaussian
                };
                mainWindow.Effect = blurEffect;
                mainWindow.IsEnabled = false;

                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();

                mainWindow.Effect = null;
                mainWindow.IsEnabled = true;
                mainWindow.Activate();
                mainWindow.Focus();
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.ShowDialog();
            }
        }

        #endregion

        #region دوال إنشاء وتعبئة الديالوج (Dialog Creation & Filling)

        private Border CreateModernCustomerDialog(bool isEdit, Window dialog, string generatedCode)
        {
            Color accentColor = isEdit ? Color.FromRgb(245, 158, 11) : Color.FromRgb(79, 70, 229);
            Color accentLight = isEdit ? Color.FromRgb(254, 243, 199) : Color.FromRgb(238, 242, 255);
            SolidColorBrush accentBrush = new SolidColorBrush(accentColor);
            SolidColorBrush accentLightBrush = new SolidColorBrush(accentLight);

            Color backgroundColor = Color.FromRgb(240, 248, 255);
            SolidColorBrush backgroundBrush = new SolidColorBrush(backgroundColor);

            var mainBorder = new Border
            {
                Background = backgroundBrush,
                CornerRadius = new CornerRadius(16),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.15,
                    Color = Colors.Black
                }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topBar = new Border
            {
                Background = accentBrush,
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                Height = 4
            };
            mainGrid.Children.Add(topBar);
            Grid.SetRow(topBar, 0);

            var headerGrid = new Grid
            {
                Margin = new Thickness(16, 12, 16, 8),
                Height = 50
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = accentLightBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(iconBorder, 0);
            var iconTextBlock = new TextBlock
            {
                Text = isEdit ? "✏️" : "👥",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconTextBlock;
            headerGrid.Children.Add(iconBorder);

            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleStack, 1);
            var titleTextBlock = new TextBlock
            {
                Text = isEdit ? "تعديل بيانات العميل" : "إضافة عميل جديد",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                FontFamily = new FontFamily("Cairo")
            };
            var subtitleTextBlock = new TextBlock
            {
                Text = isEdit ? "قم بتعديل معلومات العميل" : "أدخل معلومات العميل الجديد",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(titleTextBlock);
            titleStack.Children.Add(subtitleTextBlock);
            headerGrid.Children.Add(titleStack);

            var closeBtn = new Button
            {
                Width = 28,
                Height = 28,
                Content = "✕",
                Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
            var closeBtnBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Child = closeBtn
            };
            closeBtn.MouseEnter += (s, e) =>
            {
                closeBtn.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            };
            closeBtn.MouseLeave += (s, e) =>
            {
                closeBtn.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
            };
            closeBtn.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            Grid.SetColumn(closeBtnBorder, 3);
            headerGrid.Children.Add(closeBtnBorder);

            mainGrid.Children.Add(headerGrid);
            Grid.SetRow(headerGrid, 1);

            var contentBorder = new Border
            {
                Margin = new Thickness(16, 0, 16, 12),
                Background = Brushes.Transparent
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

            TextBox codeBox = null;
            TextBox nameArBox = null;
            TextBox nameEnBox = null;
            TextBox phoneBox = null;
            TextBox mobileBox = null;
            TextBox emailBox = null;
            TextBox addressBox = null;
            TextBox creditBox = null;
            TextBox openingBalanceBox = null;

            void AddField(string icon, string label, TextBox textBox, int marginBottom = 6)
            {
                var container = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, marginBottom),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconTextBlockField = new TextBlock
                {
                    Text = icon,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(iconTextBlockField, 0);

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Margin = new Thickness(0, 0, 0, 2)
                });
                textBox.FontSize = 12;
                textBox.BorderThickness = new Thickness(0);
                textBox.Background = Brushes.Transparent;
                panel.Children.Add(textBox);
                Grid.SetColumn(panel, 1);

                grid.Children.Add(iconTextBlockField);
                grid.Children.Add(panel);
                container.Child = grid;
                stackPanel.Children.Add(container);
            }

            codeBox = new TextBox { FontFamily = new FontFamily("Consolas") };
            codeBox.FontWeight = FontWeights.Normal;
            if (isEdit)
            {
                codeBox.IsReadOnly = true;
                codeBox.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                codeBox.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            }
            else
            {
                codeBox.Foreground = new SolidColorBrush(accentColor);
            }
            AddField("🆔", "كود العميل", codeBox, 6);

            nameArBox = new TextBox();
            AddField("👤", "اسم العميل *", nameArBox, 6);

            nameEnBox = new TextBox();
            AddField("🌐", "الاسم بالإنجليزية", nameEnBox, 6);

            var contactGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            contactGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contactGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            contactGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            phoneBox = new TextBox();
            var phoneContainer = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };
            var phoneGrid = new Grid();
            phoneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            phoneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var phoneIconText = new TextBlock { Text = "📞", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(phoneIconText, 0);
            var phonePanel = new StackPanel();
            phonePanel.Children.Add(new TextBlock { Text = "رقم الهاتف", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 2) });
            phoneBox.FontSize = 12; phoneBox.BorderThickness = new Thickness(0); phoneBox.Background = Brushes.Transparent;
            phonePanel.Children.Add(phoneBox);
            Grid.SetColumn(phonePanel, 1);
            phoneGrid.Children.Add(phoneIconText);
            phoneGrid.Children.Add(phonePanel);
            phoneContainer.Child = phoneGrid;
            Grid.SetColumn(phoneContainer, 0);

            mobileBox = new TextBox();
            var mobileContainer = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };
            var mobileGrid = new Grid();
            mobileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mobileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var mobileIconText = new TextBlock { Text = "📱", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(mobileIconText, 0);
            var mobilePanel = new StackPanel();
            mobilePanel.Children.Add(new TextBlock { Text = "رقم الجوال", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 2) });
            mobileBox.FontSize = 12; mobileBox.BorderThickness = new Thickness(0); mobileBox.Background = Brushes.Transparent;
            mobilePanel.Children.Add(mobileBox);
            Grid.SetColumn(mobilePanel, 1);
            mobileGrid.Children.Add(mobileIconText);
            mobileGrid.Children.Add(mobilePanel);
            mobileContainer.Child = mobileGrid;
            Grid.SetColumn(mobileContainer, 2);

            contactGrid.Children.Add(phoneContainer);
            contactGrid.Children.Add(mobileContainer);
            stackPanel.Children.Add(contactGrid);

            emailBox = new TextBox();
            AddField("✉️", "البريد الإلكتروني", emailBox, 6);

            addressBox = new TextBox();
            AddField("📍", "العنوان", addressBox, 6);

            creditBox = new TextBox { Text = "0" };
            AddField("💰", "الحد الائتماني", creditBox, 6);

            openingBalanceBox = new TextBox { Text = "0" };
            AddField("💵", "رصيد أول المدة", openingBalanceBox, 10);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 10)
            };

            var saveBtn = new Button
            {
                Content = isEdit ? "تحديث" : "إضافة",
                Width = 110,
                Height = 34,
                Background = accentBrush,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo")
            };
            var saveBtnBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = saveBtn,
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Opacity = 0.2, Color = accentColor },
                Margin = new Thickness(0, 0, 8, 0)
            };
            saveBtn.MouseEnter += (s, e) => saveBtn.Opacity = 0.9;
            saveBtn.MouseLeave += (s, e) => saveBtn.Opacity = 1;

            var cancelBtn = new Button
            {
                Content = "إلغاء",
                Width = 80,
                Height = 34,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Cairo")
            };
            var cancelBtnBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = cancelBtn
            };
            cancelBtn.MouseEnter += (s, e) =>
            {
                cancelBtn.Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                cancelBtn.BorderBrush = accentBrush;
            };
            cancelBtn.MouseLeave += (s, e) =>
            {
                cancelBtn.Background = Brushes.White;
                cancelBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            };

            buttonPanel.Children.Add(saveBtnBorder);
            buttonPanel.Children.Add(cancelBtnBorder);
            stackPanel.Children.Add(buttonPanel);

            contentBorder.Child = stackPanel;
            mainGrid.Children.Add(contentBorder);
            Grid.SetRow(contentBorder, 2);

            if (!isEdit)
            {
                codeBox.Text = generatedCode ?? "CUS-000001";
                nameArBox.Focus();
            }

            saveBtn.Click += async (s, ev) =>
            {
                string code = codeBox.Text.Trim();
                string nameAr = nameArBox.Text.Trim();

                if (string.IsNullOrEmpty(nameAr))
                {
                    MessageBox.Show("يرجى إدخال اسم العميل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    nameArBox.Focus();
                    return;
                }

                if (!decimal.TryParse(creditBox.Text, out decimal creditLimit) || creditLimit < 0)
                {
                    MessageBox.Show("الحد الائتماني غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    creditBox.Focus();
                    return;
                }

                if (!decimal.TryParse(openingBalanceBox.Text, out decimal openingBalance) || openingBalance < 0)
                {
                    MessageBox.Show("رصيد أول المدة غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    openingBalanceBox.Focus();
                    return;
                }

                bool codeExists = false;
                if (!isEdit)
                {
                    codeExists = await CustomerCodeExistsAsync(code);
                }

                if (codeExists)
                {
                    MessageBox.Show($"كود العميل '{code}' موجود مسبقاً. الرجاء استخدام كود آخر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    codeBox.Focus();
                    return;
                }

                var customer = new CustomerItem
                {
                    CustomerCode = code,
                    CustomerNameAr = nameAr,
                    CustomerNameEn = nameEnBox.Text.Trim(),
                    Phone = phoneBox.Text.Trim(),
                    Mobile = mobileBox.Text.Trim(),
                    Email = emailBox.Text.Trim(),
                    Address = addressBox.Text.Trim(),
                    CreditLimit = creditLimit,
                    OpeningBalance = openingBalance,
                    IsActive = true
                };

                if (isEdit)
                {
                    var tagData = dialog.Tag as Tuple<int, CustomerItem>;
                    if (tagData != null)
                    {
                        customer.CustomerID = tagData.Item1;
                        await UpdateCustomerAsync(customer);
                    }
                }
                else
                {
                    await AddCustomerAsync(customer);
                }

                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelBtn.Click += (s, ev) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            mainBorder.Child = mainGrid;
            return mainBorder;
        }

        private void FillModernCustomerDialog(Border dialogContent, CustomerItem customer)
        {
            var mainGrid = dialogContent.Child as Grid;
            if (mainGrid != null && mainGrid.Children.Count > 1)
            {
                var contentBorder = mainGrid.Children[2] as Border;
                if (contentBorder != null)
                {
                    var stackPanel = contentBorder.Child as StackPanel;
                    if (stackPanel != null && stackPanel.Children.Count >= 12)
                    {
                        int childIndex = 0;

                        if (stackPanel.Children[childIndex] is Border codeContainer)
                        {
                            var codeGrid = codeContainer.Child as Grid;
                            if (codeGrid != null && codeGrid.Children.Count > 1)
                            {
                                var codePanel = codeGrid.Children[1] as StackPanel;
                                if (codePanel != null && codePanel.Children.Count > 1)
                                {
                                    if (codePanel.Children[1] is TextBox codeBox)
                                    {
                                        codeBox.Text = customer.CustomerCode;
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border nameArContainer)
                        {
                            var nameArGrid = nameArContainer.Child as Grid;
                            if (nameArGrid != null && nameArGrid.Children.Count > 1)
                            {
                                var nameArPanel = nameArGrid.Children[1] as StackPanel;
                                if (nameArPanel != null && nameArPanel.Children.Count > 1)
                                {
                                    if (nameArPanel.Children[1] is TextBox nameArBox)
                                    {
                                        nameArBox.Text = customer.CustomerNameAr;
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border nameEnContainer)
                        {
                            var nameEnGrid = nameEnContainer.Child as Grid;
                            if (nameEnGrid != null && nameEnGrid.Children.Count > 1)
                            {
                                var nameEnPanel = nameEnGrid.Children[1] as StackPanel;
                                if (nameEnPanel != null && nameEnPanel.Children.Count > 1)
                                {
                                    if (nameEnPanel.Children[1] is TextBox nameEnBox)
                                    {
                                        nameEnBox.Text = customer.CustomerNameEn;
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Grid contactGrid)
                        {
                            if (contactGrid.Children.Count > 0 && contactGrid.Children[0] is Border phoneContainer)
                            {
                                var phoneGrid = phoneContainer.Child as Grid;
                                if (phoneGrid != null && phoneGrid.Children.Count > 1)
                                {
                                    var phonePanel = phoneGrid.Children[1] as StackPanel;
                                    if (phonePanel != null && phonePanel.Children.Count > 1)
                                    {
                                        if (phonePanel.Children[1] is TextBox phoneBox)
                                        {
                                            phoneBox.Text = customer.Phone;
                                        }
                                    }
                                }
                            }

                            if (contactGrid.Children.Count > 1 && contactGrid.Children[1] is Border mobileContainer)
                            {
                                var mobileGrid = mobileContainer.Child as Grid;
                                if (mobileGrid != null && mobileGrid.Children.Count > 1)
                                {
                                    var mobilePanel = mobileGrid.Children[1] as StackPanel;
                                    if (mobilePanel != null && mobilePanel.Children.Count > 1)
                                    {
                                        if (mobilePanel.Children[1] is TextBox mobileBox)
                                        {
                                            mobileBox.Text = customer.Mobile;
                                        }
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border emailContainer)
                        {
                            var emailGrid = emailContainer.Child as Grid;
                            if (emailGrid != null && emailGrid.Children.Count > 1)
                            {
                                var emailPanel = emailGrid.Children[1] as StackPanel;
                                if (emailPanel != null && emailPanel.Children.Count > 1)
                                {
                                    if (emailPanel.Children[1] is TextBox emailBox)
                                    {
                                        emailBox.Text = customer.Email;
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border addressContainer)
                        {
                            var addressGrid = addressContainer.Child as Grid;
                            if (addressGrid != null && addressGrid.Children.Count > 1)
                            {
                                var addressPanel = addressGrid.Children[1] as StackPanel;
                                if (addressPanel != null && addressPanel.Children.Count > 1)
                                {
                                    if (addressPanel.Children[1] is TextBox addressBox)
                                    {
                                        addressBox.Text = customer.Address;
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border creditContainer)
                        {
                            var creditGrid = creditContainer.Child as Grid;
                            if (creditGrid != null && creditGrid.Children.Count > 1)
                            {
                                var creditPanel = creditGrid.Children[1] as StackPanel;
                                if (creditPanel != null && creditPanel.Children.Count > 1)
                                {
                                    if (creditPanel.Children[1] is TextBox creditLimitBox)
                                    {
                                        creditLimitBox.Text = customer.CreditLimit.ToString("N0");
                                    }
                                }
                            }
                        }
                        childIndex++;

                        if (stackPanel.Children[childIndex] is Border openingBalanceContainer)
                        {
                            var openingBalanceGrid = openingBalanceContainer.Child as Grid;
                            if (openingBalanceGrid != null && openingBalanceGrid.Children.Count > 1)
                            {
                                var openingBalancePanel = openingBalanceGrid.Children[1] as StackPanel;
                                if (openingBalancePanel != null && openingBalancePanel.Children.Count > 1)
                                {
                                    if (openingBalancePanel.Children[1] is TextBox openingBalanceBox)
                                    {
                                        openingBalanceBox.Text = customer.OpeningBalance.ToString("N2");
                                    }
                                }
                            }
                        }

                        var dialogWindow = Window.GetWindow(dialogContent);
                        if (dialogWindow != null)
                        {
                            dialogWindow.Tag = Tuple.Create(customer.CustomerID, customer);
                        }
                    }
                }
            }
        }

        #endregion

        #region دوال عمليات قاعدة البيانات (CRUD Operations) - معدلة

        private async Task<string> GenerateUniqueCustomerCodeAsync()
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<string>(_dbService, async (connection) =>
                {
                    string sql = "SELECT MAX(CAST(SUBSTR(CustomerCode, 5) AS INTEGER)) FROM Customers WHERE CustomerCode LIKE 'CUS-%'";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        object result = await cmd.ExecuteScalarAsync();
                        int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                        return $"CUS-{nextNumber:D6}";
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateUniqueCustomerCodeAsync Error: {ex.Message}");
                return $"CUS-{DateTime.Now:yyyyMMddHHmmss}";
            }
        }

        private async Task<bool> CustomerCodeExistsAsync(string customerCode, int excludeCustomerId = 0)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                {
                    string sql = "SELECT COUNT(*) FROM Customers WHERE CustomerCode = @code AND CustomerID != @excludeId";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", customerCode);
                        cmd.Parameters.AddWithValue("@excludeId", excludeCustomerId);
                        long count = (long)await cmd.ExecuteScalarAsync();
                        return count > 0;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CustomerCodeExistsAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<CustomerItem> GetCustomerByIdAsync(int customerId)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<CustomerItem>(_dbService, async (connection) =>
                {
                    string sql = @"
                        SELECT 
                            CustomerID, CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                            AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                            Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                            ContactPerson, ContactPersonPhone, Notes, CreatedDate
                        FROM Customers 
                        WHERE CustomerID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", customerId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new CustomerItem
                                {
                                    CustomerID = reader.GetInt32(0),
                                    CustomerCode = reader.GetString(1),
                                    CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CustomerNameAr = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    CustomerNameEn = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    AccountID = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                    OpeningBalance = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                                    CurrentBalance = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                    CreditLimit = reader.IsDBNull(8) ? 0 : reader.GetDecimal(8),
                                    PaymentTerms = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    Phone = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    Mobile = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    Fax = reader.IsDBNull(12) ? "" : reader.GetString(12),
                                    Email = reader.IsDBNull(13) ? "" : reader.GetString(13),
                                    Website = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                    Address = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                    TaxNumber = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                    CommercialRegister = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                    ContactPerson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                                    ContactPersonPhone = reader.IsDBNull(19) ? "" : reader.GetString(19),
                                    Notes = reader.IsDBNull(20) ? "" : reader.GetString(20),
                                    CreatedDate = reader.IsDBNull(21) ? DateTime.Now : reader.GetDateTime(21)
                                };
                            }
                        }
                    }
                    return null;
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerByIdAsync Error: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> AddCustomerAsync(CustomerItem customer)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                {
                    string sql = @"
                        INSERT INTO Customers (
                            CustomerCode, CustomerName, CustomerNameAr, CustomerNameEn,
                            AccountID, OpeningBalance, CurrentBalance, CreditLimit, PaymentTerms,
                            Phone, Mobile, Fax, Email, Website, Address, TaxNumber, CommercialRegister,
                            ContactPerson, ContactPersonPhone, Notes, CreatedDate
                        ) VALUES (
                            @code, @nameAr, @nameAr, @nameEn, @accountId, @openingBalance, @openingBalance,
                            @creditLimit, @paymentTerms, @phone, @mobile, @fax, @email, @website,
                            @address, @taxNumber, @commercialRegister, @contactPerson, @contactPersonPhone,
                            @notes, CURRENT_TIMESTAMP
                        )";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", customer.CustomerCode);
                        cmd.Parameters.AddWithValue("@nameAr", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", customer.CustomerNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", customer.AccountID > 0 ? customer.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@openingBalance", customer.OpeningBalance);
                        cmd.Parameters.AddWithValue("@creditLimit", customer.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", customer.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", customer.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", customer.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", customer.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", customer.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", customer.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", customer.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", customer.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", customer.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", customer.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", customer.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", customer.Notes ?? "");

                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إضافة عميل: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UpdateCustomerAsync(CustomerItem customer)
        {
            try
            {
                bool success = await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                {
                    string sql = @"
                        UPDATE Customers SET 
                            CustomerNameAr = @nameAr,
                            CustomerNameEn = @nameEn,
                            AccountID = @accountId,
                            CreditLimit = @creditLimit,
                            PaymentTerms = @paymentTerms,
                            Phone = @phone,
                            Mobile = @mobile,
                            Fax = @fax,
                            Email = @email,
                            Website = @website,
                            Address = @address,
                            TaxNumber = @taxNumber,
                            CommercialRegister = @commercialRegister,
                            ContactPerson = @contactPerson,
                            ContactPersonPhone = @contactPersonPhone,
                            Notes = @notes,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE CustomerID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@nameAr", customer.CustomerNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", customer.CustomerNameEn ?? "");
                        cmd.Parameters.AddWithValue("@accountId", customer.AccountID > 0 ? customer.AccountID : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@creditLimit", customer.CreditLimit);
                        cmd.Parameters.AddWithValue("@paymentTerms", customer.PaymentTerms);
                        cmd.Parameters.AddWithValue("@phone", customer.Phone ?? "");
                        cmd.Parameters.AddWithValue("@mobile", customer.Mobile ?? "");
                        cmd.Parameters.AddWithValue("@fax", customer.Fax ?? "");
                        cmd.Parameters.AddWithValue("@email", customer.Email ?? "");
                        cmd.Parameters.AddWithValue("@website", customer.Website ?? "");
                        cmd.Parameters.AddWithValue("@address", customer.Address ?? "");
                        cmd.Parameters.AddWithValue("@taxNumber", customer.TaxNumber ?? "");
                        cmd.Parameters.AddWithValue("@commercialRegister", customer.CommercialRegister ?? "");
                        cmd.Parameters.AddWithValue("@contactPerson", customer.ContactPerson ?? "");
                        cmd.Parameters.AddWithValue("@contactPersonPhone", customer.ContactPersonPhone ?? "");
                        cmd.Parameters.AddWithValue("@notes", customer.Notes ?? "");
                        cmd.Parameters.AddWithValue("@id", customer.CustomerID);

                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }, CancellationToken.None, 3);

                if (success)
                {
                    await UpdateSingleCustomerBalance(customer.CustomerID);
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحديث عميل: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeleteCustomerAsync(int customerId)
        {
            try
            {
                return await DatabaseExecutor.ExecuteNonTransactionAsync<bool>(_dbService, async (connection) =>
                {
                    string checkTransactionsSql = "SELECT COUNT(*) FROM CustomerTransactions WHERE CustomerID = @id";
                    using (var checkCmd = new SQLiteCommand(checkTransactionsSql, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@id", customerId);
                        long transactionCount = (long)await checkCmd.ExecuteScalarAsync();

                        if (transactionCount > 0)
                        {
                            MessageBox.Show($"لا يمكن حذف العميل لأنه مرتبط بـ {transactionCount} حركة محاسبية. قم بحذف الحركات أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }

                    string sql = "DELETE FROM Customers WHERE CustomerID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", customerId);
                        return await cmd.ExecuteNonQueryAsync() > 0;
                    }
                }, CancellationToken.None, 3);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حذف العميل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteCustomerAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال الإشعارات (Notifications)

        private async Task ShowSuccessNotification(string message)
        {
            var notification = new Window
            {
                Width = 350,
                Height = 80,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ShowInTaskbar = false
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                CornerRadius = new CornerRadius(12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.3,
                    Color = Colors.Black
                },
                Padding = new Thickness(20, 15, 20, 15)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = "✓",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(icon, 0);

            var textBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Cairo")
            };
            Grid.SetColumn(textBlock, 1);

            grid.Children.Add(icon);
            grid.Children.Add(textBlock);
            border.Child = grid;
            notification.Content = border;

            notification.Show();

            await Task.Delay(2000);
            notification.Close();
        }

        #endregion

        #region أحداث النقر على الأزرار (Button Click Events)

        private async void EditRow_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int customerId = (int)button.Tag;
                await OpenEditCustomerDialog(customerId);
            }
        }

        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int customerId = (int)button.Tag;

                string customerName = "";
                foreach (var c in _customersList)
                {
                    if (c.CustomerID == customerId)
                    {
                        customerName = c.CustomerNameAr;
                        break;
                    }
                }

                MessageBoxResult result = MessageBox.Show(
                    $"هل أنت متأكد من الحذف النهائي للعميل '{customerName}'؟\n\nتحذير: هذا الإجراء لا يمكن التراجع عنه!",
                    "تأكيد الحذف النهائي",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = await DeleteCustomerAsync(customerId);
                    if (success)
                    {
                        await RefreshDataAsync();
                    }
                }
            }
        }

        #endregion
    }
}