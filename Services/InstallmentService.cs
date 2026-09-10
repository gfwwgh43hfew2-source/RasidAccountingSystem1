using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace RasidAccountingSystem.Services
{
    public class InstallmentService
    {
        #region المتغيرات والمنشئ (Fields & Constructor)

        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public InstallmentService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _connectionString = databaseService.GetConnectionString();
        }

        #endregion

        #region دوال مساعدة (Helper Methods)

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 5, int delayMs = 500)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
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

                    System.Diagnostics.Debug.WriteLine($"⚠️ قاعدة البيانات مشغولة، محاولة {retryCount}/{maxRetries}...");
                    await Task.Delay(delayMs * retryCount);
                }
            }
            throw new Exception("فشل تنفيذ العملية بعد عدة محاولات");
        }

        private async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 5, int delayMs = 500)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    await action();
                    return;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("database is locked") || ex.Message.Contains("busy"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    System.Diagnostics.Debug.WriteLine($"⚠️ قاعدة البيانات مشغولة، محاولة {retryCount}/{maxRetries}...");
                    await Task.Delay(delayMs * retryCount);
                }
            }
            throw new Exception("فشل تنفيذ العملية بعد عدة محاولات");
        }

        #endregion

        #region كلاسات البيانات (Data Models)

        /// <summary>
        /// كلاس بيانات القسط
        /// </summary>
        public class InstallmentItem
        {
            public int InstallmentID { get; set; }
            public int InvoiceID { get; set; }
            public string InvoiceNumber { get; set; }
            public int CustomerID { get; set; }
            public string CustomerName { get; set; }
            public string CustomerCode { get; set; }
            public string CustomerPhone { get; set; }
            public int InstallmentNumber { get; set; }
            public decimal InstallmentAmount { get; set; }
            public DateTime DueDate { get; set; }
            public int DueDays { get; set; }
            public string Status { get; set; }
            public decimal PaidAmount { get; set; }
            public decimal RemainingAmount { get; set; }
            public DateTime? PaidDate { get; set; }
            public string PaymentMethod { get; set; }
            public int? ReceiptVoucherID { get; set; }
            public string Notes { get; set; }
            public DateTime CreatedDate { get; set; }
            public DateTime? ModifiedDate { get; set; }
            public int SerialNumber { get; set; }

            public string StatusText
            {
                get
                {
                    switch (Status)
                    {
                        case "Paid": return "✅ مسدد";
                        case "Overdue": return "🔴 متأخر";
                        case "Pending": return "⏳ غير مسدد";
                        case "Partial": return "🟡 مسدد جزئياً";
                        default: return Status;
                    }
                }
            }

            public string FormattedAmount => InstallmentAmount.ToString("N2");
            public string FormattedPaidAmount => PaidAmount.ToString("N2");
            public string FormattedRemaining => RemainingAmount.ToString("N2");
            public string FormattedDueDate => DueDate.ToString("yyyy/MM/dd");

            public int DaysRemaining
            {
                get
                {
                    int days = (DueDate - DateTime.Now.Date).Days;
                    return days;
                }
            }

            public string DaysText
            {
                get
                {
                    int days = DaysRemaining;
                    if (days < 0) return $"تأخر {Math.Abs(days)} يوم";
                    if (days == 0) return "اليوم";
                    return $"متبقي {days} يوم";
                }
            }

            public Brush StatusColor
            {
                get
                {
                    if (Status == "Paid")
                        return new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    if (Status == "Overdue")
                        return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    if (Status == "Partial")
                        return new SolidColorBrush(Color.FromRgb(245, 158, 11));

                    int days = DaysRemaining;
                    if (days <= 0)
                        return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    if (days <= 3)
                        return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    if (days <= 7)
                        return new SolidColorBrush(Color.FromRgb(234, 179, 8));
                    return new SolidColorBrush(Color.FromRgb(59, 130, 246));
                }
            }

            public Brush StatusBackground
            {
                get
                {
                    if (Status == "Paid")
                        return new SolidColorBrush(Color.FromRgb(209, 250, 229));
                    if (Status == "Overdue")
                        return new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    if (Status == "Partial")
                        return new SolidColorBrush(Color.FromRgb(252, 234, 206));

                    int days = DaysRemaining;
                    if (days <= 0)
                        return new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    if (days <= 3)
                        return new SolidColorBrush(Color.FromRgb(252, 234, 206));
                    if (days <= 7)
                        return new SolidColorBrush(Color.FromRgb(254, 249, 195));
                    return new SolidColorBrush(Color.FromRgb(219, 234, 254));
                }
            }
        }

        /// <summary>
        /// كلاس بيانات تحصيل قسط
        /// </summary>
        public class InstallmentPaymentItem
        {
            public int PaymentID { get; set; }
            public int InstallmentID { get; set; }
            public DateTime PaymentDate { get; set; }
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; }
            public int? ReceiptVoucherID { get; set; }
            public int? TreasuryID { get; set; }
            public string TreasuryName { get; set; }
            public int? BankAccountID { get; set; }
            public string BankName { get; set; }
            public string CheckNumber { get; set; }
            public DateTime? CheckDate { get; set; }
            public string Notes { get; set; }
            public DateTime CreatedDate { get; set; }
            public string CreatedBy { get; set; }

            public string FormattedAmount => Amount.ToString("N2");
            public string FormattedPaymentDate => PaymentDate.ToString("yyyy/MM/dd");
        }

        /// <summary>
        /// ملخص أقساط العميل
        /// </summary>
        public class CustomerInstallmentSummary
        {
            public int CustomerID { get; set; }
            public string CustomerName { get; set; }
            public string CustomerCode { get; set; }
            public int TotalInstallments { get; set; }
            public int PaidInstallments { get; set; }
            public int OverdueInstallments { get; set; }
            public int PendingInstallments { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal TotalRemaining { get; set; }
            public decimal TotalOverdue { get; set; }
        }

        /// <summary>
        /// بيانات إدخال القسط
        /// </summary>
        public class InstallmentInput
        {
            public decimal Amount { get; set; }
            public int DueDays { get; set; }
            public string Notes { get; set; }
        }

        /// <summary>
        /// إحصائيات الأقساط العامة
        /// </summary>
        public class InstallmentStatistics
        {
            public int TotalInstallments { get; set; }
            public int PaidInstallments { get; set; }
            public int PendingInstallments { get; set; }
            public int OverdueInstallments { get; set; }
            public decimal TotalPendingAmount { get; set; }
            public decimal TotalOverdueAmount { get; set; }
            public decimal TotalCollected { get; set; }
            public decimal TotalRemaining { get; set; }

            public string FormattedTotalPending => TotalPendingAmount.ToString("N2");
            public string FormattedTotalOverdue => TotalOverdueAmount.ToString("N2");
            public string FormattedTotalCollected => TotalCollected.ToString("N2");
            public string FormattedTotalRemaining => TotalRemaining.ToString("N2");
        }

        #endregion

        #region دوال إنشاء الأقساط (Create Installments) - المعدلة بالكامل

        /// <summary>
        /// إنشاء أقساط لفاتورة بيع - مع دعم تمرير اتصال ومعاملة موجودة
        /// </summary>
        public async Task<bool> CreateInstallmentsForInvoiceAsync(
            int invoiceId,
            string invoiceNumber,
            int customerId,
            DateTime invoiceDate,
            decimal totalAmount,
            decimal paidUpfront,
            List<InstallmentInput> installments,
            int createdBy,
            SQLiteConnection existingConnection = null,
            SQLiteTransaction existingTransaction = null)
        {
            try
            {
                // ✅ إذا تم تمرير اتصال ومعاملة، استخدمها مباشرة (لا تcommit لأن المعاملة خارجية)
                if (existingConnection != null && existingTransaction != null)
                {
                    return await CreateInstallmentsInternalAsync(
                        existingConnection,
                        existingTransaction,
                        invoiceId,
                        invoiceNumber,
                        customerId,
                        invoiceDate,
                        totalAmount,
                        paidUpfront,
                        installments,
                        createdBy,
                        false); // ✅ لا تcommit المعاملة الخارجية
                }

                // ✅ وإلا، استخدم اتصال جديد مع إعادة المحاولة
                return await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        connection.BusyTimeout = 120;
                        await connection.OpenAsync();

                        using (var transaction = connection.BeginTransaction())
                        {
                            return await CreateInstallmentsInternalAsync(
                                connection,
                                transaction,
                                invoiceId,
                                invoiceNumber,
                                customerId,
                                invoiceDate,
                                totalAmount,
                                paidUpfront,
                                installments,
                                createdBy,
                                true); // ✅ Commit المعاملة الداخلية
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateInstallmentsForInvoiceAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في إنشاء الأقساط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// الدالة الداخلية لإنشاء الأقساط (تستخدم اتصال ومعاملة موجودين)
        /// </summary>
        /// <summary>
        /// الدالة الداخلية لإنشاء الأقساط (تستخدم اتصال ومعاملة موجودين)
        /// </summary>
        private async Task<bool> CreateInstallmentsInternalAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int invoiceId,
            string invoiceNumber,
            int customerId,
            DateTime invoiceDate,
            decimal totalAmount,
            decimal paidUpfront,
            List<InstallmentInput> installments,
            int createdBy,
            bool commitTransaction = true)
        {
            try
            {
                // ✅ التحقق من صحة الاتصال والمعاملة
                if (connection == null || connection.State != System.Data.ConnectionState.Open)
                {
                    System.Diagnostics.Debug.WriteLine("❌ الاتصال غير صالح أو مغلق");
                    return false;
                }

                if (transaction == null || transaction.Connection == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ المعاملة غير صالحة");
                    return false;
                }

                decimal remainingAmount = totalAmount - paidUpfront;
                decimal totalInstallmentAmount = 0;

                foreach (var inst in installments)
                {
                    totalInstallmentAmount += inst.Amount;
                }

                if (Math.Abs(totalInstallmentAmount - remainingAmount) > 0.01m)
                {
                    MessageBox.Show($"مجموع الأقساط ({totalInstallmentAmount:N2}) لا يساوي المبلغ المتبقي ({remainingAmount:N2})",
                                    "خطأ في الأقساط", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                int installmentNumber = 1;

                foreach (var inst in installments)
                {
                    DateTime dueDate = invoiceDate.AddDays(inst.DueDays);

                    // ✅ تحديد الحالة الأولية للقسط
                    string initialStatus = dueDate < DateTime.Now.Date ? "Overdue" : "Pending";

                    string insertSql = @"
                INSERT INTO Installments (
                    InvoiceID, CustomerID, InstallmentNumber, InstallmentAmount,
                    DueDate, DueDays, Status, RemainingAmount,
                    Notes, CreatedDate, CreatedBy
                ) VALUES (
                    @invoiceId, @customerId, @number, @amount,
                    @dueDate, @dueDays, @status, @amount,
                    @notes, CURRENT_TIMESTAMP, @createdBy
                );
                SELECT last_insert_rowid();";

                    int newInstallmentId = 0;
                    using (var cmd = new SQLiteCommand(insertSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@number", installmentNumber);
                        cmd.Parameters.AddWithValue("@amount", inst.Amount);
                        cmd.Parameters.AddWithValue("@dueDate", dueDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@dueDays", inst.DueDays);
                        cmd.Parameters.AddWithValue("@status", initialStatus);
                        cmd.Parameters.AddWithValue("@notes", inst.Notes ?? "");
                        cmd.Parameters.AddWithValue("@createdBy", createdBy);

                        object result = await cmd.ExecuteScalarAsync();
                        newInstallmentId = Convert.ToInt32(result);
                    }

                    if (newInstallmentId == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ فشل في إنشاء القسط رقم {installmentNumber}");
                        return false;
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء القسط {installmentNumber} (ID: {newInstallmentId})");
                    installmentNumber++;
                }

                string updateInvoiceSql = @"
            UPDATE SalesInvoices 
            SET 
                IsInstallment = 1,
                TotalInstallmentAmount = @remainingAmount,
                PaidUpfront = @paidUpfront,
                InstallmentCount = @count,
                ModifiedDate = CURRENT_TIMESTAMP
            WHERE InvoiceID = @invoiceId";

                using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@remainingAmount", remainingAmount);
                    cmd.Parameters.AddWithValue("@paidUpfront", paidUpfront);
                    cmd.Parameters.AddWithValue("@count", installments.Count);
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    await cmd.ExecuteNonQueryAsync();
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء {installments.Count} قسط للفاتورة {invoiceNumber}");

                // ✅ فقط قم بتنفيذ Commit إذا كانت المعاملة من الداخل
                if (commitTransaction)
                {
                    transaction.Commit();
                    System.Diagnostics.Debug.WriteLine($"✅ تم Commit المعاملة بنجاح");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ CreateInstallmentsInternalAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

                // ✅ فقط قم بـ Rollback إذا كانت المعاملة من الداخل
                if (commitTransaction)
                {
                    try { transaction.Rollback(); } catch { }
                }

                MessageBox.Show($"خطأ في إنشاء الأقساط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region دوال جلب الأقساط (Get Installments)

        /// <summary>
        /// الحصول على أقساط فاتورة محددة
        /// </summary>
        public async Task<List<InstallmentItem>> GetInstallmentsByInvoiceIdAsync(int invoiceId)
        {
            var installments = new List<InstallmentItem>();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                            SELECT 
                                i.InstallmentID,
                                i.InvoiceID,
                                si.InvoiceNumber,
                                i.CustomerID,
                                c.CustomerNameAr,
                                c.CustomerCode,
                                i.InstallmentNumber,
                                i.InstallmentAmount,
                                i.DueDate,
                                i.DueDays,
                                i.Status,
                                i.PaidAmount,
                                i.RemainingAmount,
                                i.PaidDate,
                                i.PaymentMethod,
                                i.ReceiptVoucherID,
                                i.Notes,
                                i.CreatedDate,
                                i.ModifiedDate
                            FROM Installments i
                            LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                            LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                            WHERE i.InvoiceID = @invoiceId
                            ORDER BY i.InstallmentNumber";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    installments.Add(new InstallmentItem
                                    {
                                        InstallmentID = reader.GetInt32(0),
                                        InvoiceID = reader.GetInt32(1),
                                        InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        CustomerID = reader.GetInt32(3),
                                        CustomerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                        CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                        InstallmentNumber = reader.GetInt32(6),
                                        InstallmentAmount = reader.GetDecimal(7),
                                        DueDate = reader.GetDateTime(8),
                                        DueDays = reader.GetInt32(9),
                                        Status = reader.GetString(10),
                                        PaidAmount = reader.IsDBNull(11) ? 0 : reader.GetDecimal(11),
                                        RemainingAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                        PaidDate = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13),
                                        PaymentMethod = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                        ReceiptVoucherID = reader.IsDBNull(15) ? (int?)null : reader.GetInt32(15),
                                        Notes = reader.IsDBNull(16) ? "" : reader.GetString(16),
                                        CreatedDate = reader.GetDateTime(17),
                                        ModifiedDate = reader.IsDBNull(18) ? (DateTime?)null : reader.GetDateTime(18)
                                    });
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetInstallmentsByInvoiceIdAsync Error: {ex.Message}");
            }

            return installments;
        }

        /// <summary>
        /// الحصول على أقساط عميل محددة
        /// </summary>
        public async Task<List<InstallmentItem>> GetInstallmentsByCustomerIdAsync(int customerId, string statusFilter = "All")
        {
            var installments = new List<InstallmentItem>();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                            SELECT 
                                i.InstallmentID,
                                i.InvoiceID,
                                si.InvoiceNumber,
                                i.CustomerID,
                                c.CustomerNameAr,
                                c.CustomerCode,
                                c.Phone,
                                i.InstallmentNumber,
                                i.InstallmentAmount,
                                i.DueDate,
                                i.DueDays,
                                i.Status,
                                i.PaidAmount,
                                i.RemainingAmount,
                                i.PaidDate,
                                i.PaymentMethod,
                                i.ReceiptVoucherID,
                                i.Notes,
                                i.CreatedDate,
                                i.ModifiedDate
                            FROM Installments i
                            LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                            LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                            WHERE i.CustomerID = @customerId";

                        if (statusFilter == "Pending")
                            sql += " AND i.Status IN ('Pending', 'Partial')";
                        else if (statusFilter == "Overdue")
                            sql += " AND i.Status = 'Overdue'";
                        else if (statusFilter == "Paid")
                            sql += " AND i.Status = 'Paid'";

                        sql += " ORDER BY i.DueDate";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    installments.Add(new InstallmentItem
                                    {
                                        InstallmentID = reader.GetInt32(0),
                                        InvoiceID = reader.GetInt32(1),
                                        InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        CustomerID = reader.GetInt32(3),
                                        CustomerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                        CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                        CustomerPhone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                        InstallmentNumber = reader.GetInt32(7),
                                        InstallmentAmount = reader.GetDecimal(8),
                                        DueDate = reader.GetDateTime(9),
                                        DueDays = reader.GetInt32(10),
                                        Status = reader.GetString(11),
                                        PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                        RemainingAmount = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                        PaidDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                                        PaymentMethod = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                        ReceiptVoucherID = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                                        Notes = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                        CreatedDate = reader.GetDateTime(18),
                                        ModifiedDate = reader.IsDBNull(19) ? (DateTime?)null : reader.GetDateTime(19)
                                    });
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetInstallmentsByCustomerIdAsync Error: {ex.Message}");
            }

            return installments;
        }

        /// <summary>
        /// الحصول على جميع الأقساط غير المسددة (لصفحة المستحقات)
        /// </summary>
        public async Task<List<InstallmentItem>> GetAllPendingInstallmentsAsync()
        {
            var installments = new List<InstallmentItem>();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                            SELECT 
                                i.InstallmentID,
                                i.InvoiceID,
                                si.InvoiceNumber,
                                i.CustomerID,
                                c.CustomerNameAr,
                                c.CustomerCode,
                                c.Phone,
                                i.InstallmentNumber,
                                i.InstallmentAmount,
                                i.DueDate,
                                i.DueDays,
                                i.Status,
                                i.PaidAmount,
                                i.RemainingAmount,
                                i.PaidDate,
                                i.PaymentMethod,
                                i.ReceiptVoucherID,
                                i.Notes,
                                i.CreatedDate,
                                i.ModifiedDate
                            FROM Installments i
                            LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                            LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                            WHERE i.Status IN ('Pending', 'Partial', 'Overdue')
                            ORDER BY i.DueDate";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                installments.Add(new InstallmentItem
                                {
                                    InstallmentID = reader.GetInt32(0),
                                    InvoiceID = reader.GetInt32(1),
                                    InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CustomerID = reader.GetInt32(3),
                                    CustomerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    CustomerPhone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    InstallmentNumber = reader.GetInt32(7),
                                    InstallmentAmount = reader.GetDecimal(8),
                                    DueDate = reader.GetDateTime(9),
                                    DueDays = reader.GetInt32(10),
                                    Status = reader.GetString(11),
                                    PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                    RemainingAmount = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                    PaidDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                                    PaymentMethod = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                    ReceiptVoucherID = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                                    Notes = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                    CreatedDate = reader.GetDateTime(18),
                                    ModifiedDate = reader.IsDBNull(19) ? (DateTime?)null : reader.GetDateTime(19),
                                    SerialNumber = 0
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetAllPendingInstallmentsAsync Error: {ex.Message}");
                throw;
            }

            return installments;
        }

        #endregion

        #region دوال تحصيل الأقساط (Collect Installments)

        /// <summary>
        /// تحصيل قسط (تسجيل دفعة)
        /// </summary>
        /// <summary>
        /// تحصيل قسط (تسجيل دفعة)
        /// </summary>
        /// <summary>
        /// تحصيل قسط (تسجيل دفعة)
        /// </summary>
        public async Task<bool> CollectInstallmentAsync(
            int installmentId,
            decimal amount,
            DateTime paymentDate,
            string paymentMethod,
            int treasuryId,
            int createdBy,
            string notes = "",
            string checkNumber = "",
            DateTime? checkDate = null,
            string bankName = "",
            int? bankAccountId = null)
        {
            try
            {
                return await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        connection.BusyTimeout = 120;
                        await connection.OpenAsync();

                        using (var transaction = connection.BeginTransaction())
                        {
                            int invoiceId = 0;
                            int customerId = 0;
                            decimal remainingAmount = 0;
                            decimal installmentAmount = 0;
                            string status = "";
                            string invoiceNumber = "";
                            string customerName = "";
                            int installmentNumber = 0;

                            // ✅ جلب جميع بيانات القسط بما فيها رقم القسط واسم العميل
                            string getInstallmentSql = @"
                        SELECT 
                            i.InvoiceID, 
                            i.CustomerID, 
                            i.RemainingAmount, 
                            i.InstallmentAmount, 
                            i.Status,
                            i.InstallmentNumber,
                            si.InvoiceNumber,
                            COALESCE(c.CustomerNameAr, c.CustomerName, '') as CustomerName
                        FROM Installments i
                        LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                        LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                        WHERE i.InstallmentID = @installmentId";

                            using (var cmd = new SQLiteCommand(getInstallmentSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@installmentId", installmentId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        invoiceId = reader.GetInt32(0);
                                        customerId = reader.GetInt32(1);
                                        remainingAmount = reader.GetDecimal(2);
                                        installmentAmount = reader.GetDecimal(3);
                                        status = reader.GetString(4);
                                        installmentNumber = reader.GetInt32(5);
                                        invoiceNumber = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                        customerName = reader.IsDBNull(7) ? "عميل" : reader.GetString(7);
                                    }
                                    else
                                    {
                                        transaction.Rollback();
                                        return false;
                                    }
                                }
                            }

                            if (status == "Paid")
                            {
                                MessageBox.Show("هذا القسط مسدد بالفعل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                                transaction.Rollback();
                                return false;
                            }

                            if (amount > remainingAmount)
                            {
                                MessageBox.Show($"المبلغ المدفوع ({amount:N2}) أكبر من المبلغ المتبقي ({remainingAmount:N2})",
                                                "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                                transaction.Rollback();
                                return false;
                            }

                            string voucherNumber = await GenerateReceiptVoucherNumberAsync(connection, transaction);
                            int receiptVoucherId = 0;

                            string insertVoucherSql = @"
                        INSERT INTO ReceiptVouchers (
                            VoucherNumber, VoucherDate, CustomerID, Amount, PaymentMethod,
                            TreasuryID, CheckNumber, CheckDate, BankName,
                            ReferenceNumber, Description, IsPosted, CreatedBy, CreatedDate
                        ) VALUES (
                            @voucherNumber, @voucherDate, @customerId, @amount, @paymentMethod,
                            @treasuryId, @checkNumber, @checkDate, @bankName,
                            @referenceNumber, @description, 1, @createdBy, CURRENT_TIMESTAMP
                        );
                        SELECT last_insert_rowid();";

                            using (var cmd = new SQLiteCommand(insertVoucherSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                                cmd.Parameters.AddWithValue("@voucherDate", paymentDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                                cmd.Parameters.AddWithValue("@checkDate", checkDate.HasValue ? checkDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@bankName", bankName ?? "");
                                cmd.Parameters.AddWithValue("@referenceNumber", $"INST-{installmentId}");
                                cmd.Parameters.AddWithValue("@description", $"تحصيل قسط {installmentNumber} - {customerName}");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);

                                object result = await cmd.ExecuteScalarAsync();
                                receiptVoucherId = Convert.ToInt32(result);
                            }

                            if (receiptVoucherId == 0)
                            {
                                transaction.Rollback();
                                return false;
                            }

                            decimal newPaidAmount = installmentAmount - remainingAmount + amount;
                            decimal newRemainingAmount = remainingAmount - amount;
                            string newStatus = newRemainingAmount <= 0 ? "Paid" : "Partial";

                            string updateInstallmentSql = @"
                        UPDATE Installments SET 
                            PaidAmount = @paidAmount,
                            RemainingAmount = @remainingAmount,
                            Status = @status,
                            PaidDate = CASE WHEN @status = 'Paid' THEN @paidDate ELSE PaidDate END,
                            PaymentMethod = @paymentMethod,
                            ReceiptVoucherID = @receiptVoucherId,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE InstallmentID = @installmentId";

                            using (var cmd = new SQLiteCommand(updateInstallmentSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@paidAmount", newPaidAmount);
                                cmd.Parameters.AddWithValue("@remainingAmount", newRemainingAmount);
                                cmd.Parameters.AddWithValue("@status", newStatus);
                                cmd.Parameters.AddWithValue("@paidDate", paymentDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                                cmd.Parameters.AddWithValue("@receiptVoucherId", receiptVoucherId);
                                cmd.Parameters.AddWithValue("@installmentId", installmentId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            string insertPaymentSql = @"
                        INSERT INTO InstallmentPayments (
                            InstallmentID, PaymentDate, Amount, PaymentMethod,
                            ReceiptVoucherID, TreasuryID, BankAccountID,
                            CheckNumber, CheckDate, BankName, Notes, CreatedBy
                        ) VALUES (
                            @installmentId, @paymentDate, @amount, @paymentMethod,
                            @receiptVoucherId, @treasuryId, @bankAccountId,
                            @checkNumber, @checkDate, @bankName, @notes, @createdBy
                        )";

                            using (var cmd = new SQLiteCommand(insertPaymentSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@installmentId", installmentId);
                                cmd.Parameters.AddWithValue("@paymentDate", paymentDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                                cmd.Parameters.AddWithValue("@receiptVoucherId", receiptVoucherId);
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                cmd.Parameters.AddWithValue("@bankAccountId", bankAccountId ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                                cmd.Parameters.AddWithValue("@checkDate", checkDate.HasValue ? checkDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@bankName", bankName ?? "");
                                cmd.Parameters.AddWithValue("@notes", notes ?? "");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ============================================================
                            // ✅ الحصول على الرصيد الحالي للخزينة
                            // ============================================================
                            decimal currentTreasuryBalance = 0;
                            string getBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
                            using (var cmd = new SQLiteCommand(getBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentTreasuryBalance = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newTreasuryBalance = currentTreasuryBalance + amount;

                            // ============================================================
                            // ✅ تحديث رصيد الخزينة
                            // ============================================================
                            string updateTreasurySql = @"
                        UPDATE Treasury 
                        SET CurrentBalance = @newBalance,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE TreasuryID = @treasuryId";

                            using (var cmd = new SQLiteCommand(updateTreasurySql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newTreasuryBalance);
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ============================================================
                            // ✅ ✅ ✅ إضافة حركة الخزينة مع بيان مفصل
                            // ============================================================
                            string insertTreasuryTransactionSql = @"
                        INSERT INTO TreasuryTransactions (
                            TreasuryID,
                            TransactionDate,
                            TransactionType,
                            Amount,
                            BalanceAfter,
                            Description,
                            ReferenceType,
                            ReferenceID,
                            ReferenceNumber,
                            CreatedBy,
                            CreatedDate
                        ) VALUES (
                            @treasuryId,
                            @transactionDate,
                            @transactionType,
                            @amount,
                            @balanceAfter,
                            @description,
                            @referenceType,
                            @referenceId,
                            @referenceNumber,
                            @createdBy,
                            CURRENT_TIMESTAMP
                        )";

                            // ✅ بناء بيان الحركة بالشكل المطلوب
                            // ✅ مثلاً: "تحصيل القسط الأول من العميل محمد أحمد - فاتورة SIN-000001"
                            string transactionDescription = $"تحصيل القسط {installmentNumber} من العميل {customerName}";

                            // ✅ إضافة رقم الفاتورة إذا كان متاحاً
                            if (!string.IsNullOrEmpty(invoiceNumber))
                            {
                                transactionDescription += $" - فاتورة {invoiceNumber}";
                            }

                            // ✅ إضافة الملاحظات إذا كانت موجودة
                            if (!string.IsNullOrEmpty(notes))
                            {
                                transactionDescription += $" ({notes})";
                            }

                            using (var cmd = new SQLiteCommand(insertTreasuryTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                                cmd.Parameters.AddWithValue("@transactionDate", paymentDate.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@transactionType", "Receipt");
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", newTreasuryBalance);
                                cmd.Parameters.AddWithValue("@description", transactionDescription);
                                cmd.Parameters.AddWithValue("@referenceType", "INSTALLMENT");
                                cmd.Parameters.AddWithValue("@referenceId", installmentId);
                                cmd.Parameters.AddWithValue("@referenceNumber", $"INST-{installmentId}");
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"✅ تم إضافة حركة خزينة للقسط {installmentId} بقيمة {amount} إلى الخزينة {treasuryId}");
                            System.Diagnostics.Debug.WriteLine($"📝 بيان الحركة: {transactionDescription}");

                            // ============================================================
                            // ✅ تحديث رصيد العميل
                            // ============================================================
                            decimal currentCustomerBalance = 0;
                            string getCustomerBalanceSql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @customerId";
                            using (var cmd = new SQLiteCommand(getCustomerBalanceSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                object result = await cmd.ExecuteScalarAsync();
                                currentCustomerBalance = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            decimal newCustomerBalance = currentCustomerBalance - amount;

                            string updateCustomerSql = @"
                        UPDATE Customers 
                        SET CurrentBalance = @newBalance,
                            ModifiedDate = CURRENT_TIMESTAMP
                        WHERE CustomerID = @customerId";

                            using (var cmd = new SQLiteCommand(updateCustomerSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@newBalance", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // ============================================================
                            // ✅ إضافة حركة في CustomerTransactions مع بيان مفصل
                            // ============================================================
                            string customerTransactionSql = @"
                        INSERT INTO CustomerTransactions (
                            CustomerID, TransactionDate, TransactionType,
                            DebitAmount, CreditAmount, BalanceAfter,
                            ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                        ) VALUES (
                            @customerId, @date, 'Receipt',
                            0, @amount, @balanceAfter,
                            'INSTALLMENT_PAYMENT', @installmentId, @referenceNumber, @description, @createdBy, CURRENT_TIMESTAMP
                        )";

                            // ✅ بيان حركة العميل بنفس التنسيق
                            string customerTransactionDescription = $"تحصيل القسط {installmentNumber}";

                            if (!string.IsNullOrEmpty(invoiceNumber))
                            {
                                customerTransactionDescription += $" - فاتورة {invoiceNumber}";
                            }

                            using (var cmd = new SQLiteCommand(customerTransactionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@customerId", customerId);
                                cmd.Parameters.AddWithValue("@date", paymentDate.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@amount", amount);
                                cmd.Parameters.AddWithValue("@balanceAfter", newCustomerBalance);
                                cmd.Parameters.AddWithValue("@installmentId", installmentId);
                                cmd.Parameters.AddWithValue("@referenceNumber", $"قسط-{installmentId}");
                                cmd.Parameters.AddWithValue("@description", customerTransactionDescription);
                                cmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            System.Diagnostics.Debug.WriteLine($"✅ تم إضافة حركة تحصيل قسط في CustomerTransactions للعميل {customerId}");

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"✅ تم تحصيل قسط {installmentId} بمبلغ {amount}");
                            return true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CollectInstallmentAsync Error: {ex.Message}");
                MessageBox.Show($"خطأ في تحصيل القسط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #region دوال جلب الأقساط القريبة من الاستحقاق (Upcoming Installments)

        /// <summary>
        /// جلب الأقساط التي اقترب موعد استحقاقها (خلال الأيام القادمة)
        /// </summary>
        public async Task<List<InstallmentItem>> GetUpcomingInstallmentsAsync(int daysThreshold = 7)
        {
            var installments = new List<InstallmentItem>();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                    SELECT 
                        i.InstallmentID,
                        i.InvoiceID,
                        si.InvoiceNumber,
                        i.CustomerID,
                        c.CustomerNameAr,
                        c.CustomerCode,
                        c.Phone,
                        i.InstallmentNumber,
                        i.InstallmentAmount,
                        i.DueDate,
                        i.DueDays,
                        i.Status,
                        i.PaidAmount,
                        i.RemainingAmount,
                        i.PaidDate,
                        i.PaymentMethod,
                        i.ReceiptVoucherID,
                        i.Notes,
                        i.CreatedDate,
                        i.ModifiedDate,
                        julianday(i.DueDate) - julianday('now') as DaysRemaining
                    FROM Installments i
                    LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                    LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                    WHERE i.Status IN ('Pending', 'Partial')
                    AND julianday(i.DueDate) - julianday('now') BETWEEN 0 AND @daysThreshold
                    ORDER BY i.DueDate ASC";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@daysThreshold", daysThreshold);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    installments.Add(new InstallmentItem
                                    {
                                        InstallmentID = reader.GetInt32(0),
                                        InvoiceID = reader.GetInt32(1),
                                        InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                        CustomerID = reader.GetInt32(3),
                                        CustomerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                        CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                        CustomerPhone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                        InstallmentNumber = reader.GetInt32(7),
                                        InstallmentAmount = reader.GetDecimal(8),
                                        DueDate = reader.GetDateTime(9),
                                        DueDays = reader.GetInt32(10),
                                        Status = reader.GetString(11),
                                        PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                        RemainingAmount = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                        PaidDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                                        PaymentMethod = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                        ReceiptVoucherID = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                                        Notes = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                        CreatedDate = reader.GetDateTime(18),
                                        ModifiedDate = reader.IsDBNull(19) ? (DateTime?)null : reader.GetDateTime(19)
                                    });
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUpcomingInstallmentsAsync Error: {ex.Message}");
            }

            return installments;
        }

        /// <summary>
        /// جلب الأقساط المتأخرة
        /// </summary>
        public async Task<List<InstallmentItem>> GetOverdueInstallmentsAsync()
        {
            var installments = new List<InstallmentItem>();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                    SELECT 
                        i.InstallmentID,
                        i.InvoiceID,
                        si.InvoiceNumber,
                        i.CustomerID,
                        c.CustomerNameAr,
                        c.CustomerCode,
                        c.Phone,
                        i.InstallmentNumber,
                        i.InstallmentAmount,
                        i.DueDate,
                        i.DueDays,
                        i.Status,
                        i.PaidAmount,
                        i.RemainingAmount,
                        i.PaidDate,
                        i.PaymentMethod,
                        i.ReceiptVoucherID,
                        i.Notes,
                        i.CreatedDate,
                        i.ModifiedDate
                    FROM Installments i
                    LEFT JOIN SalesInvoices si ON i.InvoiceID = si.InvoiceID
                    LEFT JOIN Customers c ON i.CustomerID = c.CustomerID
                    WHERE i.Status IN ('Pending', 'Partial')
                    AND julianday('now') > julianday(i.DueDate)
                    ORDER BY i.DueDate ASC";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                installments.Add(new InstallmentItem
                                {
                                    InstallmentID = reader.GetInt32(0),
                                    InvoiceID = reader.GetInt32(1),
                                    InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    CustomerID = reader.GetInt32(3),
                                    CustomerName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    CustomerCode = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    CustomerPhone = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    InstallmentNumber = reader.GetInt32(7),
                                    InstallmentAmount = reader.GetDecimal(8),
                                    DueDate = reader.GetDateTime(9),
                                    DueDays = reader.GetInt32(10),
                                    Status = reader.GetString(11),
                                    PaidAmount = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                    RemainingAmount = reader.IsDBNull(13) ? 0 : reader.GetDecimal(13),
                                    PaidDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                                    PaymentMethod = reader.IsDBNull(15) ? "" : reader.GetString(15),
                                    ReceiptVoucherID = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                                    Notes = reader.IsDBNull(17) ? "" : reader.GetString(17),
                                    CreatedDate = reader.GetDateTime(18),
                                    ModifiedDate = reader.IsDBNull(19) ? (DateTime?)null : reader.GetDateTime(19)
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOverdueInstallmentsAsync Error: {ex.Message}");
            }

            return installments;
        }

        #endregion

        private async Task<string> GenerateReceiptVoucherNumberAsync(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "SELECT MAX(CAST(SUBSTR(VoucherNumber, 5) AS INTEGER)) FROM ReceiptVouchers WHERE VoucherNumber LIKE 'REC-%'";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                object result = await cmd.ExecuteScalarAsync();
                int maxNumber = (result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                int nextNumber = maxNumber + 1;
                return $"REC-{nextNumber:D6}";
            }
        }

        #endregion

        #region دوال التحديث والحذف (Update & Delete)

        /// <summary>
        /// تحديث بيانات قسط (فقط إذا كان غير مسدد)
        /// </summary>
        public async Task<bool> UpdateInstallmentAsync(
            int installmentId,
            decimal amount,
            int dueDays,
            DateTime? dueDate,
            string notes)
        {
            try
            {
                return await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string checkSql = "SELECT Status FROM Installments WHERE InstallmentID = @id";
                        string currentStatus = "";

                        using (var cmd = new SQLiteCommand(checkSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", installmentId);
                            object result = await cmd.ExecuteScalarAsync();
                            if (result != null)
                                currentStatus = result.ToString();
                        }

                        if (currentStatus == "Paid")
                        {
                            MessageBox.Show("لا يمكن تعديل قسط تم سداده بالكامل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                        string updateSql = @"
                            UPDATE Installments SET 
                                InstallmentAmount = @amount,
                                DueDays = @dueDays,
                                DueDate = @dueDate,
                                Notes = @notes,
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE InstallmentID = @id";

                        using (var cmd = new SQLiteCommand(updateSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@amount", amount);
                            cmd.Parameters.AddWithValue("@dueDays", dueDays);
                            cmd.Parameters.AddWithValue("@dueDate", dueDate.HasValue ? dueDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@notes", notes ?? "");
                            cmd.Parameters.AddWithValue("@id", installmentId);

                            return await cmd.ExecuteNonQueryAsync() > 0;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateInstallmentAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// حذف قسط (فقط إذا كان غير مسدد)
        /// </summary>
        public async Task<bool> DeleteInstallmentAsync(int installmentId)
        {
            try
            {
                return await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string checkSql = "SELECT Status FROM Installments WHERE InstallmentID = @id";
                        string currentStatus = "";

                        using (var cmd = new SQLiteCommand(checkSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", installmentId);
                            object result = await cmd.ExecuteScalarAsync();
                            if (result != null)
                                currentStatus = result.ToString();
                        }

                        if (currentStatus == "Paid")
                        {
                            MessageBox.Show("لا يمكن حذف قسط تم سداده بالكامل", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }

                        string deleteSql = "DELETE FROM Installments WHERE InstallmentID = @id";
                        using (var cmd = new SQLiteCommand(deleteSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", installmentId);
                            return await cmd.ExecuteNonQueryAsync() > 0;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteInstallmentAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال التقارير والإحصائيات (Reports & Statistics)

        /// <summary>
        /// الحصول على إحصائيات الأقساط العامة
        /// </summary>
        public async Task<InstallmentStatistics> GetInstallmentStatisticsAsync()
        {
            var stats = new InstallmentStatistics();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                            SELECT 
                                COUNT(*) as TotalInstallments,
                                SUM(CASE WHEN Status = 'Paid' THEN 1 ELSE 0 END) as PaidCount,
                                SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END) as PendingCount,
                                SUM(CASE WHEN Status = 'Overdue' THEN 1 ELSE 0 END) as OverdueCount,
                                SUM(CASE WHEN Status IN ('Pending', 'Partial', 'Overdue') THEN InstallmentAmount ELSE 0 END) as TotalPendingAmount,
                                SUM(CASE WHEN Status = 'Overdue' THEN InstallmentAmount ELSE 0 END) as TotalOverdueAmount,
                                SUM(CASE WHEN Status = 'Paid' THEN PaidAmount ELSE 0 END) as TotalCollected,
                                SUM(CASE WHEN Status IN ('Pending', 'Partial', 'Overdue') THEN RemainingAmount ELSE 0 END) as TotalRemaining
                            FROM Installments";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                stats.TotalInstallments = Convert.ToInt32(reader[0]);
                                stats.PaidInstallments = Convert.ToInt32(reader[1]);
                                stats.PendingInstallments = Convert.ToInt32(reader[2]);
                                stats.OverdueInstallments = Convert.ToInt32(reader[3]);
                                stats.TotalPendingAmount = Convert.ToDecimal(reader[4]);
                                stats.TotalOverdueAmount = Convert.ToDecimal(reader[5]);
                                stats.TotalCollected = Convert.ToDecimal(reader[6]);
                                stats.TotalRemaining = Convert.ToDecimal(reader[7]);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetInstallmentStatisticsAsync Error: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// الحصول على إحصائيات أقساط عميل محدد
        /// </summary>
        public async Task<CustomerInstallmentSummary> GetCustomerInstallmentSummaryAsync(int customerId)
        {
            var summary = new CustomerInstallmentSummary();

            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string customerSql = "SELECT CustomerNameAr, CustomerCode FROM Customers WHERE CustomerID = @customerId";
                        using (var cmd = new SQLiteCommand(customerSql, connection))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    summary.CustomerID = customerId;
                                    summary.CustomerName = reader.GetString(0);
                                    summary.CustomerCode = reader.GetString(1);
                                }
                            }
                        }

                        string sql = @"
                            SELECT 
                                COUNT(*) as Total,
                                SUM(CASE WHEN Status = 'Paid' THEN 1 ELSE 0 END) as Paid,
                                SUM(CASE WHEN Status = 'Overdue' THEN 1 ELSE 0 END) as Overdue,
                                SUM(CASE WHEN Status IN ('Pending', 'Partial') THEN 1 ELSE 0 END) as Pending,
                                SUM(InstallmentAmount) as TotalAmount,
                                SUM(PaidAmount) as TotalPaid,
                                SUM(RemainingAmount) as TotalRemaining,
                                SUM(CASE WHEN Status = 'Overdue' THEN RemainingAmount ELSE 0 END) as TotalOverdue
                            FROM Installments 
                            WHERE CustomerID = @customerId";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    summary.TotalInstallments = Convert.ToInt32(reader[0]);
                                    summary.PaidInstallments = Convert.ToInt32(reader[1]);
                                    summary.OverdueInstallments = Convert.ToInt32(reader[2]);
                                    summary.PendingInstallments = Convert.ToInt32(reader[3]);
                                    summary.TotalAmount = Convert.ToDecimal(reader[4]);
                                    summary.TotalPaid = Convert.ToDecimal(reader[5]);
                                    summary.TotalRemaining = Convert.ToDecimal(reader[6]);
                                    summary.TotalOverdue = Convert.ToDecimal(reader[7]);
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCustomerInstallmentSummaryAsync Error: {ex.Message}");
            }

            return summary;
        }

        /// <summary>
        /// تحديث حالة الأقساط المتأخرة (يتم استدعاؤها تلقائياً)
        /// </summary>
        public async Task UpdateOverdueInstallmentsAsync()
        {
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                            UPDATE Installments 
                            SET Status = 'Overdue',
                                ModifiedDate = CURRENT_TIMESTAMP
                            WHERE Status IN ('Pending', 'Partial')
                            AND DueDate < DATE('now')
                            AND RemainingAmount > 0";

                        using (var cmd = new SQLiteCommand(sql, connection))
                        {
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            System.Diagnostics.Debug.WriteLine($"✅ تم تحديث {rowsAffected} قسط متأخر");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateOverdueInstallmentsAsync Error: {ex.Message}");
            }
        }

        #endregion
    }
}