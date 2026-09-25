# Kiến trúc — Inventory / Warehouse

Tài liệu này mô tả hệ thống **như code hiện tại đang chạy**. Luật code nằm ở [backend/RULES.md](../backend/RULES.md); cách chạy và các số đo nằm ở [README](../README.md).

## 1. Triển khai (container)

```mermaid
flowchart LR
    user([Người dùng<br/>trình duyệt])
    subgraph compose[docker compose / 1 máy chủ]
        web["web<br/>nginx: SPA React + proxy /api"]
        api["api<br/>ASP.NET Core .NET 10<br/>+ job nền trong cùng tiến trình"]
        db[("db<br/>SQL Server 2022")]
        redis[("redis<br/>cache, không lưu đĩa")]
    end
    user -- HTTPS --> web
    web -- "/api/*" --> api
    api -- "EF Core + stored procedure" --> db
    api -. "cache danh mục, dashboard<br/>(lỗi → đọc thẳng DB)" .-> redis
```

- **Không có trạng thái trong `api`:** chạy nhiều bản được. Cache nằm ở Redis, quyền được cache theo version token; job cảnh báo chống trùng bằng unique index.
- **Redis là tùy chọn:** không cấu hình `ConnectionStrings:Redis` thì dùng cache bộ nhớ. Redis chết thì request vẫn chạy (timeout 500 ms rồi đọc DB).
- **Job nền** (`BackgroundService`): ghi log API theo lô, dọn log cũ, quét tồn thấp mỗi 5 phút.

## 2. Các lớp backend

```mermaid
flowchart TB
    Api["Api<br/>controller mỏng · [HasPermission] · ApiLoggingMiddleware · GlobalExceptionHandler"]
    Infra["Infrastructure<br/>UnitOfWork · JWT · PermissionService · Redis cache · ClosedXML · job nền · seeder"]
    App["Application<br/>Features/V1/&lt;Feature&gt;/Commands|Queries|DTOs · StockLedger · behaviors"]
    Pers["Persistence<br/>DbContext · IEntityTypeConfiguration · interceptor · migration · Sql/*.sql"]
    Dom["Domain<br/>entity · rule · Const*"]
    Api --> Infra --> App --> Pers --> Dom
```

Pipeline MediatR của mọi request: `LoggingBehavior → ValidationBehavior → ConflictRetryBehavior → Handler`.

## 3. Luồng quan trọng nhất: ghi sổ phiếu xuất khi nhiều người tranh cùng hàng

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant R as ConflictRetryBehavior
    participant H as CreateGoodsIssueHandler
    participant L as StockLedger
    participant DB as SQL Server
    C->>R: POST /goods-issues (Idempotency-Key, post=true)
    loop tối đa 4 lần
        R->>H: chạy handler
        H->>DB: Idempotency-Key đã có? → trả phiếu cũ
        H->>DB: lấy số chứng từ (DocumentSequence, rowversion)
        H->>L: ApplyAsync(các dòng)
        L->>DB: SELECT StockLevel (1 query, giữ RowVersion gốc)
        alt có dòng thiếu hàng
            L-->>C: 409 InsufficientStock + shortages[]
        end
        L->>L: Adjust() + thêm StockMovement
        H->>DB: SaveChanges (1 transaction)<br/>UPDATE ... WHERE RowVersion = @gốc
        alt người khác vừa trừ cùng dòng (0 row)
            DB-->>R: DbUpdateConcurrencyException
            R->>R: ClearChangeTracker, chờ ngẫu nhiên, thử lại (đọc tồn mới)
        else thành công
            H-->>C: 201 phiếu đã ghi sổ
        end
    end
```

Tồn không bao giờ âm vì có ba lớp chặn: `StockLedger` kiểm tra mọi dòng, `StockLevel.Adjust`, và CHECK constraint `CK_StockLevels_Quantity_NonNegative` trong DB. `StockConcurrencyTests` chứng minh điều này: 6 phiếu × 3 cái bắn vào tồn 10 → đúng 3 phiếu thành công, tồn còn 1.

## 4. Vòng đời chứng từ

```mermaid
stateDiagram-v2
    [*] --> Draft: lập phiếu (C)
    Draft --> Draft: sửa (chủ phiếu hoặc người có U)
    Draft --> Posted: ghi sổ (U) — tồn thay đổi, sinh StockMovement
    Draft --> Cancelled: hủy (D)
    Posted --> [*]
    Cancelled --> [*]
```

Phiếu `Posted` là bất biến. Sai thì lập phiếu kiểm kê hoặc phiếu ngược lại. Mọi thao tác đổi trạng thái đều phải gửi `rowVersion` của phiếu.

## 5. Dữ liệu kho

```mermaid
erDiagram
    ProductGroup ||--o{ Product : "nhóm"
    Product ||--o{ StockLevel : "tồn"
    Warehouse ||--o{ StockLevel : "tồn"
    StockDocument ||--|{ StockDocumentLine : "dòng"
    Product ||--o{ StockDocumentLine : ""
    Warehouse ||--o{ StockDocument : "kho (xuất)"
    Supplier |o--o{ StockDocument : "NCC (phiếu nhập)"
    StockDocument ||--o{ StockMovement : "sinh ra khi ghi sổ"
    Product ||--o{ StockMovement : ""
    StockDocument ||--o{ IdempotencyRecord : ""
    Product ||--o{ StockAlert : ""

    StockLevel {
        long Id
        int ProductId "unique (ProductId, WarehouseId)"
        int WarehouseId
        decimal Quantity "CHECK >= 0"
        decimal MinThreshold
        rowversion RowVersion "optimistic concurrency"
    }
    StockMovement {
        long Id
        int ProductId
        int WarehouseId
        int Type "In/Out/TransferIn/TransferOut/Adjust"
        decimal Quantity "có dấu"
        decimal BalanceAfter
        datetime OccurredAt "index (ProductId, WarehouseId, OccurredAt)"
    }
    StockDocument {
        int Id
        string Code "PN-2026-00001"
        byte Type
        byte Status "Draft/Posted/Cancelled"
        rowversion RowVersion
    }
    StockAlert {
        long Id
        bool IsResolved "unique (ProductId, WarehouseId) WHERE open"
    }
```

- `StockMovement` là **sổ cái bất biến**: chỉ thêm, không sửa, không xóa mềm. Thẻ kho (`usp_Report_Kardex`) tính tồn cuối lũy kế bằng window function trên bảng này.
- Tổng `StockMovement.Quantity` của một (sản phẩm, kho) luôn bằng `StockLevel.Quantity` (có test).
- Bảng phân quyền `Sys_*` (6 bảng), log API và refresh token giống hệt template ở [Dự án #1](../../Helpdesk-Ticketing/README.md).

## 6. Quyết định thiết kế đáng chú ý

| Quyết định | Lý do | Đánh đổi |
|---|---|---|
| Một bảng `StockDocument` cho 4 loại phiếu | Một `StockLedger` và một luồng tạo/sửa/ghi sổ/hủy dùng chung | Vài cột chỉ có nghĩa với một loại (`SupplierId`, `ToWarehouseId`, `Reason`) |
| Optimistic concurrency (rowversion) + thử lại cả use case | Không khóa hàng lúc đọc, tranh chấp hiếm thì rẻ | Tranh chấp dày đặc thì có thể thử hết 4 lần → trả 409 |
| Import ghi theo lô 500 dòng | 20.000 dòng không timeout, change tracker nhỏ | Đứt giữa chừng thì một phần đã ghi — chấp nhận được vì là upsert idempotent |
| Dashboard cache 30 s | SP quét toàn bộ tồn (~170 ms trên 12k mã) | Số liệu trễ tối đa 30 s (hiện "Số liệu lúc …") |
| Job nền trong tiến trình API (không dùng Hangfire) | Ít thành phần, đủ cho một job định kỳ | Không có UI theo dõi job; nhiều instance thì cùng quét (đã an toàn nhờ unique index) |
