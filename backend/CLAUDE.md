# CLAUDE.md

Backend .NET của Inventory (dựng theo ServerApiTemplate) (Clean Architecture + CQRS + phân quyền 6 bảng).

**Trước khi viết hoặc sửa code, đọc và tuân thủ [RULES.md](RULES.md). Mọi mục [BẮT BUỘC] không có ngoại lệ.**

Tóm tắt những điều hay bị vi phạm:
- Handler inject `IUnitOfWork<InventoryDbContext>`, không inject DbContext; `SaveChangesAsync` một lần ở cuối.
- Feature đặt tại `Application/Features/V1/<Feature>/{Commands,Queries,DTOs}`; một Command/Query = một use case; Command luôn có Validator.
- Controller mỏng, mọi endpoint có `[HasPermission(ConstActivity.X, ActivityType.Y)]`; activity mới thêm vào `ConstActivity.All`.
- Ném exception chuẩn (`NotFoundException`, `ForbiddenException`...), không try/catch trả lỗi; không trả entity ra API.
- Không chuỗi mã nghiệp vụ trần; thời gian dùng `TimeProvider` (UTC).
- Field nhạy cảm mới → thêm vào `ApiLogging:SensitiveFields`.

Lệnh thường dùng:

```bash
dotnet build Inventory.sln
dotnet test tests/Inventory.UnitTests
dotnet ef migrations add <Name> -p src/Inventory.Persistence -s src/Inventory.Persistence
dotnet run --project src/Inventory.Api
```

Debug lỗi: lấy `traceId` trong ProblemDetails → `GET /api/v1/api-logs?traceId=...` → tìm `traceId` trong `src/Inventory.Api/logs/*.log`.
