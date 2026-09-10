using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// نموذج بيانات الحساب البنكي
    /// </summary>
    public class BankAccountItem
    {
        public int BankAccountID { get; set; }
        public string BankCode { get; set; }
        public string BankNameAr { get; set; }
        public string BankNameEn { get; set; }
        public string AccountNumber { get; set; }
        public string IBAN { get; set; }
        public string BranchName { get; set; }
        public string BranchCode { get; set; }
        public string SwiftCode { get; set; }
        public decimal CurrentBalance { get; set; }
        public string Currency { get; set; }
        public bool IsActive { get; set; }
        public string ContactPerson { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// نموذج بيانات حركة بنكية (إيداع / سحب)
    /// </summary>
    public class BankTransactionItem
    {
        public int TransactionID { get; set; }
        public int BankAccountID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } // Deposit, Withdrawal, Transfer, Check
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string ReferenceType { get; set; }
        public int? ReferenceID { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// خدمة إدارة الحسابات البنكية وحركاتها
    /// نفس نمط إعادة المحاولة المستخدم في باقي خدمات النظام (InstallmentService, InvoiceService)
    /// </summary>
    public class BankService
    {
        #region المتغيرات والمنشئ (Fields & Constructor)

        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public BankService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _connectionString = databaseService.GetConnectionString();
        }

        #endregion

        #region دوال مساعدة (Helper Methods)

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 5, int delayMs = 500)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    return await action();
                }
                catch (SQLiteException ex) when (ex.Message.Contains("database is locked") || ex.Message.Contains("busy"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    System.Diagnostics.Debug.WriteLine($"⚠️ قاعدة البيانات مشغولة (بنك)، محاولة {retryCount}/{maxRetries}...");
                    await Task.Delay(delayMs * retryCount);
                }
            }
        }

        private static BankAccountItem ReadBankAccount(SQLiteDataReader reader)
        {
            return new BankAccountItem
            {
                BankAccountID = Convert.ToInt32(reader["BankAccountID"]),
                BankCode = reader["BankCode"]?.ToString(),
                BankNameAr = reader["BankNameAr"]?.ToString(),
                BankNameEn = reader["BankNameEn"] == DBNull.Value ? null : reader["BankNameEn"].ToString(),
                AccountNumber = reader["AccountNumber"]?.ToString(),
                IBAN = reader["IBAN"] == DBNull.Value ? null : reader["IBAN"].ToString(),
                BranchName = reader["BranchName"] == DBNull.Value ? null : reader["BranchName"].ToString(),
                BranchCode = reader["BranchCode"] == DBNull.Value ? null : reader["BranchCode"].ToString(),
                SwiftCode = reader["SwiftCode"] == DBNull.Value ? null : reader["SwiftCode"].ToString(),
                CurrentBalance = reader["CurrentBalance"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["CurrentBalance"]),
                Currency = reader["Currency"] == DBNull.Value ? "SAR" : reader["Currency"].ToString(),
                IsActive = reader["IsActive"] == DBNull.Value ? true : Convert.ToInt32(reader["IsActive"]) == 1,
                ContactPerson = reader["ContactPerson"] == DBNull.Value ? null : reader["ContactPerson"].ToString(),
                Phone = reader["Phone"] == DBNull.Value ? null : reader["Phone"].ToString(),
                Email = reader["Email"] == DBNull.Value ? null : reader["Email"].ToString(),
                Notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedDate"])
            };
        }

        #endregion

        #region الحسابات البنكية (Bank Accounts CRUD)

        /// <summary>
        /// جلب كل الحسابات البنكية (النشطة فقط افتراضياً)
        /// </summary>
        public async Task<List<BankAccountItem>> GetBankAccountsAsync(bool activeOnly = true)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var list = new List<BankAccountItem>();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT * FROM BankAccounts";
                    if (activeOnly) sql += " WHERE IsActive = 1";
                    sql += " ORDER BY BankNameAr";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(ReadBankAccount(reader));
                        }
                    }
                }

                return list;
            });
        }

        /// <summary>
        /// جلب حساب بنكي واحد بالمعرف
        /// </summary>
        public async Task<BankAccountItem> GetBankAccountByIdAsync(int bankAccountId)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT * FROM BankAccounts WHERE BankAccountID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", bankAccountId);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                                return ReadBankAccount(reader);
                        }
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// التحقق من وجود كود بنك مكرر (باستثناء حساب معين عند التعديل)
        /// </summary>
        public async Task<bool> BankCodeExistsAsync(string bankCode, int excludeId = 0)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT COUNT(*) FROM BankAccounts WHERE BankCode = @code AND BankAccountID <> @excludeId";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", bankCode);
                        cmd.Parameters.AddWithValue("@excludeId", excludeId);
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return count > 0;
                    }
                }
            });
        }

        /// <summary>
        /// توليد كود بنكي تلقائي فريد (BANK-0001, BANK-0002 ...)
        /// </summary>
        public async Task<string> GenerateUniqueBankCodeAsync()
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT COUNT(*) FROM BankAccounts";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        int next = count + 1;
                        string code;
                        do
                        {
                            code = $"BANK-{next:D4}";
                            var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM BankAccounts WHERE BankCode = @c", connection);
                            checkCmd.Parameters.AddWithValue("@c", code);
                            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                            checkCmd.Dispose();
                            if (!exists) break;
                            next++;
                        } while (true);

                        return code;
                    }
                }
            });
        }

        /// <summary>
        /// إضافة حساب بنكي جديد. الرصيد الافتتاحي (إن وجد) يُسجَّل كأول حركة إيداع.
        /// </summary>
        public async Task<int> AddBankAccountAsync(BankAccountItem account, decimal openingBalance, int? createdBy)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string sql = @"
                                INSERT INTO BankAccounts
                                    (BankCode, BankNameAr, BankNameEn, AccountNumber, IBAN, BranchName, BranchCode,
                                     SwiftCode, CurrentBalance, Currency, IsActive, ContactPerson, Phone, Email,
                                     Notes, CreatedDate, CreatedBy)
                                VALUES
                                    (@code, @nameAr, @nameEn, @accNumber, @iban, @branch, @branchCode,
                                     @swift, @balance, @currency, 1, @contact, @phone, @email,
                                     @notes, @createdDate, @createdBy);
                                SELECT last_insert_rowid();";

                            int newId;
                            using (var cmd = new SQLiteCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@code", account.BankCode);
                                cmd.Parameters.AddWithValue("@nameAr", account.BankNameAr);
                                cmd.Parameters.AddWithValue("@nameEn", (object)account.BankNameEn ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@accNumber", account.AccountNumber);
                                cmd.Parameters.AddWithValue("@iban", (object)account.IBAN ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@branch", (object)account.BranchName ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@branchCode", (object)account.BranchCode ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@swift", (object)account.SwiftCode ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@balance", openingBalance);
                                cmd.Parameters.AddWithValue("@currency", string.IsNullOrEmpty(account.Currency) ? "SAR" : account.Currency);
                                cmd.Parameters.AddWithValue("@contact", (object)account.ContactPerson ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@phone", (object)account.Phone ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@email", (object)account.Email ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@notes", (object)account.Notes ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@createdBy", (object)createdBy ?? DBNull.Value);

                                newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            }

                            if (openingBalance != 0)
                            {
                                string txSql = @"
                                    INSERT INTO BankTransactions
                                        (BankAccountID, TransactionDate, TransactionType, Amount, BalanceAfter,
                                         ReferenceType, Description, CreatedBy, CreatedDate)
                                    VALUES
                                        (@bankId, @date, 'Deposit', @amount, @balanceAfter,
                                         'OpeningBalance', @desc, @createdBy, @createdDate)";

                                using (var txCmd = new SQLiteCommand(txSql, connection, transaction))
                                {
                                    txCmd.Parameters.AddWithValue("@bankId", newId);
                                    txCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd"));
                                    txCmd.Parameters.AddWithValue("@amount", openingBalance);
                                    txCmd.Parameters.AddWithValue("@balanceAfter", openingBalance);
                                    txCmd.Parameters.AddWithValue("@desc", "رصيد افتتاحي");
                                    txCmd.Parameters.AddWithValue("@createdBy", (object)createdBy ?? DBNull.Value);
                                    txCmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                    await txCmd.ExecuteNonQueryAsync();
                                }
                            }

                            transaction.Commit();
                            return newId;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// تعديل بيانات حساب بنكي (لا يعدّل الرصيد - الرصيد يتغير فقط عبر الحركات)
        /// </summary>
        public async Task<bool> UpdateBankAccountAsync(BankAccountItem account)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = @"
                        UPDATE BankAccounts SET
                            BankCode = @code,
                            BankNameAr = @nameAr,
                            BankNameEn = @nameEn,
                            AccountNumber = @accNumber,
                            IBAN = @iban,
                            BranchName = @branch,
                            BranchCode = @branchCode,
                            SwiftCode = @swift,
                            Currency = @currency,
                            ContactPerson = @contact,
                            Phone = @phone,
                            Email = @email,
                            Notes = @notes,
                            ModifiedDate = @modifiedDate
                        WHERE BankAccountID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@code", account.BankCode);
                        cmd.Parameters.AddWithValue("@nameAr", account.BankNameAr);
                        cmd.Parameters.AddWithValue("@nameEn", (object)account.BankNameEn ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@accNumber", account.AccountNumber);
                        cmd.Parameters.AddWithValue("@iban", (object)account.IBAN ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@branch", (object)account.BranchName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@branchCode", (object)account.BranchCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@swift", (object)account.SwiftCode ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@currency", string.IsNullOrEmpty(account.Currency) ? "SAR" : account.Currency);
                        cmd.Parameters.AddWithValue("@contact", (object)account.ContactPerson ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@phone", (object)account.Phone ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@email", (object)account.Email ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@notes", (object)account.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@id", account.BankAccountID);

                        int rows = await cmd.ExecuteNonQueryAsync();
                        return rows > 0;
                    }
                }
            });
        }

        /// <summary>
        /// حذف حساب بنكي. هذا حذف ناعم (تعطيل) دائماً - لا يُحذف أي سجل من قاعدة البيانات،
        /// فقط يُخفى الحساب عن القوائم النشطة، وتبقى كل حركاته السابقة محفوظة وتظهر بشكل صحيح
        /// في سجل الحركات وأي تقارير سابقة.
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteBankAccountAsync(int bankAccountId)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var delCmd = new SQLiteCommand("UPDATE BankAccounts SET IsActive = 0, ModifiedDate = @date WHERE BankAccountID = @id", connection);
                    delCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    delCmd.Parameters.AddWithValue("@id", bankAccountId);
                    int rows = await delCmd.ExecuteNonQueryAsync();
                    delCmd.Dispose();

                    return (rows > 0, rows > 0 ? "تم حذف الحساب بنجاح" : "لم يتم العثور على الحساب");
                }
            });
        }

        #endregion

        #region الحركات البنكية (Bank Transactions)

        /// <summary>
        /// جلب حركات حساب بنكي معين، أو كل الحركات إذا مرّرت 0
        /// </summary>
        public async Task<List<BankTransactionItem>> GetBankTransactionsAsync(int bankAccountId = 0)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var list = new List<BankTransactionItem>();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string sql = "SELECT * FROM BankTransactions";
                    if (bankAccountId > 0) sql += " WHERE BankAccountID = @bankId";
                    sql += " ORDER BY TransactionDate DESC, TransactionID DESC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (bankAccountId > 0)
                            cmd.Parameters.AddWithValue("@bankId", bankAccountId);

                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new BankTransactionItem
                                {
                                    TransactionID = Convert.ToInt32(reader["TransactionID"]),
                                    BankAccountID = Convert.ToInt32(reader["BankAccountID"]),
                                    TransactionDate = Convert.ToDateTime(reader["TransactionDate"]),
                                    TransactionType = reader["TransactionType"]?.ToString(),
                                    Amount = Convert.ToDecimal(reader["Amount"]),
                                    BalanceAfter = Convert.ToDecimal(reader["BalanceAfter"]),
                                    ReferenceType = reader["ReferenceType"] == DBNull.Value ? null : reader["ReferenceType"].ToString(),
                                    ReferenceID = reader["ReferenceID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ReferenceID"]),
                                    ReferenceNumber = reader["ReferenceNumber"] == DBNull.Value ? null : reader["ReferenceNumber"].ToString(),
                                    Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                                    CreatedBy = reader["CreatedBy"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CreatedBy"]),
                                    CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedDate"])
                                });
                            }
                        }
                    }
                }

                return list;
            });
        }

        /// <summary>
        /// تسجيل حركة إيداع أو سحب على حساب بنكي، وتحديث رصيده في نفس المعاملة (Transaction)
        /// </summary>
        public async Task<(bool Success, string Message)> AddTransactionAsync(
            int bankAccountId,
            string transactionType, // "Deposit" أو "Withdrawal"
            decimal amount,
            DateTime transactionDate,
            string description,
            string referenceNumber,
            int? createdBy)
        {
            if (amount <= 0)
                return (false, "المبلغ يجب أن يكون أكبر من صفر");

            if (transactionType != "Deposit" && transactionType != "Withdrawal")
                return (false, "نوع الحركة غير صحيح");

            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var balCmd = new SQLiteCommand("SELECT CurrentBalance FROM BankAccounts WHERE BankAccountID = @id", connection, transaction);
                            balCmd.Parameters.AddWithValue("@id", bankAccountId);
                            var balanceObj = await balCmd.ExecuteScalarAsync();
                            balCmd.Dispose();

                            if (balanceObj == null)
                            {
                                transaction.Rollback();
                                return (false, "الحساب البنكي غير موجود");
                            }

                            decimal currentBalance = Convert.ToDecimal(balanceObj);
                            decimal newBalance = transactionType == "Deposit"
                                ? currentBalance + amount
                                : currentBalance - amount;

                            if (transactionType == "Withdrawal" && newBalance < 0)
                            {
                                transaction.Rollback();
                                return (false, "الرصيد الحالي غير كافٍ لإتمام عملية السحب");
                            }

                            string insertSql = @"
                                INSERT INTO BankTransactions
                                    (BankAccountID, TransactionDate, TransactionType, Amount, BalanceAfter,
                                     ReferenceNumber, Description, CreatedBy, CreatedDate)
                                VALUES
                                    (@bankId, @date, @type, @amount, @balanceAfter,
                                     @refNumber, @desc, @createdBy, @createdDate)";

                            using (var insertCmd = new SQLiteCommand(insertSql, connection, transaction))
                            {
                                insertCmd.Parameters.AddWithValue("@bankId", bankAccountId);
                                insertCmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd"));
                                insertCmd.Parameters.AddWithValue("@type", transactionType);
                                insertCmd.Parameters.AddWithValue("@amount", amount);
                                insertCmd.Parameters.AddWithValue("@balanceAfter", newBalance);
                                insertCmd.Parameters.AddWithValue("@refNumber", (object)referenceNumber ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@desc", (object)description ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@createdBy", (object)createdBy ?? DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                await insertCmd.ExecuteNonQueryAsync();
                            }

                            var updateCmd = new SQLiteCommand("UPDATE BankAccounts SET CurrentBalance = @balance, ModifiedDate = @modDate WHERE BankAccountID = @id", connection, transaction);
                            updateCmd.Parameters.AddWithValue("@balance", newBalance);
                            updateCmd.Parameters.AddWithValue("@modDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            updateCmd.Parameters.AddWithValue("@id", bankAccountId);
                            await updateCmd.ExecuteNonQueryAsync();
                            updateCmd.Dispose();

                            transaction.Commit();
                            return (true, transactionType == "Deposit" ? "تم تسجيل الإيداع بنجاح" : "تم تسجيل السحب بنجاح");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// حذف حركة بنكية (يُعاد حساب رصيد كل الحركات اللاحقة تلقائياً)
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteTransactionAsync(int transactionId)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var getCmd = new SQLiteCommand("SELECT BankAccountID FROM BankTransactions WHERE TransactionID = @id", connection, transaction);
                            getCmd.Parameters.AddWithValue("@id", transactionId);
                            var bankIdObj = await getCmd.ExecuteScalarAsync();
                            getCmd.Dispose();

                            if (bankIdObj == null)
                            {
                                transaction.Rollback();
                                return (false, "الحركة غير موجودة");
                            }

                            int bankAccountId = Convert.ToInt32(bankIdObj);

                            var delCmd = new SQLiteCommand("DELETE FROM BankTransactions WHERE TransactionID = @id", connection, transaction);
                            delCmd.Parameters.AddWithValue("@id", transactionId);
                            await delCmd.ExecuteNonQueryAsync();
                            delCmd.Dispose();

                            // إعادة احتساب الرصيد بالترتيب الزمني لكل حركات هذا الحساب
                            decimal runningBalance = 0;
                            var listCmd = new SQLiteCommand(
                                "SELECT TransactionID, TransactionType, Amount FROM BankTransactions WHERE BankAccountID = @id ORDER BY TransactionDate ASC, TransactionID ASC",
                                connection, transaction);
                            listCmd.Parameters.AddWithValue("@id", bankAccountId);

                            var updates = new List<(int Id, decimal Balance)>();
                            using (var reader = (SQLiteDataReader)await listCmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int id = Convert.ToInt32(reader["TransactionID"]);
                                    string type = reader["TransactionType"].ToString();
                                    decimal amount = Convert.ToDecimal(reader["Amount"]);
                                    runningBalance += (type == "Deposit") ? amount : -amount;
                                    updates.Add((id, runningBalance));
                                }
                            }
                            listCmd.Dispose();

                            foreach (var u in updates)
                            {
                                var upCmd = new SQLiteCommand("UPDATE BankTransactions SET BalanceAfter = @bal WHERE TransactionID = @id", connection, transaction);
                                upCmd.Parameters.AddWithValue("@bal", u.Balance);
                                upCmd.Parameters.AddWithValue("@id", u.Id);
                                await upCmd.ExecuteNonQueryAsync();
                                upCmd.Dispose();
                            }

                            var updateAccCmd = new SQLiteCommand("UPDATE BankAccounts SET CurrentBalance = @bal, ModifiedDate = @modDate WHERE BankAccountID = @id", connection, transaction);
                            updateAccCmd.Parameters.AddWithValue("@bal", runningBalance);
                            updateAccCmd.Parameters.AddWithValue("@modDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            updateAccCmd.Parameters.AddWithValue("@id", bankAccountId);
                            await updateAccCmd.ExecuteNonQueryAsync();
                            updateAccCmd.Dispose();

                            transaction.Commit();
                            return (true, "تم حذف الحركة وإعادة احتساب الرصيد بنجاح");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// إجمالي أرصدة كل الحسابات البنكية النشطة (يُستخدم في لوحة التحكم/التقارير)
        /// </summary>
        public async Task<decimal> GetTotalBanksBalanceAsync()
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var cmd = new SQLiteCommand("SELECT COALESCE(SUM(CurrentBalance), 0) FROM BankAccounts WHERE IsActive = 1", connection);
                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToDecimal(result);
                }
            });
        }

        #endregion
    }
}
