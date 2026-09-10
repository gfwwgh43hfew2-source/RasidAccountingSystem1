using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace RasidAccountingSystem.Services
{
    #region كلاسات البيانات (Models)

    public class AdjustmentItem
    {
        public int RowNumber { get; set; }
        public int AdjustmentID { get; set; }
        public string AdjustmentNumber { get; set; }
        public string WarehouseName { get; set; }
        public int ItemsCount { get; set; }
        public string Reason { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public string Status { get; set; }
        public string CreatedByName { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class AdjustmentItemDetail
    {
        public int RowNumber { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public decimal SystemQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal DifferenceQuantity { get; set; }
        public string AdjustmentType { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }

        // ✅ إضافة دعم للوحدات
        public string UnitName { get; set; } = "وحدة";
        public string UnitTag { get; set; } = "Unit3";
        public decimal QuantityInBaseUnit { get; set; } // الكمية المحولة إلى الوحدة الأساسية
    }

    public class AdjustmentDetail
    {
        public int AdjustmentID { get; set; }
        public string AdjustmentNumber { get; set; }
        public int WarehouseID { get; set; }
        public string WarehouseName { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ApprovedBy { get; set; }
        public string ApprovedByName { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string Status { get; set; }
        public decimal TotalCost { get; set; }
        public List<AdjustmentItemDetail> Items { get; set; }
    }

    public class AdjustmentCreateModel
    {
        public int WarehouseID { get; set; }
        public string Reason { get; set; }
        public string Notes { get; set; }
        public DateTime AdjustmentDate { get; set; }
        public List<AdjustmentItemDetail> Items { get; set; }
    }

    #endregion

    public class InventoryAdjustmentService
    {
        private readonly DatabaseService _databaseService;

        public InventoryAdjustmentService()
        {
            _databaseService = new DatabaseService();
        }

        public InventoryAdjustmentService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        // ============================================================
        // ✅ GetAllAdjustmentsAsync
        // ============================================================
        public async Task<List<AdjustmentItem>> GetAllAdjustmentsAsync()
        {
            List<AdjustmentItem> adjustments = new List<AdjustmentItem>();

            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 بدء جلب جميع التسويات من قاعدة البيانات...");

                using (SQLiteConnection connection = _databaseService.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                            adjustment.AdjustmentID,
                            adjustment.AdjustmentNumber,
                            store.StoreNameAr as WarehouseName,
                            adjustment.Reason,
                            adjustment.AdjustmentDate,
                            adjustment.Status,
                            user.FullName AS CreatedByName,
                            adjustment.TotalCost,
                            (
                                SELECT COUNT(*) 
                                FROM InventoryAdjustmentDetails 
                                WHERE AdjustmentID = adjustment.AdjustmentID
                            ) as ItemsCount
                        FROM InventoryAdjustments adjustment
                        INNER JOIN Stores store ON adjustment.WarehouseID = store.StoreID
                        INNER JOIN Users user ON adjustment.CreatedBy = user.UserID
                        ORDER BY adjustment.AdjustmentDate DESC, adjustment.AdjustmentID DESC
                    ";

                    using (SQLiteCommand command = new SQLiteCommand(query, connection))
                    using (SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            AdjustmentItem item = new AdjustmentItem
                            {
                                AdjustmentID = reader["AdjustmentID"] != DBNull.Value ? Convert.ToInt32(reader["AdjustmentID"]) : 0,
                                AdjustmentNumber = reader["AdjustmentNumber"]?.ToString() ?? "غير معروف",
                                WarehouseName = reader["WarehouseName"]?.ToString() ?? "غير معروف",
                                Reason = reader["Reason"]?.ToString() ?? "",
                                AdjustmentDate = reader["AdjustmentDate"] != DBNull.Value ? Convert.ToDateTime(reader["AdjustmentDate"]) : DateTime.Now,
                                Status = reader["Status"]?.ToString() ?? "غير معروف",
                                CreatedByName = reader["CreatedByName"]?.ToString() ?? "غير معروف",
                                TotalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0,
                                ItemsCount = reader["ItemsCount"] != DBNull.Value ? Convert.ToInt32(reader["ItemsCount"]) : 0
                            };

                            adjustments.Add(item);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ تم تحميل {adjustments.Count} تسوية من قاعدة البيانات");
                return adjustments;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetAllAdjustmentsAsync Error: {ex.Message}");
                throw;
            }
        }

        // ============================================================
        // ✅ GetAdjustmentByIdAsync
        // ============================================================
        public async Task<AdjustmentDetail> GetAdjustmentByIdAsync(int adjustmentId)
        {
            using (SQLiteConnection connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();

                string headerQuery = @"
                    SELECT 
                        adjustment.AdjustmentID,
                        adjustment.AdjustmentNumber,
                        adjustment.WarehouseID,
                        store.StoreNameAr as WarehouseName,
                        adjustment.Reason,
                        adjustment.Notes,
                        adjustment.AdjustmentDate,
                        adjustment.CreatedBy,
                        createdUser.FullName AS CreatedByName,
                        adjustment.CreatedDate,
                        adjustment.ApprovedBy,
                        approvedUser.FullName AS ApprovedByName,
                        adjustment.ApprovedDate,
                        adjustment.Status,
                        adjustment.TotalCost
                    FROM InventoryAdjustments adjustment
                    INNER JOIN Stores store ON adjustment.WarehouseID = store.StoreID
                    INNER JOIN Users createdUser ON adjustment.CreatedBy = createdUser.UserID
                    LEFT JOIN Users approvedUser ON adjustment.ApprovedBy = approvedUser.UserID
                    WHERE adjustment.AdjustmentID = @AdjustmentID
                ";

                AdjustmentDetail adjustment = null;

                using (SQLiteCommand command = new SQLiteCommand(headerQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                    using (SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            adjustment = new AdjustmentDetail
                            {
                                AdjustmentID = Convert.ToInt32(reader["AdjustmentID"]),
                                AdjustmentNumber = reader["AdjustmentNumber"].ToString(),
                                WarehouseID = Convert.ToInt32(reader["WarehouseID"]),
                                WarehouseName = reader["WarehouseName"].ToString(),
                                Reason = reader["Reason"].ToString(),
                                Notes = reader["Notes"] != DBNull.Value ? reader["Notes"].ToString() : string.Empty,
                                AdjustmentDate = Convert.ToDateTime(reader["AdjustmentDate"]),
                                CreatedBy = Convert.ToInt32(reader["CreatedBy"]),
                                CreatedByName = reader["CreatedByName"].ToString(),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]),
                                ApprovedBy = reader["ApprovedBy"] != DBNull.Value ? Convert.ToInt32(reader["ApprovedBy"]) : (int?)null,
                                ApprovedByName = reader["ApprovedByName"] != DBNull.Value ? reader["ApprovedByName"].ToString() : string.Empty,
                                ApprovedDate = reader["ApprovedDate"] != DBNull.Value ? Convert.ToDateTime(reader["ApprovedDate"]) : (DateTime?)null,
                                Status = reader["Status"].ToString(),
                                TotalCost = Convert.ToDecimal(reader["TotalCost"]),
                                Items = new List<AdjustmentItemDetail>()
                            };
                        }
                    }
                }

                if (adjustment == null)
                {
                    return null;
                }

                string detailsQuery = @"
                    SELECT 
                        detail.ProductID,
                        product.ProductNameAr as ProductName,
                        product.ProductCode,
                        detail.SystemQuantity,
                        detail.ActualQuantity,
                        detail.DifferenceQuantity,
                        detail.AdjustmentType,
                        detail.UnitCost,
                        detail.TotalCost,
                        detail.UnitName,
                        detail.UnitTag,
                        detail.QuantityInBaseUnit
                    FROM InventoryAdjustmentDetails detail
                    INNER JOIN Products product ON detail.ProductID = product.ProductID
                    WHERE detail.AdjustmentID = @AdjustmentID
                    ORDER BY detail.DetailID
                ";

                using (SQLiteCommand command = new SQLiteCommand(detailsQuery, connection))
                {
                    command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                    using (SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync())
                    {
                        int rowNumber = 1;

                        while (await reader.ReadAsync())
                        {
                            AdjustmentItemDetail detail = new AdjustmentItemDetail
                            {
                                RowNumber = rowNumber++,
                                ProductID = Convert.ToInt32(reader["ProductID"]),
                                ProductName = reader["ProductName"].ToString(),
                                ProductCode = reader["ProductCode"] != DBNull.Value ? reader["ProductCode"].ToString() : string.Empty,
                                SystemQuantity = Convert.ToDecimal(reader["SystemQuantity"]),
                                ActualQuantity = Convert.ToDecimal(reader["ActualQuantity"]),
                                DifferenceQuantity = Convert.ToDecimal(reader["DifferenceQuantity"]),
                                AdjustmentType = reader["AdjustmentType"].ToString(),
                                UnitCost = Convert.ToDecimal(reader["UnitCost"]),
                                TotalCost = Convert.ToDecimal(reader["TotalCost"]),
                                // ✅ قراءة بيانات الوحدات
                                UnitName = reader["UnitName"] != DBNull.Value ? reader["UnitName"].ToString() : "وحدة",
                                UnitTag = reader["UnitTag"] != DBNull.Value ? reader["UnitTag"].ToString() : "Unit3",
                                QuantityInBaseUnit = reader["QuantityInBaseUnit"] != DBNull.Value ? Convert.ToDecimal(reader["QuantityInBaseUnit"]) : 0
                            };

                            adjustment.Items.Add(detail);
                        }
                    }
                }

                return adjustment;
            }
        }

        // ============================================================
        // ✅ CreateAdjustmentAsync - مع دعم الوحدات
        // ============================================================
        public async Task<int> CreateAdjustmentAsync(AdjustmentCreateModel model, int userId)
        {
            if (model.Items == null || model.Items.Count == 0)
            {
                return 0;
            }

            string adjustmentNumber = $"ADJ-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

            decimal totalCost = 0;
            foreach (AdjustmentItemDetail item in model.Items)
            {
                totalCost += item.TotalCost;
            }

            using (SQLiteConnection connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // ============================================
                        // 1️⃣ إدراج رأس التسوية
                        // ============================================
                        string headerSql = @"
                            INSERT INTO InventoryAdjustments (
                                AdjustmentNumber,
                                WarehouseID,
                                Reason,
                                Notes,
                                AdjustmentDate,
                                CreatedBy,
                                CreatedDate,
                                Status,
                                TotalCost
                            ) VALUES (
                                @AdjustmentNumber,
                                @WarehouseID,
                                @Reason,
                                @Notes,
                                @AdjustmentDate,
                                @CreatedBy,
                                @CreatedDate,
                                @Status,
                                @TotalCost
                            );
                            SELECT last_insert_rowid();
                        ";

                        int adjustmentId = 0;

                        using (SQLiteCommand command = new SQLiteCommand(headerSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@AdjustmentNumber", adjustmentNumber);
                            command.Parameters.AddWithValue("@WarehouseID", model.WarehouseID);
                            command.Parameters.AddWithValue("@Reason", model.Reason ?? string.Empty);
                            command.Parameters.AddWithValue("@Notes", model.Notes ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@AdjustmentDate", model.AdjustmentDate.ToString("yyyy-MM-dd"));
                            command.Parameters.AddWithValue("@CreatedBy", userId);
                            command.Parameters.AddWithValue("@CreatedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@Status", "Pending");
                            command.Parameters.AddWithValue("@TotalCost", totalCost);

                            object result = await command.ExecuteScalarAsync();
                            adjustmentId = result != null ? Convert.ToInt32(result) : 0;
                        }

                        if (adjustmentId == 0)
                        {
                            transaction.Rollback();
                            return 0;
                        }

                        // ============================================
                        // 2️⃣ إدراج تفاصيل التسوية (مع بيانات الوحدات)
                        // ============================================
                        string detailSql = @"
                            INSERT INTO InventoryAdjustmentDetails (
                                AdjustmentID,
                                ProductID,
                                SystemQuantity,
                                ActualQuantity,
                                DifferenceQuantity,
                                AdjustmentType,
                                UnitCost,
                                TotalCost,
                                UnitName,
                                UnitTag,
                                QuantityInBaseUnit
                            ) VALUES (
                                @AdjustmentID,
                                @ProductID,
                                @SystemQuantity,
                                @ActualQuantity,
                                @DifferenceQuantity,
                                @AdjustmentType,
                                @UnitCost,
                                @TotalCost,
                                @UnitName,
                                @UnitTag,
                                @QuantityInBaseUnit
                            )
                        ";

                        foreach (AdjustmentItemDetail item in model.Items)
                        {
                            using (SQLiteCommand command = new SQLiteCommand(detailSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);
                                command.Parameters.AddWithValue("@ProductID", item.ProductID);
                                command.Parameters.AddWithValue("@SystemQuantity", item.SystemQuantity);
                                command.Parameters.AddWithValue("@ActualQuantity", item.ActualQuantity);
                                command.Parameters.AddWithValue("@DifferenceQuantity", item.DifferenceQuantity);
                                command.Parameters.AddWithValue("@AdjustmentType", item.AdjustmentType);
                                command.Parameters.AddWithValue("@UnitCost", item.UnitCost);
                                command.Parameters.AddWithValue("@TotalCost", item.TotalCost);
                                // ✅ إضافة بيانات الوحدات
                                command.Parameters.AddWithValue("@UnitName", item.UnitName ?? "وحدة");
                                command.Parameters.AddWithValue("@UnitTag", item.UnitTag ?? "Unit3");
                                command.Parameters.AddWithValue("@QuantityInBaseUnit", item.QuantityInBaseUnit);

                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();

                        System.Diagnostics.Debug.WriteLine($"✅ تم إنشاء التسوية {adjustmentNumber} مع {model.Items.Count} صنف");

                        return adjustmentId;
                    }
                    catch (Exception exception)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"❌ CreateAdjustmentAsync Error: {exception.Message}");
                        return 0;
                    }
                }
            }
        }

        // ============================================================
        // ✅ ApproveAdjustmentAsync - مع دعم الوحدات (استخدام QuantityInBaseUnit)
        // ============================================================
        public async Task<bool> ApproveAdjustmentAsync(int adjustmentId, int userId)
        {
            using (SQLiteConnection connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // ============================================
                        // 1️⃣ التحقق من حالة التسوية
                        // ============================================
                        string checkSql = "SELECT Status FROM InventoryAdjustments WHERE AdjustmentID = @AdjustmentID";
                        using (SQLiteCommand command = new SQLiteCommand(checkSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);
                            object result = await command.ExecuteScalarAsync();
                            string status = result != null ? result.ToString() : string.Empty;

                            if (status != "Pending")
                            {
                                transaction.Rollback();
                                return false;
                            }
                        }

                        // ============================================
                        // 2️⃣ جلب تفاصيل التسوية (مع QuantityInBaseUnit)
                        // ============================================
                        string getDetailsSql = @"
                            SELECT 
                                ProductID,
                                DifferenceQuantity,
                                AdjustmentType,
                                QuantityInBaseUnit
                            FROM InventoryAdjustmentDetails 
                            WHERE AdjustmentID = @AdjustmentID
                        ";

                        List<(int ProductID, decimal Difference, string Type, decimal QuantityInBaseUnit)> details = new List<(int, decimal, string, decimal)>();

                        using (SQLiteCommand command = new SQLiteCommand(getDetailsSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                            using (SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    int productId = Convert.ToInt32(reader["ProductID"]);
                                    decimal difference = Convert.ToDecimal(reader["DifferenceQuantity"]);
                                    string type = reader["AdjustmentType"].ToString();
                                    decimal quantityInBaseUnit = reader["QuantityInBaseUnit"] != DBNull.Value ? Convert.ToDecimal(reader["QuantityInBaseUnit"]) : 0;

                                    details.Add((productId, difference, type, quantityInBaseUnit));
                                }
                            }
                        }

                        if (details.Count == 0)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        // ============================================
                        // 3️⃣ جلب معرف المخزن ورقم التسوية
                        // ============================================
                        string getAdjustmentInfoSql = @"
                            SELECT WarehouseID, AdjustmentNumber 
                            FROM InventoryAdjustments 
                            WHERE AdjustmentID = @AdjustmentID
                        ";

                        int warehouseId = 0;
                        string adjustmentNumber = string.Empty;

                        using (SQLiteCommand command = new SQLiteCommand(getAdjustmentInfoSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                            using (SQLiteDataReader reader = (SQLiteDataReader)await command.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    warehouseId = Convert.ToInt32(reader["WarehouseID"]);
                                    adjustmentNumber = reader["AdjustmentNumber"].ToString();
                                }
                            }
                        }

                        if (warehouseId == 0)
                        {
                            transaction.Rollback();
                            return false;
                        }

                        // ============================================
                        // 4️⃣ تحديث الرصيد لكل صنف (باستخدام QuantityInBaseUnit)
                        // ============================================
                        foreach (var detail in details)
                        {
                            // ✅ استخدام الكمية المحولة إلى الوحدة الأساسية
                            decimal quantityChange = detail.Type == "Increase"
                                ? Math.Abs(detail.QuantityInBaseUnit > 0 ? detail.QuantityInBaseUnit : detail.Difference)
                                : -Math.Abs(detail.QuantityInBaseUnit > 0 ? detail.QuantityInBaseUnit : detail.Difference);

                            // ============================================
                            // 4A: جلب الرصيد الحالي قبل التحديث
                            // ============================================
                            string getCurrentQuantitySql = @"
                                SELECT COALESCE(Quantity, 0) 
                                FROM StoreInventory 
                                WHERE StoreID = @StoreID AND ProductID = @ProductID
                            ";

                            decimal quantityBefore = 0;

                            using (SQLiteCommand command = new SQLiteCommand(getCurrentQuantitySql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@StoreID", warehouseId);
                                command.Parameters.AddWithValue("@ProductID", detail.ProductID);

                                object result = await command.ExecuteScalarAsync();
                                quantityBefore = result != null ? Convert.ToDecimal(result) : 0;
                            }

                            // حساب الكمية بعد التحديث
                            decimal quantityAfter = quantityBefore + quantityChange;

                            // ============================================
                            // 4B: تحديث الرصيد في StoreInventory
                            // ============================================
                            string updateStockSql = @"
                                INSERT INTO StoreInventory (
                                    StoreID, 
                                    ProductID, 
                                    Quantity, 
                                    AvailableQuantity, 
                                    CostPrice,
                                    LastUpdated
                                ) VALUES (
                                    @StoreID,
                                    @ProductID,
                                    @NewQuantity,
                                    @NewQuantity,
                                    COALESCE((SELECT CostPrice FROM StoreInventory WHERE StoreID = @StoreID AND ProductID = @ProductID), 0),
                                    @LastUpdated
                                )
                                ON CONFLICT(StoreID, ProductID) DO UPDATE SET
                                    Quantity = @NewQuantity,
                                    AvailableQuantity = @NewQuantity,
                                    LastUpdated = @LastUpdated
                            ";

                            using (SQLiteCommand command = new SQLiteCommand(updateStockSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@StoreID", warehouseId);
                                command.Parameters.AddWithValue("@ProductID", detail.ProductID);
                                command.Parameters.AddWithValue("@NewQuantity", quantityAfter);
                                command.Parameters.AddWithValue("@LastUpdated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                int rowsAffected = await command.ExecuteNonQueryAsync();
                                System.Diagnostics.Debug.WriteLine($"📦 تحديث الرصيد: ProductID={detail.ProductID}, QuantityBefore={quantityBefore}, QuantityAfter={quantityAfter}, RowsAffected={rowsAffected}");
                            }

                            // ============================================
                            // 4C: تسجيل الحركة في InventoryTransactions
                            // ============================================
                            string insertTransactionSql = @"
                                INSERT INTO InventoryTransactions (
                                    StoreID,
                                    ProductID,
                                    TransactionDate,
                                    TransactionType,
                                    Quantity,
                                    QuantityBefore,
                                    QuantityAfter,
                                    UnitPrice,
                                    TotalAmount,
                                    ReferenceType,
                                    ReferenceID,
                                    ReferenceNumber,
                                    Description,
                                    CreatedBy
                                ) VALUES (
                                    @StoreID,
                                    @ProductID,
                                    @TransactionDate,
                                    @TransactionType,
                                    @Quantity,
                                    @QuantityBefore,
                                    @QuantityAfter,
                                    @UnitPrice,
                                    @TotalAmount,
                                    @ReferenceType,
                                    @ReferenceID,
                                    @ReferenceNumber,
                                    @Description,
                                    @CreatedBy
                                )
                            ";

                            using (SQLiteCommand command = new SQLiteCommand(insertTransactionSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@StoreID", warehouseId);
                                command.Parameters.AddWithValue("@ProductID", detail.ProductID);
                                command.Parameters.AddWithValue("@TransactionDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                command.Parameters.AddWithValue("@TransactionType", "Adjustment");
                                command.Parameters.AddWithValue("@Quantity", quantityChange);
                                command.Parameters.AddWithValue("@QuantityBefore", quantityBefore);
                                command.Parameters.AddWithValue("@QuantityAfter", quantityAfter);
                                command.Parameters.AddWithValue("@UnitPrice", 0);
                                command.Parameters.AddWithValue("@TotalAmount", 0);
                                command.Parameters.AddWithValue("@ReferenceType", "InventoryAdjustment");
                                command.Parameters.AddWithValue("@ReferenceID", adjustmentId);
                                command.Parameters.AddWithValue("@ReferenceNumber", adjustmentNumber);

                                string description = detail.Type == "Increase"
                                    ? $"زيادة مخزون بمقدار {Math.Abs(quantityChange)} وحدة (تسوية جردية)"
                                    : $"نقصان مخزون بمقدار {Math.Abs(quantityChange)} وحدة (تسوية جردية)";

                                command.Parameters.AddWithValue("@Description", description);
                                command.Parameters.AddWithValue("@CreatedBy", userId);

                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        // ============================================
                        // 5️⃣ تحديث حالة التسوية إلى Approved
                        // ============================================
                        string updateSql = @"
                            UPDATE InventoryAdjustments
                            SET 
                                Status = 'Approved',
                                ApprovedBy = @ApprovedBy,
                                ApprovedDate = @ApprovedDate
                            WHERE AdjustmentID = @AdjustmentID
                        ";

                        using (SQLiteCommand command = new SQLiteCommand(updateSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@ApprovedBy", userId);
                            command.Parameters.AddWithValue("@ApprovedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                            await command.ExecuteNonQueryAsync();
                        }

                        // ============================================
                        // 6️⃣ تأكيد الترانزاكشن
                        // ============================================
                        transaction.Commit();

                        System.Diagnostics.Debug.WriteLine($"✅ تم اعتماد التسوية {adjustmentNumber} مع {details.Count} صنف");
                        System.Diagnostics.Debug.WriteLine($"✅ تم تسجيل {details.Count} حركة في InventoryTransactions");

                        return true;
                    }
                    catch (Exception exception)
                    {
                        transaction.Rollback();
                        System.Diagnostics.Debug.WriteLine($"❌ ApproveAdjustmentAsync Error: {exception.Message}");
                        System.Diagnostics.Debug.WriteLine($"❌ Stack Trace: {exception.StackTrace}");
                        return false;
                    }
                }
            }
        }

        // ============================================================
        // ✅ RejectAdjustmentAsync
        // ============================================================
        public async Task<bool> RejectAdjustmentAsync(int adjustmentId, int userId)
        {
            using (SQLiteConnection connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();

                string query = @"
                    UPDATE InventoryAdjustments
                    SET 
                        Status = 'Rejected',
                        ApprovedBy = @ApprovedBy,
                        ApprovedDate = @ApprovedDate
                    WHERE AdjustmentID = @AdjustmentID AND Status = 'Pending'
                ";

                using (SQLiteCommand command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ApprovedBy", userId);
                    command.Parameters.AddWithValue("@ApprovedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@AdjustmentID", adjustmentId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    return rowsAffected > 0;
                }
            }
        }

        // ============================================================
        // ✅ GetProductQuantityInWarehouseAsync
        // ============================================================
        public async Task<decimal> GetProductQuantityInWarehouseAsync(int warehouseId, int productId)
        {
            using (SQLiteConnection connection = _databaseService.GetConnection())
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT COALESCE(Quantity, 0) 
                    FROM StoreInventory 
                    WHERE StoreID = @WarehouseID AND ProductID = @ProductID
                ";

                using (SQLiteCommand command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@WarehouseID", warehouseId);
                    command.Parameters.AddWithValue("@ProductID", productId);

                    object result = await command.ExecuteScalarAsync();

                    return result != null ? Convert.ToDecimal(result) : 0;
                }
            }
        }
    }
}