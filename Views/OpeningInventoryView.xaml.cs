using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class OpeningInventoryView : UserControl
    {
        private DatabaseService _dbService;
        private string _connectionString;
        private ObservableCollection<OpeningInventoryItem> _openingList;
        private DateTime _selectedDate = DateTime.Now;

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; }
        }

        public OpeningInventoryView()
        {
            InitializeComponent();
            InitializeDatabase();
            Loaded += async (s, e) => await LoadData();
        }

        private void InitializeDatabase()
        {
            _dbService = new DatabaseService();
            _connectionString = _dbService.GetConnectionString();
            _openingList = new ObservableCollection<OpeningInventoryItem>();
            dgOpeningInventory.ItemsSource = _openingList;
        }

        private async Task LoadData()
        {
            await LoadStores();
            await LoadOpeningInventory();
        }

        private async Task LoadStores()
        {
            var stores = new ObservableCollection<StoreItem>();
            stores.Add(new StoreItem { Id = 0, Name = "-- اختر المخزن --" });

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT StoreID, StoreNameAr FROM Stores WHERE IsActive = 1 ORDER BY StoreNameAr";
                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stores.Add(new StoreItem
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }
            }

            cmbStores.ItemsSource = stores;
            if (stores.Count > 0)
            {
                cmbStores.SelectedItem = stores[0];
            }
        }

        private async Task LoadOpeningInventory()
        {
            _openingList.Clear();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
                    SELECT oi.OpeningID, oi.OpeningNumber, oi.OpeningDate, oi.StoreID,
                           s.StoreNameAr as StoreName, oi.Description, oi.IsPosted,
                           COUNT(oid.DetailID) as ProductsCount,
                           COALESCE(SUM(oid.TotalAmount), 0) as TotalAmount
                    FROM OpeningInventory oi
                    LEFT JOIN Stores s ON oi.StoreID = s.StoreID
                    LEFT JOIN OpeningInventoryDetails oid ON oi.OpeningID = oid.OpeningID
                    GROUP BY oi.OpeningID
                    ORDER BY oi.OpeningDate DESC";

                using (var cmd = new SQLiteCommand(sql, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        _openingList.Add(new OpeningInventoryItem
                        {
                            OpeningID = reader.GetInt32(0),
                            OpeningNumber = reader.GetString(1),
                            OpeningDate = reader.GetDateTime(2),
                            StoreID = reader.GetInt32(3),
                            StoreName = reader.GetString(4),
                            Description = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            IsPosted = reader.GetInt32(6) == 1,
                            ProductsCount = reader.GetInt32(7),
                            TotalAmount = reader.GetDecimal(8)
                        });
                    }
                }
            }

            dgOpeningInventory.Visibility = _openingList.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            lblNoData.Visibility = _openingList.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            int totalPosted = 0;
            int totalDraft = 0;
            decimal totalValue = 0;

            foreach (var item in _openingList)
            {
                if (item.IsPosted)
                    totalPosted++;
                else
                    totalDraft++;
                totalValue += item.TotalAmount;
            }

            lblInfo.Text = $"إجمالي الأرصدة: {_openingList.Count} | مرحل: {totalPosted} | مسودة: {totalDraft} | القيمة الإجمالية: {totalValue:N2}";
        }

        private async void BtnAddOpening_Click(object sender, RoutedEventArgs e)
        {
            if (cmbStores.SelectedValue == null || (int)cmbStores.SelectedValue == 0)
            {
                MessageBox.Show("الرجاء اختيار المخزن أولاً", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int storeId = (int)cmbStores.SelectedValue;
            DateTime openingDate = dtpOpeningDate.SelectedDate ?? DateTime.Now;

            var dialog = new OpeningInventoryDialog(_dbService, storeId, openingDate);
            dialog.Owner = Window.GetWindow(this);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.ShowDialog();

            if (dialog.DialogResult == true)
            {
                await LoadOpeningInventory();
                MessageBox.Show("تم إضافة الرصيد الافتتاحي بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnPost_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            int openingId = (int)button.Tag;

            MessageBoxResult userConfirmation = MessageBox.Show(
                "هل أنت متأكد من ترحيل هذا الرصيد الافتتاحي؟\nبعد الترحيل لن يمكن تعديله أو حذفه.",
                "تأكيد الترحيل",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (userConfirmation == MessageBoxResult.Yes)
            {
                bool success = await PostOpeningInventoryAsync(openingId);
                if (success)
                {
                    await LoadOpeningInventory();
                    MessageBox.Show("تم ترحيل الرصيد الافتتاحي بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async Task<bool> PostOpeningInventoryAsync(int openingId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        int storeId = 0;
                        DateTime openingDate = DateTime.Now;

                        string getOpeningSql = "SELECT StoreID, OpeningDate FROM OpeningInventory WHERE OpeningID = @id";
                        using (var cmd = new SQLiteCommand(getOpeningSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", openingId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    storeId = reader.GetInt32(0);
                                    openingDate = reader.GetDateTime(1);
                                }
                            }
                        }

                        string getDetailsSql = @"
                            SELECT ProductID, BatchNumber, ExpiryDate, Quantity, CostPrice, TotalAmount
                            FROM OpeningInventoryDetails WHERE OpeningID = @id";

                        using (var cmd = new SQLiteCommand(getDetailsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", openingId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int productId = reader.GetInt32(0);
                                    string batchNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                                    DateTime? expiryDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                                    decimal quantity = reader.GetDecimal(3);
                                    decimal costPrice = reader.GetDecimal(4);

                                    string updateInventorySql = @"
                                        INSERT INTO StoreInventory (StoreID, ProductID, BatchNumber, ExpiryDate, 
                                            Quantity, AvailableQuantity, CostPrice, LastUpdated)
                                        VALUES (@storeId, @productId, @batchNumber, @expiryDate,
                                            @quantity, @quantity, @costPrice, CURRENT_TIMESTAMP)
                                        ON CONFLICT(StoreID, ProductID) DO UPDATE SET
                                            Quantity = Quantity + @quantity,
                                            AvailableQuantity = AvailableQuantity + @quantity,
                                            CostPrice = @costPrice,
                                            LastUpdated = CURRENT_TIMESTAMP";

                                    using (var updateCmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                                    {
                                        updateCmd.Parameters.AddWithValue("@storeId", storeId);
                                        updateCmd.Parameters.AddWithValue("@productId", productId);
                                        updateCmd.Parameters.AddWithValue("@batchNumber", batchNumber ?? (object)DBNull.Value);
                                        updateCmd.Parameters.AddWithValue("@expiryDate", expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                                        updateCmd.Parameters.AddWithValue("@quantity", quantity);
                                        updateCmd.Parameters.AddWithValue("@costPrice", costPrice);
                                        await updateCmd.ExecuteNonQueryAsync();
                                    }

                                    string inventoryTransactionSql = @"
                                        INSERT INTO InventoryTransactions (
                                            StoreID, ProductID, TransactionDate, TransactionType,
                                            Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                                            ReferenceType, ReferenceID, Description, CreatedBy
                                        ) VALUES (
                                            @storeId, @productId, @date, 'Opening',
                                            @quantity, 0, @quantity, @unitPrice, @totalAmount,
                                            'OPENING_INVENTORY', @openingId, @description, @userId
                                        )";

                                    using (var transCmd = new SQLiteCommand(inventoryTransactionSql, connection, transaction))
                                    {
                                        transCmd.Parameters.AddWithValue("@storeId", storeId);
                                        transCmd.Parameters.AddWithValue("@productId", productId);
                                        transCmd.Parameters.AddWithValue("@date", openingDate.ToString("yyyy-MM-dd"));
                                        transCmd.Parameters.AddWithValue("@quantity", quantity);
                                        transCmd.Parameters.AddWithValue("@unitPrice", costPrice);
                                        transCmd.Parameters.AddWithValue("@totalAmount", quantity * costPrice);
                                        transCmd.Parameters.AddWithValue("@openingId", openingId);
                                        transCmd.Parameters.AddWithValue("@description", "رصيد افتتاحي للمخزون");
                                        transCmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                                        await transCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                        }

                        string updateStatusSql = @"
                            UPDATE OpeningInventory 
                            SET IsPosted = 1, PostedDate = CURRENT_TIMESTAMP, PostedBy = @userId
                            WHERE OpeningID = @id";

                        using (var cmd = new SQLiteCommand(updateStatusSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@userId", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                            cmd.Parameters.AddWithValue("@id", openingId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error posting opening inventory: {ex.Message}");
                MessageBox.Show($"خطأ في ترحيل الرصيد الافتتاحي: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            int openingId = (int)button.Tag;

            bool isPosted = false;
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string checkSql = "SELECT IsPosted FROM OpeningInventory WHERE OpeningID = @id";
                using (var cmd = new SQLiteCommand(checkSql, connection))
                {
                    cmd.Parameters.AddWithValue("@id", openingId);
                    object dbResult = await cmd.ExecuteScalarAsync();
                    if (dbResult != null)
                    {
                        isPosted = Convert.ToInt32(dbResult) == 1;
                    }
                }
            }

            if (isPosted)
            {
                MessageBox.Show("لا يمكن حذف رصيد افتتاحي تم ترحيله بالفعل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult userChoice = MessageBox.Show(
                "هل أنت متأكد من حذف هذا الرصيد الافتتاحي؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (userChoice == MessageBoxResult.Yes)
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string deleteSql = "DELETE FROM OpeningInventory WHERE OpeningID = @id";
                    using (var cmd = new SQLiteCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", openingId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await LoadOpeningInventory();
                MessageBox.Show("تم حذف الرصيد الافتتاحي بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    public class OpeningInventoryItem : INotifyPropertyChanged
    {
        public int OpeningID { get; set; }
        public string OpeningNumber { get; set; }
        public DateTime OpeningDate { get; set; }
        public int StoreID { get; set; }
        public string StoreName { get; set; }
        public string Description { get; set; }
        public bool IsPosted { get; set; }
        public int ProductsCount { get; set; }
        public decimal TotalAmount { get; set; }

        public string StatusText => IsPosted ? "مرحل" : "مسودة";
        public string StatusColor => IsPosted ? "#10B981" : "#F59E0B";

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StoreItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}