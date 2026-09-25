-- =============================================
-- usp_Report_Dashboard: số liệu màn Tổng quan kho, 1 round-trip, tổng hợp hoàn toàn trong SQL.
-- @WarehouseId NULL = mọi kho. Giá trị tồn = Quantity × Product.Cost (giá vốn chuẩn hiện tại).
-- Ngày được tính theo giờ địa phương: @UtcOffsetMinutes (VN = 420). @TodayStartUtc/@TodayEndUtc là
-- "hôm nay" theo giờ địa phương đã đổi sang UTC.
-- Trả 6 bảng: KPI · ValueByWarehouse · ValueByGroup · TopLowStock · InOutByDay · SlowMoving.
-- SP không có global query filter của EF → tự lọc IsDeleted = 0.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_Report_Dashboard]
    @WarehouseId      INT = NULL,
    @TodayStartUtc    DATETIME2,
    @TodayEndUtc      DATETIME2,
    @ChartFromUtc     DATETIME2,
    @SlowBeforeUtc    DATETIME2,
    @UtcOffsetMinutes INT = 0,
    @Top              INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    -- Tồn trong phạm vi (kho được chọn), kèm giá vốn — dùng lại cho nhiều bảng.
    SELECT sl.ProductId, sl.WarehouseId, sl.Quantity, sl.MinThreshold,
           p.Sku, p.Name, p.Cost, p.GroupId
    INTO #s
    FROM dbo.StockLevels sl
    JOIN dbo.Products p ON p.Id = sl.ProductId AND p.IsDeleted = 0
    WHERE sl.IsDeleted = 0
      AND (@WarehouseId IS NULL OR sl.WarehouseId = @WarehouseId);

    -- 1. KPI
    SELECT CAST(ISNULL(SUM(Quantity * Cost), 0) AS DECIMAL(19, 2))                               AS StockValue,
           COUNT(DISTINCT CASE WHEN Quantity > 0 THEN ProductId END)                            AS ProductsInStock,
           SUM(CASE WHEN MinThreshold > 0 AND Quantity < MinThreshold THEN 1 ELSE 0 END)         AS LowStockCount,
           (SELECT COUNT(*) FROM dbo.StockDocuments d
             WHERE d.IsDeleted = 0 AND d.Status = 1
               AND (@WarehouseId IS NULL OR d.WarehouseId = @WarehouseId OR d.ToWarehouseId = @WarehouseId)) AS DraftDocuments,
           (SELECT COUNT(*) FROM dbo.StockDocuments d
             WHERE d.IsDeleted = 0 AND d.Status = 2
               AND d.PostedAt >= @TodayStartUtc AND d.PostedAt < @TodayEndUtc
               AND (@WarehouseId IS NULL OR d.WarehouseId = @WarehouseId OR d.ToWarehouseId = @WarehouseId)) AS PostedToday
    FROM #s;

    -- 2. Giá trị tồn theo kho
    SELECT w.Id AS WarehouseId, w.Code, w.Name,
           CAST(ISNULL(SUM(s.Quantity * s.Cost), 0) AS DECIMAL(19, 2)) AS Value,
           ISNULL(SUM(s.Quantity), 0)                                  AS Quantity
    FROM dbo.Warehouses w
    LEFT JOIN #s s ON s.WarehouseId = w.Id
    WHERE w.IsDeleted = 0 AND (@WarehouseId IS NULL OR w.Id = @WarehouseId)
    GROUP BY w.Id, w.Code, w.Name
    ORDER BY w.Code;

    -- 3. Giá trị tồn theo nhóm hàng
    SELECT g.Id AS GroupId, g.Name, CAST(SUM(s.Quantity * s.Cost) AS DECIMAL(19, 2)) AS Value
    FROM #s s
    JOIN dbo.ProductGroups g ON g.Id = s.GroupId AND g.IsDeleted = 0
    GROUP BY g.Id, g.Name
    HAVING SUM(s.Quantity) > 0
    ORDER BY Value DESC;

    -- 4. Hàng sắp hết: dưới ngưỡng, thiếu nhiều nhất (theo tỉ lệ) lên đầu
    SELECT TOP (@Top) s.ProductId, s.Sku, s.Name, s.WarehouseId, w.Code AS WarehouseCode, s.Quantity, s.MinThreshold
    FROM #s s
    JOIN dbo.Warehouses w ON w.Id = s.WarehouseId
    WHERE s.MinThreshold > 0 AND s.Quantity < s.MinThreshold
    ORDER BY s.Quantity / NULLIF(s.MinThreshold, 0), s.Sku;

    -- 5. Giá trị nhập / xuất theo ngày (giờ địa phương). Xem cả hệ thống thì bỏ chuyển kho nội bộ
    --    (không phải nhập/xuất thật); xem một kho thì chuyển vào/ra cũng là nhập/xuất của kho đó.
    SELECT CAST(DATEADD(MINUTE, @UtcOffsetMinutes, m.OccurredAt) AS DATE) AS [Day],
           CAST(SUM(CASE WHEN m.Quantity > 0 THEN m.Quantity * p.Cost ELSE 0 END) AS DECIMAL(19, 2))  AS InValue,
           CAST(SUM(CASE WHEN m.Quantity < 0 THEN -m.Quantity * p.Cost ELSE 0 END) AS DECIMAL(19, 2)) AS OutValue
    FROM dbo.StockMovements m
    JOIN dbo.Products p ON p.Id = m.ProductId
    WHERE m.OccurredAt >= @ChartFromUtc AND m.OccurredAt < @TodayEndUtc
      AND m.Type <> 5 -- Adjust (kiểm kê) không tính là nhập/xuất
      AND (   (@WarehouseId IS NULL AND m.Type IN (1, 2))
           OR (@WarehouseId IS NOT NULL AND m.WarehouseId = @WarehouseId))
    GROUP BY CAST(DATEADD(MINUTE, @UtcOffsetMinutes, m.OccurredAt) AS DATE)
    OPTION (RECOMPILE);

    -- 6. Hàng chậm luân chuyển: còn tồn nhưng không xuất (bán/hủy/sản xuất/chuyển đi) từ @SlowBeforeUtc.
    --    Không gom MAX(OccurredAt) trên TOÀN BỘ lịch sử movement (chi phí tăng mãi theo thời gian): lọc bằng
    --    NOT EXISTS chỉ trên movement gần đây, lấy TOP trước, rồi mới tính LastOutAt cho đúng  dòng.
    --    Đo 12k mã / 24k movement: cả SP ~170 ms SQL (dựng #s ~55 ms là phần lớn nhất) → API cache 30 giây.
    ;WITH stock AS (
        SELECT ProductId, Sku, Name, SUM(Quantity) AS Quantity, SUM(Quantity * Cost) AS Value
        FROM #s
        GROUP BY ProductId, Sku, Name
        HAVING SUM(Quantity) > 0
    ), slow AS (
        SELECT TOP (@Top) s.ProductId, s.Sku, s.Name, s.Quantity, s.Value
        FROM stock s
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.StockMovements m
            WHERE m.ProductId = s.ProductId AND m.Type IN (2, 4) AND m.OccurredAt >= @SlowBeforeUtc
              AND (@WarehouseId IS NULL OR m.WarehouseId = @WarehouseId))
        ORDER BY s.Value DESC, s.Sku
    )
    SELECT sl.ProductId, sl.Sku, sl.Name, sl.Quantity, CAST(sl.Value AS DECIMAL(19, 2)) AS Value, lo.LastOutAt
    FROM slow sl
    OUTER APPLY (
        SELECT MAX(m.OccurredAt) AS LastOutAt
        FROM dbo.StockMovements m
        WHERE m.ProductId = sl.ProductId AND m.Type IN (2, 4)
          AND (@WarehouseId IS NULL OR m.WarehouseId = @WarehouseId)) lo
    ORDER BY sl.Value DESC, sl.Sku
    OPTION (RECOMPILE);
END
