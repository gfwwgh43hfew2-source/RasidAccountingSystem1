using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using RasidAccountingSystem.Helpers;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// نموذج بيانات الشيك
    /// </summary>
    public class CheckItem
    {
        public int CheckID { get; set; }
        public string CheckNumber { get; set; }
        public DateTime CheckDate { get; set; }
        public string CheckType { get; set; } // Receivable (وارد من عميل) أو Payable (صادر لمورد)
        public decimal Amount { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string PayeeName { get; set; }
        public string PayerName { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public int? CustomerID { get; set; }
        public int? SupplierID { get; set; }
        public string PartyName { get; set; }
        public DateTime? DepositDate { get; set; }
        public DateTime? ClearanceDate { get; set; }
        public string BounceReason { get; set; }
        public bool IsVoid { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// حركة تغيير حالة على شيك (يُنشئها تلقائياً Trigger قاعدة البيانات عند كل تحديث لعمود Status)
    /// </summary>
    public class CheckTransactionItem
    {
        public int TransactionID { get; set; }
        public int CheckID { get; set; }
        public DateTime TransactionDate { get; set; }
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// ملخص إجمالي الشيكات لعرضه في بطاقات لوحة التحكم أعلى الشاشة
    /// </summary>
    public class ChecksSummary
    {
        public decimal TotalReceivablePending { get; set; }  // شيكات واردة لم تُحصَّل بعد
        public decimal TotalPayablePending { get; set; }     // شيكات صادرة لم تُصرف بعد
        public int DueSoonCount { get; set; }                  // شيكات مستحقة خلال 7 أيام
        public int BouncedCount { get; set; }                  // شيكات مرتدة
    }

    /// <summary>
    /// خدمة إدارة الشيكات الواردة (من العملاء) والصادرة (للموردين).
    ///
    /// دورة حياة الشيك المعتمدة:
    /// - شيك وارد (Receivable): Received (استلمناه) → Deposited (أودعناه بالبنك) → Cleared (تحصَّل) أو Bounced (ارتد)
    /// - شيك صادر (Payable): Issued (أصدرناه) → Cleared (صُرف من حسابنا) أو Bounced (ارتد لعدم كفاية الرصيد)
    /// - Cancelled متاحة من أي حالة غير نهائية (إلغاء الشيك قبل تحصيله/صرفه)
    ///
    /// كل تغيير في عمود Status يُسجَّل تلقائياً في جدول CheckTransactions عبر Trigger قاعدة البيانات
    /// (trig_check_status_change)، فلا حاجة لإدراج يدوي مزدوج هنا (نفس الدرس المستفاد من باج
    /// مضاعفة رصيد العميل السابق - الاعتماد على مصدر واحد فقط للحقيقة).
    /// </summary>
    public class CheckService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public CheckService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _connectionString = databaseService.GetConnectionString();
        }

        #region دوال مساعدة (Helpers)

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

                    await Task.Delay(delayMs * retryCount);
                }
            }
        }

        private static CheckItem ReadCheck(SQLiteDataReader reader)
        {
            return new CheckItem
            {
                CheckID = Convert.ToInt32(reader["CheckID"]),
                CheckNumber = reader["CheckNumber"]?.ToString(),
                CheckDate = Convert.ToDateTime(reader["CheckDate"]),
                CheckType = reader["CheckType"]?.ToString(),
                Amount = Convert.ToDecimal(reader["Amount"]),
                BankName = reader["BankName"]?.ToString(),
                AccountNumber = reader["AccountNumber"] == DBNull.Value ? null : reader["AccountNumber"].ToString(),
                PayeeName = reader["PayeeName"] == DBNull.Value ? null : reader["PayeeName"].ToString(),
                PayerName = reader["PayerName"] == DBNull.Value ? null : reader["PayerName"].ToString(),
                DueDate = reader["DueDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["DueDate"]),
                Status = reader["Status"]?.ToString(),
                CustomerID = reader["CustomerID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CustomerID"]),
                SupplierID = reader["SupplierID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SupplierID"]),
                PartyName = reader["PartyName"] == DBNull.Value ? "" : reader["PartyName"].ToString(),
                DepositDate = reader["DepositDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["DepositDate"]),
                ClearanceDate = reader["ClearanceDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ClearanceDate"]),
                BounceReason = reader["BounceReason"] == DBNull.Value ? null : reader["BounceReason"].ToString(),
                IsVoid = Convert.ToInt32(reader["IsVoid"]) == 1,
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                Notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"].ToString(),
                CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader["CreatedDate"])
            };
        }

        private const string BaseSelectSql = @"
            SELECT c.*,
                   COALESCE(cu.CustomerNameAr, cu.CustomerName, sup.SupplierNameAr, sup.SupplierName, '') AS PartyName
            FROM Checks c
            LEFT JOIN Customers cu ON c.CustomerID = cu.CustomerID
            LEFT JOIN Suppliers sup ON c.SupplierID = sup.SupplierID";

        #endregion

        #region القراءة (Read)

        /// <summary>
        /// جلب الشيكات مع إمكانية الفلترة حسب النوع (Receivable/Payable) والحالة، والشيكات الملغاة مستبعدة افتراضياً
        /// </summary>
        public async Task<List<CheckItem>> GetChecksAsync(string checkType = null, string status = null, bool includeVoided = false)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var list = new List<CheckItem>();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = BaseSelectSql + " WHERE 1=1";
                    if (!includeVoided) sql += " AND c.IsVoid = 0";
                    if (!string.IsNullOrEmpty(checkType)) sql += " AND c.CheckType = @checkType";
                    if (!string.IsNullOrEmpty(status)) sql += " AND c.Status = @status";
                    sql += " ORDER BY c.DueDate ASC, c.CheckDate ASC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        if (!string.IsNullOrEmpty(checkType)) cmd.Parameters.AddWithValue("@checkType", checkType);
                        if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@status", status);

                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(ReadCheck(reader));
                            }
                        }
                    }
                }

                return list;
            });
        }

        public async Task<CheckItem> GetCheckByIdAsync(int checkId)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = BaseSelectSql + " WHERE c.CheckID = @id";
                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", checkId);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                                return ReadCheck(reader);
                        }
                    }
                }

                return null;
            });
        }

        public async Task<List<CheckTransactionItem>> GetCheckTransactionsAsync(int checkId)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var list = new List<CheckTransactionItem>();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT TransactionID, CheckID, TransactionDate, FromStatus, ToStatus, Description
                        FROM CheckTransactions
                        WHERE CheckID = @checkId
                        ORDER BY TransactionDate ASC, TransactionID ASC";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@checkId", checkId);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new CheckTransactionItem
                                {
                                    TransactionID = Convert.ToInt32(reader["TransactionID"]),
                                    CheckID = Convert.ToInt32(reader["CheckID"]),
                                    TransactionDate = Convert.ToDateTime(reader["TransactionDate"]),
                                    FromStatus = reader["FromStatus"]?.ToString(),
                                    ToStatus = reader["ToStatus"]?.ToString(),
                                    Description = reader["Description"] == DBNull.Value ? "" : reader["Description"].ToString()
                                });
                            }
                        }
                    }
                }

                return list;
            });
        }

        /// <summary>
        /// ملخص إجمالي للشيكات المعلَّقة والمستحقة قريباً والمرتدة، لعرضه في بطاقات أعلى الشاشة
        /// </summary>
        public async Task<ChecksSummary> GetChecksSummaryAsync()
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var summary = new ChecksSummary();

                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        SELECT
                            COALESCE((SELECT SUM(Amount) FROM Checks WHERE CheckType = 'Receivable' AND Status IN ('Received','Deposited') AND IsVoid = 0), 0) AS ReceivablePending,
                            COALESCE((SELECT SUM(Amount) FROM Checks WHERE CheckType = 'Payable' AND Status = 'Issued' AND IsVoid = 0), 0) AS PayablePending,
                            (SELECT COUNT(*) FROM Checks WHERE IsVoid = 0 AND Status IN ('Issued','Received','Deposited') AND DueDate IS NOT NULL AND DueDate <= date('now', '+7 days') AND DueDate >= date('now')) AS DueSoon,
                            (SELECT COUNT(*) FROM Checks WHERE IsVoid = 0 AND Status = 'Bounced') AS Bounced";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            summary.TotalReceivablePending = Convert.ToDecimal(reader["ReceivablePending"]);
                            summary.TotalPayablePending = Convert.ToDecimal(reader["PayablePending"]);
                            summary.DueSoonCount = Convert.ToInt32(reader["DueSoon"]);
                            summary.BouncedCount = Convert.ToInt32(reader["Bounced"]);
                        }
                    }
                }

                return summary;
            });
        }

        #endregion

        #region الإضافة والتعديل (Create / Update)

        public async Task<(bool Success, string Message, int CheckId)> AddCheckAsync(CheckItem check, int createdBy)
        {
            if (check.Amount <= 0)
                return (false, "قيمة الشيك يجب أن تكون أكبر من صفر", 0);

            if (string.IsNullOrWhiteSpace(check.CheckNumber))
                return (false, "يرجى إدخال رقم الشيك", 0);

            string initialStatus = check.CheckType == "Receivable" ? "Received" : "Issued";

            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        INSERT INTO Checks (
                            CheckNumber, CheckDate, CheckType, Amount, BankName, AccountNumber,
                            PayeeName, PayerName, DueDate, Status, CustomerID, SupplierID,
                            Description, Notes, CreatedBy, CreatedDate
                        ) VALUES (
                            @checkNumber, @checkDate, @checkType, @amount, @bankName, @accountNumber,
                            @payeeName, @payerName, @dueDate, @status, @customerId, @supplierId,
                            @description, @notes, @createdBy, @createdDate
                        );
                        SELECT last_insert_rowid();";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@checkNumber", check.CheckNumber.Trim());
                        cmd.Parameters.AddWithValue("@checkDate", check.CheckDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@checkType", check.CheckType);
                        cmd.Parameters.AddWithValue("@amount", check.Amount);
                        cmd.Parameters.AddWithValue("@bankName", check.BankName ?? "");
                        cmd.Parameters.AddWithValue("@accountNumber", (object)check.AccountNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@payeeName", (object)check.PayeeName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@payerName", (object)check.PayerName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@dueDate", check.DueDate.HasValue ? check.DueDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@status", initialStatus);
                        cmd.Parameters.AddWithValue("@customerId", (object)check.CustomerID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@supplierId", (object)check.SupplierID ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@description", (object)check.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@notes", (object)check.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@createdBy", createdBy);
                        cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                        int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                        Logger.LogInfo($"تم إضافة شيك جديد رقم {check.CheckNumber} بقيمة {check.Amount:N2}", "CheckService");
                        return (true, "تم إضافة الشيك بنجاح", newId);
                    }
                }
            });
        }

        public async Task<(bool Success, string Message)> UpdateCheckAsync(CheckItem check)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                        UPDATE Checks SET
                            CheckNumber = @checkNumber,
                            CheckDate = @checkDate,
                            Amount = @amount,
                            BankName = @bankName,
                            AccountNumber = @accountNumber,
                            PayeeName = @payeeName,
                            PayerName = @payerName,
                            DueDate = @dueDate,
                            Description = @description,
                            Notes = @notes,
                            ModifiedDate = @modifiedDate
                        WHERE CheckID = @id";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@checkNumber", check.CheckNumber.Trim());
                        cmd.Parameters.AddWithValue("@checkDate", check.CheckDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@amount", check.Amount);
                        cmd.Parameters.AddWithValue("@bankName", check.BankName ?? "");
                        cmd.Parameters.AddWithValue("@accountNumber", (object)check.AccountNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@payeeName", (object)check.PayeeName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@payerName", (object)check.PayerName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@dueDate", check.DueDate.HasValue ? check.DueDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@description", (object)check.Description ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@notes", (object)check.Notes ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@id", check.CheckID);

                        int rows = await cmd.ExecuteNonQueryAsync();
                        return (rows > 0, rows > 0 ? "تم تعديل بيانات الشيك بنجاح" : "لم يتم العثور على الشيك");
                    }
                }
            });
        }

        #endregion

        #region تغيير الحالة (Status Workflow)

        /// <summary>
        /// الحالات التالية المسموح بها منطقياً من حالة معيّنة، حسب نوع الشيك.
        /// تُستخدم لتقييد خيارات نافذة "تغيير الحالة" بما يمنع تحويلات غير منطقية
        /// (مثلاً: لا يمكن الرجوع من Cleared لأي حالة أخرى لأنها حالة نهائية).
        /// </summary>
        public List<string> GetValidNextStatuses(string currentStatus, string checkType)
        {
            switch (currentStatus)
            {
                case "Received": // شيك وارد استلمناه، لسه ماودعناهوش
                    return new List<string> { "Deposited", "Cancelled" };
                case "Deposited": // شيك وارد أودعناه بالبنك
                    return new List<string> { "Cleared", "Bounced" };
                case "Issued": // شيك صادر أصدرناه للمورد
                    return new List<string> { "Cleared", "Bounced", "Cancelled" };
                case "Bounced": // شيك ارتد - يمكن محاولة إعادة إيداعه أو إلغاؤه
                    return checkType == "Receivable"
                        ? new List<string> { "Deposited", "Cancelled" }
                        : new List<string> { "Cleared", "Cancelled" };
                case "Cleared":
                case "Cancelled":
                default:
                    return new List<string>(); // حالات نهائية - لا تحويل بعدها
            }
        }

        /// <summary>
        /// تحديث حالة الشيك. الانتقال للحالة نفسه يُسجَّل تلقائياً في سجل حركات الشيك عبر
        /// Trigger قاعدة البيانات (trig_check_status_change)، فلا حاجة لإدراج يدوي هنا.
        ///
        /// ✅ إصلاح: شيك مستقل (مش مرتبط بفاتورة/سند) كان لا يؤثر على رصيد العميل/المورد إطلاقاً
        /// طوال دورة حياته. القرار المعتمد: الشيك يؤثر على الرصيد فقط لحظة "تم التحصيل/الصرف"
        /// (Cleared) — قبل كده هو مجرد وعد بالدفع مش فلوس فعلية، فمفيش داعي لعكسه لو ارتد لاحقاً
        /// لأنه أصلاً ملوش أي أثر على الرصيد قبل التحصيل. التسجيل والحساب بيحصلوا في نفس الـ
        /// Transaction اللي بتحدّث بيها حالة الشيك، فإما الاتنين يتسجّلوا مع بعض أو ولا واحد فيهم.
        /// </summary>
        public async Task<(bool Success, string Message)> UpdateCheckStatusAsync(
            int checkId,
            string newStatus,
            DateTime? depositDate,
            DateTime? clearanceDate,
            string bounceReason,
            int modifiedBy)
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
                                UPDATE Checks SET
                                    Status = @status,
                                    DepositDate = COALESCE(@depositDate, DepositDate),
                                    ClearanceDate = COALESCE(@clearanceDate, ClearanceDate),
                                    BounceReason = COALESCE(@bounceReason, BounceReason),
                                    ModifiedBy = @modifiedBy,
                                    ModifiedDate = @modifiedDate
                                WHERE CheckID = @id";

                            int rows;
                            using (var cmd = new SQLiteCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@status", newStatus);
                                cmd.Parameters.AddWithValue("@depositDate", depositDate.HasValue ? depositDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@clearanceDate", clearanceDate.HasValue ? clearanceDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@bounceReason", (object)bounceReason ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@modifiedBy", modifiedBy);
                                cmd.Parameters.AddWithValue("@modifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@id", checkId);

                                rows = await cmd.ExecuteNonQueryAsync();
                            }

                            if (rows == 0)
                            {
                                transaction.Rollback();
                                return (false, "لم يتم العثور على الشيك");
                            }

                            if (newStatus == "Cleared")
                            {
                                RecordCheckSettlementIfNeeded(connection, transaction, checkId, clearanceDate ?? DateTime.Now);
                            }

                            transaction.Commit();
                            Logger.LogInfo($"تم تغيير حالة الشيك #{checkId} إلى {newStatus}", "CheckService");
                            return (true, "تم تحديث حالة الشيك بنجاح");
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
        /// عند تحصيل/صرف شيك مستقل فعلياً (Cleared)، يُضاف قيد "Receipt"/"Payment" في كشف حساب
        /// العميل/المورد المرتبط بنفس الشكل تماماً الذي يستخدمه سند القبض/الصرف بطريقة شيك، عشان
        /// كل صيغ حساب الرصيد في البرنامج (وهي أكثر من صيغة) تتعامل معه صح من غير أي تعديل إضافي.
        /// آمنة للتكرار: لو القيد مُسجَّل بالفعل (ReferenceType='CHECK' AND ReferenceID=checkId)
        /// مبتضيفش تاني. شيك من غير عميل/مورد مرتبط (نثري) بيتخطى بأمان لأنه ملوش رصيد يتأثر أصلاً.
        /// </summary>
        private void RecordCheckSettlementIfNeeded(SQLiteConnection connection, SQLiteTransaction transaction, int checkId, DateTime settlementDate)
        {
            string checkType = null;
            decimal amount = 0;
            int? customerId = null;
            int? supplierId = null;
            string checkNumber = null;

            using (var cmd = new SQLiteCommand(
                "SELECT CheckType, Amount, CustomerID, SupplierID, CheckNumber FROM Checks WHERE CheckID = @id", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", checkId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        checkType = reader["CheckType"]?.ToString();
                        amount = Convert.ToDecimal(reader["Amount"]);
                        customerId = reader["CustomerID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CustomerID"]);
                        supplierId = reader["SupplierID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SupplierID"]);
                        checkNumber = reader["CheckNumber"]?.ToString();
                    }
                }
            }

            if (checkType == "Receivable" && customerId.HasValue)
            {
                using (var existsCmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM CustomerTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
                {
                    existsCmd.Parameters.AddWithValue("@checkId", checkId);
                    long already = Convert.ToInt64(existsCmd.ExecuteScalar());
                    if (already > 0) return;
                }

                using (var insertCmd = new SQLiteCommand(@"
                    INSERT INTO CustomerTransactions (
                        CustomerID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @customerId, @date, 'Receipt',
                        0, @amount, 0,
                        'CHECK', @checkId, @checkNumber, @description, NULL, CURRENT_TIMESTAMP
                    )", connection, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@customerId", customerId.Value);
                    insertCmd.Parameters.AddWithValue("@date", settlementDate.ToString("yyyy-MM-dd"));
                    insertCmd.Parameters.AddWithValue("@amount", amount);
                    insertCmd.Parameters.AddWithValue("@checkId", checkId);
                    insertCmd.Parameters.AddWithValue("@checkNumber", (object)checkNumber ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@description", $"تحصيل شيك رقم {checkNumber}");
                    insertCmd.ExecuteNonQuery();
                }

                RecalculateSingleCustomerBalance(connection, transaction, customerId.Value);
            }
            else if (checkType == "Payable" && supplierId.HasValue)
            {
                using (var existsCmd = new SQLiteCommand(
                    "SELECT COUNT(*) FROM SupplierTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
                {
                    existsCmd.Parameters.AddWithValue("@checkId", checkId);
                    long already = Convert.ToInt64(existsCmd.ExecuteScalar());
                    if (already > 0) return;
                }

                using (var insertCmd = new SQLiteCommand(@"
                    INSERT INTO SupplierTransactions (
                        SupplierID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @supplierId, @date, 'Payment',
                        @amount, 0, 0,
                        'CHECK', @checkId, @checkNumber, @description, NULL, CURRENT_TIMESTAMP
                    )", connection, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@supplierId", supplierId.Value);
                    insertCmd.Parameters.AddWithValue("@date", settlementDate.ToString("yyyy-MM-dd"));
                    insertCmd.Parameters.AddWithValue("@amount", amount);
                    insertCmd.Parameters.AddWithValue("@checkId", checkId);
                    insertCmd.Parameters.AddWithValue("@checkNumber", (object)checkNumber ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@description", $"صرف شيك رقم {checkNumber}");
                    insertCmd.ExecuteNonQuery();
                }

                RecalculateSingleSupplierBalance(connection, transaction, supplierId.Value);
            }
            // لو الشيك مش مرتبط بعميل ولا مورد (شيك نثري/عام)، مفيش رصيد جهة يتأثر أصلاً - تجاهل آمن.
        }

        /// <summary>
        /// عكس أثر شيك سبق تحصيله/صرفه على كشف الحساب، يُستخدم فقط عند إلغاء (Void) شيك كان
        /// بالفعل في حالة Cleared. آمن للتكرار: لو مفيش قيد مسجَّل أصلاً (الشيك اتلغى قبل ما يتحصّل)
        /// مبيعملش حاجة.
        /// </summary>
        private void ReverseCheckSettlementIfNeeded(SQLiteConnection connection, SQLiteTransaction transaction, int checkId)
        {
            int? affectedCustomerId = null;
            using (var cmd = new SQLiteCommand(
                "SELECT CustomerID FROM CustomerTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@checkId", checkId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) affectedCustomerId = Convert.ToInt32(result);
            }

            if (affectedCustomerId.HasValue)
            {
                using (var delCmd = new SQLiteCommand(
                    "DELETE FROM CustomerTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
                {
                    delCmd.Parameters.AddWithValue("@checkId", checkId);
                    delCmd.ExecuteNonQuery();
                }
                RecalculateSingleCustomerBalance(connection, transaction, affectedCustomerId.Value);
                return;
            }

            int? affectedSupplierId = null;
            using (var cmd = new SQLiteCommand(
                "SELECT SupplierID FROM SupplierTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@checkId", checkId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) affectedSupplierId = Convert.ToInt32(result);
            }

            if (affectedSupplierId.HasValue)
            {
                using (var delCmd = new SQLiteCommand(
                    "DELETE FROM SupplierTransactions WHERE ReferenceType = 'CHECK' AND ReferenceID = @checkId", connection, transaction))
                {
                    delCmd.Parameters.AddWithValue("@checkId", checkId);
                    delCmd.ExecuteNonQuery();
                }
                RecalculateSingleSupplierBalance(connection, transaction, affectedSupplierId.Value);
            }
        }

        /// <summary>
        /// إعادة حساب الرصيد التراكمي (BalanceAfter) بالتسلسل الزمني الصحيح لعميل واحد، وتحديث
        /// CurrentBalance بنفس المعادلة المستخدمة في BackfillMissingCustomerInvoiceTransactions
        /// (DatabaseService.cs): فاتورة=مدين يزوّد، تحصيل=دائن يقلّل، أي نوع تاني (شيك/تسوية/مرتجع)
        /// مدين يزوّد ودائن يقلّل - نفس منطق القيد المزدوج العام.
        /// </summary>
        private void RecalculateSingleCustomerBalance(SQLiteConnection connection, SQLiteTransaction transaction, int customerId)
        {
            decimal openingBalance = 0;
            using (var cmd = new SQLiteCommand("SELECT COALESCE(OpeningBalance, 0) FROM Customers WHERE CustomerID = @id", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", customerId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) openingBalance = Convert.ToDecimal(result);
            }

            decimal runningBalance = openingBalance;
            using (var cmd = new SQLiteCommand(@"
                SELECT TransactionID, TransactionType, DebitAmount, CreditAmount
                FROM CustomerTransactions
                WHERE CustomerID = @customerId
                ORDER BY TransactionDate ASC, TransactionID ASC", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@customerId", customerId);
                var rows = new List<(long Id, string Type, decimal Debit, decimal Credit)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add((
                            reader.GetInt64(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)));
                    }
                }

                using (var updCmd = new SQLiteCommand(
                    "UPDATE CustomerTransactions SET BalanceAfter = @bal WHERE TransactionID = @id", connection, transaction))
                {
                    updCmd.Parameters.Add("@bal", System.Data.DbType.Decimal);
                    updCmd.Parameters.Add("@id", System.Data.DbType.Int64);

                    foreach (var row in rows)
                    {
                        if (row.Type == "Invoice")
                        {
                            decimal amount = row.Debit != 0 ? row.Debit : row.Credit;
                            runningBalance += amount;
                        }
                        else if (row.Type == "Receipt")
                        {
                            decimal amount = row.Credit != 0 ? row.Credit : row.Debit;
                            runningBalance -= amount;
                        }
                        else
                        {
                            runningBalance += row.Debit;
                            runningBalance -= row.Credit;
                        }

                        updCmd.Parameters["@bal"].Value = runningBalance;
                        updCmd.Parameters["@id"].Value = row.Id;
                        updCmd.ExecuteNonQuery();
                    }
                }
            }

            using (var updBalCmd = new SQLiteCommand(
                "UPDATE Customers SET CurrentBalance = @bal, ModifiedDate = @modDate WHERE CustomerID = @id", connection, transaction))
            {
                updBalCmd.Parameters.AddWithValue("@bal", runningBalance);
                updBalCmd.Parameters.AddWithValue("@modDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                updBalCmd.Parameters.AddWithValue("@id", customerId);
                updBalCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// نفس فكرة RecalculateSingleCustomerBalance بالظبط لكن بصيغة حساب الموردين المطابقة تماماً
        /// لـ BackfillMissingSupplierPurchaseTransactions: فاتورة مشتريات=دائن يزوّد، سند صرف=مدين
        /// يقلّل، أي نوع تاني=دائن يزوّد ومدين يقلّل.
        /// </summary>
        private void RecalculateSingleSupplierBalance(SQLiteConnection connection, SQLiteTransaction transaction, int supplierId)
        {
            decimal openingBalance = 0;
            using (var cmd = new SQLiteCommand("SELECT COALESCE(OpeningBalance, 0) FROM Suppliers WHERE SupplierID = @id", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", supplierId);
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) openingBalance = Convert.ToDecimal(result);
            }

            decimal runningBalance = openingBalance;
            using (var cmd = new SQLiteCommand(@"
                SELECT TransactionID, TransactionType, DebitAmount, CreditAmount
                FROM SupplierTransactions
                WHERE SupplierID = @supplierId
                ORDER BY TransactionDate ASC, TransactionID ASC", connection, transaction))
            {
                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                var rows = new List<(long Id, string Type, decimal Debit, decimal Credit)>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add((
                            reader.GetInt64(0),
                            reader.IsDBNull(1) ? "" : reader.GetString(1),
                            reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)));
                    }
                }

                using (var updCmd = new SQLiteCommand(
                    "UPDATE SupplierTransactions SET BalanceAfter = @bal WHERE TransactionID = @id", connection, transaction))
                {
                    updCmd.Parameters.Add("@bal", System.Data.DbType.Decimal);
                    updCmd.Parameters.Add("@id", System.Data.DbType.Int64);

                    foreach (var row in rows)
                    {
                        if (row.Type == "Purchase")
                        {
                            decimal amount = row.Credit != 0 ? row.Credit : row.Debit;
                            runningBalance += amount;
                        }
                        else if (row.Type == "Payment")
                        {
                            decimal amount = row.Debit != 0 ? row.Debit : row.Credit;
                            runningBalance -= amount;
                        }
                        else
                        {
                            runningBalance -= row.Debit;
                            runningBalance += row.Credit;
                        }

                        updCmd.Parameters["@bal"].Value = runningBalance;
                        updCmd.Parameters["@id"].Value = row.Id;
                        updCmd.ExecuteNonQuery();
                    }
                }
            }

            using (var updBalCmd = new SQLiteCommand(
                "UPDATE Suppliers SET CurrentBalance = @bal, ModifiedDate = @modDate WHERE SupplierID = @id", connection, transaction))
            {
                updBalCmd.Parameters.AddWithValue("@bal", runningBalance);
                updBalCmd.Parameters.AddWithValue("@modDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                updBalCmd.Parameters.AddWithValue("@id", supplierId);
                updBalCmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region الإلغاء (Void)

        /// <summary>
        /// ✅ إصلاح: إلغاء شيك كان بالفعل Cleared (وبالتالي له قيد مسجَّل في كشف الحساب) كان
        /// بيسيب القيد ده موجود من غير عكس، فيفضل رصيد العميل/المورد فيه أثر شيك مُلغى. دلوقتي
        /// بيتم عكس القيد تلقائياً (لو كان موجود أصلاً) في نفس Transaction الإلغاء.
        /// </summary>
        public async Task<(bool Success, string Message)> VoidCheckAsync(int checkId, string voidReason, int voidBy)
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
                                UPDATE Checks SET
                                    IsVoid = 1,
                                    VoidReason = @voidReason,
                                    VoidDate = @voidDate,
                                    VoidBy = @voidBy,
                                    ModifiedDate = @voidDate
                                WHERE CheckID = @id";

                            int rows;
                            using (var cmd = new SQLiteCommand(sql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voidReason", (object)voidReason ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@voidDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@voidBy", voidBy);
                                cmd.Parameters.AddWithValue("@id", checkId);

                                rows = await cmd.ExecuteNonQueryAsync();
                            }

                            if (rows == 0)
                            {
                                transaction.Rollback();
                                return (false, "لم يتم العثور على الشيك");
                            }

                            ReverseCheckSettlementIfNeeded(connection, transaction, checkId);

                            transaction.Commit();
                            return (true, "تم إلغاء الشيك بنجاح");
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

        #endregion
    }
}
