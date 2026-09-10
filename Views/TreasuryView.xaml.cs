using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ClosedXML.Excel;
using RasidAccountingSystem.Helpers;
using RasidAccountingSystem.Services;

namespace RasidAccountingSystem.Views
{
    public partial class TreasuryView : UserControl
    {
        #region المتغيرات الخاصة (Private Fields)

        private DatabaseService _dbService;
        private DatabaseService _databaseService;
        private string _connectionString;
        private bool _isLoading = false;

        #endregion

        #region الخصائص العامة (Public Properties)

        public ObservableCollection<TreasuryCardItem> TreasuryList { get; set; }
        public ObservableCollection<TreasuryTransactionItem> TransactionsList { get; set; }

        #endregion

        #region المنشئ (Constructor)

        public TreasuryView()
        {
            try
            {
                InitializeComponent();

                this._dbService = new DatabaseService();
                this._databaseService = new DatabaseService();
                this._connectionString = this._dbService.GetConnectionString();

                this.TreasuryList = new ObservableCollection<TreasuryCardItem>();
                this.TransactionsList = new ObservableCollection<TreasuryTransactionItem>();

                this.icTreasuryList.ItemsSource = this.TreasuryList;
                this.dgTransactions.ItemsSource = this.TransactionsList;

                this.Loaded += async (s, e) => await OnViewLoadedAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Constructor Error: {ex.Message}");
                MessageBox.Show($"خطأ في تهيئة الواجهة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region دوال التحميل الأساسية (Initialization Methods)

        private async Task OnViewLoadedAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                await LoadTreasuryDataAsync();
                await LoadTreasuryFilterAsync();
                await LoadTransactionsAsync();

                this.UpdateCurrencySymbol();

                this.btnAddTreasury.Click += BtnAddTreasury_Click;
                this.btnAddIncome.Click += BtnAddIncome_Click;
                this.btnAddExpense.Click += BtnAddExpense_Click;
                this.btnTransfer.Click += BtnTransfer_Click;
                this.btnRefresh.Click += BtnRefresh_Click;
                this.cmbTreasuryFilter.SelectionChanged += (s, e) => CmbTreasuryFilter_SelectionChanged(s, null);
                this.btnReceiptVoucher.Click += BtnReceiptVoucher_Click;
                this.btnPaymentVoucher.Click += BtnPaymentVoucher_Click;

                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Diagnostics.Debug.WriteLine($"OnViewLoadedAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateCurrencySymbol()
        {
            try
            {
                string symbol = CurrencyHelper.GetCurrencySymbol();
                this.lblCurrencySymbol.Text = string.IsNullOrEmpty(symbol) ? "ر.س" : symbol;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCurrencySymbol Error: {ex.Message}");
                this.lblCurrencySymbol.Text = "ر.س";
            }
        }

        private async Task LoadTreasuryDataAsync()
        {
            try
            {
                this.TreasuryList.Clear();
                decimal totalBalance = 0;

                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT TreasuryID, TreasuryCode, TreasuryNameAr, CurrentBalance FROM Treasury WHERE IsActive = 1 ORDER BY TreasuryNameAr";

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                    using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = 0;
                            try
                            {
                                id = Convert.ToInt32(reader["TreasuryID"]);
                            }
                            catch { id = 0; }

                            string code = "";
                            try
                            {
                                code = reader["TreasuryCode"]?.ToString() ?? "";
                            }
                            catch { code = ""; }

                            string name = "";
                            try
                            {
                                name = reader["TreasuryNameAr"]?.ToString() ?? "";
                            }
                            catch { name = ""; }

                            decimal balance = 0;
                            try
                            {
                                object balanceObj = reader["CurrentBalance"];
                                if (balanceObj != null && balanceObj != DBNull.Value)
                                {
                                    balance = Convert.ToDecimal(balanceObj);
                                }
                            }
                            catch { balance = 0; }

                            totalBalance += balance;

                            this.TreasuryList.Add(new TreasuryCardItem
                            {
                                Id = id,
                                Code = code,
                                Name = name,
                                Balance = balance,
                                BalanceColor = balance >= 0 ? (Brush)FindResource("SuccessColor") : (Brush)FindResource("DangerColor")
                            });
                        }
                    }
                }

                this.lblTotalBalance.Text = CurrencyHelper.FormatAmountOnly(totalBalance);
                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {this.TreasuryList.Count} خزينة");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuryDataAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحميل بيانات الخزينة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadTreasuryFilterAsync()
        {
            try
            {
                var treasuryFilterItems = new List<ComboBoxItem>();
                treasuryFilterItems.Add(new ComboBoxItem { Content = "جميع الخزائن", Tag = 0 });

                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT TreasuryID, TreasuryNameAr FROM Treasury WHERE IsActive = 1 ORDER BY TreasuryNameAr";

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                    using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string name = reader.GetString(1);
                            treasuryFilterItems.Add(new ComboBoxItem { Content = name, Tag = id });
                        }
                    }
                }

                this.cmbTreasuryFilter.ItemsSource = treasuryFilterItems;

                if (treasuryFilterItems.Count > 0)
                {
                    this.cmbTreasuryFilter.SelectedItem = treasuryFilterItems[0];
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {treasuryFilterItems.Count - 1} خزينة للفلتر");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuryFilterAsync Error: {ex.Message}");
            }
        }

        private async Task LoadTransactionsAsync(int treasuryId = 0)
        {
            try
            {
                this.TransactionsList.Clear();
                decimal totalIncome = 0;
                decimal totalExpense = 0;

                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    // تعديل الـ SQL لترتيب الحركات: الوارد أولاً ثم الصادر في نفس اليوم
                    string sql = @"
                SELECT 
                    tt.TransactionDate, 
                    tt.TransactionType, 
                    tt.Amount, 
                    COALESCE(tt.Description, '') as Description, 
                    t.TreasuryNameAr,
                    COALESCE(tt.BalanceAfter, 0) as BalanceAfter,
                    strftime('%Y-%m-%d', tt.TransactionDate) as DateOnly
                FROM TreasuryTransactions tt
                JOIN Treasury t ON tt.TreasuryID = t.TreasuryID";

                    if (treasuryId > 0)
                    {
                        sql += " WHERE tt.TreasuryID = @treasuryId";
                    }

                    // الترتيب: حسب التاريخ، ثم نوع الحركة (Receipt أولاً ثم Payment)
                    sql += @" ORDER BY 
                        DateOnly ASC,
                        CASE 
                            WHEN tt.TransactionType = 'Receipt' THEN 0 
                            ELSE 1 
                        END,
                        tt.TransactionDate ASC";

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                    {
                        if (treasuryId > 0)
                        {
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        }

                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            int serialNumber = 1;

                            while (await reader.ReadAsync())
                            {
                                DateTime transactionDate = DateTime.Now;
                                try
                                {
                                    object dateObj = reader["TransactionDate"];
                                    if (dateObj != null && dateObj != DBNull.Value)
                                    {
                                        transactionDate = Convert.ToDateTime(dateObj);
                                    }
                                }
                                catch { }

                                string type = "Receipt";
                                try
                                {
                                    object typeObj = reader["TransactionType"];
                                    if (typeObj != null && typeObj != DBNull.Value)
                                    {
                                        type = typeObj.ToString();
                                    }
                                }
                                catch { }

                                decimal amount = 0;
                                try
                                {
                                    object amountObj = reader["Amount"];
                                    if (amountObj != null && amountObj != DBNull.Value)
                                    {
                                        amount = Convert.ToDecimal(amountObj);
                                    }
                                }
                                catch { }

                                string description = "";
                                try
                                {
                                    object descObj = reader["Description"];
                                    if (descObj != null && descObj != DBNull.Value)
                                    {
                                        description = descObj.ToString();
                                    }
                                }
                                catch { }

                                string treasuryName = "";
                                try
                                {
                                    object treasuryObj = reader["TreasuryNameAr"];
                                    if (treasuryObj != null && treasuryObj != DBNull.Value)
                                    {
                                        treasuryName = treasuryObj.ToString();
                                    }
                                }
                                catch { }

                                decimal balanceAfter = 0;
                                try
                                {
                                    object balanceObj = reader["BalanceAfter"];
                                    if (balanceObj != null && balanceObj != DBNull.Value)
                                    {
                                        balanceAfter = Convert.ToDecimal(balanceObj);
                                    }
                                }
                                catch { }

                                bool isIncome = type == "Receipt";

                                if (isIncome)
                                {
                                    totalIncome += amount;
                                }
                                else
                                {
                                    totalExpense += amount;
                                }

                                this.TransactionsList.Add(new TreasuryTransactionItem
                                {
                                    SerialNumber = serialNumber++,
                                    TransactionDate = transactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                                    TransactionType = type,
                                    TransactionTreasuryName = treasuryName,
                                    TransactionDescription = description,
                                    TransactionAmount = amount,
                                    TransactionBalanceAfter = balanceAfter
                                    // تم إزالة: AmountColor = ... و TransactionColor = ...
                                });
                            }
                        }
                    }
                }

                this.lblTotalIncome.Text = CurrencyHelper.FormatAmountOnly(totalIncome);
                this.lblTotalExpense.Text = CurrencyHelper.FormatAmountOnly(totalExpense);
                this.lblNetAmount.Text = CurrencyHelper.FormatAmountOnly(totalIncome - totalExpense);

                if (this.TransactionsList.Count == 0)
                {
                    this.dgTransactions.Visibility = Visibility.Collapsed;
                    this.lblNoData.Visibility = Visibility.Visible;
                }
                else
                {
                    this.dgTransactions.Visibility = Visibility.Visible;
                    this.lblNoData.Visibility = Visibility.Collapsed;
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {this.TransactionsList.Count} حركة نقدية");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTransactionsAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                this.dgTransactions.Visibility = Visibility.Collapsed;
                this.lblNoData.Visibility = Visibility.Visible;
            }
        }

        private async Task CheckDatabaseTablesAsync()
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    string checkTreasury = "SELECT COUNT(*) FROM Treasury WHERE IsActive = 1";
                    using (var cmd = new SQLiteCommand(checkTreasury, connection))
                    {
                        long treasuryCount = (long)await cmd.ExecuteScalarAsync();
                        System.Diagnostics.Debug.WriteLine($"عدد الخزائن: {treasuryCount}");
                    }

                    long transactionsCount = 0;
                    string checkTransactions = "SELECT COUNT(*) FROM TreasuryTransactions";
                    using (var cmd = new SQLiteCommand(checkTransactions, connection))
                    {
                        transactionsCount = (long)await cmd.ExecuteScalarAsync();
                        System.Diagnostics.Debug.WriteLine($"عدد حركات الخزينة: {transactionsCount}");
                    }

                    if (transactionsCount == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ لا توجد حركات خزينة في قاعدة البيانات");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckDatabaseTablesAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region إعادة حساب أرصدة الخزائن (Recalculate Treasury Balances)

        public async Task RecalculateAllTreasuryBalancesAsync()
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string getTreasuriesSql = "SELECT TreasuryID FROM Treasury WHERE IsActive = 1";
                        List<int> treasuryIds = new List<int>();

                        using (SQLiteCommand cmd = new SQLiteCommand(getTreasuriesSql, connection, transaction))
                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                treasuryIds.Add(reader.GetInt32(0));
                            }
                        }

                        foreach (int treasuryId in treasuryIds)
                        {
                            string calculateBalanceSql = @"
                                SELECT COALESCE(SUM(
                                    CASE 
                                        WHEN TransactionType = 'Receipt' THEN Amount
                                        WHEN TransactionType = 'Payment' THEN -Amount
                                        ELSE 0
                                    END
                                ), 0) as CalculatedBalance
                                FROM TreasuryTransactions 
                                WHERE TreasuryID = @treasuryId";

                            decimal calculatedBalance = 0;
                            using (SQLiteCommand cmd = new SQLiteCommand(calculateBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                object result = await cmd.ExecuteScalarAsync();
                                calculatedBalance = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            string updateBalanceSql = "UPDATE Treasury SET CurrentBalance = @balance, ModifiedDate = CURRENT_TIMESTAMP WHERE TreasuryID = @id";
                            using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@balance", calculatedBalance);
                                cmd.Parameters.AddWithValue("@id", treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"💰 إعادة حساب رصيد الخزينة ID {treasuryId}: {calculatedBalance:N2}");
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine("✅ تم إعادة حساب جميع أرصدة الخزائن بنجاح");

                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateAllTreasuryBalancesAsync Error: {ex.Message}");
            }
        }

        public async Task<decimal> RecalculateTreasuryBalanceAsync(int treasuryId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    string calculateBalanceSql = @"
                        SELECT COALESCE(SUM(
                            CASE 
                                WHEN TransactionType = 'Receipt' THEN Amount
                                WHEN TransactionType = 'Payment' THEN -Amount
                                ELSE 0
                            END
                        ), 0) as CalculatedBalance
                        FROM TreasuryTransactions 
                        WHERE TreasuryID = @treasuryId";

                    decimal calculatedBalance = 0;
                    using (SQLiteCommand cmd = new SQLiteCommand(calculateBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        object result = await cmd.ExecuteScalarAsync();
                        calculatedBalance = result != null ? Convert.ToDecimal(result) : 0;
                    }

                    string updateBalanceSql = "UPDATE Treasury SET CurrentBalance = @balance, ModifiedDate = CURRENT_TIMESTAMP WHERE TreasuryID = @id";
                    using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@balance", calculatedBalance);
                        cmd.Parameters.AddWithValue("@id", treasuryId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"💰 إعادة حساب رصيد الخزينة ID {treasuryId}: {calculatedBalance:N2}");
                    return calculatedBalance;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateTreasuryBalanceAsync Error: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region دوال حذف الحركات (Delete Transaction Methods)

        public async Task<bool> DeleteTreasuryTransactionAsync(int transactionId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        int treasuryId = 0;
                        string transactionType = "";
                        decimal amount = 0;

                        string getTransactionSql = @"
                            SELECT TreasuryID, TransactionType, Amount 
                            FROM TreasuryTransactions 
                            WHERE TransactionID = @id";

                        using (SQLiteCommand cmd = new SQLiteCommand(getTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", transactionId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    treasuryId = reader.GetInt32(0);
                                    transactionType = reader.GetString(1);
                                    amount = reader.GetDecimal(2);
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }

                        decimal balanceCorrection = transactionType == "Receipt" ? -amount : amount;

                        string updateBalanceSql = @"
                            UPDATE Treasury 
                            SET CurrentBalance = CurrentBalance + @correction,
                                ModifiedDate = CURRENT_TIMESTAMP 
                            WHERE TreasuryID = @id";

                        using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                            cmd.Parameters.AddWithValue("@id", treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteTransactionSql = "DELETE FROM TreasuryTransactions WHERE TransactionID = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(deleteTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", transactionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم حذف حركة الخزينة {transactionId} وتصحيح الرصيد");

                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTreasuryTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteTreasuryTransactionsByReferenceAsync(string referenceType, int referenceId, string referenceNumber)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string getTransactionsSql = @"
                            SELECT TransactionID, TreasuryID, TransactionType, Amount 
                            FROM TreasuryTransactions 
                            WHERE ReferenceType = @refType 
                            AND (ReferenceID = @refId OR ReferenceNumber = @refNumber)";

                        var transactionsToDelete = new List<(int id, int treasuryId, string type, decimal amount)>();

                        using (SQLiteCommand cmd = new SQLiteCommand(getTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refType", referenceType);
                            cmd.Parameters.AddWithValue("@refId", referenceId);
                            cmd.Parameters.AddWithValue("@refNumber", referenceNumber);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    transactionsToDelete.Add((
                                        id: reader.GetInt32(0),
                                        treasuryId: reader.GetInt32(1),
                                        type: reader.GetString(2),
                                        amount: reader.GetDecimal(3)
                                    ));
                                }
                            }
                        }

                        foreach (var trans in transactionsToDelete)
                        {
                            decimal balanceCorrection = trans.type == "Receipt" ? -trans.amount : trans.amount;

                            string updateBalanceSql = @"
                                UPDATE Treasury 
                                SET CurrentBalance = CurrentBalance + @correction,
                                    ModifiedDate = CURRENT_TIMESTAMP 
                                WHERE TreasuryID = @id";

                            using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                                cmd.Parameters.AddWithValue("@id", trans.treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        string deleteTransactionsSql = @"
                            DELETE FROM TreasuryTransactions 
                            WHERE ReferenceType = @refType 
                            AND (ReferenceID = @refId OR ReferenceNumber = @refNumber)";

                        using (SQLiteCommand cmd = new SQLiteCommand(deleteTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refType", referenceType);
                            cmd.Parameters.AddWithValue("@refId", referenceId);
                            cmd.Parameters.AddWithValue("@refNumber", referenceNumber);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم حذف {transactionsToDelete.Count} حركة خزينة مرتبطة بـ {referenceType} - {referenceNumber}");

                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTreasuryTransactionsByReferenceAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال تنظيف الحركات العالقة (Cleanup Orphaned Transactions)

        public async Task<bool> DeleteSpecificTreasuryTransactionAndRefreshAsync(int transactionId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    int treasuryId = 0;
                    string transactionType = "";
                    decimal amount = 0;

                    string getTransactionSql = @"
                        SELECT TreasuryID, TransactionType, Amount 
                        FROM TreasuryTransactions 
                        WHERE TransactionID = @id";

                    using (var cmd = new SQLiteCommand(getTransactionSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                treasuryId = reader.GetInt32(0);
                                transactionType = reader.GetString(1);
                                amount = reader.GetDecimal(2);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }

                    decimal balanceCorrection = transactionType == "Receipt" ? -amount : amount;

                    string updateBalanceSql = @"
                        UPDATE Treasury 
                        SET CurrentBalance = CurrentBalance + @correction,
                            ModifiedDate = CURRENT_TIMESTAMP 
                        WHERE TreasuryID = @id";

                    using (var cmd = new SQLiteCommand(updateBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                        cmd.Parameters.AddWithValue("@id", treasuryId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string deleteTransactionSql = "DELETE FROM TreasuryTransactions WHERE TransactionID = @id";
                    using (var cmd = new SQLiteCommand(deleteTransactionSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await LoadTreasuryDataAsync();

                int currentFilter = 0;
                if (this.cmbTreasuryFilter.SelectedItem is ComboBoxItem selected && selected.Tag is int id && id > 0)
                {
                    currentFilter = id;
                }
                await LoadTransactionsAsync(currentFilter);

                System.Diagnostics.Debug.WriteLine($"✅ تم حذف حركة الخزينة {transactionId} وتحديث الواجهة");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteSpecificTreasuryTransactionAndRefreshAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> CleanupOrphanedTransactionsByInvoiceNumberAsync(string invoiceNumber)
        {
            int deletedCount = 0;

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    string findTransactionsSql = @"
                        SELECT TransactionID, TreasuryID, TransactionType, Amount, ReferenceType
                        FROM TreasuryTransactions 
                        WHERE ReferenceNumber = @invoiceNumber";

                    var transactionsToDelete = new List<(int id, int treasuryId, string type, decimal amount)>();

                    using (var cmd = new SQLiteCommand(findTransactionsSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                transactionsToDelete.Add((
                                    id: reader.GetInt32(0),
                                    treasuryId: reader.GetInt32(1),
                                    type: reader.GetString(2),
                                    amount: reader.GetDecimal(3)
                                ));
                            }
                        }
                    }

                    if (transactionsToDelete.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"لا توجد حركات خزينة مرتبطة بالفاتورة {invoiceNumber}");
                        return 0;
                    }

                    System.Diagnostics.Debug.WriteLine($"🗑️ سيتم حذف {transactionsToDelete.Count} حركة خزينة مرتبطة بالفاتورة {invoiceNumber}");

                    foreach (var trans in transactionsToDelete)
                    {
                        decimal balanceCorrection = trans.type == "Receipt" ? -trans.amount : trans.amount;

                        string updateBalanceSql = @"
                            UPDATE Treasury 
                            SET CurrentBalance = CurrentBalance + @correction,
                                ModifiedDate = CURRENT_TIMESTAMP 
                            WHERE TreasuryID = @id";

                        using (var cmd = new SQLiteCommand(updateBalanceSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                            cmd.Parameters.AddWithValue("@id", trans.treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteTransactionSql = "DELETE FROM TreasuryTransactions WHERE TransactionID = @id";
                        using (var cmd = new SQLiteCommand(deleteTransactionSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", trans.id);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        deletedCount++;
                    }
                }

                await ForceRefreshTreasuryUIAsync();

                System.Diagnostics.Debug.WriteLine($"✅ تم تنظيف {deletedCount} حركة خزينة عالقة للفاتورة {invoiceNumber}");
                return deletedCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanupOrphanedTransactionsByInvoiceNumberAsync Error: {ex.Message}");
                return deletedCount;
            }
        }

        public async Task ForceRefreshTreasuryUIAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await LoadTreasuryDataAsync();
                    await LoadTreasuryFilterAsync();

                    int currentFilter = 0;
                    if (this.cmbTreasuryFilter.SelectedItem is ComboBoxItem selected && selected.Tag is int id && id > 0)
                    {
                        currentFilter = id;
                    }
                    await LoadTransactionsAsync(currentFilter);

                    System.Diagnostics.Debug.WriteLine("✅ تم تحديث واجهة الخزينة بالكامل");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceRefreshTreasuryUIAsync Error: {ex.Message}");
            }
        }

        public async Task<bool> CleanupOrphanedTreasuryTransactionsAsync(string referenceNumber, string referenceType)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string findTransactionsSql = @"
                            SELECT TransactionID, TreasuryID, TransactionType, Amount, ReferenceID
                            FROM TreasuryTransactions 
                            WHERE ReferenceNumber = @refNumber";

                        var transactionsToDelete = new List<(int id, int treasuryId, string type, decimal amount, int refId)>();

                        using (var cmd = new SQLiteCommand(findTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refNumber", referenceNumber);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    transactionsToDelete.Add((
                                        id: reader.GetInt32(0),
                                        treasuryId: reader.GetInt32(1),
                                        type: reader.GetString(2),
                                        amount: reader.GetDecimal(3),
                                        refId: reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                                    ));
                                }
                            }
                        }

                        if (transactionsToDelete.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"لا توجد حركات خزينة مرتبطة بالمرجع {referenceNumber}");
                            return true;
                        }

                        System.Diagnostics.Debug.WriteLine($"🗑️ سيتم حذف {transactionsToDelete.Count} حركة خزينة مرتبطة بالمرجع {referenceNumber}");

                        foreach (var trans in transactionsToDelete)
                        {
                            decimal balanceCorrection = trans.type == "Receipt" ? -trans.amount : trans.amount;

                            string updateBalanceSql = @"
                                UPDATE Treasury 
                                SET CurrentBalance = CurrentBalance + @correction,
                                    ModifiedDate = CURRENT_TIMESTAMP 
                                WHERE TreasuryID = @id";

                            using (var cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                                cmd.Parameters.AddWithValue("@id", trans.treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"   💰 تصحيح رصيد الخزينة {trans.treasuryId}: {balanceCorrection:+#;-#;0}");
                        }

                        string deleteAllSql = @"
                            DELETE FROM TreasuryTransactions 
                            WHERE ReferenceNumber = @refNumber";

                        using (var cmd = new SQLiteCommand(deleteAllSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refNumber", referenceNumber);
                            int deleted = await cmd.ExecuteNonQueryAsync();
                            System.Diagnostics.Debug.WriteLine($"   ✅ تم حذف {deleted} حركة خزينة");
                        }

                        transaction.Commit();

                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();

                        System.Diagnostics.Debug.WriteLine($"✅ تم تنظيف حركات الخزينة المرتبطة بالمرجع {referenceNumber}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanupOrphanedTreasuryTransactionsAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteTreasuryTransactionsByInvoiceNumberAsync(string invoiceNumber, string paymentMethod)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string referenceType = "";

                        if (paymentMethod == "Sales" || paymentMethod == "Sale" || paymentMethod == "SALES_INVOICE")
                        {
                            referenceType = "SALES_INVOICE";
                        }
                        else if (paymentMethod == "Purchase" || paymentMethod == "PURCHASE_INVOICE")
                        {
                            referenceType = "PURCHASE_INVOICE";
                        }
                        else
                        {
                            referenceType = "SALES_INVOICE";
                        }

                        string findTransactionsSql = @"
                            SELECT TransactionID, TreasuryID, TransactionType, Amount 
                            FROM TreasuryTransactions 
                            WHERE ReferenceNumber = @refNumber 
                            AND ReferenceType = @refType";

                        var transactionsToDelete = new List<(int id, int treasuryId, string type, decimal amount)>();

                        using (var cmd = new SQLiteCommand(findTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refNumber", invoiceNumber);
                            cmd.Parameters.AddWithValue("@refType", referenceType);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    transactionsToDelete.Add((
                                        id: reader.GetInt32(0),
                                        treasuryId: reader.GetInt32(1),
                                        type: reader.GetString(2),
                                        amount: reader.GetDecimal(3)
                                    ));
                                }
                            }
                        }

                        if (transactionsToDelete.Count == 0)
                        {
                            string otherType = referenceType == "SALES_INVOICE" ? "PURCHASE_INVOICE" : "SALES_INVOICE";

                            using (var cmd = new SQLiteCommand(findTransactionsSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@refNumber", invoiceNumber);
                                cmd.Parameters.AddWithValue("@refType", otherType);

                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        transactionsToDelete.Add((
                                            id: reader.GetInt32(0),
                                            treasuryId: reader.GetInt32(1),
                                            type: reader.GetString(2),
                                            amount: reader.GetDecimal(3)
                                        ));
                                    }
                                }
                            }
                        }

                        if (transactionsToDelete.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"لا توجد حركات خزينة مرتبطة بالفاتورة {invoiceNumber}");
                            return true;
                        }

                        foreach (var trans in transactionsToDelete)
                        {
                            decimal balanceCorrection = trans.type == "Receipt" ? -trans.amount : trans.amount;

                            string updateBalanceSql = @"
                                UPDATE Treasury 
                                SET CurrentBalance = CurrentBalance + @correction,
                                    ModifiedDate = CURRENT_TIMESTAMP 
                                WHERE TreasuryID = @id";

                            using (var cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                                cmd.Parameters.AddWithValue("@id", trans.treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        string deleteSql = @"
                            DELETE FROM TreasuryTransactions 
                            WHERE ReferenceNumber = @refNumber 
                            AND (ReferenceType = 'SALES_INVOICE' OR ReferenceType = 'PURCHASE_INVOICE')";

                        using (var cmd = new SQLiteCommand(deleteSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@refNumber", invoiceNumber);
                            int deleted = await cmd.ExecuteNonQueryAsync();
                            System.Diagnostics.Debug.WriteLine($"   ✅ تم حذف {deleted} حركة خزينة");
                        }

                        transaction.Commit();

                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTreasuryTransactionsByInvoiceNumberAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال تحميل العملاء والموردين (Load Customers & Suppliers)

        /// <summary>
        /// جلب الرصيد الفعلي للعميل من CustomerTransactions
        /// </summary>
        /// <summary>
        /// جلب الرصيد الفعلي للعميل من CustomerTransactions مع إضافة رصيد أول المدة
        /// المعادلة الصحيحة: الرصيد = OpeningBalance + SUM(CreditAmount) - SUM(DebitAmount)
        /// </summary>
        /// <summary>
        /// جلب الرصيد الفعلي للعميل من CustomerTransactions مع إضافة رصيد أول المدة
        /// المعادلة الصحيحة: الرصيد = OpeningBalance + SUM(CreditAmount) - SUM(DebitAmount)
        /// </summary>
        private async Task<decimal> GetCustomerActualBalanceForTreasuryAsync(int customerId)
        {
            try
            {
                using (var connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    // ✅ إصلاح باج خطير: كانت تقرأ CreditAmount لحركات 'Invoice'، بينما هذه الحركات
                    // تُخزَّن فعليًا في DebitAmount (راجع AddCustomerInvoiceTransactionAsync)، فكانت
                    // مساهمة كل فواتير العميل تساوي صفر دائمًا. المعادلة الصحيحة: OpeningBalance +
                    // فواتير (DebitAmount) - تحصيلات (CreditAmount)
                    string sql = @"
                SELECT 
                    COALESCE((SELECT OpeningBalance FROM Customers WHERE CustomerID = @customerId), 0) +
                    COALESCE((
                        SELECT SUM(DebitAmount) 
                        FROM CustomerTransactions 
                        WHERE CustomerID = @customerId 
                        AND TransactionType = 'Invoice'
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount) 
                        FROM CustomerTransactions 
                        WHERE CustomerID = @customerId 
                        AND TransactionType = 'Receipt'
                    ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"الرصيد الفعلي للعميل {customerId}: {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerActualBalanceForTreasuryAsync Error: {ex.Message}");
                return 0;
            }
        }

        private async Task<decimal> GetSupplierActualBalanceForTreasuryAsync(int supplierId)
        {
            try
            {
                using (var connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    // ✅ المعادلة الصحيحة: استخدام CreditAmount لحركات الدفع
                    string sql = @"
                SELECT 
                    COALESCE((SELECT OpeningBalance FROM Suppliers WHERE SupplierID = @supplierId), 0) +
                    COALESCE((
                        SELECT SUM(CreditAmount) 
                        FROM SupplierTransactions 
                        WHERE SupplierID = @supplierId 
                        AND TransactionType = 'Purchase'
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount) 
                        FROM SupplierTransactions 
                        WHERE SupplierID = @supplierId 
                        AND TransactionType = 'Payment'
                    ), 0) as ActualBalance";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        object result = await cmd.ExecuteScalarAsync();
                        decimal balance = result != null ? Convert.ToDecimal(result) : 0;
                        System.Diagnostics.Debug.WriteLine($"💰 الرصيد الفعلي للمورد {supplierId}: {balance}");
                        return balance;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSupplierActualBalanceForTreasuryAsync Error: {ex.Message}");
                return 0;
            }
        }
        private async Task LoadCustomersToComboAsync(ComboBox combo)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                combo.Items.Clear();
                try
                {
                    using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                    {
                        await connection.OpenAsync();

                        // ✅ استعلام واحد مجمّع يحسب رصيد كل عميل داخل نفس الاستعلام (subquery لكل عمود)
                        // بدل فتح اتصال SQLite منفصل وتشغيل استعلام كامل لكل عميل على حدة داخل الـ loop،
                        // وهو ما كان يسبب تجمّد الواجهة كلما زاد عدد العملاء (نفس منطق حساب الرصيد بالظبط،
                        // فقط مجمّع في استعلام واحد بدل استعلامات متكررة).
                        string sql = @"
                SELECT
                    c.CustomerID,
                    c.CustomerNameAr,
                    COALESCE(c.OpeningBalance, 0) +
                    COALESCE((
                        SELECT SUM(DebitAmount)
                        FROM CustomerTransactions
                        WHERE CustomerID = c.CustomerID
                        AND TransactionType = 'Invoice'
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount)
                        FROM CustomerTransactions
                        WHERE CustomerID = c.CustomerID
                        AND TransactionType = 'Receipt'
                    ), 0) as ActualBalance
                FROM Customers c
                WHERE c.IsActive = 1
                ORDER BY c.CustomerNameAr";

                        using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int id = reader.GetInt32(0);
                                string name = reader.GetString(1);
                                decimal actualBalance = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));

                                string displayText;
                                if (actualBalance > 0)
                                {
                                    displayText = $"{name} (عليه: {CurrencyHelper.FormatAmount(actualBalance)})";
                                }
                                else if (actualBalance < 0)
                                {
                                    decimal absoluteBalance = Math.Abs(actualBalance);
                                    displayText = $"{name} (له: {CurrencyHelper.FormatAmount(absoluteBalance)})";
                                }
                                else
                                {
                                    displayText = $"{name} (متزن)";
                                }

                                combo.Items.Add(new ComboBoxItem { Content = displayText, Tag = id });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadCustomersToComboAsync Error: {ex.Message}");
                }
            });
        }

        private async Task LoadSuppliersToComboAsync(ComboBox combo)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                combo.Items.Clear();
                try
                {
                    using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                    {
                        await connection.OpenAsync();

                        // ✅ نفس مبدأ التجميع في استعلام واحد المطبّق أعلاه في LoadCustomersToComboAsync
                        string sql = @"
                SELECT
                    s.SupplierID,
                    s.SupplierNameAr,
                    COALESCE(s.OpeningBalance, 0) +
                    COALESCE((
                        SELECT SUM(CreditAmount)
                        FROM SupplierTransactions
                        WHERE SupplierID = s.SupplierID
                        AND TransactionType = 'Purchase'
                    ), 0) -
                    COALESCE((
                        SELECT SUM(CreditAmount)
                        FROM SupplierTransactions
                        WHERE SupplierID = s.SupplierID
                        AND TransactionType = 'Payment'
                    ), 0) as ActualBalance
                FROM Suppliers s
                WHERE s.IsActive = 1
                ORDER BY s.SupplierNameAr";

                        using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int id = reader.GetInt32(0);
                                string name = reader.GetString(1);
                                decimal actualBalance = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));

                                string displayText;
                                if (actualBalance > 0)
                                {
                                    displayText = $"{name} (علينا: {CurrencyHelper.FormatAmount(actualBalance)})";
                                }
                                else if (actualBalance < 0)
                                {
                                    decimal absoluteBalance = Math.Abs(actualBalance);
                                    displayText = $"{name} (لنا: {CurrencyHelper.FormatAmount(absoluteBalance)})";
                                }
                                else
                                {
                                    displayText = $"{name} (متزن)";
                                }

                                combo.Items.Add(new ComboBoxItem { Content = displayText, Tag = id });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadSuppliersToComboAsync Error: {ex.Message}");
                }
            });
        }

        #endregion

        #region أحداث الأزرار (Button Events)

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadTreasuryDataAsync();
            await LoadTreasuryFilterAsync();
            await LoadTransactionsAsync();
            this.UpdateCurrencySymbol();
        }

        private async void CmbTreasuryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (this.cmbTreasuryFilter.SelectedItem is ComboBoxItem selected)
            {
                int treasuryId = selected.Tag is int id ? id : 0;
                await LoadTransactionsAsync(treasuryId);
            }
        }

        private void ViewTransactions_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int treasuryId = (int)button.Tag;
                var treasury = this.TreasuryList.FirstOrDefault(t => t.Id == treasuryId);
                if (treasury != null)
                {
                    ShowTreasuryTransactionsDialog(treasuryId, treasury.Name);
                }
            }
        }

        private void EditTreasury_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag != null)
            {
                int treasuryId = (int)button.Tag;
                EditTreasuryDialog(treasuryId);
            }
        }

        #endregion

        #region نافذة تعديل الخزينة (Edit Treasury Dialog)

        private void EditTreasuryDialog(int treasuryId)
        {
            TreasuryCardItem treasury = this.TreasuryList.FirstOrDefault(t => t.Id == treasuryId);
            if (treasury == null)
            {
                MessageBox.Show("لم يتم العثور على الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = CreateEditTreasuryDialog(treasury);
            this.ShowDialog(dialog);
        }

        private Window CreateEditTreasuryDialog(TreasuryCardItem treasury)
        {
            Window dialog = new Window
            {
                Title = "تعديل خزينة",
                Width = 450,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("SurfaceColor"),
                FontFamily = new FontFamily("Segoe UI")
            };

            Grid mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = "✏️", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            titlePanel.Children.Add(new TextBlock { Text = "تعديل خزينة", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("PrimaryColor") });
            Grid.SetRow(titlePanel, 0);
            mainGrid.Children.Add(titlePanel);

            Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            TextBlock codeLabel = new TextBlock { Text = "كود الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(codeLabel, 2);
            mainGrid.Children.Add(codeLabel);

            TextBox codeBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernTextBox"), Text = treasury.Code };
            Grid.SetRow(codeBox, 3);
            mainGrid.Children.Add(codeBox);

            TextBlock nameLabel = new TextBlock { Text = "اسم الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(nameLabel, 4);
            mainGrid.Children.Add(nameLabel);

            TextBox nameBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20), Style = (Style)FindResource("ModernTextBox"), Text = treasury.Name };
            Grid.SetRow(nameBox, 5);
            mainGrid.Children.Add(nameBox);

            TextBlock balanceLabel = new TextBlock { Text = "الرصيد الحالي", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(balanceLabel, 6);
            mainGrid.Children.Add(balanceLabel);

            TextBlock balanceValue = new TextBlock { Text = CurrencyHelper.FormatAmount(treasury.Balance), FontSize = 14, FontWeight = FontWeights.Bold, Foreground = treasury.Balance >= 0 ? (Brush)FindResource("SuccessColor") : (Brush)FindResource("DangerColor"), Margin = new Thickness(0, 0, 0, 20) };
            Grid.SetRow(balanceValue, 7);
            mainGrid.Children.Add(balanceValue);

            StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            Button saveBtn = new Button { Content = "حفظ التغييرات", Width = 140, Height = 40, Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 12, 0) };
            Button cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Style = (Style)FindResource("SecondaryButton") };
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 8);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;

            saveBtn.Click += async (s, ev) =>
            {
                if (string.IsNullOrEmpty(codeBox.Text) || string.IsNullOrEmpty(nameBox.Text))
                {
                    MessageBox.Show("يرجى إدخال كود واسم الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                    {
                        await connection.OpenAsync();
                        string sql = "UPDATE Treasury SET TreasuryCode = @code, TreasuryNameAr = @name WHERE TreasuryID = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", codeBox.Text);
                            cmd.Parameters.AddWithValue("@name", nameBox.Text);
                            cmd.Parameters.AddWithValue("@id", treasury.Id);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    MessageBox.Show("تم تعديل الخزينة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadTreasuryDataAsync();
                    await LoadTreasuryFilterAsync();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelBtn.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        #endregion

        #region نافذة إضافة خزينة جديدة (Add Treasury Dialog)

        private void BtnAddTreasury_Click(object sender, RoutedEventArgs e)
        {
            Window dialog = CreateAddTreasuryDialog();
            this.ShowDialog(dialog);
        }

        private Window CreateAddTreasuryDialog()
        {
            Window dialog = new Window
            {
                Title = "إضافة خزينة جديدة",
                Width = 450,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("SurfaceColor"),
                FontFamily = new FontFamily("Segoe UI")
            };

            Grid mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = "🏦", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            titlePanel.Children.Add(new TextBlock { Text = "إضافة خزينة جديدة", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextPrimaryColor") });
            Grid.SetRow(titlePanel, 0);
            mainGrid.Children.Add(titlePanel);

            Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            TextBlock codeLabel = new TextBlock { Text = "كود الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(codeLabel, 2);
            mainGrid.Children.Add(codeLabel);

            TextBox codeBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(codeBox, 3);
            mainGrid.Children.Add(codeBox);

            TextBlock nameLabel = new TextBlock { Text = "اسم الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(nameLabel, 4);
            mainGrid.Children.Add(nameLabel);

            TextBox nameBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(nameBox, 5);
            mainGrid.Children.Add(nameBox);

            StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            Button saveBtn = new Button { Content = "حفظ", Width = 110, Height = 40, Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 12, 0) };
            Button cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Style = (Style)FindResource("SecondaryButton") };
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 6);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;

            saveBtn.Click += async (s, ev) =>
            {
                if (string.IsNullOrEmpty(codeBox.Text) || string.IsNullOrEmpty(nameBox.Text))
                {
                    MessageBox.Show("يرجى إدخال كود واسم الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                    {
                        await connection.OpenAsync();
                        string sql = "INSERT INTO Treasury (TreasuryCode, TreasuryNameAr, CurrentBalance, IsActive, CreatedDate) VALUES (@code, @name, 0, 1, @date)";
                        using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@code", codeBox.Text);
                            cmd.Parameters.AddWithValue("@name", nameBox.Text);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    MessageBox.Show("تم إضافة الخزينة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadTreasuryDataAsync();
                    await LoadTreasuryFilterAsync();
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelBtn.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        #endregion

        #region نافذة إيداع (Income Dialog)

        private void BtnAddIncome_Click(object sender, RoutedEventArgs e)
        {
            if (this.TreasuryList.Count == 0)
            {
                MessageBox.Show("لا توجد خزائن. يرجى إضافة خزينة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = CreateIncomeDialog();
            this.ShowDialog(dialog);
        }

        private Window CreateIncomeDialog()
        {
            Window dialog = new Window
            {
                Title = "إيداع",
                Width = 450,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                FontFamily = new FontFamily("Segoe UI")
            };

            Grid mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = "💰", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            titlePanel.Children.Add(new TextBlock { Text = "إيداع", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("SuccessColor") });
            Grid.SetRow(titlePanel, 0);
            mainGrid.Children.Add(titlePanel);

            Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            TextBlock treasuryLabel = new TextBlock { Text = "اختر الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(treasuryLabel, 2);
            mainGrid.Children.Add(treasuryLabel);

            ComboBox treasuryCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernComboBoxStyle") };
            foreach (TreasuryCardItem treasury in this.TreasuryList)
            {
                treasuryCombo.Items.Add(treasury.Name);
            }
            if (treasuryCombo.Items.Count > 0) treasuryCombo.SelectedIndex = 0;
            Grid.SetRow(treasuryCombo, 3);
            mainGrid.Children.Add(treasuryCombo);

            TextBlock amountLabel = new TextBlock { Text = "المبلغ", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(amountLabel, 4);
            mainGrid.Children.Add(amountLabel);

            TextBox amountBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(amountBox, 5);
            mainGrid.Children.Add(amountBox);

            TextBlock descLabel = new TextBlock { Text = "البيان", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(descLabel, 6);
            mainGrid.Children.Add(descLabel);

            TextBox descBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(descBox, 7);
            mainGrid.Children.Add(descBox);

            StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            Button saveBtn = new Button { Content = "إيداع", Width = 110, Height = 40, Style = (Style)FindResource("SuccessButton"), Margin = new Thickness(0, 0, 12, 0) };
            Button cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Style = (Style)FindResource("SecondaryButton") };
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 8);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;

            saveBtn.Click += async (s, ev) =>
            {
                if (!decimal.TryParse(amountBox.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("المبلغ غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int selectedIndex = treasuryCombo.SelectedIndex;
                if (selectedIndex < 0 || selectedIndex >= this.TreasuryList.Count)
                {
                    MessageBox.Show("يرجى اختيار خزينة صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int treasuryId = this.TreasuryList[selectedIndex].Id;
                await AddTransactionAsync(treasuryId, "Receipt", amount, descBox.Text);
                dialog.Close();
            };

            cancelBtn.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        #endregion

        #region نافذة صرف (Expense Dialog)

        private void BtnAddExpense_Click(object sender, RoutedEventArgs e)
        {
            if (this.TreasuryList.Count == 0)
            {
                MessageBox.Show("لا توجد خزائن. يرجى إضافة خزينة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = CreateExpenseDialog();
            this.ShowDialog(dialog);
        }

        private Window CreateExpenseDialog()
        {
            Window dialog = new Window
            {
                Title = "صرف",
                Width = 450,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                FontFamily = new FontFamily("Segoe UI")
            };

            Grid mainGrid = new Grid { Margin = new Thickness(25) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = "💸", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            titlePanel.Children.Add(new TextBlock { Text = "صرف", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("DangerColor") });
            Grid.SetRow(titlePanel, 0);
            mainGrid.Children.Add(titlePanel);

            Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            TextBlock treasuryLabel = new TextBlock { Text = "اختر الخزينة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(treasuryLabel, 2);
            mainGrid.Children.Add(treasuryLabel);

            ComboBox treasuryCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernComboBoxStyle") };
            for (int i = 0; i < this.TreasuryList.Count; i++)
            {
                treasuryCombo.Items.Add(this.TreasuryList[i].Name);
            }
            if (treasuryCombo.Items.Count > 0) treasuryCombo.SelectedIndex = 0;
            Grid.SetRow(treasuryCombo, 3);
            mainGrid.Children.Add(treasuryCombo);

            TextBlock balanceLabel = new TextBlock { Text = "الرصيد المتاح", FontSize = 11, Foreground = (Brush)FindResource("TextSecondaryColor"), Margin = new Thickness(0, -10, 0, 5) };
            Grid.SetRow(balanceLabel, 4);
            mainGrid.Children.Add(balanceLabel);

            TextBlock balanceValue = new TextBlock
            {
                Text = CurrencyHelper.FormatAmount(this.TreasuryList.Count > 0 ? this.TreasuryList[0].Balance : 0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("SuccessColor"),
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(balanceValue, 5);
            mainGrid.Children.Add(balanceValue);

            TextBlock amountLabel = new TextBlock { Text = "المبلغ", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(amountLabel, 6);
            mainGrid.Children.Add(amountLabel);

            TextBox amountBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 15), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(amountBox, 7);
            mainGrid.Children.Add(amountBox);

            TextBlock descLabel = new TextBlock { Text = "البيان", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(descLabel, 8);
            mainGrid.Children.Add(descLabel);

            TextBox descBox = new TextBox { Height = 40, Margin = new Thickness(0, 0, 0, 20), Style = (Style)FindResource("ModernTextBox") };
            Grid.SetRow(descBox, 9);
            mainGrid.Children.Add(descBox);

            StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            Button saveBtn = new Button { Content = "صرف", Width = 110, Height = 40, Style = (Style)FindResource("DangerButton"), Margin = new Thickness(0, 0, 12, 0) };
            Button cancelBtn = new Button { Content = "إلغاء", Width = 110, Height = 40, Style = (Style)FindResource("SecondaryButton") };
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            Grid.SetRow(buttonPanel, 10);
            mainGrid.Children.Add(buttonPanel);

            treasuryCombo.SelectionChanged += (s, ev) =>
            {
                int selectedIndex = treasuryCombo.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < this.TreasuryList.Count)
                {
                    decimal balance = this.TreasuryList[selectedIndex].Balance;
                    balanceValue.Text = CurrencyHelper.FormatAmount(balance);
                    balanceValue.Foreground = balance < 0 ? (Brush)FindResource("DangerColor") : (Brush)FindResource("SuccessColor");
                }
            };

            dialog.Content = mainGrid;

            saveBtn.Click += async (s, ev) =>
            {
                if (!decimal.TryParse(amountBox.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("المبلغ غير صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int selectedIndex = treasuryCombo.SelectedIndex;
                if (selectedIndex < 0 || selectedIndex >= this.TreasuryList.Count)
                {
                    MessageBox.Show("يرجى اختيار خزينة صحيحة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int treasuryId = this.TreasuryList[selectedIndex].Id;
                decimal currentBalance = this.TreasuryList[selectedIndex].Balance;

                if (amount > currentBalance)
                {
                    MessageBox.Show($"الرصيد غير كافٍ. الرصيد الحالي: {CurrencyHelper.FormatAmount(currentBalance)}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await AddTransactionAsync(treasuryId, "Payment", amount, descBox.Text);
                dialog.Close();
            };

            cancelBtn.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        #endregion

        #region نافذة تحويل (Transfer Dialog)

        private void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (this.TreasuryList.Count < 2)
            {
                MessageBox.Show("يجب وجود خزينتين على الأقل للتحويل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = CreateTransferDialog();
            this.ShowDialog(dialog);
        }

        private Window CreateTransferDialog()
        {
            Window dialog = new Window
            {
                Title = "تحويل بين الخزائن",
                Width = 520,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)),
                FontFamily = new FontFamily("Segoe UI")
            };

            StackPanel mainPanel = new StackPanel { Margin = new Thickness(25) };

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            titlePanel.Children.Add(new TextBlock { Text = "🔄", FontSize = 24, Margin = new Thickness(0, 0, 12, 0) });
            titlePanel.Children.Add(new TextBlock { Text = "تحويل بين الخزائن", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("PrimaryColor") });
            mainPanel.Children.Add(titlePanel);

            Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15), Height = 1 };
            mainPanel.Children.Add(line);

            TextBlock fromLabel = new TextBlock
            {
                Text = "من خزينة",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(fromLabel);

            ComboBox fromCombo = new ComboBox
            {
                Height = 42,
                Margin = new Thickness(0, 0, 0, 12),
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            for (int i = 0; i < this.TreasuryList.Count; i++)
            {
                fromCombo.Items.Add(this.TreasuryList[i].Name);
            }
            if (fromCombo.Items.Count > 0) fromCombo.SelectedIndex = 0;
            mainPanel.Children.Add(fromCombo);

            TextBlock fromBalanceLabel = new TextBlock
            {
                Text = "الرصيد المتاح",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(fromBalanceLabel);

            TextBlock fromBalanceValue = new TextBlock
            {
                Text = CurrencyHelper.FormatAmount(this.TreasuryList.Count > 0 ? this.TreasuryList[0].Balance : 0),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("SuccessColor"),
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainPanel.Children.Add(fromBalanceValue);

            TextBlock toLabel = new TextBlock
            {
                Text = "إلى خزينة",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(toLabel);

            ComboBox toCombo = new ComboBox
            {
                Height = 42,
                Margin = new Thickness(0, 0, 0, 20),
                Style = (Style)FindResource("ModernComboBoxStyle")
            };
            for (int i = 0; i < this.TreasuryList.Count; i++)
            {
                toCombo.Items.Add(this.TreasuryList[i].Name);
            }
            if (toCombo.Items.Count > 1) toCombo.SelectedIndex = 1;
            else if (toCombo.Items.Count > 0) toCombo.SelectedIndex = 0;
            mainPanel.Children.Add(toCombo);

            TextBlock amountLabel = new TextBlock
            {
                Text = "المبلغ",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(amountLabel);

            TextBox amountBox = new TextBox
            {
                Height = 42,
                Margin = new Thickness(0, 0, 0, 20),
                Style = (Style)FindResource("ModernTextBox"),
                Text = "0"
            };
            mainPanel.Children.Add(amountBox);

            TextBlock descLabel = new TextBlock
            {
                Text = "البيان (اختياري)",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(descLabel);

            TextBox descBox = new TextBox
            {
                Height = 42,
                Margin = new Thickness(0, 0, 0, 25),
                Style = (Style)FindResource("ModernTextBox"),
                Text = ""
            };
            mainPanel.Children.Add(descBox);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            Button saveBtn = new Button
            {
                Content = "تحويل",
                Width = 130,
                Height = 45,
                Style = (Style)FindResource("PrimaryButton"),
                Margin = new Thickness(0, 0, 20, 0),
                FontSize = 14
            };

            Button cancelBtn = new Button
            {
                Content = "إلغاء",
                Width = 130,
                Height = 45,
                Style = (Style)FindResource("SecondaryButton"),
                FontSize = 14
            };

            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;

            fromCombo.SelectionChanged += (s, ev) =>
            {
                int selectedIndex = fromCombo.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < this.TreasuryList.Count)
                {
                    decimal balance = this.TreasuryList[selectedIndex].Balance;
                    fromBalanceValue.Text = CurrencyHelper.FormatAmount(balance);
                    fromBalanceValue.Foreground = balance < 0 ? (Brush)FindResource("DangerColor") : (Brush)FindResource("SuccessColor");
                }
            };

            saveBtn.Click += async (s, ev) =>
            {
                int fromIndex = fromCombo.SelectedIndex;
                int toIndex = toCombo.SelectedIndex;

                if (fromIndex < 0 || toIndex < 0 || fromIndex >= this.TreasuryList.Count || toIndex >= this.TreasuryList.Count)
                {
                    MessageBox.Show("يرجى اختيار الخزائن بشكل صحيح", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (fromIndex == toIndex)
                {
                    MessageBox.Show("لا يمكن التحويل إلى نفس الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(amountBox.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("المبلغ غير صحيح. يرجى إدخال مبلغ أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    amountBox.Focus();
                    return;
                }

                int fromId = this.TreasuryList[fromIndex].Id;
                int toId = this.TreasuryList[toIndex].Id;
                string fromName = this.TreasuryList[fromIndex].Name;
                string toName = this.TreasuryList[toIndex].Name;
                decimal fromBalance = this.TreasuryList[fromIndex].Balance;

                if (amount > fromBalance)
                {
                    MessageBox.Show($"الرصيد غير كافٍ في خزينة {fromName}\n\nالرصيد الحالي: {CurrencyHelper.FormatAmount(fromBalance)}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    amountBox.Focus();
                    return;
                }

                string transferDescription = string.IsNullOrEmpty(descBox.Text) ? $"تحويل من {fromName} إلى {toName}" : descBox.Text;

                await AddTransactionAsync(fromId, "Payment", amount, $"{transferDescription} (من {fromName})");
                await AddTransactionAsync(toId, "Receipt", amount, $"{transferDescription} (إلى {toName})");

                dialog.Close();
            };

            cancelBtn.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        #endregion

        #region إضافة حركة نقدية (Add Transaction)

        private async Task AddTransactionAsync(int treasuryId, string type, decimal amount, string description)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    string getBalanceSql = "SELECT CurrentBalance FROM Treasury WHERE TreasuryID = @id";
                    decimal currentBalance = 0;

                    using (SQLiteCommand cmd = new SQLiteCommand(getBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", treasuryId);
                        object result = await cmd.ExecuteScalarAsync();
                        currentBalance = Convert.ToDecimal(result);
                    }

                    decimal newBalance = type == "Receipt" ? currentBalance + amount : currentBalance - amount;

                    string insertSql = @"
                        INSERT INTO TreasuryTransactions (
                            TreasuryID, TransactionDate, TransactionType, Amount, 
                            BalanceAfter, Description, CreatedBy, CreatedDate
                        ) VALUES (
                            @tid, @date, @type, @amount, @balance, @desc, @user, @created
                        )";

                    using (SQLiteCommand cmd = new SQLiteCommand(insertSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@tid", treasuryId);
                        cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@amount", amount);
                        cmd.Parameters.AddWithValue("@balance", newBalance);
                        cmd.Parameters.AddWithValue("@desc", description ?? "");
                        cmd.Parameters.AddWithValue("@user", LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                        cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string updateSql = "UPDATE Treasury SET CurrentBalance = @balance WHERE TreasuryID = @id";
                    using (SQLiteCommand cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@balance", newBalance);
                        cmd.Parameters.AddWithValue("@id", treasuryId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await LoadTreasuryDataAsync();

                int currentFilter = 0;
                if (this.cmbTreasuryFilter.SelectedItem is ComboBoxItem selected && selected.Tag is int id && id > 0)
                {
                    currentFilter = id;
                }
                await LoadTransactionsAsync(currentFilter);

                string msg = type == "Receipt" ? "تم إيداع المبلغ بنجاح" : "تم صرف المبلغ بنجاح";
                MessageBox.Show(msg, "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"AddTransactionAsync Error: {ex.Message}");
            }
        }

        #endregion

        #region سندات القبض والصرف - نسخة محسنة (Receipt & Payment Vouchers - Enhanced)

        private async void BtnReceiptVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (this.TreasuryList.Count == 0)
            {
                MessageBox.Show("لا توجد خزائن. يرجى إضافة خزينة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = await CreateEnhancedReceiptVoucherDialogAsync();
            this.ShowDialog(dialog);
        }

        private async void BtnPaymentVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (this.TreasuryList.Count == 0)
            {
                MessageBox.Show("لا توجد خزائن. يرجى إضافة خزينة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Window dialog = await CreateEnhancedPaymentVoucherDialogAsync();
            this.ShowDialog(dialog);
        }

        private async Task<Window> CreateEnhancedReceiptVoucherDialogAsync()
        {
            Window dialog = new Window
            {
                Title = "سند قبض - تسجيل تحصيل نقدي",
                Width = 850,
                Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("SurfaceColor"),
                FontFamily = new FontFamily("Segoe UI"),
                FlowDirection = FlowDirection.RightToLeft
            };

            Grid mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border headerBorder = new Border
            {
                Background = (Brush)FindResource("SuccessColor"),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 15),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetRow(headerBorder, 0);

            StackPanel headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock { Text = "💰", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            headerPanel.Children.Add(new TextBlock { Text = "سند قبض", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            headerBorder.Child = headerPanel;
            mainGrid.Children.Add(headerBorder);

            Border line = new Border
            {
                BorderBrush = (Brush)FindResource("BorderLight"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            Grid row1 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row1, 2);

            StackPanel voucherNumberPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            voucherNumberPanel.Children.Add(new TextBlock
            {
                Text = "رقم السند",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtVoucherNumber = new TextBox
            {
                Height = 35,
                Style = (Style)FindResource("ModernTextBox"),
                IsReadOnly = true,
                Background = (Brush)FindResource("Gray100")
            };
            string voucherNumber = await _databaseService.GenerateReceiptVoucherNumberAsync();
            txtVoucherNumber.Text = voucherNumber;
            voucherNumberPanel.Children.Add(txtVoucherNumber);
            Grid.SetColumn(voucherNumberPanel, 0);
            row1.Children.Add(voucherNumberPanel);

            StackPanel datePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            datePanel.Children.Add(new TextBlock
            {
                Text = "التاريخ",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            DatePicker dtpDate = new DatePicker
            {
                Height = 35,
                SelectedDate = DateTime.Now,
                Style = (Style)FindResource("ModernDatePicker")
            };
            datePanel.Children.Add(dtpDate);
            Grid.SetColumn(datePanel, 1);
            row1.Children.Add(datePanel);
            mainGrid.Children.Add(row1);

            Grid row2 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row2, 3);

            StackPanel treasuryPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            treasuryPanel.Children.Add(new TextBlock
            {
                Text = "الخزينة المستلمة",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            ComboBox cmbTreasury = new ComboBox { Height = 35, Style = (Style)FindResource("ModernComboBoxStyle") };
            foreach (var treasury in this.TreasuryList)
            {
                cmbTreasury.Items.Add(new ComboBoxItem { Content = $"{treasury.Name}", Tag = treasury.Id });
            }
            cmbTreasury.SelectedIndex = -1;
            treasuryPanel.Children.Add(cmbTreasury);
            Grid.SetColumn(treasuryPanel, 0);
            row2.Children.Add(treasuryPanel);

            StackPanel partyTypePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            partyTypePanel.Children.Add(new TextBlock
            {
                Text = "نوع الطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            StackPanel radioPanel = new StackPanel { Orientation = Orientation.Horizontal, Height = 35 };
            RadioButton rbCustomer = new RadioButton { Content = "عميل", IsChecked = true, Margin = new Thickness(0, 0, 25, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            RadioButton rbSupplier = new RadioButton { Content = "مورد", Margin = new Thickness(0, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            radioPanel.Children.Add(rbCustomer);
            radioPanel.Children.Add(rbSupplier);
            partyTypePanel.Children.Add(radioPanel);
            Grid.SetColumn(partyTypePanel, 1);
            row2.Children.Add(partyTypePanel);
            mainGrid.Children.Add(row2);

            Grid row3 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row3, 4);

            StackPanel partyPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            partyPanel.Children.Add(new TextBlock
            {
                Text = "الطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            ComboBox cmbParty = new ComboBox { Height = 35, Style = (Style)FindResource("ModernComboBoxStyle") };
            await LoadCustomersToComboAsync(cmbParty);
            cmbParty.SelectedIndex = -1;
            partyPanel.Children.Add(cmbParty);
            Grid.SetColumn(partyPanel, 0);
            row3.Children.Add(partyPanel);

            StackPanel balancePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            balancePanel.Children.Add(new TextBlock
            {
                Text = "الرصيد الحالي للطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            Border balanceBorder = new Border
            {
                Background = (Brush)FindResource("Gray50"),
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(6)
            };
            TextBlock txtPartyBalance = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Gray400"),
                Text = "---",
                TextAlignment = TextAlignment.Center
            };
            balanceBorder.Child = txtPartyBalance;
            balancePanel.Children.Add(balanceBorder);
            Grid.SetColumn(balancePanel, 1);
            row3.Children.Add(balancePanel);
            mainGrid.Children.Add(row3);

            Grid row4 = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row4, 5);

            StackPanel amountPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            amountPanel.Children.Add(new TextBlock
            {
                Text = "المبلغ",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtAmount = new TextBox { Height = 35, Style = (Style)FindResource("ModernTextBox") };
            amountPanel.Children.Add(txtAmount);
            Grid.SetColumn(amountPanel, 0);
            row4.Children.Add(amountPanel);

            StackPanel descPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            descPanel.Children.Add(new TextBlock
            {
                Text = "البيان",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtDescription = new TextBox { Height = 35, Style = (Style)FindResource("ModernTextBox") };
            descPanel.Children.Add(txtDescription);
            Grid.SetColumn(descPanel, 1);
            row4.Children.Add(descPanel);
            mainGrid.Children.Add(row4);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 6);

            Button btnSave = new Button
            {
                Content = "💾 حفظ السند",
                Width = 130,
                Height = 38,
                Style = (Style)FindResource("SuccessButton"),
                Margin = new Thickness(0, 0, 15, 0),
                FontSize = 13
            };
            Button btnCancel = new Button
            {
                Content = "✖ إلغاء",
                Width = 110,
                Height = 38,
                Style = (Style)FindResource("SecondaryButton"),
                FontSize = 13
            };
            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;

            rbCustomer.Checked += async (s, ev) =>
            {
                await LoadCustomersToComboAsync(cmbParty);
                cmbParty.SelectedIndex = -1;
                txtPartyBalance.Text = "---";
                txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
            };

            rbSupplier.Checked += async (s, ev) =>
            {
                await LoadSuppliersToComboAsync(cmbParty);
                cmbParty.SelectedIndex = -1;
                txtPartyBalance.Text = "---";
                txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
            };

            cmbParty.SelectionChanged += async (s, ev) =>
            {
                if (cmbParty.SelectedItem != null)
                {
                    await UpdatePartyBalanceAsync(cmbParty, txtPartyBalance, rbCustomer.IsChecked == true);
                }
                else
                {
                    txtPartyBalance.Text = "---";
                    txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
                }
            };

            // ============================================================
            // زر الحفظ - الجزء المعدل
            // ============================================================
            btnSave.Click += async (s, ev) =>
            {
                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbTreasury.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbParty.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار العميل أو المورد", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int treasuryId = (int)((ComboBoxItem)cmbTreasury.SelectedItem).Tag;
                int partyId = (int)((ComboBoxItem)cmbParty.SelectedItem).Tag;
                string partyName = ((ComboBoxItem)cmbParty.SelectedItem).Content.ToString();
                bool isCustomer = rbCustomer.IsChecked == true;
                DateTime selectedDate = dtpDate.SelectedDate ?? DateTime.Now;

                bool success = await _databaseService.SaveCustomerPaymentWithTreasuryAsync(
                    voucherNumber, selectedDate, isCustomer ? partyId : 0, amount, "Cash", "",
                    txtDescription.Text, null, null, treasuryId, LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);

                if (success)
                {
                    MessageBox.Show($"تم حفظ سند القبض {voucherNumber} بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ✅ تحديث بيانات الخزينة في الواجهة الرئيسية
                    await LoadTreasuryDataAsync();
                    await LoadTransactionsAsync();

                    // ✅ إعادة تحميل قائمة العملاء أو الموردين (لتحديث الرصيد)
                    if (isCustomer)
                    {
                        await LoadCustomersToComboAsync(cmbParty);
                    }
                    else
                    {
                        await LoadSuppliersToComboAsync(cmbParty);
                    }

                    // ✅ تحديث الرصيد المعروض للطرف الحالي
                    await UpdatePartyBalanceAsync(cmbParty, txtPartyBalance, isCustomer);

                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("حدث خطأ أثناء حفظ السند", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnCancel.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        private async Task<Window> CreateEnhancedPaymentVoucherDialogAsync()
        {
            Window dialog = new Window
            {
                Title = "سند صرف - تسجيل دفعة نقدية",
                Width = 850,
                Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("SurfaceColor"),
                FontFamily = new FontFamily("Segoe UI"),
                FlowDirection = FlowDirection.RightToLeft
            };

            Grid mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border headerBorder = new Border
            {
                Background = (Brush)FindResource("DangerColor"),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 15),
                CornerRadius = new CornerRadius(8)
            };
            Grid.SetRow(headerBorder, 0);

            StackPanel headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock { Text = "💸", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            headerPanel.Children.Add(new TextBlock { Text = "سند صرف", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            headerBorder.Child = headerPanel;
            mainGrid.Children.Add(headerBorder);

            Border line = new Border
            {
                BorderBrush = (Brush)FindResource("BorderLight"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(line, 1);
            mainGrid.Children.Add(line);

            Grid row1 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row1, 2);

            StackPanel voucherNumberPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            voucherNumberPanel.Children.Add(new TextBlock
            {
                Text = "رقم السند",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtVoucherNumber = new TextBox
            {
                Height = 35,
                Style = (Style)FindResource("ModernTextBox"),
                IsReadOnly = true,
                Background = (Brush)FindResource("Gray100")
            };
            string voucherNumber = await _databaseService.GeneratePaymentVoucherNumberAsync();
            txtVoucherNumber.Text = voucherNumber;
            voucherNumberPanel.Children.Add(txtVoucherNumber);
            Grid.SetColumn(voucherNumberPanel, 0);
            row1.Children.Add(voucherNumberPanel);

            StackPanel datePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            datePanel.Children.Add(new TextBlock
            {
                Text = "التاريخ",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            DatePicker dtpDate = new DatePicker
            {
                Height = 35,
                SelectedDate = DateTime.Now,
                Style = (Style)FindResource("ModernDatePicker")
            };
            datePanel.Children.Add(dtpDate);
            Grid.SetColumn(datePanel, 1);
            row1.Children.Add(datePanel);
            mainGrid.Children.Add(row1);

            Grid row2 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row2, 3);

            StackPanel treasuryPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            treasuryPanel.Children.Add(new TextBlock
            {
                Text = "الخزينة الصارفة",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            ComboBox cmbTreasury = new ComboBox { Height = 35, Style = (Style)FindResource("ModernComboBoxStyle") };
            foreach (var treasury in this.TreasuryList)
            {
                cmbTreasury.Items.Add(new ComboBoxItem { Content = $"{treasury.Name}", Tag = treasury.Id });
            }
            cmbTreasury.SelectedIndex = -1;
            treasuryPanel.Children.Add(cmbTreasury);
            Grid.SetColumn(treasuryPanel, 0);
            row2.Children.Add(treasuryPanel);

            StackPanel partyTypePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            partyTypePanel.Children.Add(new TextBlock
            {
                Text = "نوع الطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            StackPanel radioPanel = new StackPanel { Orientation = Orientation.Horizontal, Height = 35 };
            RadioButton rbSupplier = new RadioButton { Content = "مورد", IsChecked = true, Margin = new Thickness(0, 0, 25, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            RadioButton rbCustomer = new RadioButton { Content = "عميل", Margin = new Thickness(0, 0, 0, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            radioPanel.Children.Add(rbSupplier);
            radioPanel.Children.Add(rbCustomer);
            partyTypePanel.Children.Add(radioPanel);
            Grid.SetColumn(partyTypePanel, 1);
            row2.Children.Add(partyTypePanel);
            mainGrid.Children.Add(row2);

            Grid row3 = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row3, 4);

            StackPanel partyPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            partyPanel.Children.Add(new TextBlock
            {
                Text = "الطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            ComboBox cmbParty = new ComboBox { Height = 35, Style = (Style)FindResource("ModernComboBoxStyle") };
            await LoadSuppliersToComboAsync(cmbParty);
            cmbParty.SelectedIndex = -1;
            partyPanel.Children.Add(cmbParty);
            Grid.SetColumn(partyPanel, 0);
            row3.Children.Add(partyPanel);

            StackPanel balancePanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            balancePanel.Children.Add(new TextBlock
            {
                Text = "الرصيد الحالي للطرف",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            Border balanceBorder = new Border
            {
                Background = (Brush)FindResource("Gray50"),
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(6)
            };
            TextBlock txtPartyBalance = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Gray400"),
                Text = "---",
                TextAlignment = TextAlignment.Center
            };
            balanceBorder.Child = txtPartyBalance;
            balancePanel.Children.Add(balanceBorder);
            Grid.SetColumn(balancePanel, 1);
            row3.Children.Add(balancePanel);
            mainGrid.Children.Add(row3);

            Grid row4 = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row4.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(row4, 5);

            StackPanel amountPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            amountPanel.Children.Add(new TextBlock
            {
                Text = "المبلغ",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtAmount = new TextBox { Height = 35, Style = (Style)FindResource("ModernTextBox") };
            amountPanel.Children.Add(txtAmount);
            Grid.SetColumn(amountPanel, 0);
            row4.Children.Add(amountPanel);

            StackPanel descPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            descPanel.Children.Add(new TextBlock
            {
                Text = "البيان",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            TextBox txtDescription = new TextBox { Height = 35, Style = (Style)FindResource("ModernTextBox") };
            descPanel.Children.Add(txtDescription);
            Grid.SetColumn(descPanel, 1);
            row4.Children.Add(descPanel);
            mainGrid.Children.Add(row4);

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttonPanel, 6);

            Button btnSave = new Button
            {
                Content = "💾 حفظ السند",
                Width = 130,
                Height = 38,
                Style = (Style)FindResource("DangerButton"),
                Margin = new Thickness(0, 0, 15, 0),
                FontSize = 13
            };
            Button btnCancel = new Button
            {
                Content = "✖ إلغاء",
                Width = 110,
                Height = 38,
                Style = (Style)FindResource("SecondaryButton"),
                FontSize = 13
            };
            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            mainGrid.Children.Add(buttonPanel);

            dialog.Content = mainGrid;

            rbSupplier.Checked += async (s, ev) =>
            {
                await LoadSuppliersToComboAsync(cmbParty);
                cmbParty.SelectedIndex = -1;
                txtPartyBalance.Text = "---";
                txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
            };

            rbCustomer.Checked += async (s, ev) =>
            {
                await LoadCustomersToComboAsync(cmbParty);
                cmbParty.SelectedIndex = -1;
                txtPartyBalance.Text = "---";
                txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
            };

            cmbParty.SelectionChanged += async (s, ev) =>
            {
                if (cmbParty.SelectedItem != null)
                {
                    await UpdatePartyBalanceAsync(cmbParty, txtPartyBalance, rbCustomer.IsChecked == true);
                }
                else
                {
                    txtPartyBalance.Text = "---";
                    txtPartyBalance.Foreground = (Brush)FindResource("Gray400");
                }
            };

            // ============================================================
            // زر الحفظ - الجزء المعدل
            // ============================================================
            btnSave.Click += async (s, ev) =>
            {
                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbTreasury.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار الخزينة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbParty.SelectedItem == null)
                {
                    MessageBox.Show("الرجاء اختيار المورد أو العميل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int treasuryId = (int)((ComboBoxItem)cmbTreasury.SelectedItem).Tag;
                int partyId = (int)((ComboBoxItem)cmbParty.SelectedItem).Tag;
                bool isCustomer = rbCustomer.IsChecked == true;
                DateTime selectedDate = dtpDate.SelectedDate ?? DateTime.Now;

                decimal treasuryBalance = await _databaseService.GetTreasuryBalanceAsync(treasuryId);
                if (amount > treasuryBalance)
                {
                    MessageBox.Show($"الرصيد غير كافٍ في الخزينة!\nالرصيد الحالي: {CurrencyHelper.FormatAmount(treasuryBalance)}\nالمبلغ المطلوب: {CurrencyHelper.FormatAmount(amount)}",
                        "رصيد غير كافٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool success;
                if (rbSupplier.IsChecked == true)
                {
                    success = await _databaseService.SavePaymentVoucherAsync(
                        txtVoucherNumber.Text, selectedDate, partyId, 0, amount, "Cash", "",
                        txtDescription.Text, treasuryId, LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                }
                else
                {
                    success = await _databaseService.SavePaymentVoucherAsync(
                        txtVoucherNumber.Text, selectedDate, 0, partyId, amount, "Cash", "",
                        txtDescription.Text, treasuryId, LoginView.CurrentUserId > 0 ? LoginView.CurrentUserId : 1);
                }

                if (success)
                {
                    MessageBox.Show($"تم حفظ سند الصرف {txtVoucherNumber.Text} بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ✅ تحديث بيانات الخزينة في الواجهة الرئيسية
                    await LoadTreasuryDataAsync();
                    await LoadTransactionsAsync();

                    // ✅ إعادة تحميل قائمة العملاء أو الموردين (لتحديث الرصيد)
                    if (isCustomer)
                    {
                        await LoadCustomersToComboAsync(cmbParty);
                    }
                    else
                    {
                        await LoadSuppliersToComboAsync(cmbParty);
                    }

                    // ✅ تحديث الرصيد المعروض للطرف الحالي
                    await UpdatePartyBalanceAsync(cmbParty, txtPartyBalance, isCustomer);

                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("حدث خطأ أثناء حفظ السند", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnCancel.Click += (s, ev) => dialog.Close();

            return dialog;
        }

        private async Task UpdatePartyBalanceAsync(ComboBox cmbParty, TextBlock txtBalance, bool isCustomer)
        {
            if (cmbParty.SelectedItem != null && cmbParty.SelectedItem is ComboBoxItem selectedItem)
            {
                int partyId = (int)selectedItem.Tag;
                decimal balance = 0;

                if (isCustomer)
                {
                    balance = await GetCustomerActualBalanceForTreasuryAsync(partyId);

                    if (balance > 0)
                    {
                        txtBalance.Text = CurrencyHelper.FormatAmount(balance);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // أحمر
                    }
                    else if (balance < 0)
                    {
                        decimal absoluteBalance = Math.Abs(balance);
                        txtBalance.Text = CurrencyHelper.FormatAmount(absoluteBalance);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // أخضر
                        txtBalance.Text = $"({txtBalance.Text})";
                    }
                    else
                    {
                        txtBalance.Text = CurrencyHelper.FormatAmount(0);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    }
                }
                else
                {
                    balance = await GetSupplierActualBalanceForTreasuryAsync(partyId);

                    if (balance > 0)
                    {
                        txtBalance.Text = CurrencyHelper.FormatAmount(balance);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                        txtBalance.Text = $"({txtBalance.Text})";
                    }
                    else if (balance < 0)
                    {
                        decimal absoluteBalance = Math.Abs(balance);
                        txtBalance.Text = CurrencyHelper.FormatAmount(absoluteBalance);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    }
                    else
                    {
                        txtBalance.Text = CurrencyHelper.FormatAmount(0);
                        txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    }
                }
            }
            else
            {
                txtBalance.Text = "---";
                txtBalance.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }

        #endregion

        #region نافذة عرض حركات الخزينة المنبثقة (Treasury Transactions Dialog)

        private async void ShowTreasuryTransactionsDialog(int treasuryId, string treasuryName)
        {
            try
            {
                var transactions = await LoadTreasuryTransactionsForDialogAsync(treasuryId);

                if (transactions == null || transactions.Count == 0)
                {
                    MessageBox.Show($"لا توجد حركات مسجلة للخزينة {treasuryName}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Window dialog = CreateTreasuryTransactionsDialog(treasuryId, treasuryName, transactions);
                this.ShowDialog(dialog);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في عرض حركات الخزينة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ShowTreasuryTransactionsDialog Error: {ex.Message}");
            }
        }

        private async Task<List<TreasuryTransactionDetailItem>> LoadTreasuryTransactionsForDialogAsync(int treasuryId)
        {
            var transactions = new List<TreasuryTransactionDetailItem>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    string cleanupSql = @"
                DELETE FROM TreasuryTransactions 
                WHERE ReferenceType = 'RECEIPT_VOUCHER' 
                AND NOT EXISTS (SELECT 1 FROM ReceiptVouchers WHERE VoucherID = TreasuryTransactions.ReferenceID);
                
                DELETE FROM TreasuryTransactions 
                WHERE ReferenceType = 'PAYMENT_VOUCHER' 
                AND NOT EXISTS (SELECT 1 FROM PaymentVouchers WHERE VoucherID = TreasuryTransactions.ReferenceID);
                
                DELETE FROM TreasuryTransactions 
                WHERE ReferenceType = 'SALES_INVOICE' 
                AND NOT EXISTS (SELECT 1 FROM SalesInvoices WHERE InvoiceID = TreasuryTransactions.ReferenceID);
                
                DELETE FROM TreasuryTransactions 
                WHERE ReferenceType = 'PURCHASE_INVOICE' 
                AND NOT EXISTS (SELECT 1 FROM PurchaseInvoices WHERE InvoiceID = TreasuryTransactions.ReferenceID);
            ";

                    using (SQLiteCommand cleanupCmd = new SQLiteCommand(cleanupSql, connection))
                    {
                        int deletedCount = await cleanupCmd.ExecuteNonQueryAsync();
                        if (deletedCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"🧹 تم حذف {deletedCount} حركة خزينة عالقة");
                        }
                    }

                    // تعديل SQL لترتيب الحركات: الوارد أولاً ثم الصادر في نفس اليوم
                    string sql = @"
                SELECT 
                    TransactionDate, 
                    TransactionType, 
                    COALESCE(Description, '') as Description, 
                    Amount, 
                    BalanceAfter,
                    strftime('%Y-%m-%d', TransactionDate) as DateOnly
                FROM TreasuryTransactions 
                WHERE TreasuryID = @treasuryId
                ORDER BY 
                    DateOnly ASC,
                    CASE 
                        WHEN TransactionType = 'Receipt' THEN 0 
                        ELSE 1 
                    END,
                    TransactionDate ASC";

                    using (SQLiteCommand cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            int serialNumber = 1;

                            while (await reader.ReadAsync())
                            {
                                DateTime transactionDate;
                                object dateObj = reader["TransactionDate"];
                                if (dateObj != null && dateObj != DBNull.Value)
                                {
                                    transactionDate = Convert.ToDateTime(dateObj);
                                }
                                else
                                {
                                    transactionDate = DateTime.Now;
                                }

                                string transactionType;
                                object typeObj = reader["TransactionType"];
                                if (typeObj != null && typeObj != DBNull.Value)
                                {
                                    transactionType = typeObj.ToString();
                                }
                                else
                                {
                                    transactionType = "Receipt";
                                }

                                string description;
                                object descObj = reader["Description"];
                                if (descObj != null && descObj != DBNull.Value)
                                {
                                    description = descObj.ToString();
                                }
                                else
                                {
                                    description = "";
                                }

                                decimal amount;
                                object amountObj = reader["Amount"];
                                if (amountObj != null && amountObj != DBNull.Value)
                                {
                                    amount = Convert.ToDecimal(amountObj);
                                }
                                else
                                {
                                    amount = 0;
                                }

                                decimal balanceAfter;
                                object balanceObj = reader["BalanceAfter"];
                                if (balanceObj != null && balanceObj != DBNull.Value)
                                {
                                    balanceAfter = Convert.ToDecimal(balanceObj);
                                }
                                else
                                {
                                    balanceAfter = 0;
                                }

                                transactions.Add(new TreasuryTransactionDetailItem
                                {
                                    SerialNumber = serialNumber++,
                                    TransactionDate = transactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                                    TransactionType = transactionType,
                                    Description = description,
                                    Amount = amount,
                                    BalanceAfter = balanceAfter
                                });
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ بعد التنظيف: تم تحميل {transactions.Count} حركة للخزينة رقم {treasuryId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadTreasuryTransactionsForDialogAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            return transactions;
        }

        private Window CreateTreasuryTransactionsDialog(int treasuryId, string treasuryName, List<TreasuryTransactionDetailItem> transactions)
        {
            decimal totalIncome = transactions.Where(t => t.TransactionType == "Receipt").Sum(t => t.Amount);
            decimal totalExpense = transactions.Where(t => t.TransactionType == "Payment").Sum(t => t.Amount);
            decimal netAmount = totalIncome - totalExpense;
            decimal currentBalance = transactions.FirstOrDefault()?.BalanceAfter ?? 0;

            string treasuryCode = this.TreasuryList.FirstOrDefault(t => t.Id == treasuryId)?.Code ?? "";

            Window dialog = new Window
            {
                Title = $"حركات الخزينة - {treasuryName}",
                Width = 830,
                Height = 580,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.CanResize,
                Background = (Brush)FindResource("BackgroundColor"),
                FontFamily = new FontFamily("Segoe UI"),
                FlowDirection = FlowDirection.RightToLeft
            };

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border headerBorder = new Border
            {
                Background = (Brush)FindResource("PrimaryColor"),
                Padding = new Thickness(17, 12, 17, 12)
            };
            Grid.SetRow(headerBorder, 0);

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerIcon = new TextBlock { Text = "🏦", FontSize = 23, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(headerIcon, 0);
            headerGrid.Children.Add(headerIcon);

            StackPanel headerTextPanel = new StackPanel();
            headerTextPanel.Children.Add(new TextBlock
            {
                Text = $"حركات الخزينة - {treasuryName}",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            headerTextPanel.Children.Add(new TextBlock
            {
                Text = $"كود الخزينة: {treasuryCode}",
                FontSize = 10,
                Foreground = (Brush)FindResource("Gray400")
            });
            Grid.SetColumn(headerTextPanel, 1);
            headerGrid.Children.Add(headerTextPanel);

            Button closeBtn = new Button
            {
                Content = "✕",
                Width = 27,
                Height = 27,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };
            closeBtn.Click += (s, ev) => dialog.Close();
            Grid.SetColumn(closeBtn, 2);
            headerGrid.Children.Add(closeBtn);

            headerBorder.Child = headerGrid;
            mainGrid.Children.Add(headerBorder);

            Grid statsGrid = new Grid
            {
                Margin = new Thickness(17, 12, 17, 8)
            };
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(statsGrid, 1);
            mainGrid.Children.Add(statsGrid);

            Border balanceCard = CreateSmallStatCard("💰 الرصيد الحالي", CurrencyHelper.FormatAmount(currentBalance),
                new SolidColorBrush(Color.FromRgb(254, 249, 195)), new SolidColorBrush(Color.FromRgb(234, 179, 8)));
            Grid.SetColumn(balanceCard, 0);
            statsGrid.Children.Add(balanceCard);

            Border netCard = CreateSmallStatCard("⚖️ صافي الحركة", CurrencyHelper.FormatAmount(netAmount),
                new SolidColorBrush(Color.FromRgb(219, 234, 254)), new SolidColorBrush(Color.FromRgb(79, 70, 229)));
            Grid.SetColumn(netCard, 1);
            statsGrid.Children.Add(netCard);

            Border expenseCard = CreateSmallStatCard("📉 إجمالي الصرف", CurrencyHelper.FormatAmount(totalExpense),
                new SolidColorBrush(Color.FromRgb(254, 226, 226)), new SolidColorBrush(Color.FromRgb(239, 68, 68)));
            Grid.SetColumn(expenseCard, 2);
            statsGrid.Children.Add(expenseCard);

            Border incomeCard = CreateSmallStatCard("📈 إجمالي الإيداعات", CurrencyHelper.FormatAmount(totalIncome),
                new SolidColorBrush(Color.FromRgb(220, 252, 231)), new SolidColorBrush(Color.FromRgb(16, 185, 129)));
            Grid.SetColumn(incomeCard, 3);
            statsGrid.Children.Add(incomeCard);

            StackPanel actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(17, 0, 17, 12)
            };
            Grid.SetRow(actionsPanel, 2);
            mainGrid.Children.Add(actionsPanel);

            Button exportBtn = new Button
            {
                Content = "📊 تصدير Excel",
                Width = 100,
                Height = 29,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 10,
                Style = (Style)FindResource("SuccessButton")
            };
            Button printBtn = new Button
            {
                Content = "🖨️ طباعة",
                Width = 83,
                Height = 29,
                FontSize = 10,
                Style = (Style)FindResource("PrimaryButton")
            };
            actionsPanel.Children.Add(exportBtn);
            actionsPanel.Children.Add(printBtn);

            DataGrid transactionsGrid = new DataGrid
            {
                Margin = new Thickness(17, 0, 17, 8),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                RowHeight = 37,
                Background = (Brush)FindResource("SurfaceColor"),
                AlternatingRowBackground = (Brush)FindResource("Gray50"),
                RowBackground = (Brush)FindResource("SurfaceColor"),
                BorderBrush = (Brush)FindResource("BorderLight"),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FlowDirection = FlowDirection.RightToLeft
            };
            Grid.SetRow(transactionsGrid, 3);
            mainGrid.Children.Add(transactionsGrid);

            // عمود الرقم التسلسلي
            DataGridTextColumn serialColumn = new DataGridTextColumn();
            serialColumn.Header = "م";
            serialColumn.Binding = new System.Windows.Data.Binding("SerialNumber");
            serialColumn.Width = new DataGridLength(50);
            Style serialCellStyle = new Style(typeof(TextBlock));
            serialCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            serialCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            serialCellStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            serialCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            serialColumn.ElementStyle = serialCellStyle;
            transactionsGrid.Columns.Add(serialColumn);

            // عمود التاريخ
            DataGridTextColumn dateColumn = new DataGridTextColumn();
            dateColumn.Header = "التاريخ";
            dateColumn.Binding = new System.Windows.Data.Binding("TransactionDate");
            dateColumn.Width = new DataGridLength(130);
            Style dateCellStyle = new Style(typeof(TextBlock));
            dateCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            dateCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            dateCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            dateColumn.ElementStyle = dateCellStyle;
            transactionsGrid.Columns.Add(dateColumn);

            // عمود البيان
            DataGridTextColumn descColumn = new DataGridTextColumn();
            descColumn.Header = "البيان";
            descColumn.Binding = new System.Windows.Data.Binding("Description");
            descColumn.Width = new DataGridLength(290);
            Style descCellStyle = new Style(typeof(TextBlock));
            descCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            descCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            descCellStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            descCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            descColumn.ElementStyle = descCellStyle;
            transactionsGrid.Columns.Add(descColumn);

            // عمود وارد (Income)
            DataGridTextColumn incomeColumn = new DataGridTextColumn();
            incomeColumn.Header = "وارد";
            incomeColumn.Binding = new System.Windows.Data.Binding("FormattedIncomeAmount");
            incomeColumn.Width = new DataGridLength(110);
            Style incomeCellStyle = new Style(typeof(TextBlock));
            incomeCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            incomeCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            incomeCellStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            incomeCellStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(16, 185, 129))));
            incomeCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            incomeColumn.ElementStyle = incomeCellStyle;
            transactionsGrid.Columns.Add(incomeColumn);

            // عمود صادر (Expense)
            DataGridTextColumn expenseColumn = new DataGridTextColumn();
            expenseColumn.Header = "صادر";
            expenseColumn.Binding = new System.Windows.Data.Binding("FormattedExpenseAmount");
            expenseColumn.Width = new DataGridLength(110);
            Style expenseCellStyle = new Style(typeof(TextBlock));
            expenseCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            expenseCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            expenseCellStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            expenseCellStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(239, 68, 68))));
            expenseCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            expenseColumn.ElementStyle = expenseCellStyle;
            transactionsGrid.Columns.Add(expenseColumn);

            // عمود الرصيد
            DataGridTextColumn balanceColumn = new DataGridTextColumn();
            balanceColumn.Header = "الرصيد";
            balanceColumn.Binding = new System.Windows.Data.Binding("FormattedBalance");
            balanceColumn.Width = new DataGridLength(120);
            Style balanceCellStyle = new Style(typeof(TextBlock));
            balanceCellStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            balanceCellStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            balanceCellStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            balanceCellStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
            balanceColumn.ElementStyle = balanceCellStyle;
            transactionsGrid.Columns.Add(balanceColumn);

            // ربط البيانات مع إضافة الرقم التسلسلي
            var transactionsWithSerial = transactions.Select((item, index) =>
            {
                var newItem = new TreasuryTransactionDetailItem
                {
                    TransactionDate = item.TransactionDate,
                    TransactionType = item.TransactionType,
                    Description = item.Description,
                    Amount = item.Amount,
                    BalanceAfter = item.BalanceAfter
                };
                // إضافة خاصية SerialNumber باستخدام الانعكاس أو إنشاء كلاس جديد
                return new { SerialNumber = index + 1, Item = newItem };
            }).ToList();

            // ربط البيانات مع تنسيق الأرقام بدون رمز العملة
            var displayList = new List<dynamic>();
            for (int i = 0; i < transactions.Count; i++)
            {
                var item = transactions[i];
                displayList.Add(new
                {
                    SerialNumber = i + 1,
                    TransactionDate = item.TransactionDate,
                    Description = item.Description,
                    FormattedIncomeAmount = item.TransactionType == "Receipt" ? item.Amount.ToString("N2") : "",
                    FormattedExpenseAmount = item.TransactionType == "Payment" ? item.Amount.ToString("N2") : "",
                    FormattedBalance = item.BalanceAfter.ToString("N2")
                });
            }

            transactionsGrid.ItemsSource = displayList;

            transactionsGrid.LoadingRow += (s, e) =>
            {
                try
                {
                    var item = e.Row.DataContext as TreasuryTransactionDetailItem;
                    if (item != null)
                    {
                        if (item.TransactionType == "Receipt")
                        {
                            e.Row.Background = new SolidColorBrush(Color.FromRgb(240, 255, 240));
                        }
                        else
                        {
                            e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 240, 240));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadingRow Error: {ex.Message}");
                }
            };

            Border footerBorder = new Border
            {
                Background = (Brush)FindResource("Gray50"),
                Padding = new Thickness(17, 8, 17, 8),
                BorderBrush = (Brush)FindResource("BorderLight"),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(footerBorder, 4);

            StackPanel footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            TextBlock recordCount = new TextBlock
            {
                Text = $"عدد السجلات: {transactions.Count}",
                FontSize = 9,
                Foreground = (Brush)FindResource("TextSecondaryColor")
            };
            footerPanel.Children.Add(recordCount);

            footerBorder.Child = footerPanel;
            mainGrid.Children.Add(footerBorder);

            dialog.Content = mainGrid;

            printBtn.Click += (s, e) => PrintTreasuryTransactionsDialog(transactions, treasuryName);
            exportBtn.Click += (s, e) => ExportTreasuryTransactionsToExcel(transactions, treasuryName);

            return dialog;
        }

        private Border CreateSmallStatCard(string label, string value, SolidColorBrush bgColor, SolidColorBrush textColor)
        {
            Border card = new Border
            {
                Background = bgColor,
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(4, 0, 4, 0),
                CornerRadius = new CornerRadius(10)
            };

            StackPanel panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock labelText = new TextBlock
            {
                Text = label,
                FontSize = 9,
                Foreground = (Brush)FindResource("TextSecondaryColor"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(labelText);

            TextBlock valueText = new TextBlock
            {
                Text = value,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = textColor,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(valueText);

            card.Child = panel;
            return card;
        }

        #endregion

        #region طباعة حركات الخزينة (Print Treasury Transactions)

        private void PrintTreasuryTransactionsDialog(List<TreasuryTransactionDetailItem> transactions, string treasuryName)
        {
            try
            {
                if (transactions == null || transactions.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                double pageWidth = 827;
                double pageHeight = 1169;
                double leftMargin = 40;
                double rightMargin = pageWidth - 40;
                double currentY = 40;

                FixedDocument fixedDocument = new FixedDocument();
                FixedPage fixedPage = new FixedPage();
                fixedPage.Width = pageWidth;
                fixedPage.Height = pageHeight;
                fixedPage.FlowDirection = FlowDirection.RightToLeft;
                fixedPage.Language = XmlLanguage.GetLanguage("ar-SA");

                Canvas canvas = new Canvas();
                canvas.Width = pageWidth;
                canvas.Height = pageHeight;

                decimal totalIncome = transactions.Where(t => t.TransactionType == "Receipt").Sum(t => t.Amount);
                decimal totalExpense = transactions.Where(t => t.TransactionType == "Payment").Sum(t => t.Amount);
                decimal netAmount = totalIncome - totalExpense;
                decimal currentBalance = transactions.FirstOrDefault()?.BalanceAfter ?? 0;

                TextBlock title = new TextBlock
                {
                    Text = $"كشف حركات الخزينة - {treasuryName}",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Arial"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 51, 102))
                };
                double titleWidth = MeasureTextWidth(title);
                Canvas.SetLeft(title, (pageWidth - titleWidth) / 2);
                Canvas.SetTop(title, currentY);
                canvas.Children.Add(title);
                currentY += 40;

                TextBlock dateText = new TextBlock
                {
                    Text = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    FontFamily = new FontFamily("Segoe UI")
                };
                Canvas.SetLeft(dateText, leftMargin);
                Canvas.SetTop(dateText, currentY);
                canvas.Children.Add(dateText);
                currentY += 30;

                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 20;

                double summaryCardWidth = (pageWidth - 100) / 4;
                double summaryStartX = leftMargin;

                Border incomeCardPrint = CreatePrintSummaryCard("إجمالي الإيداعات", CurrencyHelper.FormatAmount(totalIncome),
                    new SolidColorBrush(Color.FromRgb(220, 252, 231)), new SolidColorBrush(Color.FromRgb(16, 185, 129)), summaryCardWidth);
                Canvas.SetLeft(incomeCardPrint, summaryStartX);
                Canvas.SetTop(incomeCardPrint, currentY);
                canvas.Children.Add(incomeCardPrint);

                Border expenseCardPrint = CreatePrintSummaryCard("إجمالي الصرف", CurrencyHelper.FormatAmount(totalExpense),
                    new SolidColorBrush(Color.FromRgb(254, 226, 226)), new SolidColorBrush(Color.FromRgb(239, 68, 68)), summaryCardWidth);
                Canvas.SetLeft(expenseCardPrint, summaryStartX + summaryCardWidth + 10);
                Canvas.SetTop(expenseCardPrint, currentY);
                canvas.Children.Add(expenseCardPrint);

                Border netCardPrint = CreatePrintSummaryCard("صافي الحركة", CurrencyHelper.FormatAmount(netAmount),
                    new SolidColorBrush(Color.FromRgb(219, 234, 254)), new SolidColorBrush(Color.FromRgb(79, 70, 229)), summaryCardWidth);
                Canvas.SetLeft(netCardPrint, summaryStartX + (summaryCardWidth + 10) * 2);
                Canvas.SetTop(netCardPrint, currentY);
                canvas.Children.Add(netCardPrint);

                Border balanceCardPrint = CreatePrintSummaryCard("الرصيد الحالي", CurrencyHelper.FormatAmount(currentBalance),
                    new SolidColorBrush(Color.FromRgb(254, 249, 195)), new SolidColorBrush(Color.FromRgb(234, 179, 8)), summaryCardWidth);
                Canvas.SetLeft(balanceCardPrint, summaryStartX + (summaryCardWidth + 10) * 3);
                Canvas.SetTop(balanceCardPrint, currentY);
                canvas.Children.Add(balanceCardPrint);

                currentY += 85;

                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 20;

                double[] colWidths = { 45, 150, 90, 250, 100, 100 };
                string[] colHeaders = { "م", "التاريخ", "نوع الحركة", "البيان", "المبلغ", "الرصيد بعد" };
                double[] colStarts = new double[6];

                double startX = rightMargin;
                for (int i = 0; i < colWidths.Length; i++)
                {
                    startX -= colWidths[i];
                    colStarts[i] = startX;
                }

                double headerHeight = 35;
                double headerY = currentY;

                for (int i = 0; i < colHeaders.Length; i++)
                {
                    Border headerBg = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        Width = colWidths[i],
                        Height = headerHeight
                    };
                    Canvas.SetLeft(headerBg, colStarts[i]);
                    Canvas.SetTop(headerBg, headerY);
                    canvas.Children.Add(headerBg);

                    TextBlock headerText = new TextBlock
                    {
                        Text = colHeaders[i],
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Width = colWidths[i],
                        Foreground = Brushes.Black,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    Canvas.SetLeft(headerText, colStarts[i]);
                    Canvas.SetTop(headerText, headerY + (headerHeight - 18) / 2);
                    canvas.Children.Add(headerText);
                }
                currentY += headerHeight;

                double rowHeight = 30;
                int rowsPerPage = (int)Math.Floor((pageHeight - currentY - 80) / rowHeight);
                int totalRows = transactions.Count;
                int rowsToPrint = Math.Min(rowsPerPage, totalRows);

                for (int i = 0; i < rowsToPrint; i++)
                {
                    var item = transactions[i];
                    double rowY = currentY + (i * rowHeight);
                    bool isEvenRow = i % 2 == 0;

                    for (int j = 0; j < colWidths.Length; j++)
                    {
                        Color bgColor = isEvenRow ? Color.FromRgb(255, 255, 255) : Color.FromRgb(248, 250, 252);
                        Border cellBorder = new Border
                        {
                            Background = new SolidColorBrush(bgColor),
                            BorderBrush = Brushes.LightGray,
                            BorderThickness = new Thickness(1),
                            Width = colWidths[j],
                            Height = rowHeight
                        };
                        Canvas.SetLeft(cellBorder, colStarts[j]);
                        Canvas.SetTop(cellBorder, rowY);
                        canvas.Children.Add(cellBorder);

                        string cellText = "";
                        TextAlignment alignment = TextAlignment.Center;
                        SolidColorBrush textColor = Brushes.Black;

                        switch (j)
                        {
                            case 0:
                                cellText = (i + 1).ToString();
                                break;
                            case 1:
                                cellText = item.TransactionDate;
                                break;
                            case 2:
                                cellText = item.TransactionTypeArabic;
                                textColor = item.TransactionType == "Receipt" ?
                                    new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                                    new SolidColorBrush(Color.FromRgb(239, 68, 68));
                                break;
                            case 3:
                                cellText = item.Description;
                                alignment = TextAlignment.Right;
                                break;
                            case 4:
                                if (item.TransactionType == "Receipt")
                                {
                                    cellText = item.FormattedIncomeAmount;
                                    textColor = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                                }
                                else
                                {
                                    cellText = item.FormattedExpenseAmount;
                                    textColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                                }
                                break;
                            case 5:
                                cellText = item.FormattedBalance;
                                break;
                        }

                        if (!string.IsNullOrEmpty(cellText))
                        {
                            TextBlock cellTextBlock = new TextBlock
                            {
                                Text = cellText,
                                FontSize = 10,
                                FontFamily = new FontFamily("Segoe UI"),
                                TextAlignment = alignment,
                                Width = colWidths[j] - 6,
                                Foreground = textColor,
                                TextWrapping = TextWrapping.Wrap
                            };
                            Canvas.SetLeft(cellTextBlock, colStarts[j] + 3);
                            Canvas.SetTop(cellTextBlock, rowY + (rowHeight - 16) / 2);
                            canvas.Children.Add(cellTextBlock);
                        }
                    }
                }

                currentY += (rowsToPrint * rowHeight) + 20;
                AddDottedLine(canvas, leftMargin, rightMargin, currentY);
                currentY += 25;

                if (currentY < pageHeight - 60)
                {
                    TextBlock footer = new TextBlock
                    {
                        Text = "تم إنشاء هذا التقرير بواسطة نظام راصد المحاسبي",
                        FontSize = 10,
                        FontStyle = FontStyles.Italic,
                        Foreground = Brushes.Gray,
                        TextAlignment = TextAlignment.Center,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    double footerWidth = MeasureTextWidth(footer);
                    Canvas.SetLeft(footer, (pageWidth - footerWidth) / 2);
                    Canvas.SetTop(footer, pageHeight - 45);
                    canvas.Children.Add(footer);

                    TextBlock pageNumber = new TextBlock
                    {
                        Text = "صفحة 1 من 1",
                        FontSize = 9,
                        Foreground = Brushes.Gray,
                        TextAlignment = TextAlignment.Center,
                        FontFamily = new FontFamily("Segoe UI")
                    };
                    double pageNumberWidth = MeasureTextWidth(pageNumber);
                    Canvas.SetLeft(pageNumber, (pageWidth - pageNumberWidth) / 2);
                    Canvas.SetTop(pageNumber, pageHeight - 30);
                    canvas.Children.Add(pageNumber);
                }

                fixedPage.Children.Add(canvas);
                PageContent pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);
                fixedDocument.Pages.Add(pageContent);

                PrintPreviewWindow previewWindow = new PrintPreviewWindow(fixedDocument);
                previewWindow.Owner = Window.GetWindow(this);
                previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"PrintTreasuryTransactionsDialog Error: {ex.Message}");
            }
        }

        private Border CreatePrintSummaryCard(string label, string value, SolidColorBrush bgColor, SolidColorBrush textColor, double width)
        {
            Border card = new Border
            {
                Background = bgColor,
                Width = width,
                Height = 65
            };

            StackPanel stackPanel = new StackPanel
            {
                Margin = new Thickness(8, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock labelText = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = Brushes.Gray,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center
            };
            stackPanel.Children.Add(labelText);

            TextBlock valueText = new TextBlock
            {
                Text = value,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = textColor,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center
            };
            stackPanel.Children.Add(valueText);

            card.Child = stackPanel;
            return card;
        }

        #endregion

        #region تصدير حركات الخزينة إلى Excel (Export to Excel)

        private void ExportTreasuryTransactionsToExcel(List<TreasuryTransactionDetailItem> transactions, string treasuryName)
        {
            try
            {
                if (transactions == null || transactions.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"حركات_الخزينة_{treasuryName}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = "xlsx",
                    AddExtension = true
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("حركات الخزينة");

                        worksheet.Cell(1, 1).Value = $"كشف حركات الخزينة - {treasuryName}";
                        worksheet.Cell(1, 1).Style.Font.Bold = true;
                        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                        worksheet.Range(1, 1, 1, 5).Merge();

                        worksheet.Cell(2, 1).Value = $"تاريخ التقرير: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                        worksheet.Range(2, 1, 2, 5).Merge();

                        decimal totalIncome = transactions.Where(t => t.TransactionType == "Receipt").Sum(t => t.Amount);
                        decimal totalExpense = transactions.Where(t => t.TransactionType == "Payment").Sum(t => t.Amount);
                        decimal netAmount = totalIncome - totalExpense;

                        worksheet.Cell(4, 1).Value = "إجمالي الإيداعات:";
                        worksheet.Cell(4, 2).Value = totalIncome;
                        worksheet.Cell(4, 2).Style.Font.FontColor = ClosedXML.Excel.XLColor.Green;

                        worksheet.Cell(5, 1).Value = "إجمالي الصرف:";
                        worksheet.Cell(5, 2).Value = totalExpense;
                        worksheet.Cell(5, 2).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;

                        worksheet.Cell(6, 1).Value = "صافي الحركة:";
                        worksheet.Cell(6, 2).Value = netAmount;

                        worksheet.Cell(7, 1).Value = "الرصيد الحالي:";
                        worksheet.Cell(7, 2).Value = transactions.FirstOrDefault()?.BalanceAfter ?? 0;

                        int startRow = 9;
                        string[] headers = { "التاريخ", "نوع الحركة", "البيان", "المبلغ", "الرصيد بعد" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(startRow, i + 1).Value = headers[i];
                            worksheet.Cell(startRow, i + 1).Style.Font.Bold = true;
                            worksheet.Cell(startRow, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(99, 102, 241);
                            worksheet.Cell(startRow, i + 1).Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                        }

                        int rowIndex = startRow + 1;

                        foreach (var item in transactions)
                        {
                            worksheet.Cell(rowIndex, 1).Value = item.TransactionDate;
                            worksheet.Cell(rowIndex, 2).Value = item.TransactionTypeArabic;
                            worksheet.Cell(rowIndex, 3).Value = item.Description;
                            worksheet.Cell(rowIndex, 4).Value = item.Amount;
                            worksheet.Cell(rowIndex, 5).Value = item.BalanceAfter;

                            if (item.TransactionType == "Receipt")
                            {
                                worksheet.Cell(rowIndex, 4).Style.Font.FontColor = ClosedXML.Excel.XLColor.Green;
                            }
                            else
                            {
                                worksheet.Cell(rowIndex, 4).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;
                            }

                            if (rowIndex % 2 == 0)
                            {
                                worksheet.Row(rowIndex).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(248, 250, 252);
                            }

                            rowIndex++;
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show($"تم تصدير {transactions.Count} حركة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تصدير Excel: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"ExportTreasuryTransactionsToExcel Error: {ex.Message}");
            }
        }

        #endregion

        #region دوال مساعدة للطباعة (Print Helper Methods)

        private double MeasureTextWidth(TextBlock textBlock)
        {
            FormattedText formattedText = new FormattedText(
                textBlock.Text,
                System.Globalization.CultureInfo.GetCultureInfo("ar-SA"),
                FlowDirection.RightToLeft,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1);
            return formattedText.Width;
        }

        private void AddDottedLine(Canvas canvas, double startX, double endX, double y)
        {
            Line dottedLine = new Line
            {
                X1 = startX,
                X2 = endX,
                Y1 = y,
                Y2 = y,
                Stroke = Brushes.Gray,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 }
            };
            canvas.Children.Add(dottedLine);
        }

        #endregion

        #region دوال عامة (Public Methods)

        private int SafeToInt(object value, int defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            try { return Convert.ToInt32(value); }
            catch { return defaultValue; }
        }

        private async Task<decimal> GetTreasuryBalanceAsync(int treasuryId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                    object result = await cmd.ExecuteScalarAsync();
                    return Convert.ToDecimal(result);
                }
            }
        }

        private async Task<bool> UpdateTreasuryBalanceAsync(int treasuryId, decimal newBalance, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "UPDATE Treasury SET CurrentBalance = @newBalance, ModifiedDate = CURRENT_TIMESTAMP WHERE TreasuryID = @treasuryId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@newBalance", newBalance);
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
        }

        private async Task<bool> AddTreasuryTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int treasuryId,
            DateTime transactionDate,
            string transactionType,
            decimal amount,
            decimal balanceAfter,
            string description,
            string referenceType,
            int referenceId,
            string referenceNumber,
            int createdBy)
        {
            try
            {
                string sql = @"
                    INSERT INTO TreasuryTransactions (
                        TreasuryID, TransactionDate, TransactionType, Amount,
                        BalanceAfter, Description, ReferenceType, ReferenceID,
                        ReferenceNumber, CreatedBy, CreatedDate
                    ) VALUES (
                        @treasuryId, @date, @type, @amount,
                        @balanceAfter, @description, @refType,
                        @refId, @refNumber, @createdBy, CURRENT_TIMESTAMP
                    )";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                    cmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@type", transactionType);
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@balanceAfter", balanceAfter);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@refType", referenceType);
                    cmd.Parameters.AddWithValue("@refId", referenceId);
                    cmd.Parameters.AddWithValue("@refNumber", referenceNumber);
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddTreasuryTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<decimal> GetCustomerBalanceAsync(int customerId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @customerId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    object result = await cmd.ExecuteScalarAsync();
                    return Convert.ToDecimal(result);
                }
            }
        }

        private async Task<decimal> GetSupplierBalanceAsync(int supplierId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Suppliers WHERE SupplierID = @supplierId";
                using (var cmd = new SQLiteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("@supplierId", supplierId);
                    object result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToDecimal(result) : 0;
                }
            }
        }

        private async Task<bool> UpdateCustomerBalanceAsync(int customerId, decimal newBalance, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "UPDATE Customers SET CurrentBalance = @newBalance, ModifiedDate = CURRENT_TIMESTAMP WHERE CustomerID = @customerId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@newBalance", newBalance);
                cmd.Parameters.AddWithValue("@customerId", customerId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
        }

        private async Task<bool> UpdateSupplierBalanceAsync(int supplierId, decimal newBalance, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "UPDATE Suppliers SET CurrentBalance = @newBalance, ModifiedDate = CURRENT_TIMESTAMP WHERE SupplierID = @supplierId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@newBalance", newBalance);
                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
        }

        private void ShowDialog(Window dialog)
        {
            Window mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                BlurEffect blurEffect = new BlurEffect { Radius = 8, KernelType = KernelType.Gaussian };
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
                dialog.ShowDialog();
            }
        }

        #endregion

        #region تعديل وحذف الحركات النقدية (Edit & Delete Transactions)

        /// <summary>
        /// حدث حذف الحركة النقدية
        /// </summary>
        private async void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button == null || button.Tag == null) return;

                // استخراج بيانات الحركة من الـ Tag
                dynamic item = button.Tag;
                string transactionDate = item.TransactionDate;
                string description = item.Description;
                decimal amount = item.Amount;
                string transactionType = item.TransactionType;
                string transactionTypeArabic = transactionType == "Receipt" ? "إيداع" : "صرف";

                MessageBoxResult result = MessageBox.Show(
                    $"هل أنت متأكد من حذف هذه الحركة؟\n\n" +
                    $"📅 التاريخ: {transactionDate}\n" +
                    $"📝 البيان: {description}\n" +
                    $"💰 المبلغ: {CurrencyHelper.FormatAmount(amount)}\n" +
                    $"📊 النوع: {transactionTypeArabic}\n\n" +
                    "⚠️ تحذير: هذا الإجراء لا يمكن التراجع عنه!",
                    "تأكيد حذف الحركة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                Mouse.OverrideCursor = Cursors.Wait;

                // حذف الحركة من قاعدة البيانات
                bool deleted = await DeleteTreasuryTransactionAsync(button.Tag);

                Mouse.OverrideCursor = null;

                if (deleted)
                {
                    MessageBox.Show("تم حذف الحركة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadTreasuryDataAsync();
                    await LoadTransactionsAsync();
                }
                else
                {
                    MessageBox.Show("حدث خطأ أثناء حذف الحركة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"خطأ في حذف الحركة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"DeleteTransaction_Click Error: {ex.Message}");
            }
        }

        /// <summary>
        /// حدث تعديل الحركة النقدية
        /// </summary>
        private async void EditTransaction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Button button = sender as Button;
                if (button == null || button.Tag == null) return;

                // استخراج بيانات الحركة من الـ Tag
                dynamic item = button.Tag;
                string transactionDate = item.TransactionDate;
                string description = item.Description;
                decimal amount = decimal.Parse(item.Amount.ToString());
                string transactionType = item.TransactionType;
                decimal balanceAfter = decimal.Parse(item.BalanceAfter.ToString());

                // فتح نافذة تعديل الحركة
                await OpenEditTransactionDialogAsync(transactionDate, description, amount, transactionType, balanceAfter, button.Tag);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تعديل الحركة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"EditTransaction_Click Error: {ex.Message}");
            }
        }

        /// <summary>
        /// فتح نافذة تعديل الحركة النقدية
        /// </summary>
        private async Task OpenEditTransactionDialogAsync(string transactionDate, string description, decimal amount, string transactionType, decimal balanceAfter, object tag)
        {
            try
            {
                Window dialog = new Window
                {
                    Title = "تعديل الحركة النقدية",
                    Width = 450,
                    Height = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Background = (Brush)FindResource("SurfaceColor"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FlowDirection = FlowDirection.RightToLeft
                };

                Grid mainGrid = new Grid { Margin = new Thickness(25) };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // العنوان
                StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                titlePanel.Children.Add(new TextBlock { Text = "✏️", FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
                titlePanel.Children.Add(new TextBlock { Text = "تعديل الحركة النقدية", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("PrimaryColor") });
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                Border line = new Border { BorderBrush = (Brush)FindResource("BorderLight"), BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 0, 0, 15) };
                Grid.SetRow(line, 1);
                mainGrid.Children.Add(line);

                // نوع الحركة (للقراءة فقط)
                TextBlock typeLabel = new TextBlock { Text = "نوع الحركة", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
                Grid.SetRow(typeLabel, 2);
                mainGrid.Children.Add(typeLabel);

                Border typeBorder = new Border
                {
                    Background = transactionType == "Receipt" ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                TextBlock typeText = new TextBlock
                {
                    Text = transactionType == "Receipt" ? "💰 إيداع" : "💸 صرف",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = transactionType == "Receipt" ? (Brush)FindResource("SuccessColor") : (Brush)FindResource("DangerColor"),
                    TextAlignment = TextAlignment.Center
                };
                typeBorder.Child = typeText;
                Grid.SetRow(typeBorder, 3);
                mainGrid.Children.Add(typeBorder);

                // التاريخ
                TextBlock dateLabel = new TextBlock { Text = "التاريخ", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
                Grid.SetRow(dateLabel, 4);
                mainGrid.Children.Add(dateLabel);

                DatePicker dtpDate = new DatePicker
                {
                    Height = 40,
                    SelectedDate = DateTime.Parse(transactionDate),
                    Style = (Style)FindResource("ModernDatePicker"),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(dtpDate, 5);
                mainGrid.Children.Add(dtpDate);

                // المبلغ
                TextBlock amountLabel = new TextBlock { Text = "المبلغ", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
                Grid.SetRow(amountLabel, 6);
                mainGrid.Children.Add(amountLabel);

                TextBox txtAmount = new TextBox
                {
                    Height = 40,
                    Text = amount.ToString("N2"),
                    Style = (Style)FindResource("ModernTextBox"),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(txtAmount, 7);
                mainGrid.Children.Add(txtAmount);

                // البيان
                TextBlock descLabel = new TextBlock { Text = "البيان", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryColor"), Margin = new Thickness(0, 0, 0, 6) };
                Grid.SetRow(descLabel, 8);
                mainGrid.Children.Add(descLabel);

                TextBox txtDescription = new TextBox
                {
                    Height = 40,
                    Text = description,
                    Style = (Style)FindResource("ModernTextBox"),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                Grid.SetRow(txtDescription, 9);
                mainGrid.Children.Add(txtDescription);

                // الأزرار
                StackPanel buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
                Grid.SetRow(buttonPanel, 10);

                Button saveBtn = new Button
                {
                    Content = "💾 حفظ التعديلات",
                    Width = 140,
                    Height = 40,
                    Style = (Style)FindResource("PrimaryButton"),
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Button cancelBtn = new Button
                {
                    Content = "✖ إلغاء",
                    Width = 110,
                    Height = 40,
                    Style = (Style)FindResource("SecondaryButton")
                };
                buttonPanel.Children.Add(saveBtn);
                buttonPanel.Children.Add(cancelBtn);

                mainGrid.Children.Add(buttonPanel);
                dialog.Content = mainGrid;

                saveBtn.Click += async (s, ev) =>
                {
                    if (!decimal.TryParse(txtAmount.Text, out decimal newAmount) || newAmount <= 0)
                    {
                        MessageBox.Show("المبلغ غير صحيح. يرجى إدخال مبلغ أكبر من صفر", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    DateTime newDate = dtpDate.SelectedDate ?? DateTime.Now;
                    string newDescription = txtDescription.Text.Trim();

                    // تحديث الحركة في قاعدة البيانات
                    bool success = await UpdateTreasuryTransactionAsync(tag, newDate, newAmount, newDescription);

                    if (success)
                    {
                        MessageBox.Show("تم تعديل الحركة بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        await LoadTreasuryDataAsync();
                        await LoadTransactionsAsync();
                    }
                    else
                    {
                        MessageBox.Show("حدث خطأ أثناء تعديل الحركة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                cancelBtn.Click += (s, ev) => dialog.Close();

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenEditTransactionDialogAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في فتح نافذة التعديل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// تحديث حركة نقدية في قاعدة البيانات
        /// </summary>
        private async Task<bool> UpdateTreasuryTransactionAsync(object tag, DateTime newDate, decimal newAmount, string newDescription)
        {
            try
            {
                // استخراج بيانات الحركة من الـ Tag
                dynamic item = tag;
                string oldTransactionDate = item.TransactionDate;
                string oldDescription = item.Description;
                decimal oldAmount = decimal.Parse(item.Amount.ToString());
                string transactionType = item.TransactionType;

                // البحث عن TransactionID الحقيقي من قاعدة البيانات
                int transactionId = 0;
                int treasuryId = 0;

                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // البحث عن الحركة بناءً على التاريخ والمبلغ والبيان
                    string findSql = @"
                        SELECT TransactionID, TreasuryID 
                        FROM TreasuryTransactions 
                        WHERE TransactionDate LIKE @date 
                        AND Amount = @amount 
                        AND Description = @description 
                        AND TransactionType = @type
                        ORDER BY TransactionID DESC 
                        LIMIT 1";

                    using (SQLiteCommand cmd = new SQLiteCommand(findSql, connection))
                    {
                        // استخدام LIKE مع التاريخ للتعامل مع التنسيقات المختلفة
                        cmd.Parameters.AddWithValue("@date", oldTransactionDate.Split(' ')[0] + "%");
                        cmd.Parameters.AddWithValue("@amount", oldAmount);
                        cmd.Parameters.AddWithValue("@description", oldDescription);
                        cmd.Parameters.AddWithValue("@type", transactionType);

                        using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                transactionId = reader.GetInt32(0);
                                treasuryId = reader.GetInt32(1);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("⚠️ لم يتم العثور على الحركة للتعديل");
                                return false;
                            }
                        }
                    }

                    if (transactionId == 0) return false;

                    // حساب الفرق في المبلغ
                    decimal amountDifference = newAmount - oldAmount;

                    // تحديث الحركة
                    string updateSql = @"
                        UPDATE TreasuryTransactions 
                        SET TransactionDate = @date,
                            Amount = @amount,
                            Description = @description,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE TransactionID = @id";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@date", newDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@amount", newAmount);
                        cmd.Parameters.AddWithValue("@description", newDescription);
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // تحديث BalanceAfter للحركات التالية في نفس الخزينة
                    string updateBalanceSql = @"
                        UPDATE TreasuryTransactions 
                        SET BalanceAfter = BalanceAfter + @difference
                        WHERE TreasuryID = @treasuryId 
                        AND TransactionID > @id
                        ORDER BY TransactionDate ASC";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@difference", amountDifference);
                        cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // تحديث رصيد الخزينة
                    decimal currentTreasuryBalance = await GetTreasuryBalanceAsync(treasuryId);
                    decimal newTreasuryBalance = currentTreasuryBalance + amountDifference;

                    string updateTreasurySql = @"
                        UPDATE Treasury 
                        SET CurrentBalance = @newBalance,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE TreasuryID = @id";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateTreasurySql, connection))
                    {
                        cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                        cmd.Parameters.AddWithValue("@id", treasuryId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم تعديل الحركة {transactionId}: المبلغ {oldAmount} → {newAmount}, الرصيد الجديد للخزينة: {newTreasuryBalance}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTreasuryTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// حذف حركة نقدية من قاعدة البيانات
        /// </summary>
        private async Task<bool> DeleteTreasuryTransactionAsync(object tag)
        {
            try
            {
                // استخراج بيانات الحركة من الـ Tag
                dynamic item = tag;
                string transactionDate = item.TransactionDate;
                string description = item.Description;
                decimal amount = decimal.Parse(item.Amount.ToString());
                string transactionType = item.TransactionType;

                int transactionId = 0;
                int treasuryId = 0;

                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // البحث عن الحركة
                        string findSql = @"
                            SELECT TransactionID, TreasuryID 
                            FROM TreasuryTransactions 
                            WHERE TransactionDate LIKE @date 
                            AND Amount = @amount 
                            AND Description = @description 
                            AND TransactionType = @type
                            ORDER BY TransactionID DESC 
                            LIMIT 1";

                        using (SQLiteCommand cmd = new SQLiteCommand(findSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@date", transactionDate.Split(' ')[0] + "%");
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@description", description);
                            cmd.Parameters.AddWithValue("@type", transactionType);

                            using (SQLiteDataReader reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    transactionId = reader.GetInt32(0);
                                    treasuryId = reader.GetInt32(1);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("⚠️ لم يتم العثور على الحركة للحذف");
                                    return false;
                                }
                            }
                        }

                        if (transactionId == 0) return false;

                        // حساب التصحيح للرصيد
                        decimal balanceCorrection = transactionType == "Receipt" ? -amount : amount;

                        // تحديث BalanceAfter للحركات التالية
                        string updateBalanceSql = @"
                            UPDATE TreasuryTransactions 
                            SET BalanceAfter = BalanceAfter + @correction
                            WHERE TreasuryID = @treasuryId 
                            AND TransactionID > @id";

                        using (SQLiteCommand cmd = new SQLiteCommand(updateBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@correction", balanceCorrection);
                            cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                            cmd.Parameters.AddWithValue("@id", transactionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // حذف الحركة
                        string deleteSql = "DELETE FROM TreasuryTransactions WHERE TransactionID = @id";
                        using (SQLiteCommand cmd = new SQLiteCommand(deleteSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", transactionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // تحديث رصيد الخزينة
                        decimal currentTreasuryBalance = await GetTreasuryBalanceAsync(treasuryId);
                        decimal newTreasuryBalance = currentTreasuryBalance + balanceCorrection;

                        string updateTreasurySql = @"
                            UPDATE Treasury 
                            SET CurrentBalance = @newBalance,
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE TreasuryID = @id";

                        using (SQLiteCommand cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                            cmd.Parameters.AddWithValue("@id", treasuryId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"✅ تم حذف الحركة {transactionId}: {transactionType} - {amount}, الرصيد الجديد للخزينة: {newTreasuryBalance}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTreasuryTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region الكلاسات المساعدة (Helper Classes)

    public class TreasuryCardItem
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Balance { get; set; }
        public Brush BalanceColor { get; set; }
        public string FormattedBalance => CurrencyHelper.FormatAmount(this.Balance);
    }

    public class TreasuryTransactionItem
    {
        public int SerialNumber { get; set; }
        public string TransactionDate { get; set; }
        public string TransactionType { get; set; }
        public string TransactionTreasuryName { get; set; }
        public string TransactionDescription { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal TransactionBalanceAfter { get; set; }

        // خصائص مخصصة لعرض الوارد والصادر
        public decimal IncomeAmount => TransactionType == "Receipt" ? TransactionAmount : 0;
        public decimal ExpenseAmount => TransactionType == "Payment" ? TransactionAmount : 0;

        public string FormattedIncomeAmount => IncomeAmount > 0 ? CurrencyHelper.FormatAmount(IncomeAmount) : "";
        public string FormattedExpenseAmount => ExpenseAmount > 0 ? CurrencyHelper.FormatAmount(ExpenseAmount) : "";
        public string FormattedBalanceAfter => CurrencyHelper.FormatAmount(TransactionBalanceAfter);

        // خصائص الألوان (تُحسب بناءً على نوع الحركة وليس تعيينها)
        public Brush AmountColor => TransactionType == "Receipt" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        public Brush TransactionColor => TransactionType == "Receipt" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
    }

    public class TreasuryTransactionDetailItem
    {
        public int SerialNumber { get; set; }
        public string TransactionDate { get; set; }
        public string TransactionType { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }

        public decimal IncomeAmount => TransactionType == "Receipt" ? Amount : 0;
        public decimal ExpenseAmount => TransactionType == "Payment" ? Amount : 0;

        // تعديل هذه الخصائص - إزالة رمز العملة
        public string FormattedIncomeAmount => IncomeAmount > 0 ? IncomeAmount.ToString("N2") : "";
        public string FormattedExpenseAmount => ExpenseAmount > 0 ? ExpenseAmount.ToString("N2") : "";
        public string FormattedBalance => BalanceAfter.ToString("N2");

        public string TransactionTypeArabic => TransactionType == "Receipt" ? "إيداع" : "صرف";
    }

    #endregion
}