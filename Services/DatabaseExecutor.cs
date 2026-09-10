// DatabaseExecutor.cs
using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// منفذ آمن لعمليات قاعدة البيانات مع دعم المعاملات وإعادة المحاولة
    /// </summary>
    public static class DatabaseExecutor
    {
        /// <summary>
        /// تنفيذ عملية مع إعادة المحاولة التلقائية عند القفل
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(
            DatabaseService databaseService,
            Func<SQLiteConnection, SQLiteTransaction, Task<T>> action,
            CancellationToken cancellationToken = default,
            int maxRetries = 5)
        {
            int retryCount = 0;
            int delay = 100;

            while (retryCount < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SQLiteConnection connection = null;
                SQLiteTransaction transaction = null;

                try
                {
                    // ✅ إنشاء اتصال مباشر (بدون ManagedConnection)
                    connection = new SQLiteConnection(databaseService.GetConnectionString());
                    connection.BusyTimeout = 120;
                    await connection.OpenAsync(cancellationToken);

                    transaction = connection.BeginTransaction();

                    var result = await action(connection, transaction);

                    // ✅ التحقق من صحة المعاملة قبل Commit
                    if (transaction != null &&
                        transaction.Connection != null &&
                        transaction.Connection.State == System.Data.ConnectionState.Open)
                    {
                        transaction.Commit();
                        System.Diagnostics.Debug.WriteLine("✅ تم Commit المعاملة بنجاح");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ المعاملة غير صالحة، تم تخطي Commit");
                    }

                    return result;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ فشل التنفيذ بعد {maxRetries} محاولات: {ex.Message}");
                        throw new TimeoutException($"تعذر تنفيذ العملية بعد {maxRetries} محاولات", ex);
                    }

                    int actualDelay = Math.Min(delay, 2000);
                    System.Diagnostics.Debug.WriteLine($"⚠️ إعادة محاولة {retryCount}/{maxRetries} بعد {actualDelay}ms");
                    await Task.Delay(actualDelay, cancellationToken);
                    delay *= 2;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في التنفيذ: {ex.Message}");

                    // ✅ محاولة Rollback فقط إذا كانت المعاملة صالحة
                    try
                    {
                        if (transaction != null &&
                            transaction.Connection != null &&
                            transaction.Connection.State == System.Data.ConnectionState.Open)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine("✅ تم Rollback المعاملة");
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ خطأ في Rollback: {rollbackEx.Message}");
                    }

                    throw;
                }
                finally
                {
                    // ✅ إغلاق المعاملة والاتصال بشكل آمن
                    try
                    {
                        if (transaction != null)
                        {
                            try { transaction.Dispose(); } catch { }
                        }
                        if (connection != null && connection.State == System.Data.ConnectionState.Open)
                        {
                            connection.Close();
                            connection.Dispose();
                            System.Diagnostics.Debug.WriteLine("✅ تم إغلاق الاتصال");
                        }
                    }
                    catch (Exception closeEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ خطأ في إغلاق الاتصال: {closeEx.Message}");
                    }
                }
            }

            throw new TimeoutException("تعذر تنفيذ العملية");
        }

        /// <summary>
        /// تنفيذ عملية معاملة مع إعادة المحاولة التلقائية
        /// </summary>
        public static async Task<T> ExecuteTransactionAsync<T>(
            DatabaseService databaseService,
            Func<SQLiteConnection, SQLiteTransaction, Task<T>> action,
            CancellationToken cancellationToken = default,
            int maxRetries = 5)
        {
            return await ExecuteAsync(databaseService, action, cancellationToken, maxRetries);
        }

        /// <summary>
        /// تنفيذ عملية بدون معاملة مع إرجاع قيمة
        /// </summary>
        public static async Task<T> ExecuteNonTransactionAsync<T>(
            DatabaseService databaseService,
            Func<SQLiteConnection, Task<T>> action,
            CancellationToken cancellationToken = default,
            int maxRetries = 3)
        {
            return await ExecuteAsync(databaseService, async (connection, transaction) =>
            {
                return await action(connection);
            }, cancellationToken, maxRetries);
        }

        /// <summary>
        /// تنفيذ عملية بدون معاملة وبدون إرجاع قيمة (void)
        /// </summary>
        public static async Task ExecuteVoidAsync(
            DatabaseService databaseService,
            Func<SQLiteConnection, Task> action,
            CancellationToken cancellationToken = default,
            int maxRetries = 3)
        {
            await ExecuteAsync<bool>(databaseService, async (connection, transaction) =>
            {
                await action(connection);
                return true;
            }, cancellationToken, maxRetries);
        }
    }
}