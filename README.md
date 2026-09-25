# Inventory / Warehouse Management

Dự án #2 trong roadmap ([spec](../../Roadmap/projects/02-Inventory-Warehouse.md)). Backend copy template từ [Dự án #1](../Helpdesk-Ticketing/backend), giữ nguyên auth, phân quyền 6 bảng, `IUnitOfWork<TContext>` và log API; phần viết thêm là `Features/V1/<nghiệp vụ kho>`. Luật code: [backend/RULES.md](backend/RULES.md) (mục 12 là luật riêng cho tồn kho).

Trọng tâm: **tồn kho không bao giờ sai hoặc âm khi nhiều người thao tác cùng lúc.**

## Tiến độ theo lộ trình

| Tuần | Nội dung | Trạng thái |
|---|---|---|
| 1 | Copy template, activity kho, domain + schema, migration | ✅ |
| 2 | Phiếu nhập + StockMovement + cập nhật tồn | ✅ |
| 3 | Phiếu xuất + optimistic concurrency + chống tồn âm | ✅ |
| 4 | Chuyển kho (nguyên tử) + kiểm kê + audit | ✅ |
| 8 (một phần) | Idempotency-Key; unit test logic tồn; integration test concurrency | ✅ |
| 5 | FE: bảng tồn kho lớn (virtualized, server-side), form phiếu, danh mục | ✅ |
| 6 | Kardex (SP + window function) + dashboard (SP, 6 bảng) + cache Redis | ✅ |
| 7 | Import/Export Excel theo lô (báo lỗi từng dòng) + job cảnh báo tồn thấp | ✅ |
| 9 | Docker compose (API + SQL + Redis + FE) xong; còn deploy + diagram | ⏳ |

## Chạy local

Cần .NET SDK 10 và SQL Server (mặc định `.\MSSQLSERVER01`, Windows auth; sửa trong `backend/src/Inventory.Api/appsettings.Development.json`).

```bash
cd backend && dotnet run --project src/Inventory.Api --urls http://localhost:5090
```

Swagger: http://localhost:5090/swagger. Ở môi trường Development, app tự **migrate + seed**: activity, role, 3 kho, 18 sản phẩm, 2 nhà cung cấp, phiếu nhập tồn đầu kỳ cho kho A và B, ngưỡng tối thiểu (có sẵn vài mã dưới ngưỡng).

| Email | Mật khẩu | Role | Được làm gì |
|---|---|---|---|
| admin@inventory.local | Admin@123 | Admin | Toàn quyền |
| manager@inventory.local | Manager@123 | Manager | Lập + **duyệt/ghi sổ** phiếu, đặt ngưỡng, xem báo cáo |
| staff@inventory.local, staff2@inventory.local | Staff@123 | Staff | Lập phiếu **nháp**, xem tồn |

Frontend (React 19 + TS, Vite, shadcn/ui, TanStack Query + Table + Virtual, RHF + Zod; cấu trúc feature-based như Helpdesk):

```bash
cd frontend && npm install && npm run dev
```

FE: http://localhost:5174 (Vite proxy `/api` → `:5090`). Chạy toàn bộ bằng Docker: `docker compose up --build` → FE http://localhost:8091, API http://localhost:8090/swagger.

## Frontend

| Màn | Điểm chính |
|---|---|
| Tồn kho `/stock` | Bảng **virtualized**, cuộn tới đâu tải thêm trang tới đó (infinite query, server trả 100 dòng mỗi trang). Mỗi kho một cột; dòng dưới ngưỡng tô đỏ; bấm ô số lượng để đặt ngưỡng (cần WAREHOUSE:U). Bộ lọc lưu trên URL. |
| Lập phiếu `/<loại>/new` | Một form dùng cho 4 loại phiếu. Mỗi dòng hiện **tồn hiện tại**; vượt tồn thì tô đỏ và khóa nút “Ghi sổ”. Lỗi 409 kèm `shortages` từ server được gắn về đúng dòng. Mỗi phiên form dùng một `Idempotency-Key`, nên bấm hai lần hay gửi lại vẫn chỉ tạo một phiếu. |
| Chi tiết phiếu `/<loại>/:id` | Ghi sổ hoặc hủy phiếu nháp, gửi kèm `rowVersion`. Phiếu kiểm kê hiện tồn sổ sách và chênh lệch. |
| Danh mục `/catalog` | Sản phẩm (tìm phía server), nhóm hàng, kho, nhà cung cấp. |
| Tổng quan `/dashboard` | KPI (giá trị tồn, mã còn hàng, dòng dưới ngưỡng, phiếu nháp, phiếu ghi sổ hôm nay), biểu đồ nhập–xuất 30 ngày, giá trị tồn theo kho / nhóm, hàng sắp hết, hàng chậm luân chuyển. Lọc theo kho. |
| Thẻ kho `/kardex` | Sổ nhập – xuất – tồn một sản phẩm: tồn đầu kỳ, từng chứng từ (link sang phiếu), tồn cuối lũy kế, phân trang. Mở từ SKU ở bảng tồn hoặc từ dashboard. |

**Dữ liệu lớn:** `Database:SeedBulkProducts` (Development = 12000) seed thêm 12.000 mã, kèm phiếu nhập tồn đầu kỳ đi qua StockLedger. Đo trên 12.018 mã sau khi warm-up: `GET /stock` với mọi kiểu lọc (trang 1, trang 100, theo kho, theo nhóm, dưới ngưỡng, tìm SKU) đều mất 15–70 ms, nên chưa cần stored procedure (RULES 3.11). Trên trình duyệt: đã tải 1.600 dòng nhưng DOM chỉ giữ khoảng 40 dòng.

## Test

```bash
cd backend && dotnet test tests/Inventory.UnitTests
```

Integration test chạy trên SQL Server thật. Có Docker thì tự dùng Testcontainers; không có thì trỏ vào SQL Server local (mỗi lần chạy tạo DB tạm rồi xóa):

```bash
cd backend && TEST_SQL_CONNECTION="Server=.\MSSQLSERVER01;Trusted_Connection=True;TrustServerCertificate=True" dotnet test tests/Inventory.IntegrationTests
```

```bash
cd frontend && npm test -- --run
```

## Thiết kế tồn kho

```text
Controller [HasPermission GOODS_ISSUE:C]
  → Validation → ConflictRetryBehavior (thử lại khi xung đột) → CreateGoodsIssueCommandHandler
      → StockDocumentWriter: Idempotency-Key, số chứng từ (DocumentSequence), dòng hàng, ghi sổ nếu post=true (cần GOODS_ISSUE:U)
          → StockDocumentPoster: phiếu → danh sách StockChange
              → StockLedger: đọc StockLevel (1 query) → kiểm tra đủ hàng mọi dòng → Adjust → StockMovement
      → SaveChangesAsync (MỘT lần = một transaction)
```

- **Một nơi duy nhất đổi tồn:** `StockLedger`. Mỗi thay đổi sinh một `StockMovement` (sổ cái bất biến), nên tổng movement luôn bằng tồn.
- **Chống bán vượt (oversell):** `StockLevel.RowVersion`. Hai phiếu cùng trừ một dòng thì phiếu lưu sau nhận `DbUpdateConcurrencyException`. `ConflictRetryBehavior` xóa change tracker và chạy lại handler: tồn được đọc lại, phiếu hoặc thành công với tồn mới, hoặc bị chặn bằng 409 kèm `shortages` (dòng nào thiếu, còn bao nhiêu).
- **Tồn không âm, 3 lớp:** kiểm tra trong ledger, `StockLevel.Adjust`, và CHECK constraint `CK_StockLevels_Quantity_NonNegative` trong DB.
- **Chuyển kho nguyên tử:** trừ kho A và cộng kho B nằm trong cùng một `SaveChangesAsync`.
- **Idempotency:** header `Idempotency-Key` được lưu cùng lần save với phiếu, có unique index `(UserId, Key)`. Request trùng, kể cả gửi song song, đều trả về cùng một phiếu.
- **Vòng đời phiếu:** `Draft → Posted` (tồn đổi, bất biến) hoặc `Draft → Cancelled`. Duyệt và hủy phải gửi `rowVersion` của phiếu.

Đã đo trên SQL Server thật: 6 phiếu xuất × 3 cái bắn song song vào SKU còn 10 → đúng 3 phiếu `201`, 3 phiếu `409`, tồn còn 1, tổng sổ cái = 1 (xem `StockConcurrencyTests`).

## Báo cáo & cache

- **Thẻ kho** — `usp_Report_Kardex`. Tồn đầu kỳ bằng tổng các movement trước `from`. Tồn cuối từng dòng = đầu kỳ + `SUM(Quantity) OVER (ORDER BY OccurredAt, Id ROWS UNBOUNDED PRECEDING)`. Running total được tính trên cả khoảng thời gian **rồi mới** phân trang, nên số dư ở trang 2 vẫn đúng (có test). Đo: 23–70 ms.
- **Dashboard** — `usp_Report_Dashboard` trả 6 bảng trong một lần gọi. Đo trên 12.018 mã: khoảng 170 ms phía SQL, phần lớn là bước dựng bảng tạm tồn kho. Vì vậy API cache kết quả **30 giây**, và FE hiện "Số liệu lúc …".
  - Câu "hàng chậm luân chuyển" được viết lại để chỉ xét movement gần đây, không gom trên toàn bộ lịch sử. Đo trên dữ liệu hiện tại thì nhanh ngang bản cũ, nhưng không bị chậm dần khi lịch sử dài ra.
- **Ngày nghiệp vụ** — DB lưu giờ UTC; "hôm nay", biểu đồ theo ngày và khoảng ngày của thẻ kho cắt theo `App:TimeZoneId` (mặc định `Asia/Ho_Chi_Minh`).
- **Cache danh mục** (`ICatalogCache`) — danh sách nhóm hàng, kho, và kết quả tìm sản phẩm được cache 10 phút. Mọi lệnh sửa danh mục đổi một *version token*, nên tất cả key cũ tự bị bỏ qua mà không phải xóa từng key.
  - Có `ConnectionStrings:Redis` thì dùng Redis (timeout 500 ms); không có thì dùng bộ nhớ trong tiến trình.
  - Redis lỗi hay chậm thì log cảnh báo rồi đọc thẳng DB, request không bị lỗi (có unit test).

## Import / Export Excel & cảnh báo tồn thấp

- **Import sản phẩm** (Danh mục → Import Excel; cần `IMPORT_EXPORT:C` **và** `PRODUCT:C`).
  - Bước 1 là **kiểm tra** (dry run): không ghi gì, liệt kê mọi dòng lỗi (số dòng Excel, cột, lý do). Bước 2 mới import các dòng hợp lệ.
  - Upsert theo SKU: SKU đã có thì cập nhật, chưa có thì thêm; nhóm hàng chưa có sẽ được tạo. Import lại cùng một file là an toàn, không nhân đôi.
  - Ghi theo **lô 500 dòng**, mỗi lô một `SaveChanges` rồi xóa change tracker. Đây là ngoại lệ có chủ đích với RULES 3.2 (xem 12.9).
  - Đo 10.000 dòng (có 10 dòng lỗi): kiểm tra 1,65 s; tạo mới 9.990 mã 6,7 s (lần gọi đầu, gồm cả khởi động); import lại (toàn cập nhật) 2,9 s. Giới hạn 20.000 dòng / 5 MB.
  - Tiêu đề cột so khớp không phân biệt hoa thường và dấu. Ô số được lấy đúng giá trị số. Ô chữ đọc theo thói quen Việt Nam: `35.000` là 35 nghìn, `1,5` là một phẩy năm.
- **Nhập dòng phiếu từ Excel** (nút trong form lập phiếu): chỉ đọc file, tra sản phẩm và báo dòng lỗi, không ghi gì. Các dòng hợp lệ được đổ vào form; sản phẩm đã có sẵn trong phiếu thì bỏ qua và báo lại.
- **Export**: tồn kho (dùng đúng bộ lọc của màn Tồn kho, qua `StockSearch`) và thẻ kho, thời gian ghi theo giờ địa phương. Tối đa 100.000 dòng mỗi file. Đo: xuất 22k dòng tồn mất 1,7 s.
- **Cảnh báo tồn thấp**: `LowStockAlertService` (BackgroundService) quét mỗi `StockAlerts:ScanIntervalSeconds` (mặc định 300 s).
  - Tồn xuống dưới ngưỡng → mở `StockAlert`; tồn hồi lại hoặc ngưỡng bị bỏ → đóng.
  - Mỗi (sản phẩm, kho) có tối đa một cảnh báo đang mở, nhờ unique filtered index. Nhờ vậy nhiều instance cùng chạy job cũng không sinh cảnh báo trùng.
  - Thông báo gửi cho người có `WAREHOUSE:U`, **một thông báo tóm tắt cho mỗi kho** chứ không gửi từng mặt hàng. Bấm vào thông báo sẽ mở màn Tồn kho đã lọc sẵn hàng sắp hết của kho đó.
  - Có trang `/alerts` và nút "Quét ngay".

## API

| Method | Route | Quyền |
|---|---|---|
| GET | `/api/v1/stock?warehouseId=&groupId=&search=&belowThreshold=&page=&pageSize=` | STOCK_REPORT:R |
| GET | `/api/v1/stock/available?warehouseId=&productIds=` | STOCK_REPORT:R |
| GET | `/api/v1/reports/dashboard?warehouseId=` | STOCK_REPORT:R |
| GET | `/api/v1/reports/kardex?productId=&warehouseId=&from=&to=&page=&pageSize=` | STOCK_REPORT:R |
| PUT | `/api/v1/stock/threshold` | WAREHOUSE:U |
| POST | `/api/v1/imports/products?dryRun=` (multipart `file`) | IMPORT_EXPORT:C + PRODUCT:C |
| POST | `/api/v1/imports/document-lines?type=` (multipart `file`) | C của loại phiếu |
| GET | `/api/v1/imports/templates/{kind}` (`Products`, `DocumentLines`) | đăng nhập |
| GET | `/api/v1/exports/stock?…` · `/api/v1/exports/kardex?…` | IMPORT_EXPORT:R + STOCK_REPORT:R |
| GET / POST | `/api/v1/stock-alerts?isResolved=&warehouseId=` · `/api/v1/stock-alerts/scan` | STOCK_REPORT:R · WAREHOUSE:U |
| GET/POST | `/api/v1/goods-receipts` · `goods-issues` · `transfers` · `stock-takes` | R / C (+U nếu `post: true`) |
| GET | `/api/v1/<loại phiếu>/{id}` | R |
| POST | `/api/v1/<loại phiếu>/{id}/post` · `/{id}/cancel` (body `{ rowVersion }`) | U / D |
| GET/POST/PUT/DELETE | `/api/v1/products`, `/product-groups`, `/warehouses`, `/suppliers` | PRODUCT / WAREHOUSE / SUPPLIER |
| | `/auth/*`, `/users`, `/roles`, `/activities`, `/api-logs`, `/notifications` | như template |

Ví dụ tạo phiếu xuất và ghi sổ luôn:

```http
POST /api/v1/goods-issues
Idempotency-Key: 7f1c2e0a-issue-001

{ "warehouseId": 1, "reason": "Sale", "note": "Đơn #123", "post": true,
  "lines": [ { "productId": 1, "quantity": 30 }, { "productId": 6, "quantity": 10 } ] }
```
