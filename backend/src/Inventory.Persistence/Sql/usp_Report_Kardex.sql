-- =============================================
-- usp_Report_Kardex: sổ nhập – xuất – tồn (thẻ kho) của MỘT sản phẩm trong [@From, @To).
-- @WarehouseId NULL = mọi kho (chuyển kho nội bộ hiện cả dòng xuất và dòng nhập, tổng không đổi).
-- Tồn cuối mỗi dòng = tồn đầu kỳ + SUM(Quantity) OVER (ORDER BY OccurredAt, Id ROWS UNBOUNDED PRECEDING)
-- → running total tính trên TOÀN khoảng rồi mới phân trang, nên trang 2 vẫn đúng số dư.
-- Trả 2 bảng: (1) tổng hợp: TotalCount, Opening, TotalIn, TotalOut, Closing · (2) các dòng của trang.
-- StockMovements là sổ cái bất biến (không xóa mềm); chỉ lọc IsDeleted của bảng tham chiếu.
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[usp_Report_Kardex]
    @ProductId   INT,
    @WarehouseId INT = NULL,
    @From        DATETIME2,
    @To          DATETIME2,   -- exclusive
    @Page        INT = 1,
    @PageSize    INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Opening DECIMAL(18, 3) =
        (SELECT ISNULL(SUM(m.Quantity), 0)
         FROM dbo.StockMovements m
         WHERE m.ProductId = @ProductId
           AND (@WarehouseId IS NULL OR m.WarehouseId = @WarehouseId)
           AND m.OccurredAt < @From);

    SELECT m.Id, m.OccurredAt, m.DocumentId, m.DocumentCode, m.Type, m.WarehouseId, m.Quantity,
           @Opening + SUM(m.Quantity) OVER (ORDER BY m.OccurredAt, m.Id ROWS UNBOUNDED PRECEDING) AS Balance
    INTO #k
    FROM dbo.StockMovements m
    WHERE m.ProductId = @ProductId
      AND (@WarehouseId IS NULL OR m.WarehouseId = @WarehouseId)
      AND m.OccurredAt >= @From AND m.OccurredAt < @To
    OPTION (RECOMPILE); -- @WarehouseId tùy chọn: mỗi lần chạy lấy plan đúng (seek theo ProductId [+ WarehouseId])

    -- 1. Tổng hợp kỳ
    SELECT COUNT(*)                                                   AS TotalCount,
           @Opening                                                   AS Opening,
           ISNULL(SUM(CASE WHEN Quantity > 0 THEN Quantity END), 0)   AS TotalIn,
           ISNULL(SUM(CASE WHEN Quantity < 0 THEN -Quantity END), 0)  AS TotalOut,
           @Opening + ISNULL(SUM(Quantity), 0)                        AS Closing
    FROM #k;

    -- 2. Dòng của trang (theo thời gian tăng dần như sổ sách)
    SELECT k.Id, k.OccurredAt, k.DocumentId, k.DocumentCode, d.Type AS DocumentType, k.Type AS MovementType,
           k.WarehouseId, w.Code AS WarehouseCode,
           CASE WHEN k.Quantity > 0 THEN k.Quantity ELSE 0 END  AS InQty,
           CASE WHEN k.Quantity < 0 THEN -k.Quantity ELSE 0 END AS OutQty,
           k.Balance
    FROM #k k
    JOIN dbo.StockDocuments d ON d.Id = k.DocumentId
    JOIN dbo.Warehouses w ON w.Id = k.WarehouseId
    ORDER BY k.OccurredAt, k.Id
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
