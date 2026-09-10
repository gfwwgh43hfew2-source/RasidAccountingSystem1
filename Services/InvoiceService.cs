using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using RasidAccountingSystem.Helpers;

namespace RasidAccountingSystem.Services
{
    public class InvoiceService
    {
        #region المتغيرات والمنشئ (Fields & Constructor)

        private readonly DatabaseService _databaseService;
        private readonly string _connectionString;

        public InvoiceService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _connectionString = databaseService.GetConnectionString();
        }

        #endregion

        #region دوال مساعدة أساسية (Basic Helper Methods)

        private decimal SafeToDecimal(object value, decimal defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            try { return Convert.ToDecimal(value); }
            catch { return defaultValue; }
        }

        private int SafeToInt(object value, int defaultValue = 0)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            try { return Convert.ToInt32(value); }
            catch { return defaultValue; }
        }

        private async Task<int> GetDefaultStoreIdAsync(SQLiteConnection connection, SQLiteTransaction transaction = null)
        {
            string sql = "SELECT StoreID FROM Stores WHERE IsActive = 1 LIMIT 1";
            using (var cmd = transaction != null ? new SQLiteCommand(sql, connection, transaction) : new SQLiteCommand(sql, connection))
            {
                object result = await cmd.ExecuteScalarAsync();
                return SafeToInt(result, 0);
            }
        }

        private async Task<int> GetDefaultTreasuryIdAsync(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "SELECT TreasuryID FROM Treasury WHERE IsActive = 1 LIMIT 1";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                object result = await cmd.ExecuteScalarAsync();
                return SafeToInt(result, 0);
            }
        }

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

        #endregion

        #region دوال الخزينة المتقدمة (Advanced Treasury Methods)

        private async Task<decimal> GetTreasuryBalanceAsync(SQLiteConnection connection, SQLiteTransaction transaction, int treasuryId)
        {
            string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Treasury WHERE TreasuryID = @treasuryId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                object result = await cmd.ExecuteScalarAsync();
                return SafeToDecimal(result);
            }
        }

        private async Task<bool> UpdateTreasuryBalanceDirectlyAsync(int treasuryId, decimal newBalance, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string sql = "UPDATE Treasury SET CurrentBalance = @newBalance, ModifiedDate = CURRENT_TIMESTAMP WHERE TreasuryID = @treasuryId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@newBalance", newBalance);
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                return await cmd.ExecuteNonQueryAsync() > 0;
            }
        }

        private async Task<decimal> GetCurrentTreasuryBalanceFromTransactionsAsync(SQLiteConnection connection, SQLiteTransaction transaction, int treasuryId)
        {
            string getCurrentBalanceSql = @"
                SELECT COALESCE(SUM(
                    CASE 
                        WHEN TransactionType = 'Receipt' THEN Amount
                        WHEN TransactionType = 'Payment' THEN -Amount
                        ELSE 0
                    END
                ), 0) as CurrentBalance
                FROM TreasuryTransactions 
                WHERE TreasuryID = @treasuryId";

            using (var cmd = new SQLiteCommand(getCurrentBalanceSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                object result = await cmd.ExecuteScalarAsync();
                return result != null ? Convert.ToDecimal(result) : 0;
            }
        }

        private async Task<bool> RecalculateAndUpdateTreasuryBalanceAsync(int treasuryId, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            try
            {
                decimal calculatedBalance = await GetCurrentTreasuryBalanceFromTransactionsAsync(connection, transaction, treasuryId);
                return await UpdateTreasuryBalanceDirectlyAsync(treasuryId, calculatedBalance, connection, transaction);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateAndUpdateTreasuryBalanceAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> AddTreasuryTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int treasuryId,
            DateTime transactionDate,
            string transactionType,
            decimal amount,
            string description,
            string referenceType,
            int referenceId,
            string referenceNumber,
            int createdBy)
        {
            try
            {
                decimal currentBalance = await GetCurrentTreasuryBalanceFromTransactionsAsync(connection, transaction, treasuryId);

                decimal newBalance;
                if (transactionType == "Receipt")
                {
                    newBalance = currentBalance + amount;
                }
                else if (transactionType == "Payment")
                {
                    newBalance = currentBalance - amount;
                }
                else
                {
                    newBalance = currentBalance;
                }

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
                    cmd.Parameters.AddWithValue("@balanceAfter", newBalance);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@refType", referenceType);
                    cmd.Parameters.AddWithValue("@refId", referenceId);
                    cmd.Parameters.AddWithValue("@refNumber", referenceNumber);
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    System.Diagnostics.Debug.WriteLine($"💰 إضافة حركة خزينة: {transactionType}, المبلغ: {amount}, الرصيد السابق: {currentBalance}, الرصيد الجديد: {newBalance}");
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddTreasuryTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال العملاء والموردين (Customer & Supplier Methods)

        private async Task<decimal> GetCustomerBalanceAsync(SQLiteConnection connection, SQLiteTransaction transaction, int customerId)
        {
            string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Customers WHERE CustomerID = @customerId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@customerId", customerId);
                object result = await cmd.ExecuteScalarAsync();
                return SafeToDecimal(result);
            }
        }

        private async Task<decimal> GetSupplierBalanceAsync(SQLiteConnection connection, SQLiteTransaction transaction, int supplierId)
        {
            string sql = "SELECT COALESCE(CurrentBalance, 0) FROM Suppliers WHERE SupplierID = @supplierId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                object result = await cmd.ExecuteScalarAsync();
                return SafeToDecimal(result);
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

        private async Task<bool> AddCustomerInvoiceTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int customerId,
            DateTime transactionDate,
            decimal amount,
            decimal balanceAfter,
            string description,
            int invoiceId,
            string invoiceNumber,
            int createdBy)
        {
            try
            {
                // ✅ إصلاح جذري (خطوة 1 من إصلاح كشف الحساب):
                // 1) كان المبلغ بيتسجل غلط في عمود CreditAmount بدل DebitAmount لحركة من نوع 'Invoice'
                //    (ده اللي كان بيخلي كل فاتورة تظهر في الكشف وكأنها "تحصيل" مش "بيع").
                // 2) الدالة بقت Idempotent: بتمسح أي صف "Invoice" سابق لنفس الفاتورة قبل ما تضيف
                //    الصف الصحيح، فمفيش احتمال يتسجل صفين لنفس الفاتورة تاني (سواء من الـ Trigger
                //    trig_customer_transaction_from_sales أو من استدعاء يدوي سابق أو من تعديل الفاتورة
                //    أكتر من مرة) - بغض النظر هل كانت الحركة موجودة قبل كده ولا لأ.
                string deleteSql = @"
                    DELETE FROM CustomerTransactions
                    WHERE ReferenceType = 'SALES_INVOICE'
                      AND ReferenceID = @invoiceId
                      AND TransactionType = 'Invoice'";

                using (var deleteCmd = new SQLiteCommand(deleteSql, connection, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    int deletedRows = await deleteCmd.ExecuteNonQueryAsync();
                    if (deletedRows > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"🧹 تم حذف {deletedRows} صف حركة 'Invoice' قديم/مكرر للفاتورة {invoiceNumber} قبل إعادة التسجيل الصحيح");
                    }
                }

                string sql = @"
                    INSERT INTO CustomerTransactions (
                        CustomerID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @customerId, @date, 'Invoice',
                        @amount, 0, @balanceAfter,
                        'SALES_INVOICE', @invoiceId, @invoiceNumber, @description, @createdBy, CURRENT_TIMESTAMP
                    )";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    cmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@balanceAfter", balanceAfter);
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                // ✅ إصلاح: كانت الدالة بترجع false بهدوء من غير ما توقف حفظ الفاتورة، فيبقى
                // ممكن الفاتورة تتحفظ وتتحسب في رصيد العميل من غير أي أثر لها في كشف الحساب.
                // دلوقتي بنعيد رمي الاستثناء عشان المعاملة اللي بتحيط بحفظ الفاتورة (Transaction)
                // تتراجع بالكامل (Rollback) لو فشل تسجيل الحركة، فمفيش فاتورة تتحفظ من غير حركة
                // مقابلة لها في الكشف.
                System.Diagnostics.Debug.WriteLine($"AddCustomerInvoiceTransactionAsync Error: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> AddCustomerReceiptTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int customerId,
            DateTime transactionDate,
            decimal amount,
            decimal balanceAfter,
            string description,
            int voucherId,
            string voucherNumber,
            int createdBy)
        {
            try
            {
                string sql = @"
                    INSERT INTO CustomerTransactions (
                        CustomerID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @customerId, @date, 'Receipt',
                        0, @amount, @balanceAfter,
                        'RECEIPT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                    )";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    cmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@balanceAfter", balanceAfter);
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddCustomerReceiptTransactionAsync Error: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> AddSupplierPurchaseTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int supplierId,
            DateTime transactionDate,
            decimal amount,
            decimal balanceAfter,
            string description,
            int invoiceId,
            string invoiceNumber,
            int createdBy)
        {
            try
            {
                string sql = @"
                    INSERT INTO SupplierTransactions (
                        SupplierID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @supplierId, @date, 'Purchase',
                        0, @amount, @balanceAfter,
                        'PURCHASE_INVOICE', @invoiceId, @invoiceNumber, @description, @createdBy, CURRENT_TIMESTAMP
                    )";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@supplierId", supplierId);
                    cmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@balanceAfter", balanceAfter);
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddSupplierPurchaseTransactionAsync Error: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> AddSupplierPaymentTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int supplierId,
            DateTime transactionDate,
            decimal amount,
            decimal balanceAfter,
            string description,
            int voucherId,
            string voucherNumber,
            int createdBy)
        {
            try
            {
                string sql = @"
                    INSERT INTO SupplierTransactions (
                        SupplierID, TransactionDate, TransactionType,
                        DebitAmount, CreditAmount, BalanceAfter,
                        ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                    ) VALUES (
                        @supplierId, @date, 'Payment',
                        @amount, 0, @balanceAfter,
                        'PAYMENT_VOUCHER', @voucherId, @voucherNumber, @description, @createdBy, CURRENT_TIMESTAMP
                    )";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@supplierId", supplierId);
                    cmd.Parameters.AddWithValue("@date", transactionDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@amount", amount);
                    cmd.Parameters.AddWithValue("@balanceAfter", balanceAfter);
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                    cmd.Parameters.AddWithValue("@description", description ?? "");
                    cmd.Parameters.AddWithValue("@createdBy", createdBy);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddSupplierPaymentTransactionAsync Error: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> DeleteCustomerInvoiceTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int invoiceId)
        {
            try
            {
                string sql = @"
                    DELETE FROM CustomerTransactions 
                    WHERE ReferenceType = 'SALES_INVOICE' AND ReferenceID = @invoiceId";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ تم حذف {rowsAffected} حركة فاتورة من CustomerTransactions للفاتورة ID {invoiceId}");
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteCustomerInvoiceTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeleteSupplierPurchaseTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int invoiceId)
        {
            try
            {
                string sql = @"
                    DELETE FROM SupplierTransactions 
                    WHERE ReferenceType = 'PURCHASE_INVOICE' AND ReferenceID = @invoiceId";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ تم حذف {rowsAffected} حركة Purchase من SupplierTransactions للفاتورة ID {invoiceId}");
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteSupplierPurchaseTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeleteSupplierPaymentTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int voucherId)
        {
            try
            {
                string sql = @"
                    DELETE FROM SupplierTransactions 
                    WHERE ReferenceType = 'PAYMENT_VOUCHER' AND ReferenceID = @voucherId";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteSupplierPaymentTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeleteCustomerReceiptTransactionAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int voucherId)
        {
            try
            {
                string sql = @"
                    DELETE FROM CustomerTransactions 
                    WHERE ReferenceType = 'RECEIPT_VOUCHER' AND ReferenceID = @voucherId";

                using (var cmd = new SQLiteCommand(sql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteCustomerReceiptTransactionAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<int> GetSupplierPurchaseTransactionCountAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int invoiceId)
        {
            string sql = @"
                SELECT COUNT(*) FROM SupplierTransactions 
                WHERE ReferenceType = 'PURCHASE_INVOICE' AND ReferenceID = @invoiceId";

            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                object result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        // ملحوظة (خطوة 1): دالة GetCustomerInvoiceTransactionCountAsync اتشالت من هنا - كانت بتُستخدم
        // كفحص قبل إضافة حركة الفاتورة لمنع التكرار، لكن الفحص ده كان بيفشل عمليًا (راجع كل الفواتير
        // في النسخة الاحتياطية اللي كانت مسجَّلة مرتين). الحل الأضمن دلوقتي إن AddCustomerInvoiceTransactionAsync
        // نفسها بقت تمسح أي حركة قديمة لنفس الفاتورة قبل ما تضيف الجديدة، فمفيش داعي لفحص خارجي خالص.

        private async Task RecalculateCustomerBalanceFromTransactions(SQLiteConnection connection, SQLiteTransaction transaction, int customerId)
        {
            try
            {
                // ✅ إصلاح (خطوة 1): كانت الدالة بتجمع عمود CreditAmount لحركات النوع 'Invoice' بدل
                // DebitAmount - فكانت النتيجة إنها بتجمع دايمًا صفر لحركات الفواتير الصحيحة (لأن الفاتورة
                // الصحيحة بتسجل مبلغها في DebitAmount مش CreditAmount)، وكانت بتظبط بالصدفة بس لما كان
                // فيه صف مكرر وغلط بيسجل المبلغ في CreditAmount (نفس الخطأ اللي اتصلح في
                // AddCustomerInvoiceTransactionAsync). دلوقتي بعد إصلاح مصدر البيانات، لازم الحساب هنا
                // يتصلح كمان عشان يفضل صحيح.
                string recalcSql = @"
                    UPDATE Customers SET 
                        CurrentBalance = COALESCE((
                            SELECT OpeningBalance + 
                                (SELECT COALESCE(SUM(DebitAmount), 0) FROM CustomerTransactions WHERE CustomerID = @customerId AND TransactionType = 'Invoice') -
                                (SELECT COALESCE(SUM(CreditAmount), 0) FROM CustomerTransactions WHERE CustomerID = @customerId AND TransactionType = 'Receipt')
                        ), OpeningBalance)
                    WHERE CustomerID = @customerId";

                using (var cmd = new SQLiteCommand(recalcSql, connection))
                {
                    cmd.Transaction = transaction;
                    cmd.Parameters.AddWithValue("@customerId", customerId);
                    int rows = await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ إعادة حساب رصيد العميل {customerId} من الحركات: {rows} صف متأثر");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecalculateCustomerBalanceFromTransactions Error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region دوال حذف الأقساط (Delete Installments)

        private async Task<bool> DeleteInstallmentsByInvoiceIdAsync(SQLiteConnection connection, SQLiteTransaction transaction, int invoiceId)
        {
            try
            {
                string deletePaymentsSql = @"
                    DELETE FROM InstallmentPayments 
                    WHERE InstallmentID IN (SELECT InstallmentID FROM Installments WHERE InvoiceID = @invoiceId)";

                using (var cmd = new SQLiteCommand(deletePaymentsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    int paymentsDeleted = await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ تم حذف {paymentsDeleted} سجل تحصيل أقساط للفاتورة ID {invoiceId}");
                }

                string deleteInstallmentsSql = "DELETE FROM Installments WHERE InvoiceID = @invoiceId";
                using (var cmd = new SQLiteCommand(deleteInstallmentsSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    int installmentsDeleted = await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"✅ تم حذف {installmentsDeleted} قسط للفاتورة ID {invoiceId}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteInstallmentsByInvoiceIdAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region دوال المخزون (Inventory Methods) - المعدلة لدعم نظام الوحدات والسماح بالبيع حتى لو الرصيد غير كافٍ

        /// <summary>
        /// التحقق من كمية المخزون المتاحة - تحذير فقط ولا يمنع الحفظ
        /// </summary>
        private async Task<bool> CheckInventoryQuantityAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int storeId,
            int productId,
            decimal requiredQuantityInBaseUnit,
            string productName)
        {
            string sql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
            using (var cmd = new SQLiteCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@storeId", storeId);
                cmd.Parameters.AddWithValue("@productId", productId);
                object result = await cmd.ExecuteScalarAsync();
                decimal currentQuantity = SafeToDecimal(result);

                System.Diagnostics.Debug.WriteLine($"🔍 التحقق من المخزون: ProductID={productId}, المطلوب={requiredQuantityInBaseUnit}, المتوفر={currentQuantity}");

                if (currentQuantity < requiredQuantityInBaseUnit)
                {
                    string productNameFinal = productName;
                    if (string.IsNullOrEmpty(productNameFinal))
                    {
                        string getNameSql = "SELECT ProductNameAr FROM Products WHERE ProductID = @productId";
                        using (var nameCmd = new SQLiteCommand(getNameSql, connection, transaction))
                        {
                            nameCmd.Parameters.AddWithValue("@productId", productId);
                            object nameResult = await nameCmd.ExecuteScalarAsync();
                            productNameFinal = nameResult?.ToString() ?? "غير معروف";
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"⚠️ تحذير: الكمية غير كافية للمنتج {productNameFinal}. المتوفر: {currentQuantity}, المطلوب: {requiredQuantityInBaseUnit}");
                    return true;
                }
                return true;
            }
        }

        /// <summary>
        /// تحديث المخزون لفواتير المشتريات مع دعم نظام الوحدات
        /// الكمية المستخدمة هي الكمية المحولة إلى الوحدة الأساسية
        /// </summary>
        private async Task<bool> UpdateInventoryForPurchaseAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int storeId,
            List<PurchaseInvoiceItemClass> items,
            int invoiceId,
            string invoiceNumber,
            DateTime invoiceDate,
            int createdBy,
            bool isAdding)
        {
            try
            {
                foreach (var item in items)
                {
                    decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;

                    System.Diagnostics.Debug.WriteLine($"📦 تحديث المخزون (شراء): ProductID={item.ProductID}, Quantity={item.Quantity}, QuantityInBaseUnit={item.QuantityInBaseUnit}, المستخدمة={quantityInBaseUnit}");

                    if (isAdding)
                    {
                        string updateInventorySql = @"
                            INSERT INTO StoreInventory (StoreID, ProductID, Quantity, AvailableQuantity, CostPrice, LastUpdated)
                            VALUES (@storeId, @productId, @quantity, @quantity, @unitPrice, CURRENT_TIMESTAMP)
                            ON CONFLICT(StoreID, ProductID) DO UPDATE SET
                                Quantity = Quantity + @quantity,
                                AvailableQuantity = AvailableQuantity + @quantity,
                                CostPrice = (CostPrice * Quantity + @unitPrice * @quantity) / (Quantity + @quantity),
                                LastUpdated = CURRENT_TIMESTAMP";

                        using (var cmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string inventoryTransactionSql = @"
                            INSERT INTO InventoryTransactions (
                                StoreID, ProductID, TransactionDate, TransactionType,
                                Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                                ReferenceType, ReferenceID, ReferenceNumber, CreatedBy
                            ) VALUES (
                                @storeId, @productId, @transactionDate, 'Purchase',
                                @quantity,
                                (SELECT COALESCE(Quantity, 0) - @quantity FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId),
                                (SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId),
                                @unitPrice, @totalAmount,
                                'PURCHASE_INVOICE', @invoiceId, @invoiceNumber, @createdBy
                            );";

                        using (var cmd = new SQLiteCommand(inventoryTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@transactionDate", invoiceDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string updateProductQuantitySql = @"
                            UPDATE Products 
                            SET QuantityInBaseUnit = QuantityInBaseUnit + @quantity
                            WHERE ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        string updateInventorySql = @"
                            UPDATE StoreInventory 
                            SET Quantity = Quantity - @quantity,
                                AvailableQuantity = AvailableQuantity - @quantity,
                                LastUpdated = CURRENT_TIMESTAMP
                            WHERE StoreID = @storeId AND ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string updateProductQuantitySql = @"
                            UPDATE Products 
                            SET QuantityInBaseUnit = QuantityInBaseUnit - @quantity
                            WHERE ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateInventoryForPurchaseAsync Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// تحديث المخزون لفواتير المبيعات مع دعم نظام الوحدات - يسمح بقيم سالبة
        /// </summary>
        /// <summary>
        /// تحديث المخزون لفواتير المبيعات مع دعم نظام الوحدات - يسمح بقيم سالبة
        /// </summary>
        private async Task<bool> UpdateInventoryForSaleAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int storeId,
            List<SalesInvoiceItemClass> items,
            int invoiceId,
            string invoiceNumber,
            DateTime invoiceDate,
            int createdBy,
            bool isAdding)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📦 بدء تحديث المخزون للبيع: InvoiceID={invoiceId}, isAdding={isAdding}");

                foreach (var item in items)
                {
                    decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;

                    System.Diagnostics.Debug.WriteLine($"📦 تحديث المخزون (بيع): ProductID={item.ProductID}, QuantityInBaseUnit={quantityInBaseUnit}");

                    // ✅ الحصول على الكمية الحالية قبل التحديث
                    decimal currentQuantity = 0;
                    string getCurrentQuantitySql = "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId";
                    using (var getCmd = new SQLiteCommand(getCurrentQuantitySql, connection, transaction))
                    {
                        getCmd.Parameters.AddWithValue("@storeId", storeId);
                        getCmd.Parameters.AddWithValue("@productId", item.ProductID);
                        object result = await getCmd.ExecuteScalarAsync();
                        currentQuantity = SafeToDecimal(result);
                    }

                    if (isAdding)
                    {
                        decimal quantityAfter = currentQuantity - quantityInBaseUnit;

                        // ✅ تحديث المخزون (خصم الكمية)
                        string updateInventorySql = @"
                    UPDATE StoreInventory 
                    SET Quantity = Quantity - @quantity,
                        AvailableQuantity = AvailableQuantity - @quantity,
                        LastUpdated = CURRENT_TIMESTAMP
                    WHERE StoreID = @storeId AND ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            System.Diagnostics.Debug.WriteLine($"✅ تم تحديث المخزون: {rowsAffected} صف متأثر");
                        }

                        // ✅ تسجيل حركة المخزون (مع قيم QuantityBefore و QuantityAfter)
                        string inventoryTransactionSql = @"
                    INSERT INTO InventoryTransactions (
                        StoreID, ProductID, TransactionDate, TransactionType,
                        Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                        ReferenceType, ReferenceID, ReferenceNumber, CreatedBy
                    ) VALUES (
                        @storeId, @productId, @transactionDate, 'Sale',
                        -@quantity, @quantityBefore, @quantityAfter, @unitPrice, @totalAmount,
                        'SALES_INVOICE', @invoiceId, @invoiceNumber, @createdBy
                    );";

                        using (var cmd = new SQLiteCommand(inventoryTransactionSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@transactionDate", invoiceDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@quantityBefore", currentQuantity);
                            cmd.Parameters.AddWithValue("@quantityAfter", quantityAfter);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ✅ تحديث الكمية بالوحدة الأساسية في جدول المنتجات
                        string updateProductQuantitySql = @"
                    UPDATE Products 
                    SET QuantityInBaseUnit = COALESCE(QuantityInBaseUnit, 0) - @quantity
                    WHERE ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            System.Diagnostics.Debug.WriteLine($"✅ تم تحديث كمية المنتج في Products: {rowsAffected} صف متأثر");
                        }
                    }
                    else
                    {
                        // ✅ عند حذف فاتورة بيع: إعادة الكمية إلى المخزون
                        decimal quantityAfter = currentQuantity + quantityInBaseUnit;

                        string updateInventorySql = @"
                    UPDATE StoreInventory 
                    SET Quantity = Quantity + @quantity,
                        AvailableQuantity = AvailableQuantity + @quantity,
                        LastUpdated = CURRENT_TIMESTAMP
                    WHERE StoreID = @storeId AND ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ✅ تحديث الكمية بالوحدة الأساسية في جدول المنتجات (إضافة)
                        string updateProductQuantitySql = @"
                    UPDATE Products 
                    SET QuantityInBaseUnit = COALESCE(QuantityInBaseUnit, 0) + @quantity
                    WHERE ProductID = @productId";

                        using (var cmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحديث المخزون بنجاح");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ UpdateInventoryForSaleAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        #region دوال السندات (Voucher Methods)

        private async Task<int> CreateReceiptVoucherAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string invoiceNumber,
            DateTime invoiceDate,
            int customerId,
            decimal totalAmount,
            string paymentMethod,
            string checkNumber,
            DateTime? checkDate,
            string bankName,
            string referenceNumber,
            int treasuryId,
            int createdBy)
        {
            string voucherNumber = await _databaseService.GenerateReceiptVoucherNumberAsync();

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

            int newVoucherId = 0;
            using (var cmd = new SQLiteCommand(insertVoucherSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                cmd.Parameters.AddWithValue("@voucherDate", invoiceDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@customerId", customerId);
                cmd.Parameters.AddWithValue("@amount", totalAmount);
                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                cmd.Parameters.AddWithValue("@checkDate", checkDate.HasValue ? checkDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@bankName", bankName ?? "");
                cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? invoiceNumber);
                cmd.Parameters.AddWithValue("@description", $"تحصيل قيمة فاتورة رقم {invoiceNumber}");
                cmd.Parameters.AddWithValue("@createdBy", createdBy);

                object result = await cmd.ExecuteScalarAsync();
                newVoucherId = SafeToInt(result);
            }

            System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء سند قبض جديد رقم {voucherNumber} (ID: {newVoucherId})");
            return newVoucherId;
        }

        private async Task<int> CreatePaymentVoucherAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string invoiceNumber,
            DateTime invoiceDate,
            int supplierId,
            decimal totalAmount,
            string paymentMethod,
            string checkNumber,
            DateTime? checkDate,
            string bankName,
            string referenceNumber,
            int treasuryId,
            int createdBy)
        {
            string voucherNumber = await _databaseService.GeneratePaymentVoucherNumberAsync();

            string insertVoucherSql = @"
                INSERT INTO PaymentVouchers (
                    VoucherNumber, VoucherDate, SupplierID, Amount, PaymentMethod,
                    TreasuryID, CheckNumber, CheckDate, BankName,
                    ReferenceNumber, Description, IsPosted, CreatedBy, CreatedDate
                ) VALUES (
                    @voucherNumber, @voucherDate, @supplierId, @amount, @paymentMethod,
                    @treasuryId, @checkNumber, @checkDate, @bankName,
                    @referenceNumber, @description, 1, @createdBy, CURRENT_TIMESTAMP
                );
                SELECT last_insert_rowid();";

            int newVoucherId = 0;
            using (var cmd = new SQLiteCommand(insertVoucherSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@voucherNumber", voucherNumber);
                cmd.Parameters.AddWithValue("@voucherDate", invoiceDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@supplierId", supplierId);
                cmd.Parameters.AddWithValue("@amount", totalAmount);
                cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                cmd.Parameters.AddWithValue("@treasuryId", treasuryId);
                cmd.Parameters.AddWithValue("@checkNumber", checkNumber ?? "");
                cmd.Parameters.AddWithValue("@checkDate", checkDate.HasValue ? checkDate.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@bankName", bankName ?? "");
                cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? invoiceNumber);
                cmd.Parameters.AddWithValue("@description", $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}");
                cmd.Parameters.AddWithValue("@createdBy", createdBy);

                object result = await cmd.ExecuteScalarAsync();
                newVoucherId = SafeToInt(result);
            }

            System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء سند صرف جديد رقم {voucherNumber} (ID: {newVoucherId})");
            return newVoucherId;
        }

        private async Task<bool> DeleteReceiptVoucherAndRelatedAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int voucherId,
            int treasuryId,
            string invoiceNumber,
            int customerId)
        {
            try
            {
                string deleteTreasuryByVoucherId = @"
                    DELETE FROM TreasuryTransactions 
                    WHERE ReferenceID = @voucherId 
                    AND ReferenceType = 'RECEIPT_VOUCHER'";

                using (var cmd = new SQLiteCommand(deleteTreasuryByVoucherId, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    await cmd.ExecuteNonQueryAsync();
                }

                string deleteTreasuryByInvoiceNumber = @"
                    DELETE FROM TreasuryTransactions 
                    WHERE ReferenceNumber = @invoiceNumber";

                using (var cmd = new SQLiteCommand(deleteTreasuryByInvoiceNumber, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    await cmd.ExecuteNonQueryAsync();
                }

                await DeleteCustomerReceiptTransactionAsync(connection, transaction, voucherId);

                string deleteVoucherSql = "DELETE FROM ReceiptVouchers WHERE VoucherID = @voucherId";
                using (var cmd = new SQLiteCommand(deleteVoucherSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await RecalculateAndUpdateTreasuryBalanceAsync(treasuryId, connection, transaction);

                System.Diagnostics.Debug.WriteLine($"✅ تم حذف سند القبض ID: {voucherId} وجميع آثاره");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteReceiptVoucherAndRelatedAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DeletePaymentVoucherAndRelatedAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            int voucherId,
            int treasuryId,
            string invoiceNumber)
        {
            try
            {
                string deleteTreasuryByVoucherId = @"
                    DELETE FROM TreasuryTransactions 
                    WHERE ReferenceID = @voucherId 
                    AND ReferenceType = 'PAYMENT_VOUCHER'";

                using (var cmd = new SQLiteCommand(deleteTreasuryByVoucherId, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    await cmd.ExecuteNonQueryAsync();
                }

                string deleteTreasuryByInvoiceNumber = @"
                    DELETE FROM TreasuryTransactions 
                    WHERE ReferenceNumber = @invoiceNumber";

                using (var cmd = new SQLiteCommand(deleteTreasuryByInvoiceNumber, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                    await cmd.ExecuteNonQueryAsync();
                }

                await DeleteSupplierPaymentTransactionAsync(connection, transaction, voucherId);

                string deleteVoucherSql = "DELETE FROM PaymentVouchers WHERE VoucherID = @voucherId";
                using (var cmd = new SQLiteCommand(deleteVoucherSql, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@voucherId", voucherId);
                    await cmd.ExecuteNonQueryAsync();
                }

                await RecalculateAndUpdateTreasuryBalanceAsync(treasuryId, connection, transaction);

                System.Diagnostics.Debug.WriteLine($"✅ تم حذف سند الصرف ID: {voucherId} وجميع آثاره");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeletePaymentVoucherAndRelatedAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 1. فواتير المشتريات - حفظ (Save Purchase Invoice)

        public async Task<bool> SavePurchaseInvoiceAsync(
            string invoiceNumber,
            DateTime invoiceDate,
            int supplierId,
            int storeId,
            decimal subTotal,
            decimal discountAmount,
            decimal taxAmount,
            decimal taxPercent,
            decimal totalAmount,
            string paymentMethod,
            string referenceNumber,
            string notes,
            List<PurchaseInvoiceItemClass> items,
            int createdBy,
            int? treasuryId = null,
            string checkNumber = null,
            DateTime? checkDate = null,
            string bankName = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                MessageBox.Show("رقم الفاتورة لا يمكن أن يكون فارغاً", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (supplierId <= 0)
            {
                MessageBox.Show("معرف المورد غير صالح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (items == null || items.Count == 0)
            {
                MessageBox.Show("لا توجد عناصر في الفاتورة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (createdBy <= 0) createdBy = 1;

            bool exists = await _databaseService.PurchaseInvoiceNumberExistsAsync(invoiceNumber);
            if (exists)
            {
                MessageBox.Show($"الفاتورة {invoiceNumber} موجودة مسبقاً", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                return await DatabaseExecutor.ExecuteTransactionAsync(_databaseService, async (connection, transaction) =>
                {
                    int finalStoreId = storeId;
                    if (finalStoreId <= 0)
                    {
                        finalStoreId = await GetDefaultStoreIdAsync(connection, transaction);
                        if (finalStoreId <= 0)
                        {
                            throw new Exception("لا يوجد مخزن نشط في النظام");
                        }
                    }

                    int invoiceId = 0;

                    string invoiceSql = @"
                        INSERT INTO PurchaseInvoices (
                            InvoiceNumber, InvoiceDate, SupplierID, StoreID, InvoiceType,
                            SubTotal, DiscountAmount, TaxAmount, TaxPercent,
                            TotalAmount, PaidAmount, RemainingAmount,
                            PaymentMethod, ReferenceNumber, IsPosted,
                            Notes, CreatedDate, CreatedBy
                        ) VALUES (
                            @invoiceNumber, @invoiceDate, @supplierId, @storeId, 'Purchase',
                            @subTotal, @discountAmount, @taxAmount, @taxPercent,
                            @totalAmount, 0, @totalAmount,
                            @paymentMethod, @referenceNumber, 1,
                            @notes, CURRENT_TIMESTAMP, @createdBy
                        );
                        SELECT last_insert_rowid();";

                    using (var cmd = new SQLiteCommand(invoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        cmd.Parameters.AddWithValue("@invoiceDate", invoiceDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        cmd.Parameters.AddWithValue("@storeId", finalStoreId);
                        cmd.Parameters.AddWithValue("@subTotal", subTotal);
                        cmd.Parameters.AddWithValue("@discountAmount", discountAmount);
                        cmd.Parameters.AddWithValue("@taxAmount", taxAmount);
                        cmd.Parameters.AddWithValue("@taxPercent", taxPercent);
                        cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                        cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? "");
                        cmd.Parameters.AddWithValue("@notes", notes ?? "");
                        cmd.Parameters.AddWithValue("@createdBy", createdBy);

                        object result = await cmd.ExecuteScalarAsync();
                        invoiceId = SafeToInt(result);
                    }

                    if (invoiceId == 0)
                    {
                        throw new Exception("فشل في إدراج الفاتورة");
                    }

                    string itemSql = @"
                        INSERT INTO PurchaseInvoiceItems (
                            InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                            DiscountPercent, DiscountAmount, TotalAmount
                        ) VALUES (
                            @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                            @discountPercent, @discountAmount, @totalAmount
                        );";

                    foreach (var item in items)
                    {
                        using (var cmd = new SQLiteCommand(itemSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@quantityInBaseUnit", item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@discountPercent", item.DiscountPercent);
                            cmd.Parameters.AddWithValue("@discountAmount", item.DiscountAmount);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await UpdateInventoryForPurchaseAsync(connection, transaction, finalStoreId, items, invoiceId, invoiceNumber, invoiceDate, createdBy, true);

                    decimal currentSupplierBalance = await GetSupplierBalanceAsync(connection, transaction, supplierId);
                    decimal supplierBalanceAfterInvoice = currentSupplierBalance + totalAmount;

                    int existingCount = await GetSupplierPurchaseTransactionCountAsync(connection, transaction, invoiceId);

                    if (existingCount == 0)
                    {
                        await AddSupplierPurchaseTransactionAsync(
                            connection, transaction,
                            supplierId,
                            invoiceDate,
                            totalAmount,
                            supplierBalanceAfterInvoice,
                            notes ?? "",
                            invoiceId,
                            invoiceNumber,
                            createdBy);

                        await UpdateSupplierBalanceAsync(supplierId, supplierBalanceAfterInvoice, connection, transaction);
                        System.Diagnostics.Debug.WriteLine($"✅ إضافة حركة فاتورة المورد: {currentSupplierBalance} → {supplierBalanceAfterInvoice}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ حركة الفاتورة {invoiceNumber} موجودة مسبقاً، تم تخطي الإضافة");
                    }

                    if (paymentMethod != "Credit")
                    {
                        int finalTreasuryId = treasuryId ?? await GetDefaultTreasuryIdAsync(connection, transaction);
                        if (finalTreasuryId == 0)
                        {
                            throw new Exception("لا توجد خزينة متاحة للدفع");
                        }

                        decimal currentTreasuryBalance = await GetTreasuryBalanceAsync(connection, transaction, finalTreasuryId);
                        if (currentTreasuryBalance < totalAmount)
                        {
                            throw new Exception($"رصيد الخزينة غير كافٍ!\nالرصيد الحالي: {currentTreasuryBalance:N2}\nالمبلغ المطلوب: {totalAmount:N2}");
                        }

                        int newVoucherId = await CreatePaymentVoucherAsync(
                            connection, transaction,
                            invoiceNumber, invoiceDate, supplierId, totalAmount,
                            paymentMethod, checkNumber, checkDate, bankName,
                            invoiceNumber, finalTreasuryId, createdBy);

                        if (newVoucherId == 0)
                        {
                            throw new Exception("فشل في إنشاء سند الصرف");
                        }

                        await AddTreasuryTransactionAsync(
                            connection, transaction,
                            finalTreasuryId,
                            invoiceDate,
                            "Payment",
                            totalAmount,
                            $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}",
                            "PURCHASE_INVOICE",
                            invoiceId,
                            invoiceNumber,
                            createdBy);

                        await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);

                        decimal supplierBalanceAfterPayment = supplierBalanceAfterInvoice - totalAmount;

                        await AddSupplierPaymentTransactionAsync(
                            connection, transaction,
                            supplierId,
                            invoiceDate,
                            totalAmount,
                            supplierBalanceAfterPayment,
                            $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}",
                            newVoucherId,
                            invoiceNumber,
                            createdBy);

                        await UpdateSupplierBalanceAsync(supplierId, supplierBalanceAfterPayment, connection, transaction);

                        System.Diagnostics.Debug.WriteLine($"✅ إضافة حركة دفع للمورد: {supplierBalanceAfterInvoice} → {supplierBalanceAfterPayment}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ فاتورة أجل - رصيد المورد النهائي: {supplierBalanceAfterInvoice}");
                    }

                    return true;

                }, CancellationToken.None, 5);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
            {
                MessageBox.Show($"تعذر حفظ الفاتورة بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.\n\nالتفاصيل: {ex.Message}",
                                "قاعدة البيانات مشغولة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ SavePurchaseInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"SavePurchaseInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        #region 2. فواتير المشتريات - تحديث (Update Purchase Invoice)

        public async Task<bool> UpdatePurchaseInvoiceAsync(
            int invoiceId,
            string invoiceNumber,
            DateTime invoiceDate,
            int supplierId,
            int storeId,
            decimal subTotal,
            decimal discountAmount,
            decimal taxAmount,
            decimal taxPercent,
            decimal totalAmount,
            string paymentMethod,
            string referenceNumber,
            string notes,
            List<PurchaseInvoiceItemClass> items,
            int modifiedBy,
            int? treasuryId = null,
            string checkNumber = null,
            DateTime? checkDate = null,
            string bankName = null)
        {
            if (invoiceId <= 0)
            {
                MessageBox.Show("معرف الفاتورة غير صالح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (items == null || items.Count == 0)
            {
                MessageBox.Show("لا توجد عناصر في الفاتورة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (modifiedBy <= 0) modifiedBy = 1;

            try
            {
                return await DatabaseExecutor.ExecuteTransactionAsync(_databaseService, async (connection, transaction) =>
                {
                    int finalStoreId = storeId;
                    if (finalStoreId <= 0)
                    {
                        finalStoreId = await GetDefaultStoreIdAsync(connection, transaction);
                        if (finalStoreId <= 0)
                        {
                            throw new Exception("لا يوجد مخزن نشط في النظام");
                        }
                    }

                    decimal oldTotalAmount = 0;
                    string oldPaymentMethod = "";
                    int oldVoucherId = 0;
                    int oldTreasuryId = 0;
                    decimal oldVoucherAmount = 0;

                    string getOldInvoiceSql = @"
                        SELECT TotalAmount, PaymentMethod 
                        FROM PurchaseInvoices 
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(getOldInvoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                oldTotalAmount = reader.GetDecimal(0);
                                oldPaymentMethod = reader.GetString(1);
                            }
                        }
                    }

                    string findOldVoucherSql = @"
                        SELECT VoucherID, TreasuryID, Amount 
                        FROM PaymentVouchers 
                        WHERE ReferenceNumber = @invoiceNumber
                        ORDER BY VoucherID DESC LIMIT 1";

                    using (var cmd = new SQLiteCommand(findOldVoucherSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                oldVoucherId = reader.GetInt32(0);
                                oldTreasuryId = reader.GetInt32(1);
                                oldVoucherAmount = reader.GetDecimal(2);
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"===== تحديث فاتورة المشتريات {invoiceNumber} =====");
                    System.Diagnostics.Debug.WriteLine($"القديم: المبلغ={oldTotalAmount}, طريقة الدفع={oldPaymentMethod}");
                    System.Diagnostics.Debug.WriteLine($"الجديد: المبلغ={totalAmount}, طريقة الدفع={paymentMethod}");

                    if (oldPaymentMethod != "Credit" && paymentMethod != "Credit")
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 سيناريو: نقدي → نقدي");

                        if (oldVoucherId > 0 && oldTreasuryId > 0)
                        {
                            await DeletePaymentVoucherAndRelatedAsync(connection, transaction, oldVoucherId, oldTreasuryId, invoiceNumber);
                        }

                        int finalTreasuryId = treasuryId ?? (oldTreasuryId > 0 ? oldTreasuryId : await GetDefaultTreasuryIdAsync(connection, transaction));

                        decimal currentTreasuryBalance = await GetTreasuryBalanceAsync(connection, transaction, finalTreasuryId);
                        if (currentTreasuryBalance < totalAmount)
                        {
                            throw new Exception($"رصيد الخزينة غير كافٍ!\nالرصيد الحالي: {currentTreasuryBalance:N2}\nالمبلغ المطلوب: {totalAmount:N2}");
                        }

                        int newVoucherId = await CreatePaymentVoucherAsync(
                            connection, transaction,
                            invoiceNumber, invoiceDate, supplierId, totalAmount,
                            paymentMethod, checkNumber, checkDate, bankName,
                            invoiceNumber, finalTreasuryId, modifiedBy);

                        await AddTreasuryTransactionAsync(
                            connection, transaction,
                            finalTreasuryId,
                            invoiceDate,
                            "Payment",
                            totalAmount,
                            $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}",
                            "PURCHASE_INVOICE",
                            invoiceId,
                            invoiceNumber,
                            modifiedBy);

                        await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);
                    }
                    else if (oldPaymentMethod != "Credit" && paymentMethod == "Credit")
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 سيناريو: نقدي → أجل");

                        if (oldVoucherId > 0 && oldTreasuryId > 0)
                        {
                            await DeletePaymentVoucherAndRelatedAsync(connection, transaction, oldVoucherId, oldTreasuryId, invoiceNumber);
                        }
                    }
                    else if (oldPaymentMethod == "Credit" && paymentMethod != "Credit")
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 سيناريو: أجل → نقدي");

                        int finalTreasuryId = treasuryId ?? await GetDefaultTreasuryIdAsync(connection, transaction);
                        if (finalTreasuryId == 0)
                        {
                            throw new Exception("لا توجد خزينة متاحة للدفع");
                        }

                        decimal currentTreasuryBalance = await GetTreasuryBalanceAsync(connection, transaction, finalTreasuryId);
                        if (currentTreasuryBalance < totalAmount)
                        {
                            throw new Exception($"رصيد الخزينة غير كافٍ!\nالرصيد الحالي: {currentTreasuryBalance:N2}\nالمبلغ المطلوب: {totalAmount:N2}");
                        }

                        int newVoucherId = await CreatePaymentVoucherAsync(
                            connection, transaction,
                            invoiceNumber, invoiceDate, supplierId, totalAmount,
                            paymentMethod, checkNumber, checkDate, bankName,
                            invoiceNumber, finalTreasuryId, modifiedBy);

                        await AddTreasuryTransactionAsync(
                            connection, transaction,
                            finalTreasuryId,
                            invoiceDate,
                            "Payment",
                            totalAmount,
                            $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}",
                            "PURCHASE_INVOICE",
                            invoiceId,
                            invoiceNumber,
                            modifiedBy);

                        await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 سيناريو: أجل → أجل (لا تغيير في الخزينة)");
                    }

                    await DeleteSupplierPurchaseTransactionAsync(connection, transaction, invoiceId);

                    string deleteInventoryTransactionsSql = @"
                        DELETE FROM InventoryTransactions 
                        WHERE ReferenceType = 'PURCHASE_INVOICE' AND ReferenceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(deleteInventoryTransactionsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string getOldItemsSql = "SELECT ProductID, QuantityInBaseUnit FROM PurchaseInvoiceItems WHERE InvoiceID = @invoiceId";
                    using (var cmd = new SQLiteCommand(getOldItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int productId = reader.GetInt32(0);
                                decimal oldQuantity = reader.GetDecimal(1);

                                string updateInventorySql = @"
                                    UPDATE StoreInventory 
                                    SET Quantity = Quantity - @quantity,
                                        AvailableQuantity = AvailableQuantity - @quantity,
                                        LastUpdated = CURRENT_TIMESTAMP
                                    WHERE StoreID = @storeId AND ProductID = @productId";

                                using (var updateCmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@quantity", oldQuantity);
                                    updateCmd.Parameters.AddWithValue("@storeId", finalStoreId);
                                    updateCmd.Parameters.AddWithValue("@productId", productId);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }

                                string updateProductQuantitySql = @"
                                    UPDATE Products 
                                    SET QuantityInBaseUnit = QuantityInBaseUnit - @quantity
                                    WHERE ProductID = @productId";

                                using (var updateProductCmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                                {
                                    updateProductCmd.Parameters.AddWithValue("@quantity", oldQuantity);
                                    updateProductCmd.Parameters.AddWithValue("@productId", productId);
                                    await updateProductCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    string deleteItemsSql = "DELETE FROM PurchaseInvoiceItems WHERE InvoiceID = @invoiceId";
                    using (var cmd = new SQLiteCommand(deleteItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string updateInvoiceSql = @"
                        UPDATE PurchaseInvoices SET 
                            InvoiceNumber = @invoiceNumber,
                            InvoiceDate = @invoiceDate,
                            SupplierID = @supplierId,
                            StoreID = @storeId,
                            SubTotal = @subTotal,
                            DiscountAmount = @discountAmount,
                            TaxAmount = @taxAmount,
                            TaxPercent = @taxPercent,
                            TotalAmount = @totalAmount,
                            PaymentMethod = @paymentMethod,
                            ReferenceNumber = @referenceNumber,
                            Notes = @notes,
                            ModifiedDate = CURRENT_TIMESTAMP,
                            ModifiedBy = @modifiedBy
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        cmd.Parameters.AddWithValue("@invoiceDate", invoiceDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@supplierId", supplierId);
                        cmd.Parameters.AddWithValue("@storeId", finalStoreId);
                        cmd.Parameters.AddWithValue("@subTotal", subTotal);
                        cmd.Parameters.AddWithValue("@discountAmount", discountAmount);
                        cmd.Parameters.AddWithValue("@taxAmount", taxAmount);
                        cmd.Parameters.AddWithValue("@taxPercent", taxPercent);
                        cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                        cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? "");
                        cmd.Parameters.AddWithValue("@notes", notes ?? "");
                        cmd.Parameters.AddWithValue("@modifiedBy", modifiedBy);
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string insertItemSql = @"
                        INSERT INTO PurchaseInvoiceItems (
                            InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                            DiscountPercent, DiscountAmount, TotalAmount
                        ) VALUES (
                            @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                            @discountPercent, @discountAmount, @totalAmount
                        );";

                    foreach (var item in items)
                    {
                        using (var cmd = new SQLiteCommand(insertItemSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@quantityInBaseUnit", item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@discountPercent", item.DiscountPercent);
                            cmd.Parameters.AddWithValue("@discountAmount", item.DiscountAmount);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await UpdateInventoryForPurchaseAsync(connection, transaction, finalStoreId, items, invoiceId, invoiceNumber, invoiceDate, modifiedBy, true);

                    decimal currentSupplierBalance = await GetSupplierBalanceAsync(connection, transaction, supplierId);
                    decimal newSupplierBalance = currentSupplierBalance + totalAmount;

                    int existingCount = await GetSupplierPurchaseTransactionCountAsync(connection, transaction, invoiceId);

                    if (existingCount == 0)
                    {
                        await AddSupplierPurchaseTransactionAsync(
                            connection, transaction,
                            supplierId,
                            invoiceDate,
                            totalAmount,
                            newSupplierBalance,
                            notes ?? "",
                            invoiceId,
                            invoiceNumber,
                            modifiedBy);

                        await UpdateSupplierBalanceAsync(supplierId, newSupplierBalance, connection, transaction);
                        System.Diagnostics.Debug.WriteLine($"✅ إضافة حركة فاتورة جديدة للمورد: {currentSupplierBalance} → {newSupplierBalance}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ حركة الفاتورة {invoiceNumber} موجودة مسبقاً، تم تخطي الإضافة");
                    }

                    if (paymentMethod != "Credit")
                    {
                        int newVoucherId = 0;
                        string getNewVoucherSql = @"
                            SELECT VoucherID FROM PaymentVouchers 
                            WHERE ReferenceNumber = @invoiceNumber 
                            ORDER BY VoucherID DESC LIMIT 1";

                        using (var cmd = new SQLiteCommand(getNewVoucherSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            object result = await cmd.ExecuteScalarAsync();
                            if (result != null) newVoucherId = Convert.ToInt32(result);
                        }

                        if (newVoucherId > 0)
                        {
                            decimal supplierBalanceAfterPayment = newSupplierBalance - totalAmount;

                            await AddSupplierPaymentTransactionAsync(
                                connection, transaction,
                                supplierId,
                                invoiceDate,
                                totalAmount,
                                supplierBalanceAfterPayment,
                                $"دفع قيمة فاتورة مشتريات رقم {invoiceNumber}",
                                newVoucherId,
                                invoiceNumber,
                                modifiedBy);

                            await UpdateSupplierBalanceAsync(supplierId, supplierBalanceAfterPayment, connection, transaction);

                            System.Diagnostics.Debug.WriteLine($"✅ إضافة حركة دفع للمورد بالقيمة {totalAmount}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"===== اكتمل تحديث فاتورة المشتريات {invoiceNumber} بنجاح =====");
                    return true;

                }, CancellationToken.None, 5);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
            {
                MessageBox.Show($"تعذر تحديث الفاتورة بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.\n\nالتفاصيل: {ex.Message}",
                                "قاعدة البيانات مشغولة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديث الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ UpdatePurchaseInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"UpdatePurchaseInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        #region 3. فواتير المشتريات - حذف (Delete Purchase Invoice)

        public async Task<bool> DeletePurchaseInvoiceAsync(int invoiceId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string invoiceNumber = "";
                        int storeId = 0;
                        int supplierId = 0;
                        decimal totalAmount = 0;

                        string getInvoiceSql = @"
                            SELECT InvoiceNumber, StoreID, SupplierID, TotalAmount 
                            FROM PurchaseInvoices 
                            WHERE InvoiceID = @invoiceId";

                        using (var cmd = new SQLiteCommand(getInvoiceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    invoiceNumber = reader.GetString(0);
                                    storeId = reader.GetInt32(1);
                                    supplierId = reader.GetInt32(2);
                                    totalAmount = reader.GetDecimal(3);
                                }
                                else
                                {
                                    MessageBox.Show("الفاتورة غير موجودة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return false;
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"===== بدء حذف فاتورة المشتريات {invoiceNumber} =====");

                        int paymentVoucherId = 0;
                        int treasuryId = 0;

                        string getPaymentVoucherSql = @"
                            SELECT VoucherID, TreasuryID FROM PaymentVouchers 
                            WHERE ReferenceNumber = @invoiceNumber 
                            ORDER BY VoucherID DESC LIMIT 1";

                        using (var cmd = new SQLiteCommand(getPaymentVoucherSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    paymentVoucherId = reader.GetInt32(0);
                                    treasuryId = reader.GetInt32(1);
                                }
                            }
                        }

                        if (paymentVoucherId > 0 && treasuryId > 0)
                        {
                            await DeletePaymentVoucherAndRelatedAsync(connection, transaction, paymentVoucherId, treasuryId, invoiceNumber);
                        }

                        string deleteInventoryTransactionsSql = @"
                            DELETE FROM InventoryTransactions 
                            WHERE ReferenceType = 'PURCHASE_INVOICE' AND ReferenceID = @invoiceId";

                        using (var cmd = new SQLiteCommand(deleteInventoryTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string getItemsSql = "SELECT ProductID, QuantityInBaseUnit FROM PurchaseInvoiceItems WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(getItemsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int productId = reader.GetInt32(0);
                                    decimal quantity = reader.GetDecimal(1);

                                    string updateInventorySql = @"
                                        UPDATE StoreInventory 
                                        SET Quantity = Quantity - @quantity,
                                            AvailableQuantity = AvailableQuantity - @quantity,
                                            LastUpdated = CURRENT_TIMESTAMP
                                        WHERE StoreID = @storeId AND ProductID = @productId";

                                    using (var updateCmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                                    {
                                        updateCmd.Parameters.AddWithValue("@quantity", quantity);
                                        updateCmd.Parameters.AddWithValue("@storeId", storeId);
                                        updateCmd.Parameters.AddWithValue("@productId", productId);
                                        await updateCmd.ExecuteNonQueryAsync();
                                    }

                                    string updateProductQuantitySql = @"
                                        UPDATE Products 
                                        SET QuantityInBaseUnit = QuantityInBaseUnit - @quantity
                                        WHERE ProductID = @productId";

                                    using (var updateProductCmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                                    {
                                        updateProductCmd.Parameters.AddWithValue("@quantity", quantity);
                                        updateProductCmd.Parameters.AddWithValue("@productId", productId);
                                        await updateProductCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                        }

                        await DeleteSupplierPurchaseTransactionAsync(connection, transaction, invoiceId);

                        string deleteItemsSql = "DELETE FROM PurchaseInvoiceItems WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(deleteItemsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteInvoiceSql = "DELETE FROM PurchaseInvoices WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(deleteInvoiceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ✅ إصلاح باج مشابه لباج رصيد العملاء: المعادلة القديمة كانت تتجاهل رصيد
                        // أول المدة للمورد بالكامل، وتتجاهل حركات مرتجعات المشتريات (Refund) تماماً.
                        // أعمدة 'Purchase' و'Payment' كانت صحيحة أصلاً فلم تُغيَّر.
                        string recalcSupplierBalanceSql = @"
                            UPDATE Suppliers SET CurrentBalance =
                                COALESCE(OpeningBalance, 0) + COALESCE((
                                SELECT SUM(
                                    CASE 
                                        WHEN TransactionType = 'Purchase' THEN CreditAmount
                                        WHEN TransactionType = 'Payment' THEN -DebitAmount
                                        WHEN TransactionType = 'Refund' THEN -DebitAmount
                                        ELSE (CreditAmount - DebitAmount)
                                    END
                                ) FROM SupplierTransactions 
                                WHERE SupplierTransactions.SupplierID = Suppliers.SupplierID
                            ), 0)
                            WHERE SupplierID = @supplierId";

                        using (var cmd = new SQLiteCommand(recalcSupplierBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@supplierId", supplierId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"===== اكتمل حذف فاتورة المشتريات {invoiceNumber} بنجاح =====");

                        MessageBox.Show($"تم حذف الفاتورة {invoiceNumber} بنجاح", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حذف الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ DeletePurchaseInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"DeletePurchaseInvoiceAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 4. فواتير المبيعات - حفظ (Save Sales Invoice) - المعدلة بالكامل

        public async Task<bool> SaveSalesInvoiceAsync(
            string invoiceNumber,
            DateTime invoiceDate,
            int customerId,
            int storeId,
            decimal subTotal,
            decimal discountAmount,
            decimal taxAmount,
            decimal taxPercent,
            decimal totalAmount,
            decimal paidAmount,
            string paymentMethod,
            string referenceNumber,
            string notes,
            List<SalesInvoiceItemClass> items,
            int createdBy,
            int? treasuryId = null,
            string checkNumber = null,
            DateTime? checkDate = null,
            string bankName = null,
            bool isInstallment = false,
            List<InstallmentService.InstallmentInput> installments = null,
            decimal paidUpfront = 0)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                MessageBox.Show("رقم الفاتورة لا يمكن أن يكون فارغاً", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (customerId <= 0)
            {
                MessageBox.Show("معرف العميل غير صالح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (items == null || items.Count == 0)
            {
                MessageBox.Show("لا توجد عناصر في الفاتورة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (createdBy <= 0) createdBy = 1;

            bool exists = await _databaseService.SalesInvoiceNumberExistsAsync(invoiceNumber);
            if (exists)
            {
                MessageBox.Show($"الفاتورة {invoiceNumber} موجودة مسبقاً", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                return await DatabaseExecutor.ExecuteTransactionAsync(_databaseService, async (connection, transaction) =>
                {
                    int finalStoreId = storeId;
                    if (finalStoreId <= 0)
                    {
                        finalStoreId = await GetDefaultStoreIdAsync(connection, transaction);
                        if (finalStoreId <= 0)
                        {
                            throw new Exception("لا يوجد مخزن نشط في النظام");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"📌 بدء حفظ الفاتورة {invoiceNumber}");
                    System.Diagnostics.Debug.WriteLine($"💰 الإجمالي: {totalAmount:N2}, المدفوع: {paidAmount:N2}, طريقة الدفع: {paymentMethod}");
                    System.Diagnostics.Debug.WriteLine($"📦 عدد المنتجات: {items.Count}");

                    foreach (var item in items)
                    {
                        decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;
                        await CheckInventoryQuantityAsync(
                            connection,
                            transaction,
                            finalStoreId,
                            item.ProductID,
                            quantityInBaseUnit,
                            item.ProductName);
                    }

                    int invoiceId = 0;

                    string paymentStatus;
                    if (paidAmount >= totalAmount)
                        paymentStatus = "Paid";
                    else if (paidAmount > 0)
                        paymentStatus = "Partial";
                    else
                        paymentStatus = "Pending";

                    string invoiceSql = @"
                        INSERT INTO SalesInvoices (
                            InvoiceNumber, InvoiceDate, CustomerID, StoreID, InvoiceType,
                            SubTotal, DiscountAmount, TaxAmount, TaxPercent,
                            TotalAmount, PaidAmount, RemainingAmount,
                            PaymentMethod, PaymentStatus, ReferenceNumber,
                            IsPosted, Notes, CreatedDate, CreatedBy,
                            IsInstallment, TotalInstallmentAmount, PaidUpfront, InstallmentCount, InstallmentStatus
                        ) VALUES (
                            @invoiceNumber, @invoiceDate, @customerId, @storeId, 'Sales',
                            @subTotal, @discountAmount, @taxAmount, @taxPercent,
                            @totalAmount, @paidAmount, @totalAmount - @paidAmount,
                            @paymentMethod, @paymentStatus, @referenceNumber,
                            1, @notes, CURRENT_TIMESTAMP, @createdBy,
                            @isInstallment, @totalInstallmentAmount, @paidUpfront, @installmentCount, 'Active'
                        );
                        SELECT last_insert_rowid();";

                    using (var cmd = new SQLiteCommand(invoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        cmd.Parameters.AddWithValue("@invoiceDate", invoiceDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@storeId", finalStoreId);
                        cmd.Parameters.AddWithValue("@subTotal", subTotal);
                        cmd.Parameters.AddWithValue("@discountAmount", discountAmount);
                        cmd.Parameters.AddWithValue("@taxAmount", taxAmount);
                        cmd.Parameters.AddWithValue("@taxPercent", taxPercent);
                        cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                        cmd.Parameters.AddWithValue("@paidAmount", paidAmount);
                        cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@paymentStatus", paymentStatus);
                        cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? "");
                        cmd.Parameters.AddWithValue("@notes", notes ?? "");
                        cmd.Parameters.AddWithValue("@createdBy", createdBy);
                        cmd.Parameters.AddWithValue("@isInstallment", isInstallment ? 1 : 0);
                        cmd.Parameters.AddWithValue("@totalInstallmentAmount", isInstallment ? (totalAmount - paidUpfront) : 0);
                        cmd.Parameters.AddWithValue("@paidUpfront", isInstallment ? paidUpfront : 0);
                        cmd.Parameters.AddWithValue("@installmentCount", isInstallment ? (installments?.Count ?? 0) : 0);

                        object result = await cmd.ExecuteScalarAsync();
                        invoiceId = SafeToInt(result);
                    }

                    if (invoiceId == 0)
                    {
                        throw new Exception("فشل في إدراج الفاتورة");
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم إدراج الفاتورة ID: {invoiceId}");

                    string itemSql = @"
                        INSERT INTO SalesInvoiceItems (
                            InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                            DiscountPercent, DiscountAmount, TotalAmount
                        ) VALUES (
                            @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                            @discountPercent, @discountAmount, @totalAmount
                        );";

                    foreach (var item in items)
                    {
                        using (var cmd = new SQLiteCommand(itemSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@quantityInBaseUnit", item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@discountPercent", item.DiscountPercent);
                            cmd.Parameters.AddWithValue("@discountAmount", item.DiscountAmount);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ تم إدراج {items.Count} عنصر في الفاتورة");

                    bool inventoryUpdated = await UpdateInventoryForSaleAsync(
                        connection, transaction,
                        finalStoreId, items,
                        invoiceId, invoiceNumber,
                        invoiceDate, createdBy,
                        true);

                    if (!inventoryUpdated)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ فشل في تحديث المخزون");
                        throw new Exception("فشل في تحديث المخزون");
                    }

                    decimal currentCustomerBalance = await GetCustomerBalanceAsync(connection, transaction, customerId);
                    decimal customerBalanceAfterInvoice = currentCustomerBalance + totalAmount;

                    System.Diagnostics.Debug.WriteLine($"💰 رصيد العميل قبل الفاتورة: {currentCustomerBalance:N2}");
                    System.Diagnostics.Debug.WriteLine($"💰 رصيد العميل بعد الفاتورة: {customerBalanceAfterInvoice:N2}");

                    // ✅ إصلاح (خطوة 1): بقينا مش محتاجين نتحقق "هل فيه حركة مسجَّلة قبل كده؟" قبل الإضافة -
                    // الفحص القديم ده كان بيفشل عمليًا وبيسمح بتكرار الحركة (السطر مسجَّل مرتين لكل فاتورة
                    // تقريبًا في بيانات النسخة الاحتياطية). الدالة AddCustomerInvoiceTransactionAsync بقت
                    // نفسها بتمسح أي حركة "Invoice" سابقة لنفس الفاتورة قبل ما تضيف الصحيحة، فهي آمنة تتنادى
                    // مباشرة من غير أي فحص خارجي، وهيفضل دايمًا صف واحد بس صحيح لكل فاتورة.
                    await AddCustomerInvoiceTransactionAsync(
                        connection, transaction,
                        customerId,
                        invoiceDate,
                        totalAmount,
                        customerBalanceAfterInvoice,
                        notes ?? "",
                        invoiceId,
                        invoiceNumber,
                        createdBy);

                    await UpdateCustomerBalanceAsync(customerId, customerBalanceAfterInvoice, connection, transaction);
                    System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل: {currentCustomerBalance:N2} → {customerBalanceAfterInvoice:N2}");

                    if (isInstallment && installments != null && installments.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"📊 بدء إنشاء {installments.Count} قسط للفاتورة {invoiceNumber}");
                        System.Diagnostics.Debug.WriteLine($"💰 المبلغ المتبقي للتقسيط: {totalAmount - paidUpfront:N2}");
                        System.Diagnostics.Debug.WriteLine($"💰 المدفوع مقدمًا: {paidUpfront:N2}");

                        var installmentService = new InstallmentService(_databaseService);
                        bool installmentsCreated = await installmentService.CreateInstallmentsForInvoiceAsync(
                            invoiceId,
                            invoiceNumber,
                            customerId,
                            invoiceDate,
                            totalAmount,
                            paidUpfront,
                            installments,
                            createdBy,
                            connection,
                            transaction);

                        if (!installmentsCreated)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ فشل في إنشاء الأقساط");
                            throw new Exception("فشل في إنشاء الأقساط");
                        }

                        System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء {installments.Count} قسط");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"ℹ️ الفاتورة {invoiceNumber} ليست بالتقسيط أو لا توجد أقساط");
                    }

                    if (paymentMethod != "Credit" && paidAmount > 0)
                    {
                        int finalTreasuryId = treasuryId ?? await GetDefaultTreasuryIdAsync(connection, transaction);
                        if (finalTreasuryId == 0)
                        {
                            throw new Exception("لا توجد خزينة متاحة للتحصيل");
                        }

                        int newVoucherId = await CreateReceiptVoucherAsync(
                            connection, transaction,
                            invoiceNumber, invoiceDate, customerId, paidAmount,
                            paymentMethod, checkNumber, checkDate, bankName,
                            invoiceNumber, finalTreasuryId, createdBy);

                        if (newVoucherId == 0)
                        {
                            throw new Exception("فشل في إنشاء سند القبض");
                        }

                        await AddTreasuryTransactionAsync(
                            connection, transaction,
                            finalTreasuryId,
                            invoiceDate,
                            "Receipt",
                            paidAmount,
                            $"تحصيل قيمة فاتورة بيع رقم {invoiceNumber}",
                            "SALES_INVOICE",
                            invoiceId,
                            invoiceNumber,
                            createdBy);

                        await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);

                        decimal customerBalanceAfterReceipt = customerBalanceAfterInvoice - paidAmount;

                        await AddCustomerReceiptTransactionAsync(
                            connection, transaction,
                            customerId,
                            invoiceDate,
                            paidAmount,
                            customerBalanceAfterReceipt,
                            $"تحصيل قيمة فاتورة رقم {invoiceNumber}",
                            newVoucherId,
                            invoiceNumber,
                            createdBy);

                        await UpdateCustomerBalanceAsync(customerId, customerBalanceAfterReceipt, connection, transaction);
                        System.Diagnostics.Debug.WriteLine($"✅ تم تحديث رصيد العميل بعد التحصيل");
                    }

                    await RecalculateCustomerBalanceFromTransactions(connection, transaction, customerId);

                    transaction.Commit();
                    System.Diagnostics.Debug.WriteLine($"✅ تم حفظ الفاتورة {invoiceNumber} بنجاح");
                    return true;

                }, CancellationToken.None, 5);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
            {
                MessageBox.Show($"تعذر حفظ الفاتورة بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.\n\nالتفاصيل: {ex.Message}",
                                "قاعدة البيانات مشغولة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ SaveSalesInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"❌ SaveSalesInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        #region 5. فواتير المبيعات - تحديث (Update Sales Invoice)

        public async Task<bool> UpdateSalesInvoiceAsync(
            int invoiceId,
            string invoiceNumber,
            DateTime invoiceDate,
            int customerId,
            int storeId,
            decimal subTotal,
            decimal discountAmount,
            decimal taxAmount,
            decimal taxPercent,
            decimal totalAmount,
            decimal paidAmount,
            string paymentMethod,
            string referenceNumber,
            string notes,
            List<SalesInvoiceItemClass> items,
            int modifiedBy,
            int? treasuryId = null,
            string checkNumber = null,
            DateTime? checkDate = null,
            string bankName = null)
        {
            if (invoiceId <= 0)
            {
                MessageBox.Show("معرف الفاتورة غير صالح", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (items == null || items.Count == 0)
            {
                MessageBox.Show("لا توجد عناصر في الفاتورة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (modifiedBy <= 0) modifiedBy = 1;

            try
            {
                return await DatabaseExecutor.ExecuteTransactionAsync(_databaseService, async (connection, transaction) =>
                {
                    int finalStoreId = storeId;
                    if (finalStoreId <= 0)
                    {
                        finalStoreId = await GetDefaultStoreIdAsync(connection, transaction);
                        if (finalStoreId <= 0)
                        {
                            throw new Exception("لا يوجد مخزن نشط في النظام");
                        }
                    }

                    decimal oldTotalAmount = 0;
                    string oldPaymentMethod = "";
                    decimal oldPaidAmount = 0;
                    int oldVoucherId = 0;
                    int oldTreasuryId = 0;

                    string getOldInvoiceSql = @"
                        SELECT TotalAmount, PaymentMethod, PaidAmount 
                        FROM SalesInvoices 
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(getOldInvoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                oldTotalAmount = reader.GetDecimal(0);
                                oldPaymentMethod = reader.GetString(1);
                                oldPaidAmount = reader.GetDecimal(2);
                            }
                            else
                            {
                                throw new Exception("الفاتورة غير موجودة");
                            }
                        }
                    }

                    string findOldVoucherSql = @"
                        SELECT VoucherID, TreasuryID 
                        FROM ReceiptVouchers 
                        WHERE ReferenceNumber = @invoiceNumber AND CustomerID = @customerId 
                        ORDER BY VoucherID DESC LIMIT 1";

                    using (var cmd = new SQLiteCommand(findOldVoucherSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                oldVoucherId = reader.GetInt32(0);
                                oldTreasuryId = reader.GetInt32(1);
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"===== تحديث فاتورة المبيعات {invoiceNumber} =====");
                    System.Diagnostics.Debug.WriteLine($"القديم: المبلغ={oldTotalAmount}, المدفوع={oldPaidAmount}, طريقة الدفع={oldPaymentMethod}");
                    System.Diagnostics.Debug.WriteLine($"الجديد: المبلغ={totalAmount}, المدفوع={paidAmount}, طريقة الدفع={paymentMethod}");

                    if (oldPaymentMethod != "Credit" && paymentMethod != "Credit")
                    {
                        if (oldVoucherId > 0 && oldTreasuryId > 0)
                        {
                            await DeleteReceiptVoucherAndRelatedAsync(connection, transaction, oldVoucherId, oldTreasuryId, invoiceNumber, customerId);
                        }

                        if (paidAmount > 0)
                        {
                            int finalTreasuryId = treasuryId ?? (oldTreasuryId > 0 ? oldTreasuryId : await GetDefaultTreasuryIdAsync(connection, transaction));
                            int newVoucherId = await CreateReceiptVoucherAsync(
                                connection, transaction,
                                invoiceNumber, invoiceDate, customerId, paidAmount,
                                paymentMethod, checkNumber, checkDate, bankName,
                                invoiceNumber, finalTreasuryId, modifiedBy);

                            await AddTreasuryTransactionAsync(
                                connection, transaction,
                                finalTreasuryId,
                                invoiceDate,
                                "Receipt",
                                paidAmount,
                                $"تحصيل قيمة فاتورة بيع رقم {invoiceNumber}",
                                "SALES_INVOICE",
                                invoiceId,
                                invoiceNumber,
                                modifiedBy);

                            await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);
                        }
                    }
                    else if (oldPaymentMethod != "Credit" && paymentMethod == "Credit")
                    {
                        if (oldVoucherId > 0 && oldTreasuryId > 0)
                        {
                            await DeleteReceiptVoucherAndRelatedAsync(connection, transaction, oldVoucherId, oldTreasuryId, invoiceNumber, customerId);
                        }
                    }
                    else if (oldPaymentMethod == "Credit" && paymentMethod != "Credit")
                    {
                        if (paidAmount > 0)
                        {
                            int finalTreasuryId = treasuryId ?? await GetDefaultTreasuryIdAsync(connection, transaction);
                            if (finalTreasuryId == 0)
                            {
                                throw new Exception("لا توجد خزينة متاحة للتحصيل");
                            }

                            int newVoucherId = await CreateReceiptVoucherAsync(
                                connection, transaction,
                                invoiceNumber, invoiceDate, customerId, paidAmount,
                                paymentMethod, checkNumber, checkDate, bankName,
                                invoiceNumber, finalTreasuryId, modifiedBy);

                            await AddTreasuryTransactionAsync(
                                connection, transaction,
                                finalTreasuryId,
                                invoiceDate,
                                "Receipt",
                                paidAmount,
                                $"تحصيل قيمة فاتورة بيع رقم {invoiceNumber}",
                                "SALES_INVOICE",
                                invoiceId,
                                invoiceNumber,
                                modifiedBy);

                            await RecalculateAndUpdateTreasuryBalanceAsync(finalTreasuryId, connection, transaction);
                        }
                    }

                    await DeleteCustomerInvoiceTransactionAsync(connection, transaction, invoiceId);

                    string deleteInventoryTransactionsSql = @"
                        DELETE FROM InventoryTransactions 
                        WHERE ReferenceType = 'SALES_INVOICE' AND ReferenceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(deleteInventoryTransactionsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string getOldItemsSql = "SELECT ProductID, QuantityInBaseUnit FROM SalesInvoiceItems WHERE InvoiceID = @invoiceId";
                    using (var cmd = new SQLiteCommand(getOldItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int productId = reader.GetInt32(0);
                                decimal oldQuantity = reader.GetDecimal(1);

                                string updateInventorySql = @"
                                    UPDATE StoreInventory 
                                    SET Quantity = Quantity + @quantity,
                                        AvailableQuantity = AvailableQuantity + @quantity,
                                        LastUpdated = CURRENT_TIMESTAMP
                                    WHERE StoreID = @storeId AND ProductID = @productId";

                                using (var updateCmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@quantity", oldQuantity);
                                    updateCmd.Parameters.AddWithValue("@storeId", finalStoreId);
                                    updateCmd.Parameters.AddWithValue("@productId", productId);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }

                                string updateProductQuantitySql = @"
                                    UPDATE Products 
                                    SET QuantityInBaseUnit = QuantityInBaseUnit + @quantity
                                    WHERE ProductID = @productId";

                                using (var updateProductCmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                                {
                                    updateProductCmd.Parameters.AddWithValue("@quantity", oldQuantity);
                                    updateProductCmd.Parameters.AddWithValue("@productId", productId);
                                    await updateProductCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                    string deleteItemsSql = "DELETE FROM SalesInvoiceItems WHERE InvoiceID = @invoiceId";
                    using (var cmd = new SQLiteCommand(deleteItemsSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    foreach (var item in items)
                    {
                        decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;
                        await CheckInventoryQuantityAsync(
                            connection,
                            transaction,
                            finalStoreId,
                            item.ProductID,
                            quantityInBaseUnit,
                            item.ProductName);
                    }

                    string paymentStatus;
                    if (paidAmount >= totalAmount)
                        paymentStatus = "Paid";
                    else if (paidAmount > 0)
                        paymentStatus = "Partial";
                    else
                        paymentStatus = "Pending";

                    string updateInvoiceSql = @"
                        UPDATE SalesInvoices SET 
                            InvoiceNumber = @invoiceNumber,
                            InvoiceDate = @invoiceDate,
                            CustomerID = @customerId,
                            StoreID = @storeId,
                            SubTotal = @subTotal,
                            DiscountAmount = @discountAmount,
                            TaxAmount = @taxAmount,
                            TaxPercent = @taxPercent,
                            TotalAmount = @totalAmount,
                            PaidAmount = @paidAmount,
                            RemainingAmount = @totalAmount - @paidAmount,
                            PaymentMethod = @paymentMethod,
                            PaymentStatus = @paymentStatus,
                            ReferenceNumber = @referenceNumber,
                            Notes = @notes,
                            ModifiedDate = CURRENT_TIMESTAMP,
                            ModifiedBy = @modifiedBy
                        WHERE InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(updateInvoiceSql, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                        cmd.Parameters.AddWithValue("@invoiceDate", invoiceDate.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@customerId", customerId);
                        cmd.Parameters.AddWithValue("@storeId", finalStoreId);
                        cmd.Parameters.AddWithValue("@subTotal", subTotal);
                        cmd.Parameters.AddWithValue("@discountAmount", discountAmount);
                        cmd.Parameters.AddWithValue("@taxAmount", taxAmount);
                        cmd.Parameters.AddWithValue("@taxPercent", taxPercent);
                        cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                        cmd.Parameters.AddWithValue("@paidAmount", paidAmount);
                        cmd.Parameters.AddWithValue("@paymentMethod", paymentMethod);
                        cmd.Parameters.AddWithValue("@paymentStatus", paymentStatus);
                        cmd.Parameters.AddWithValue("@referenceNumber", referenceNumber ?? "");
                        cmd.Parameters.AddWithValue("@notes", notes ?? "");
                        cmd.Parameters.AddWithValue("@modifiedBy", modifiedBy);
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    string insertItemSql = @"
                        INSERT INTO SalesInvoiceItems (
                            InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                            DiscountPercent, DiscountAmount, TotalAmount
                        ) VALUES (
                            @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                            @discountPercent, @discountAmount, @totalAmount
                        );";

                    foreach (var item in items)
                    {
                        using (var cmd = new SQLiteCommand(insertItemSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            cmd.Parameters.AddWithValue("@productId", item.ProductID);
                            cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                            cmd.Parameters.AddWithValue("@quantityInBaseUnit", item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity);
                            cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                            cmd.Parameters.AddWithValue("@discountPercent", item.DiscountPercent);
                            cmd.Parameters.AddWithValue("@discountAmount", item.DiscountAmount);
                            cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    await UpdateInventoryForSaleAsync(
                        connection, transaction,
                        finalStoreId, items,
                        invoiceId, invoiceNumber,
                        invoiceDate, modifiedBy,
                        true);

                    // ✅ إصلاح (خطوة 1): AddCustomerInvoiceTransactionAsync بقت تمسح الحركة القديمة لنفس
                    // الفاتورة قبل ما تسجل الجديدة، فبقدر أنادّيها من غير فحص "existingCount" القديم -
                    // وده كمان بيصلّح مشكلة تانية كانت موجودة: لو اتعدّل مبلغ الفاتورة، الحركة القديمة في
                    // كشف الحساب كانت بتفضل بالمبلغ القديم لأن الكود كان بيتجاهل الإضافة تمامًا لو لقى
                    // حركة مسجَّلة بالفعل. دلوقتي هيتم تحديثها للمبلغ الصحيح الجديد كل مرة.
                    decimal currentCustomerBalance = await GetCustomerBalanceAsync(connection, transaction, customerId);
                    decimal newCustomerBalance = currentCustomerBalance + totalAmount;

                    await AddCustomerInvoiceTransactionAsync(
                        connection, transaction,
                        customerId,
                        invoiceDate,
                        totalAmount,
                        newCustomerBalance,
                        notes ?? "",
                        invoiceId,
                        invoiceNumber,
                        modifiedBy);

                    await UpdateCustomerBalanceAsync(customerId, newCustomerBalance, connection, transaction);
                    System.Diagnostics.Debug.WriteLine($"✅ تم تحديث حركة فاتورة العميل {customerId}");

                    if (paymentMethod != "Credit" && paidAmount > 0)
                    {
                        int newVoucherId = 0;
                        string getNewVoucherSql = @"
                            SELECT VoucherID FROM ReceiptVouchers 
                            WHERE ReferenceNumber = @invoiceNumber AND CustomerID = @customerId 
                            ORDER BY VoucherID DESC LIMIT 1";

                        using (var cmd = new SQLiteCommand(getNewVoucherSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            object result = await cmd.ExecuteScalarAsync();
                            if (result != null) newVoucherId = Convert.ToInt32(result);
                        }

                        if (newVoucherId > 0)
                        {
                            decimal currentBalance = await GetCustomerBalanceAsync(connection, transaction, customerId);
                            await AddCustomerReceiptTransactionAsync(
                                connection, transaction,
                                customerId,
                                invoiceDate,
                                paidAmount,
                                currentBalance - paidAmount,
                                $"سند قبض - تحصيل قيمة فاتورة رقم {invoiceNumber}",
                                newVoucherId,
                                invoiceNumber,
                                modifiedBy);

                            await UpdateCustomerBalanceAsync(customerId, currentBalance - paidAmount, connection, transaction);
                            System.Diagnostics.Debug.WriteLine($"✅ إضافة حركة تحصيل للعميل بالقيمة {paidAmount}");
                        }
                    }

                    await RecalculateCustomerBalanceFromTransactions(connection, transaction, customerId);

                    System.Diagnostics.Debug.WriteLine($"===== اكتمل تحديث فاتورة المبيعات {invoiceNumber} بنجاح =====");
                    return true;

                }, CancellationToken.None, 5);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
            {
                MessageBox.Show($"تعذر تحديث الفاتورة بسبب انشغال قاعدة البيانات. الرجاء المحاولة مرة أخرى.\n\nالتفاصيل: {ex.Message}",
                                "قاعدة البيانات مشغولة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحديث الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ UpdateSalesInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"UpdateSalesInvoiceAsync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        #region 6. فواتير المبيعات - حذف (Delete Sales Invoice)

        public async Task<bool> DeleteSalesInvoiceAsync(int invoiceId)
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        string invoiceNumber = "";
                        int storeId = 0;
                        int customerId = 0;
                        decimal totalAmount = 0;

                        string getInvoiceSql = @"
                            SELECT InvoiceNumber, StoreID, CustomerID, TotalAmount 
                            FROM SalesInvoices 
                            WHERE InvoiceID = @invoiceId";

                        using (var cmd = new SQLiteCommand(getInvoiceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    invoiceNumber = reader.GetString(0);
                                    storeId = reader.GetInt32(1);
                                    customerId = reader.GetInt32(2);
                                    totalAmount = reader.GetDecimal(3);
                                }
                                else
                                {
                                    MessageBox.Show("الفاتورة غير موجودة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return false;
                                }
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"===== بدء حذف فاتورة المبيعات {invoiceNumber} =====");

                        await DeleteInstallmentsByInvoiceIdAsync(connection, transaction, invoiceId);

                        int receiptVoucherId = 0;
                        int treasuryId = 0;

                        string getReceiptVoucherSql = @"
                            SELECT VoucherID, TreasuryID FROM ReceiptVouchers 
                            WHERE ReferenceNumber = @invoiceNumber AND CustomerID = @customerId 
                            ORDER BY VoucherID DESC LIMIT 1";

                        using (var cmd = new SQLiteCommand(getReceiptVoucherSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber);
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    receiptVoucherId = reader.GetInt32(0);
                                    treasuryId = reader.GetInt32(1);
                                }
                            }
                        }

                        if (receiptVoucherId > 0 && treasuryId > 0)
                        {
                            await DeleteReceiptVoucherAndRelatedAsync(connection, transaction, receiptVoucherId, treasuryId, invoiceNumber, customerId);
                        }

                        string deleteInventoryTransactionsSql = @"
                            DELETE FROM InventoryTransactions 
                            WHERE ReferenceType = 'SALES_INVOICE' AND ReferenceID = @invoiceId";

                        using (var cmd = new SQLiteCommand(deleteInventoryTransactionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string getItemsSql = "SELECT ProductID, QuantityInBaseUnit FROM SalesInvoiceItems WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(getItemsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int productId = reader.GetInt32(0);
                                    decimal quantity = reader.GetDecimal(1);

                                    string updateInventorySql = @"
                                        UPDATE StoreInventory 
                                        SET Quantity = Quantity + @quantity,
                                            AvailableQuantity = AvailableQuantity + @quantity,
                                            LastUpdated = CURRENT_TIMESTAMP
                                        WHERE StoreID = @storeId AND ProductID = @productId";

                                    using (var updateCmd = new SQLiteCommand(updateInventorySql, connection, transaction))
                                    {
                                        updateCmd.Parameters.AddWithValue("@quantity", quantity);
                                        updateCmd.Parameters.AddWithValue("@storeId", storeId);
                                        updateCmd.Parameters.AddWithValue("@productId", productId);
                                        await updateCmd.ExecuteNonQueryAsync();
                                    }

                                    string updateProductQuantitySql = @"
                                        UPDATE Products 
                                        SET QuantityInBaseUnit = QuantityInBaseUnit + @quantity
                                        WHERE ProductID = @productId";

                                    using (var updateProductCmd = new SQLiteCommand(updateProductQuantitySql, connection, transaction))
                                    {
                                        updateProductCmd.Parameters.AddWithValue("@quantity", quantity);
                                        updateProductCmd.Parameters.AddWithValue("@productId", productId);
                                        await updateProductCmd.ExecuteNonQueryAsync();
                                    }
                                }
                            }
                        }

                        await DeleteCustomerInvoiceTransactionAsync(connection, transaction, invoiceId);

                        string deleteItemsSql = "DELETE FROM SalesInvoiceItems WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(deleteItemsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteInvoiceSql = "DELETE FROM SalesInvoices WHERE InvoiceID = @invoiceId";
                        using (var cmd = new SQLiteCommand(deleteInvoiceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // ✅ إصلاح باج خطير (تصحيح لإصلاح سابق كان معكوسًا بالغلط): المعادلة هنا
                        // كانت بتقرأ عمود CreditAmount لحركات النوع 'Invoice'، لكن التأكد المباشر
                        // من AddCustomerInvoiceTransactionAsync وتريجر trig_customer_transaction_from_sales
                        // بيوضّح إن حركة 'Invoice' بتتسجل فعليًا في عمود DebitAmount (مش CreditAmount) -
                        // فكان SUM(CreditAmount) لحركات الفواتير يساوي صفر دائمًا. النتيجة: كل حذف
                        // لفاتورة بيع كان يعيد حساب رصيد العميل وهو يتجاهل كل فواتيره الباقية تمامًا.
                        // المعادلة الصحيحة الموحَّدة مع باقي أنحاء البرنامج (كشف الحساب،
                        // RecalculateAllCustomerRunningBalances):
                        //   الرصيد = رصيد أول المدة + فواتير DebitAmount (Invoice) - تحصيلات CreditAmount (Receipt) - مرتجعات CreditAmount (Refund)
                        string recalcCustomerBalanceSql = @"
                            UPDATE Customers SET CurrentBalance =
                                COALESCE(OpeningBalance, 0) + COALESCE((
                                SELECT SUM(
                                    CASE 
                                        WHEN TransactionType = 'Invoice' THEN DebitAmount
                                        WHEN TransactionType = 'Receipt' THEN -CreditAmount
                                        WHEN TransactionType = 'Refund' THEN -CreditAmount
                                        ELSE (DebitAmount - CreditAmount)
                                    END
                                ) FROM CustomerTransactions 
                                WHERE CustomerTransactions.CustomerID = Customers.CustomerID
                            ), 0)
                            WHERE CustomerID = @customerId";

                        using (var cmd = new SQLiteCommand(recalcCustomerBalanceSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine($"===== اكتمل حذف فاتورة المبيعات {invoiceNumber} بنجاح =====");

                        MessageBox.Show($"تم حذف الفاتورة {invoiceNumber} بنجاح", "تم الحذف", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حذف الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("فشل تنفيذ DeleteSalesInvoiceAsync", ex, "InvoiceService");
                System.Diagnostics.Debug.WriteLine($"DeleteSalesInvoiceAsync Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region مرتجعات البيع والمشتريات (Sales & Purchase Returns)

        /// <summary>
        /// نتيجة البحث عن فاتورة أصلية لعمل مرتجع عليها، مع قائمة الأصناف القابلة للإرجاع
        /// </summary>
        public class InvoiceForReturnResult
        {
            public bool Found { get; set; }
            public string Message { get; set; }
            public int InvoiceID { get; set; }
            public string InvoiceNumber { get; set; }
            public DateTime InvoiceDate { get; set; }
            public int PartyID { get; set; }
            public string PartyName { get; set; }
            public int StoreID { get; set; }
            public string StoreName { get; set; }
            public List<ReturnableInvoiceItem> Items { get; set; } = new List<ReturnableInvoiceItem>();
        }

        public class ReturnableInvoiceItem
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public string Unit { get; set; }
            public decimal OriginalQuantity { get; set; }
            public decimal AlreadyReturnedQuantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal MaxReturnableQuantity => Math.Max(0, OriginalQuantity - AlreadyReturnedQuantity);
        }

        /// <summary>
        /// عنصر مبسّط لعرض فاتورة ضمن قائمة الاقتراحات عند البحث عن فاتورة لعمل مرتجع عليها،
        /// حتى لا يحتاج المستخدم لتذكر رقم الفاتورة كاملاً وكتابته يدوياً.
        /// </summary>
        public class ReturnInvoiceListItem
        {
            public int InvoiceID { get; set; }
            public string InvoiceNumber { get; set; }
            public DateTime InvoiceDate { get; set; }
            public string PartyName { get; set; }
            public decimal TotalAmount { get; set; }
        }

        /// <summary>
        /// تحميل قائمة بأحدث فواتير البيع (غير الملغاة) لعرضها كاقتراحات قابلة للاختيار المباشر
        /// بدلاً من إجبار المستخدم على كتابة رقم الفاتورة كاملاً من الذاكرة
        /// </summary>
        public async Task<List<ReturnInvoiceListItem>> GetRecentSalesInvoicesForReturnAsync(int maxCount = 500)
        {
            var list = new List<ReturnInvoiceListItem>();
            try
            {
                using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    string sql = @"
                        SELECT si.InvoiceID, si.InvoiceNumber, si.InvoiceDate, si.TotalAmount,
                               COALESCE(c.CustomerNameAr, c.CustomerName, 'عميل غير معروف') AS PartyName
                        FROM SalesInvoices si
                        LEFT JOIN Customers c ON si.CustomerID = c.CustomerID
                        WHERE si.InvoiceType = 'Sales' AND si.IsVoid = 0
                        ORDER BY si.InvoiceDate DESC, si.InvoiceID DESC
                        LIMIT @maxCount";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@maxCount", maxCount);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ReturnInvoiceListItem
                                {
                                    InvoiceID = Convert.ToInt32(reader["InvoiceID"]),
                                    InvoiceNumber = reader["InvoiceNumber"].ToString(),
                                    InvoiceDate = Convert.ToDateTime(reader["InvoiceDate"]),
                                    TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"]),
                                    PartyName = reader["PartyName"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل قائمة فواتير البيع لاقتراحات المرتجع", ex, "InvoiceService");
            }
            return list;
        }

        /// <summary>
        /// تحميل قائمة بأحدث فواتير المشتريات (غير الملغاة) لعرضها كاقتراحات قابلة للاختيار المباشر
        /// بدلاً من إجبار المستخدم على كتابة رقم الفاتورة كاملاً من الذاكرة
        /// </summary>
        public async Task<List<ReturnInvoiceListItem>> GetRecentPurchaseInvoicesForReturnAsync(int maxCount = 500)
        {
            var list = new List<ReturnInvoiceListItem>();
            try
            {
                using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();
                    string sql = @"
                        SELECT pi.InvoiceID, pi.InvoiceNumber, pi.InvoiceDate, pi.TotalAmount,
                               COALESCE(sup.SupplierNameAr, sup.SupplierName, 'مورد غير معروف') AS PartyName
                        FROM PurchaseInvoices pi
                        LEFT JOIN Suppliers sup ON pi.SupplierID = sup.SupplierID
                        WHERE pi.InvoiceType = 'Purchase' AND pi.IsVoid = 0
                        ORDER BY pi.InvoiceDate DESC, pi.InvoiceID DESC
                        LIMIT @maxCount";

                    using (var cmd = new SQLiteCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@maxCount", maxCount);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                list.Add(new ReturnInvoiceListItem
                                {
                                    InvoiceID = Convert.ToInt32(reader["InvoiceID"]),
                                    InvoiceNumber = reader["InvoiceNumber"].ToString(),
                                    InvoiceDate = Convert.ToDateTime(reader["InvoiceDate"]),
                                    TotalAmount = reader["TotalAmount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmount"]),
                                    PartyName = reader["PartyName"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تحميل قائمة فواتير المشتريات لاقتراحات المرتجع", ex, "InvoiceService");
            }
            return list;
        }

        /// <summary>
        /// البحث عن فاتورة بيع أصلية برقمها، مع حساب الكمية المتاحة للإرجاع لكل صنف بها
        /// (بعد خصم أي مرتجعات سابقة على نفس الفاتورة والصنف)
        /// </summary>
        public async Task<InvoiceForReturnResult> GetSalesInvoiceForReturnAsync(string invoiceNumber)
        {
            var result = new InvoiceForReturnResult();
            try
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    result.Message = "يرجى إدخال رقم الفاتورة";
                    return result;
                }

                using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string headerSql = @"
                        SELECT si.InvoiceID, si.InvoiceNumber, si.InvoiceDate, si.CustomerID, si.StoreID,
                               COALESCE(c.CustomerNameAr, c.CustomerName, 'عميل غير معروف') AS CustomerName,
                               COALESCE(s.StoreNameAr, '') AS StoreName
                        FROM SalesInvoices si
                        LEFT JOIN Customers c ON si.CustomerID = c.CustomerID
                        LEFT JOIN Stores s ON si.StoreID = s.StoreID
                        WHERE si.InvoiceNumber = @invoiceNumber
                        AND si.InvoiceType = 'Sales'
                        AND si.IsVoid = 0";

                    using (var cmd = new SQLiteCommand(headerSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber.Trim());
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                result.Message = "لم يتم العثور على فاتورة بيع بهذا الرقم، أو أنها فاتورة مُلغاة";
                                return result;
                            }

                            result.Found = true;
                            result.InvoiceID = Convert.ToInt32(reader["InvoiceID"]);
                            result.InvoiceNumber = reader["InvoiceNumber"].ToString();
                            result.InvoiceDate = Convert.ToDateTime(reader["InvoiceDate"]);
                            result.PartyID = reader["CustomerID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CustomerID"]);
                            result.PartyName = reader["CustomerName"].ToString();
                            result.StoreID = Convert.ToInt32(reader["StoreID"]);
                            result.StoreName = reader["StoreName"].ToString();
                        }
                    }

                    if (result.PartyID == 0)
                    {
                        result.Found = false;
                        result.Message = "لا يمكن عمل مرتجع لفاتورة بدون عميل محدد";
                        return result;
                    }

                    string itemsSql = @"
                        SELECT sii.ProductID, p.ProductNameAr AS ProductName, p.Unit AS Unit,
                               sii.Quantity, sii.UnitPrice,
                               COALESCE((
                                   SELECT SUM(rsii.Quantity)
                                   FROM SalesInvoiceItems rsii
                                   INNER JOIN SalesInvoices rsi ON rsii.InvoiceID = rsi.InvoiceID
                                   WHERE rsi.OriginalInvoiceID = si.InvoiceID
                                   AND rsi.InvoiceType = 'SalesReturn'
                                   AND rsi.IsVoid = 0
                                   AND rsii.ProductID = sii.ProductID
                               ), 0) AS AlreadyReturned
                        FROM SalesInvoiceItems sii
                        INNER JOIN SalesInvoices si ON sii.InvoiceID = si.InvoiceID
                        INNER JOIN Products p ON sii.ProductID = p.ProductID
                        WHERE sii.InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(itemsSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", result.InvoiceID);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Items.Add(new ReturnableInvoiceItem
                                {
                                    ProductID = Convert.ToInt32(reader["ProductID"]),
                                    ProductName = reader["ProductName"].ToString(),
                                    Unit = reader["Unit"] == DBNull.Value ? "" : reader["Unit"].ToString(),
                                    OriginalQuantity = Convert.ToDecimal(reader["Quantity"]),
                                    UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                                    AlreadyReturnedQuantity = Convert.ToDecimal(reader["AlreadyReturned"])
                                });
                            }
                        }
                    }
                }

                if (result.Items.Count == 0)
                {
                    result.Found = false;
                    result.Message = "الفاتورة لا تحتوي على أي أصناف";
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ GetSalesInvoiceForReturnAsync", ex, "InvoiceService");
                result.Found = false;
                result.Message = $"حدث خطأ أثناء البحث عن الفاتورة: {ex.Message}";
                return result;
            }
        }

        public async Task<InvoiceForReturnResult> GetPurchaseInvoiceForReturnAsync(string invoiceNumber)
        {
            var result = new InvoiceForReturnResult();
            try
            {
                if (string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    result.Message = "يرجى إدخال رقم الفاتورة";
                    return result;
                }

                using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    string headerSql = @"
                        SELECT pi.InvoiceID, pi.InvoiceNumber, pi.InvoiceDate, pi.SupplierID, pi.StoreID,
                               COALESCE(sup.SupplierNameAr, sup.SupplierName, 'مورد غير معروف') AS SupplierName,
                               COALESCE(s.StoreNameAr, '') AS StoreName
                        FROM PurchaseInvoices pi
                        LEFT JOIN Suppliers sup ON pi.SupplierID = sup.SupplierID
                        LEFT JOIN Stores s ON pi.StoreID = s.StoreID
                        WHERE pi.InvoiceNumber = @invoiceNumber
                        AND pi.InvoiceType = 'Purchase'
                        AND pi.IsVoid = 0";

                    using (var cmd = new SQLiteCommand(headerSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceNumber", invoiceNumber.Trim());
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                result.Message = "لم يتم العثور على فاتورة مشتريات بهذا الرقم، أو أنها فاتورة مُلغاة";
                                return result;
                            }

                            result.Found = true;
                            result.InvoiceID = Convert.ToInt32(reader["InvoiceID"]);
                            result.InvoiceNumber = reader["InvoiceNumber"].ToString();
                            result.InvoiceDate = Convert.ToDateTime(reader["InvoiceDate"]);
                            result.PartyID = reader["SupplierID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SupplierID"]);
                            result.PartyName = reader["SupplierName"].ToString();
                            result.StoreID = Convert.ToInt32(reader["StoreID"]);
                            result.StoreName = reader["StoreName"].ToString();
                        }
                    }

                    if (result.PartyID == 0)
                    {
                        result.Found = false;
                        result.Message = "لا يمكن عمل مرتجع لفاتورة بدون مورد محدد";
                        return result;
                    }

                    string itemsSql = @"
                        SELECT pii.ProductID, p.ProductNameAr AS ProductName, p.Unit AS Unit,
                               pii.Quantity, pii.UnitPrice,
                               COALESCE((
                                   SELECT SUM(rpii.Quantity)
                                   FROM PurchaseInvoiceItems rpii
                                   INNER JOIN PurchaseInvoices rpi ON rpii.InvoiceID = rpi.InvoiceID
                                   WHERE rpi.OriginalInvoiceID = pi.InvoiceID
                                   AND rpi.InvoiceType = 'PurchaseReturn'
                                   AND rpi.IsVoid = 0
                                   AND rpii.ProductID = pii.ProductID
                               ), 0) AS AlreadyReturned
                        FROM PurchaseInvoiceItems pii
                        INNER JOIN PurchaseInvoices pi ON pii.InvoiceID = pi.InvoiceID
                        INNER JOIN Products p ON pii.ProductID = p.ProductID
                        WHERE pii.InvoiceID = @invoiceId";

                    using (var cmd = new SQLiteCommand(itemsSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", result.InvoiceID);
                        using (var reader = (SQLiteDataReader)await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Items.Add(new ReturnableInvoiceItem
                                {
                                    ProductID = Convert.ToInt32(reader["ProductID"]),
                                    ProductName = reader["ProductName"].ToString(),
                                    Unit = reader["Unit"] == DBNull.Value ? "" : reader["Unit"].ToString(),
                                    OriginalQuantity = Convert.ToDecimal(reader["Quantity"]),
                                    UnitPrice = Convert.ToDecimal(reader["UnitPrice"]),
                                    AlreadyReturnedQuantity = Convert.ToDecimal(reader["AlreadyReturned"])
                                });
                            }
                        }
                    }
                }

                if (result.Items.Count == 0)
                {
                    result.Found = false;
                    result.Message = "الفاتورة لا تحتوي على أي أصناف";
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("فشل تنفيذ GetPurchaseInvoiceForReturnAsync", ex, "InvoiceService");
                result.Found = false;
                result.Message = $"حدث خطأ أثناء البحث عن الفاتورة: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// حفظ فاتورة مرتجع بيع جديدة: تُنشئ فاتورة مرتبطة بالفاتورة الأصلية، تُعيد الكمية المرتجعة
        /// إلى المخزون، وتُنقص المبلغ المرتجع من رصيد العميل (لأنه أصبح يدين بمبلغ أقل).
        ///
        /// ملاحظة هامة: حركة العميل تُسجَّل بنوع 'Refund' (وليس 'SalesReturn') لأن عمود
        /// TransactionType في جدول CustomerTransactions مقيَّد بقائمة قيم محددة سلفاً
        /// (Invoice, Receipt, Refund, Check, Adjustment) عبر قيد CHECK في قاعدة البيانات،
        /// و'Refund' هي القيمة الجاهزة والمناسبة دلالياً لهذه الحالة (ولم تكن مستخدمة من قبل
        /// في أي مكان آخر بالنظام، فلا يوجد أي تعارض).
        /// </summary>
        public async Task<(bool Success, string Message, int InvoiceId)> SaveSalesReturnInvoiceAsync(
            int originalInvoiceId,
            int customerId,
            int storeId,
            DateTime returnDate,
            List<SalesInvoiceItemClass> returnItems,
            string notes,
            int createdBy)
        {
            if (returnItems == null || returnItems.Count == 0)
                return (false, "يجب اختيار صنف واحد على الأقل للإرجاع بكمية أكبر من صفر", 0);

            using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        decimal totalAmount = returnItems.Sum(i => i.TotalAmount);

                        string returnInvoiceNumber = await GenerateReturnInvoiceNumberAsync(connection, transaction, "SalesInvoices", "SRTN");

                        string insertHeaderSql = @"
                            INSERT INTO SalesInvoices (
                                InvoiceNumber, InvoiceDate, CustomerID, StoreID, InvoiceType, OriginalInvoiceID,
                                SubTotal, TotalAmount, PaidAmount, RemainingAmount,
                                PaymentStatus, IsPosted, PostedDate, PostedBy, CreatedBy, CreatedDate, Notes
                            ) VALUES (
                                @invoiceNumber, @invoiceDate, @customerId, @storeId, 'SalesReturn', @originalInvoiceId,
                                @totalAmount, @totalAmount, 0, @totalAmount,
                                'Paid', 1, @postedDate, @createdBy, @createdBy, @createdDate, @notes
                            );
                            SELECT last_insert_rowid();";

                        int newInvoiceId;
                        using (var cmd = new SQLiteCommand(insertHeaderSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                            cmd.Parameters.AddWithValue("@invoiceDate", returnDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@customerId", customerId);
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@originalInvoiceId", originalInvoiceId);
                            cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                            cmd.Parameters.AddWithValue("@postedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@notes", (object)notes ?? "");

                            newInvoiceId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        foreach (var item in returnItems)
                        {
                            decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;

                            string insertItemSql = @"
                                INSERT INTO SalesInvoiceItems (
                                    InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                                    DiscountPercent, DiscountAmount, TotalAmount, CreatedDate
                                ) VALUES (
                                    @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                                    0, 0, @totalAmount, @createdDate
                                )";

                            using (var cmd = new SQLiteCommand(insertItemSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                                cmd.Parameters.AddWithValue("@productId", item.ProductID);
                                cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@quantityInBaseUnit", quantityInBaseUnit);
                                cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                                cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                                cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                await cmd.ExecuteNonQueryAsync();
                            }

                            decimal currentQuantity = 0;
                            using (var getCmd = new SQLiteCommand(
                                "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId",
                                connection, transaction))
                            {
                                getCmd.Parameters.AddWithValue("@storeId", storeId);
                                getCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                object qtyResult = await getCmd.ExecuteScalarAsync();
                                currentQuantity = SafeToDecimal(qtyResult);
                            }

                            decimal quantityAfter = currentQuantity + quantityInBaseUnit;

                            using (var updInvCmd = new SQLiteCommand(@"
                                UPDATE StoreInventory
                                SET Quantity = Quantity + @quantity,
                                    AvailableQuantity = AvailableQuantity + @quantity,
                                    LastUpdated = CURRENT_TIMESTAMP
                                WHERE StoreID = @storeId AND ProductID = @productId",
                                connection, transaction))
                            {
                                updInvCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                updInvCmd.Parameters.AddWithValue("@storeId", storeId);
                                updInvCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                await updInvCmd.ExecuteNonQueryAsync();
                            }

                            using (var updProdCmd = new SQLiteCommand(@"
                                UPDATE Products
                                SET QuantityInBaseUnit = COALESCE(QuantityInBaseUnit, 0) + @quantity
                                WHERE ProductID = @productId",
                                connection, transaction))
                            {
                                updProdCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                updProdCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                await updProdCmd.ExecuteNonQueryAsync();
                            }

                            using (var invTxCmd = new SQLiteCommand(@"
                                INSERT INTO InventoryTransactions (
                                    StoreID, ProductID, TransactionDate, TransactionType,
                                    Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                                    ReferenceType, ReferenceID, ReferenceNumber, CreatedBy
                                ) VALUES (
                                    @storeId, @productId, @transactionDate, 'Return',
                                    @quantity, @quantityBefore, @quantityAfter, @unitPrice, @totalAmount,
                                    'SALES_RETURN', @invoiceId, @invoiceNumber, @createdBy
                                )", connection, transaction))
                            {
                                invTxCmd.Parameters.AddWithValue("@storeId", storeId);
                                invTxCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                invTxCmd.Parameters.AddWithValue("@transactionDate", returnDate.ToString("yyyy-MM-dd"));
                                invTxCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                invTxCmd.Parameters.AddWithValue("@quantityBefore", currentQuantity);
                                invTxCmd.Parameters.AddWithValue("@quantityAfter", quantityAfter);
                                invTxCmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                                invTxCmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                                invTxCmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                                invTxCmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                                invTxCmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await invTxCmd.ExecuteNonQueryAsync();
                            }
                        }

                        decimal currentCustomerBalance = await GetCustomerBalanceAsync(connection, transaction, customerId);
                        decimal customerBalanceAfterReturn = currentCustomerBalance - totalAmount;

                        using (var custTxCmd = new SQLiteCommand(@"
                            INSERT INTO CustomerTransactions (
                                CustomerID, TransactionDate, TransactionType,
                                DebitAmount, CreditAmount, BalanceAfter,
                                ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                            ) VALUES (
                                @customerId, @date, 'Refund',
                                0, @amount, @balanceAfter,
                                'SALES_INVOICE_RETURN', @invoiceId, @invoiceNumber, @description, @createdBy, CURRENT_TIMESTAMP
                            )", connection, transaction))
                        {
                            custTxCmd.Parameters.AddWithValue("@customerId", customerId);
                            custTxCmd.Parameters.AddWithValue("@date", returnDate.ToString("yyyy-MM-dd"));
                            custTxCmd.Parameters.AddWithValue("@amount", totalAmount);
                            custTxCmd.Parameters.AddWithValue("@balanceAfter", customerBalanceAfterReturn);
                            custTxCmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                            custTxCmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                            custTxCmd.Parameters.AddWithValue("@description", string.IsNullOrEmpty(notes) ? $"مرتجع بيع - فاتورة {returnInvoiceNumber}" : notes);
                            custTxCmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await custTxCmd.ExecuteNonQueryAsync();
                        }

                        await UpdateCustomerBalanceAsync(customerId, customerBalanceAfterReturn, connection, transaction);

                        transaction.Commit();
                        Logger.LogInfo($"تم حفظ فاتورة مرتجع بيع {returnInvoiceNumber} بقيمة {totalAmount:N2} مرتبطة بالفاتورة الأصلية #{originalInvoiceId}", "InvoiceService");

                        return (true, $"تم حفظ مرتجع البيع بنجاح برقم {returnInvoiceNumber}", newInvoiceId);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.LogError("فشل تنفيذ SaveSalesReturnInvoiceAsync", ex, "InvoiceService");
                        return (false, $"حدث خطأ أثناء حفظ مرتجع البيع: {ex.Message}", 0);
                    }
                }
            }
        }

        /// <summary>
        /// حفظ فاتورة مرتجع مشتريات جديدة: تُنشئ فاتورة مرتبطة بالفاتورة الأصلية، تُنقص الكمية
        /// المرتجعة من المخزون (لأننا نعيدها للمورد)، وتُنقص المبلغ المرتجع من رصيدنا لدى المورد.
        /// نفس ملاحظة استخدام 'Refund' بدل 'PurchaseReturn' في حركة المورد (بسبب قيد CHECK).
        /// </summary>
        public async Task<(bool Success, string Message, int InvoiceId)> SavePurchaseReturnInvoiceAsync(
            int originalInvoiceId,
            int supplierId,
            int storeId,
            DateTime returnDate,
            List<PurchaseInvoiceItemClass> returnItems,
            string notes,
            int createdBy)
        {
            if (returnItems == null || returnItems.Count == 0)
                return (false, "يجب اختيار صنف واحد على الأقل للإرجاع بكمية أكبر من صفر", 0);

            using (var connection = new SQLiteConnection(_databaseService.GetConnectionString()))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        decimal totalAmount = returnItems.Sum(i => i.TotalAmount);

                        string returnInvoiceNumber = await GenerateReturnInvoiceNumberAsync(connection, transaction, "PurchaseInvoices", "PRTN");

                        string insertHeaderSql = @"
                            INSERT INTO PurchaseInvoices (
                                InvoiceNumber, InvoiceDate, SupplierID, StoreID, InvoiceType, OriginalInvoiceID,
                                SubTotal, TotalAmount, PaidAmount, RemainingAmount,
                                PaymentStatus, IsPosted, PostedDate, PostedBy, CreatedBy, CreatedDate, Notes
                            ) VALUES (
                                @invoiceNumber, @invoiceDate, @supplierId, @storeId, 'PurchaseReturn', @originalInvoiceId,
                                @totalAmount, @totalAmount, 0, @totalAmount,
                                'Paid', 1, @postedDate, @createdBy, @createdBy, @createdDate, @notes
                            );
                            SELECT last_insert_rowid();";

                        int newInvoiceId;
                        using (var cmd = new SQLiteCommand(insertHeaderSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                            cmd.Parameters.AddWithValue("@invoiceDate", returnDate.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@supplierId", supplierId);
                            cmd.Parameters.AddWithValue("@storeId", storeId);
                            cmd.Parameters.AddWithValue("@originalInvoiceId", originalInvoiceId);
                            cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                            cmd.Parameters.AddWithValue("@postedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@createdBy", createdBy);
                            cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@notes", (object)notes ?? "");

                            newInvoiceId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        foreach (var item in returnItems)
                        {
                            decimal quantityInBaseUnit = item.QuantityInBaseUnit > 0 ? item.QuantityInBaseUnit : item.Quantity;

                            string insertItemSql = @"
                                INSERT INTO PurchaseInvoiceItems (
                                    InvoiceID, ProductID, Quantity, QuantityInBaseUnit, UnitPrice,
                                    DiscountPercent, DiscountAmount, TotalAmount, CreatedDate
                                ) VALUES (
                                    @invoiceId, @productId, @quantity, @quantityInBaseUnit, @unitPrice,
                                    0, 0, @totalAmount, @createdDate
                                )";

                            using (var cmd = new SQLiteCommand(insertItemSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                                cmd.Parameters.AddWithValue("@productId", item.ProductID);
                                cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                                cmd.Parameters.AddWithValue("@quantityInBaseUnit", quantityInBaseUnit);
                                cmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                                cmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                                cmd.Parameters.AddWithValue("@createdDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                await cmd.ExecuteNonQueryAsync();
                            }

                            decimal currentQuantity = 0;
                            using (var getCmd = new SQLiteCommand(
                                "SELECT COALESCE(Quantity, 0) FROM StoreInventory WHERE StoreID = @storeId AND ProductID = @productId",
                                connection, transaction))
                            {
                                getCmd.Parameters.AddWithValue("@storeId", storeId);
                                getCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                object qtyResult = await getCmd.ExecuteScalarAsync();
                                currentQuantity = SafeToDecimal(qtyResult);
                            }

                            decimal quantityAfter = currentQuantity - quantityInBaseUnit;

                            using (var updInvCmd = new SQLiteCommand(@"
                                UPDATE StoreInventory
                                SET Quantity = Quantity - @quantity,
                                    AvailableQuantity = AvailableQuantity - @quantity,
                                    LastUpdated = CURRENT_TIMESTAMP
                                WHERE StoreID = @storeId AND ProductID = @productId",
                                connection, transaction))
                            {
                                updInvCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                updInvCmd.Parameters.AddWithValue("@storeId", storeId);
                                updInvCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                await updInvCmd.ExecuteNonQueryAsync();
                            }

                            using (var updProdCmd = new SQLiteCommand(@"
                                UPDATE Products
                                SET QuantityInBaseUnit = COALESCE(QuantityInBaseUnit, 0) - @quantity
                                WHERE ProductID = @productId",
                                connection, transaction))
                            {
                                updProdCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                updProdCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                await updProdCmd.ExecuteNonQueryAsync();
                            }

                            using (var invTxCmd = new SQLiteCommand(@"
                                INSERT INTO InventoryTransactions (
                                    StoreID, ProductID, TransactionDate, TransactionType,
                                    Quantity, QuantityBefore, QuantityAfter, UnitPrice, TotalAmount,
                                    ReferenceType, ReferenceID, ReferenceNumber, CreatedBy
                                ) VALUES (
                                    @storeId, @productId, @transactionDate, 'Return',
                                    -@quantity, @quantityBefore, @quantityAfter, @unitPrice, @totalAmount,
                                    'PURCHASE_RETURN', @invoiceId, @invoiceNumber, @createdBy
                                )", connection, transaction))
                            {
                                invTxCmd.Parameters.AddWithValue("@storeId", storeId);
                                invTxCmd.Parameters.AddWithValue("@productId", item.ProductID);
                                invTxCmd.Parameters.AddWithValue("@transactionDate", returnDate.ToString("yyyy-MM-dd"));
                                invTxCmd.Parameters.AddWithValue("@quantity", quantityInBaseUnit);
                                invTxCmd.Parameters.AddWithValue("@quantityBefore", currentQuantity);
                                invTxCmd.Parameters.AddWithValue("@quantityAfter", quantityAfter);
                                invTxCmd.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                                invTxCmd.Parameters.AddWithValue("@totalAmount", item.TotalAmount);
                                invTxCmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                                invTxCmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                                invTxCmd.Parameters.AddWithValue("@createdBy", createdBy);
                                await invTxCmd.ExecuteNonQueryAsync();
                            }
                        }

                        decimal currentSupplierBalance = await GetSupplierBalanceAsync(connection, transaction, supplierId);
                        decimal supplierBalanceAfterReturn = currentSupplierBalance - totalAmount;

                        using (var suppTxCmd = new SQLiteCommand(@"
                            INSERT INTO SupplierTransactions (
                                SupplierID, TransactionDate, TransactionType,
                                DebitAmount, CreditAmount, BalanceAfter,
                                ReferenceType, ReferenceID, ReferenceNumber, Description, CreatedBy, CreatedDate
                            ) VALUES (
                                @supplierId, @date, 'Refund',
                                @amount, 0, @balanceAfter,
                                'PURCHASE_INVOICE_RETURN', @invoiceId, @invoiceNumber, @description, @createdBy, CURRENT_TIMESTAMP
                            )", connection, transaction))
                        {
                            suppTxCmd.Parameters.AddWithValue("@supplierId", supplierId);
                            suppTxCmd.Parameters.AddWithValue("@date", returnDate.ToString("yyyy-MM-dd"));
                            suppTxCmd.Parameters.AddWithValue("@amount", totalAmount);
                            suppTxCmd.Parameters.AddWithValue("@balanceAfter", supplierBalanceAfterReturn);
                            suppTxCmd.Parameters.AddWithValue("@invoiceId", newInvoiceId);
                            suppTxCmd.Parameters.AddWithValue("@invoiceNumber", returnInvoiceNumber);
                            suppTxCmd.Parameters.AddWithValue("@description", string.IsNullOrEmpty(notes) ? $"مرتجع مشتريات - فاتورة {returnInvoiceNumber}" : notes);
                            suppTxCmd.Parameters.AddWithValue("@createdBy", createdBy);
                            await suppTxCmd.ExecuteNonQueryAsync();
                        }

                        await UpdateSupplierBalanceAsync(supplierId, supplierBalanceAfterReturn, connection, transaction);

                        transaction.Commit();
                        Logger.LogInfo($"تم حفظ فاتورة مرتجع مشتريات {returnInvoiceNumber} بقيمة {totalAmount:N2} مرتبطة بالفاتورة الأصلية #{originalInvoiceId}", "InvoiceService");

                        return (true, $"تم حفظ مرتجع المشتريات بنجاح برقم {returnInvoiceNumber}", newInvoiceId);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.LogError("فشل تنفيذ SavePurchaseReturnInvoiceAsync", ex, "InvoiceService");
                        return (false, $"حدث خطأ أثناء حفظ مرتجع المشتريات: {ex.Message}", 0);
                    }
                }
            }
        }

        private async Task<string> GenerateReturnInvoiceNumberAsync(SQLiteConnection connection, SQLiteTransaction transaction, string tableName, string prefix)
        {
            var countCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {tableName}", connection, transaction);
            int count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            countCmd.Dispose();

            int next = count + 1;
            string candidate;

            do
            {
                candidate = $"{prefix}-{next:D6}";

                var checkCmd = new SQLiteCommand($"SELECT COUNT(*) FROM {tableName} WHERE InvoiceNumber = @num", connection, transaction);
                checkCmd.Parameters.AddWithValue("@num", candidate);
                bool exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                checkCmd.Dispose();

                if (!exists) break;
                next++;
            } while (true);

            return candidate;
        }

        #endregion

        #region كلاسات العناصر (Item Classes) - المعدلة لدعم الوحدات

        public class PurchaseInvoiceItemClass
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal QuantityInBaseUnit { get; set; }
        }

        public class SalesInvoiceItemClass
        {
            public int ProductID { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal QuantityInBaseUnit { get; set; }
        }

        #endregion
    }
}