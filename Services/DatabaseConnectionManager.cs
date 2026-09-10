using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace RasidAccountingSystem.Services
{
    /// <summary>
    /// مدير مركزي لإدارة اتصالات قاعدة البيانات مع دعم إعادة المحاولة التلقائية
    /// </summary>
    public class DatabaseConnectionManager : IDisposable
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly ConcurrentDictionary<int, SQLiteConnection> _activeConnections;
        private int _connectionCounter = 0;
        private bool _disposed = false;
        private readonly object _lockObject = new object();

        // إعدادات إعادة المحاولة
        private const int MAX_RETRY_COUNT = 10;
        private const int INITIAL_RETRY_DELAY_MS = 100;
        private const int MAX_RETRY_DELAY_MS = 5000;
        private const int MAX_CONCURRENT_CONNECTIONS = 50;
        private const int BUSY_TIMEOUT_SECONDS = 120;

        public DatabaseConnectionManager(string databasePath)
        {
            _connectionString = $@"
                Data Source={databasePath};
                Version=3;
                FailIfMissing=False;
                Journal Mode=WAL;
                Pooling=True;
                Max Pool Size=200;
                BusyTimeout={BUSY_TIMEOUT_SECONDS};
                Synchronous=NORMAL;
                Cache Size=10000;
                Page Size=4096;
                Default Isolation Level=ReadCommitted";

            _connectionSemaphore = new SemaphoreSlim(MAX_CONCURRENT_CONNECTIONS, MAX_CONCURRENT_CONNECTIONS);
            _activeConnections = new ConcurrentDictionary<int, SQLiteConnection>();
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }

        /// <summary>
        /// الحصول على اتصال جديد مع إعادة المحاولة التلقائية عند القفل
        /// </summary>
        public async Task<ManagedConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            int retryCount = 0;
            int delay = INITIAL_RETRY_DELAY_MS;

            while (retryCount < MAX_RETRY_COUNT)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _connectionSemaphore.WaitAsync(cancellationToken);

                    var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken);

                    // تعيين مهلة إضافية للأوامر
                    using (var cmd = new SQLiteCommand("PRAGMA busy_timeout = 120000;", connection))
                    {
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }

                    int connectionId = Interlocked.Increment(ref _connectionCounter);
                    _activeConnections.TryAdd(connectionId, connection);

                    System.Diagnostics.Debug.WriteLine($"✅ تم فتح اتصال جديد ID: {connectionId}, الاتصالات النشطة: {_activeConnections.Count}");

                    return new ManagedConnection(connection, connectionId, this);
                }
                catch (SQLiteException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("busy"))
                {
                    retryCount++;
                    if (retryCount >= MAX_RETRY_COUNT)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ فشل الاتصال بعد {MAX_RETRY_COUNT} محاولات: {ex.Message}");
                        throw new TimeoutException($"تعذر الاتصال بقاعدة البيانات بعد {MAX_RETRY_COUNT} محاولات", ex);
                    }

                    int actualDelay = Math.Min(delay, MAX_RETRY_DELAY_MS);
                    System.Diagnostics.Debug.WriteLine($"⚠️ قاعدة البيانات مشغولة، محاولة {retryCount}/{MAX_RETRY_COUNT}... الانتظار {actualDelay}ms");
                    await Task.Delay(actualDelay, cancellationToken);
                    delay *= 2;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ خطأ في الاتصال: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (retryCount >= MAX_RETRY_COUNT)
                    {
                        _connectionSemaphore.Release();
                    }
                }
            }

            throw new TimeoutException("تعذر الحصول على اتصال بقاعدة البيانات");
        }

        /// <summary>
        /// تحرير اتصال وإعادته إلى التجمع
        /// </summary>
        internal void ReleaseConnection(int connectionId)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (_activeConnections.TryRemove(connectionId, out var connection))
                {
                    try
                    {
                        if (connection.State != System.Data.ConnectionState.Closed)
                        {
                            connection.Close();
                        }
                        connection.Dispose();
                        System.Diagnostics.Debug.WriteLine($"✅ تم إغلاق الاتصال ID: {connectionId}, الاتصالات المتبقية: {_activeConnections.Count}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ خطأ في إغلاق الاتصال: {ex.Message}");
                    }
                }
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// إغلاق جميع الاتصالات النشطة
        /// </summary>
        public void CloseAllConnections()
        {
            lock (_lockObject)
            {
                foreach (var kvp in _activeConnections)
                {
                    try
                    {
                        var connection = kvp.Value;
                        if (connection.State != System.Data.ConnectionState.Closed)
                        {
                            connection.Close();
                        }
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ خطأ في إغلاق الاتصال {kvp.Key}: {ex.Message}");
                    }
                }
                _activeConnections.Clear();

                while (_connectionSemaphore.CurrentCount < MAX_CONCURRENT_CONNECTIONS)
                {
                    _connectionSemaphore.Release();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseAllConnections();
            _connectionSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// اتصال مُدار مع تحرير تلقائي
    /// </summary>
    public class ManagedConnection : IDisposable
    {
        public SQLiteConnection Connection { get; }
        private readonly int _connectionId;
        private readonly DatabaseConnectionManager _manager;
        private bool _disposed = false;

        internal ManagedConnection(SQLiteConnection connection, int connectionId, DatabaseConnectionManager manager)
        {
            Connection = connection;
            _connectionId = connectionId;
            _manager = manager;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _manager.ReleaseConnection(_connectionId);
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, Action<SQLiteParameterCollection> parameterAction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = new SQLiteCommand(sql, Connection))
            {
                parameterAction?.Invoke(cmd.Parameters);
                return await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public async Task<object> ExecuteScalarAsync(string sql, Action<SQLiteParameterCollection> parameterAction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = new SQLiteCommand(sql, Connection))
            {
                parameterAction?.Invoke(cmd.Parameters);
                return await cmd.ExecuteScalarAsync(cancellationToken);
            }
        }

        /// <summary>
        /// تنفيذ استعلام وإرجاع SQLiteDataReader
        /// </summary>
        public async Task<SQLiteDataReader> ExecuteReaderAsync(string sql, Action<SQLiteParameterCollection> parameterAction = null, CancellationToken cancellationToken = default)
        {
            var cmd = new SQLiteCommand(sql, Connection);
            parameterAction?.Invoke(cmd.Parameters);

            // ✅ تحويل صريح إلى SQLiteDataReader
            var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return (SQLiteDataReader)reader;
        }
    }
}