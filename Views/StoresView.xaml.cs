using RasidAccountingSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RasidAccountingSystem.Views
{
    public partial class StoresView : UserControl
    {
        #region المتغيرات الخاصة

        private DatabaseService _databaseService;
        private ObservableCollection<StoreItem> _storesList;
        private bool _isEditMode = false;
        private int _editingStoreId = 0;

        #endregion

        #region كلاس المخزن

        public class StoreItem : INotifyPropertyChanged
        {
            private int _storeID;
            private string _storeCode;
            private string _storeNameAr;
            private string _storeNameEn;
            private string _storeType;
            private string _location;
            private string _responsiblePerson;
            private string _phone;
            private string _email;
            private string _notes;
            private bool _isActive;
            private int _serialNumber;

            public int StoreID
            {
                get => _storeID;
                set { _storeID = value; OnPropertyChanged(nameof(StoreID)); }
            }

            public string StoreCode
            {
                get => _storeCode;
                set { _storeCode = value; OnPropertyChanged(nameof(StoreCode)); }
            }

            public string StoreNameAr
            {
                get => _storeNameAr;
                set { _storeNameAr = value; OnPropertyChanged(nameof(StoreNameAr)); }
            }

            public string StoreNameEn
            {
                get => _storeNameEn;
                set { _storeNameEn = value; OnPropertyChanged(nameof(StoreNameEn)); }
            }

            public string StoreType
            {
                get => _storeType;
                set { _storeType = value; OnPropertyChanged(nameof(StoreType)); }
            }

            public string Location
            {
                get => _location;
                set { _location = value; OnPropertyChanged(nameof(Location)); }
            }

            public string ResponsiblePerson
            {
                get => _responsiblePerson;
                set { _responsiblePerson = value; OnPropertyChanged(nameof(ResponsiblePerson)); }
            }

            public string Phone
            {
                get => _phone;
                set { _phone = value; OnPropertyChanged(nameof(Phone)); }
            }

            public string Email
            {
                get => _email;
                set { _email = value; OnPropertyChanged(nameof(Email)); }
            }

            public string Notes
            {
                get => _notes;
                set { _notes = value; OnPropertyChanged(nameof(Notes)); }
            }

            public bool IsActive
            {
                get => _isActive;
                set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
            }

            public int SerialNumber
            {
                get => _serialNumber;
                set { _serialNumber = value; OnPropertyChanged(nameof(SerialNumber)); }
            }

            public string StatusText => IsActive ? "نشط" : "غير نشط";

            // إصلاح خطأ الألوان - استخدام ألوان ثابتة بدلاً من البحث عن موارد
            public SolidColorBrush StatusColor => IsActive ?
                new SolidColorBrush(Color.FromRgb(209, 250, 229)) : // لون أخضر فاتح (#D1FAE5)
                new SolidColorBrush(Color.FromRgb(229, 231, 235));   // لون رمادي فاتح (#E5E7EB)

            public SolidColorBrush StatusForeground => IsActive ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :   // لون أخضر (#10B981)
                new SolidColorBrush(Color.FromRgb(107, 114, 128));   // لون رمادي (#6B7280)

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region المنشئ

        public StoresView()
        {
            try
            {
                InitializeComponent();
                InitializeDatabase();
                InitializeEvents();
                Loaded += async (s, e) => await LoadStoresAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال التهيئة

        private void InitializeDatabase()
        {
            try
            {
                _databaseService = new DatabaseService();
                _storesList = new ObservableCollection<StoreItem>();
                dgStores.ItemsSource = _storesList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تهيئة قاعدة البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"InitializeDatabase Error: {ex.Message}");
            }
        }

        private void InitializeEvents()
        {
            try
            {
                btnAddStore.Click += async (s, e) => await OpenAddStoreDialog();
                btnRefresh.Click += async (s, e) => await RefreshStoresAsync();
                btnCloseDialog.Click += (s, e) => CloseDialog();
                btnCancelStore.Click += (s, e) => CloseDialog();
                btnSaveStore.Click += async (s, e) => await SaveStoreAsync();

                StoreDialogOverlay.MouseDown += (s, e) =>
                {
                    if (e.OriginalSource == StoreDialogOverlay)
                        CloseDialog();
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeEvents Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال تحميل البيانات

        private async Task LoadStoresAsync()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _storesList.Clear();

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();
                    string sql = "SELECT StoreID, StoreCode, StoreNameAr, StoreNameEn, StoreType, Location, ResponsiblePerson, Phone, Email, Notes, IsActive FROM Stores WHERE IsActive = 1 ORDER BY StoreCode";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        int serial = 1;
                        while (await reader.ReadAsync())
                        {
                            _storesList.Add(new StoreItem
                            {
                                SerialNumber = serial++,
                                StoreID = reader.GetInt32(0),
                                StoreCode = reader.GetString(1),
                                StoreNameAr = reader.GetString(2),
                                StoreNameEn = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                StoreType = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Location = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                ResponsiblePerson = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                Phone = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                Email = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Notes = reader.IsDBNull(9) ? "" : reader.GetString(9),
                                IsActive = reader.GetInt32(10) == 1
                            });
                        }
                    }
                }

                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"تم تحميل {_storesList.Count} مخزن");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في تحميل المخازن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"LoadStoresAsync Error: {ex.Message}");
            }
        }

        private async Task RefreshStoresAsync()
        {
            await LoadStoresAsync();
            MessageBox.Show("تم تحديث البيانات", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region دوال النوافذ المنبثقة

        private void CloseDialog()
        {
            StoreDialogOverlay.Visibility = Visibility.Collapsed;
            ClearDialogFields();
            _isEditMode = false;
            _editingStoreId = 0;
        }

        private void ClearDialogFields()
        {
            txtStoreCode.Text = string.Empty;
            txtStoreNameAr.Text = string.Empty;
            txtStoreNameEn.Text = string.Empty;
            cmbStoreType.SelectedIndex = 0;
            txtLocation.Text = string.Empty;
            txtResponsiblePerson.Text = string.Empty;
            txtPhone.Text = string.Empty;
            txtEmail.Text = string.Empty;
            txtNotes.Text = string.Empty;
        }

        private async Task OpenAddStoreDialog()
        {
            _isEditMode = false;
            _editingStoreId = 0;
            DialogTitle.Text = "إضافة مخزن جديد";
            ClearDialogFields();

            string newCode = await GenerateStoreCodeAsync();
            txtStoreCode.Text = newCode;
            txtStoreCode.IsEnabled = true;

            StoreDialogOverlay.Visibility = Visibility.Visible;
            txtStoreNameAr.Focus();
        }

        private async Task OpenEditStoreDialog(int storeId, StoreItem store)
        {
            _isEditMode = true;
            _editingStoreId = storeId;
            DialogTitle.Text = "تعديل مخزن";

            txtStoreCode.Text = store.StoreCode;
            txtStoreCode.IsEnabled = false;
            txtStoreNameAr.Text = store.StoreNameAr;
            txtStoreNameEn.Text = store.StoreNameEn;

            switch (store.StoreType)
            {
                case "Main":
                    cmbStoreType.SelectedIndex = 0;
                    break;
                case "Sub":
                    cmbStoreType.SelectedIndex = 1;
                    break;
                case "Consignment":
                    cmbStoreType.SelectedIndex = 2;
                    break;
                default:
                    cmbStoreType.SelectedIndex = 0;
                    break;
            }

            txtLocation.Text = store.Location;
            txtResponsiblePerson.Text = store.ResponsiblePerson;
            txtPhone.Text = store.Phone;
            txtEmail.Text = store.Email;
            txtNotes.Text = store.Notes;

            StoreDialogOverlay.Visibility = Visibility.Visible;
            txtStoreNameAr.Focus();
        }

        private async Task<string> GenerateStoreCodeAsync()
        {
            using (var connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();
                string sql = "SELECT MAX(CAST(SUBSTR(StoreCode, 5) AS INTEGER)) FROM Stores WHERE StoreCode LIKE 'STR-%'";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    int nextNumber = (result == DBNull.Value) ? 1 : Convert.ToInt32(result) + 1;
                    return $"STR-{nextNumber:D6}";
                }
            }
        }

        private async Task<bool> IsStoreCodeExistsAsync(string storeCode, int excludeStoreId = 0)
        {
            using (var connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();
                string sql = "SELECT COUNT(*) FROM Stores WHERE StoreCode = @code AND IsActive = 1 AND StoreID != @excludeId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@code", storeCode);
                    cmd.Parameters.AddWithValue("@excludeId", excludeStoreId);
                    long count = (long)await cmd.ExecuteScalarAsync();
                    return count > 0;
                }
            }
        }

        #endregion

        #region دوال الحفظ

        private async Task SaveStoreAsync()
        {
            try
            {
                string storeCode = txtStoreCode.Text.Trim();
                string storeNameAr = txtStoreNameAr.Text.Trim();
                string storeNameEn = txtStoreNameEn.Text.Trim();
                string storeType = ((ComboBoxItem)cmbStoreType.SelectedItem).Content.ToString();
                string location = txtLocation.Text.Trim();
                string responsiblePerson = txtResponsiblePerson.Text.Trim();
                string phone = txtPhone.Text.Trim();
                string email = txtEmail.Text.Trim();
                string notes = txtNotes.Text.Trim();

                if (string.IsNullOrEmpty(storeCode))
                {
                    MessageBox.Show("الرجاء إدخال كود المخزن", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtStoreCode.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(storeNameAr))
                {
                    MessageBox.Show("الرجاء إدخال اسم المخزن", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtStoreNameAr.Focus();
                    return;
                }

                if (!_isEditMode)
                {
                    bool codeExists = await IsStoreCodeExistsAsync(storeCode);
                    if (codeExists)
                    {
                        MessageBox.Show($"كود المخزن '{storeCode}' موجود مسبقاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        txtStoreCode.Focus();
                        txtStoreCode.SelectAll();
                        return;
                    }
                }

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    if (_isEditMode)
                    {
                        string sql = @"
                            UPDATE Stores SET 
                                StoreNameAr = @nameAr,
                                StoreNameEn = @nameEn,
                                StoreType = @type,
                                Location = @location,
                                ResponsiblePerson = @person,
                                Phone = @phone,
                                Email = @email,
                                Notes = @notes,
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE StoreID = @id";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@nameAr", storeNameAr);
                            cmd.Parameters.AddWithValue("@nameEn", storeNameEn);
                            cmd.Parameters.AddWithValue("@type", storeType);
                            cmd.Parameters.AddWithValue("@location", location);
                            cmd.Parameters.AddWithValue("@person", responsiblePerson);
                            cmd.Parameters.AddWithValue("@phone", phone);
                            cmd.Parameters.AddWithValue("@email", email);
                            cmd.Parameters.AddWithValue("@notes", notes);
                            cmd.Parameters.AddWithValue("@id", _editingStoreId);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        string sql = @"
                            INSERT INTO Stores (
                                StoreCode, StoreNameAr, StoreNameEn, StoreType,
                                Location, ResponsiblePerson, Phone, Email,
                                Notes, IsActive, CreatedDate, CreatedBy
                            ) VALUES (
                                @code, @nameAr, @nameEn, @type,
                                @location, @person, @phone, @email,
                                @notes, 1, CURRENT_TIMESTAMP, @userId
                            )";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", storeCode);
                            cmd.Parameters.AddWithValue("@nameAr", storeNameAr);
                            cmd.Parameters.AddWithValue("@nameEn", storeNameEn);
                            cmd.Parameters.AddWithValue("@type", storeType);
                            cmd.Parameters.AddWithValue("@location", location);
                            cmd.Parameters.AddWithValue("@person", responsiblePerson);
                            cmd.Parameters.AddWithValue("@phone", phone);
                            cmd.Parameters.AddWithValue("@email", email);
                            cmd.Parameters.AddWithValue("@notes", notes);
                            cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                await LoadStoresAsync();
                CloseDialog();

                MessageBox.Show(_isEditMode ? "تم تعديل المخزن بنجاح" : "تم إضافة المخزن بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ المخزن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"SaveStoreAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال الحذف

        private async Task DeleteStoreAsync(int storeId, string storeName)
        {
            try
            {
                MessageBoxResult result = MessageBox.Show(
                    $"هل أنت متأكد من حذف المخزن '{storeName}'؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                using (var connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();
                    string sql = "UPDATE Stores SET IsActive = 0 WHERE StoreID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", storeId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await LoadStoresAsync();
                MessageBox.Show("تم حذف المخزن بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حذف المخزن: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteStoreAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال الأحداث

        private async void EditStore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button != null && button.Tag != null)
                {
                    int storeId = (int)button.Tag;
                    var store = _storesList.FirstOrDefault(x => x.StoreID == storeId);
                    if (store != null)
                    {
                        await OpenEditStoreDialog(storeId, store);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"EditStore_Click Error: {ex.Message}");
            }
        }

        private async void DeleteStore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button != null && button.Tag != null)
                {
                    int storeId = (int)button.Tag;
                    var store = _storesList.FirstOrDefault(x => x.StoreID == storeId);
                    if (store != null)
                    {
                        await DeleteStoreAsync(storeId, store.StoreNameAr);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteStore_Click Error: {ex.Message}");
            }
        }

        #endregion
    }
}