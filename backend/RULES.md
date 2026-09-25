# RULES — Quy tắc BẮT BUỘC (đồng bộ với Backend_Api_Template/RULES.md)

> Tài liệu này là **luật**, không phải gợi ý. Code review (người hoặc AI) từ chối mọi PR vi phạm mục có đánh dấu **[BẮT BUỘC]**.
> Giải thích chi tiết và lý do nằm trong `Roadmap/projects/00-Chuan-Backend-DotNet.md`. Bản gốc có nghiệp vụ thật: `Projects/Helpdesk-Ticketing/backend` (dự án này copy template từ đó).

Mức độ: **[BẮT BUỘC]** = vi phạm thì không merge · **[NÊN]** = làm khác phải ghi lý do trong PR.

---

## 1. Kiến trúc & phụ thuộc

| # | Quy tắc |
|---|---|
| 1.1 | **[BẮT BUỘC]** Hướng phụ thuộc một chiều: `Api → Infrastructure → Application → Persistence → Domain`. Không đảo chiều, không tham chiếu vòng. |
| 1.2 | **[BẮT BUỘC]** `Domain` không tham chiếu EF Core, ASP.NET hay bất kỳ package hạ tầng nào. |
| 1.3 | **[BẮT BUỘC]** `Persistence` chỉ chứa DbContext, `IEntityTypeConfiguration<T>`, interceptor, migration. Không có logic nghiệp vụ. |
| 1.4 | **[BẮT BUỘC]** Controller mỏng: chỉ nhận request → `Mediator.Send(...)` → trả kết quả. Không truy cập DB, không if/else nghiệp vụ, không try/catch. |
| 1.5 | **[BẮT BUỘC]** Không tạo "Service layer" kiểu cũ (`IXxxService` + `XxxService` cho từng bảng). Mỗi use case là một Command/Query handler. Service chỉ dành cho logic dùng chung giữa nhiều handler, đặt ở `Features/V1/<Feature>/Services`. |

## 2. Cấu trúc feature (CQRS)

| # | Quy tắc |
|---|---|
| 2.1 | **[BẮT BUỘC]** Mỗi feature nằm ở `Application/Features/V1/<Feature>/` với 3 nhóm `Commands/`, `Queries/`, `DTOs/` (+ `Services/` nếu cần). |
| 2.2 | **[BẮT BUỘC]** **Một Command/Query = một use case.** Tên lớp kết thúc bằng `Command` / `Query` / `Handler` / `Validator`; command, validator và handler đặt cùng một file `Commands/<UseCase>/<UseCase>Command.cs`. |
| 2.3 | **[BẮT BUỘC]** Mọi Command có input phải có `AbstractValidator<TCommand>`. Không validate thủ công trong controller hay handler (trừ khi cần dữ liệu đã load, lúc đó ném `ValidationException(field, message)`). |
| 2.4 | **[BẮT BUỘC]** Trường lấy từ route (`id`) đánh `[JsonIgnore] public X Id { get; init; }` và gán ở controller bằng `command with { Id = id }`. |
| 2.5 | **[NÊN]** Command/Query/DTO là `sealed record`. Handler là `sealed class` dùng primary constructor. |

## 3. Truy cập dữ liệu

| # | Quy tắc |
|---|---|
| 3.1 | **[BẮT BUỘC]** Handler/service inject `IUnitOfWork<InventoryDbContext>`. **Không** inject DbContext trực tiếp, **không** `new SqlConnection`, **không** `new DbContext`. |
| 3.2 | **[BẮT BUỘC]** Mỗi handler ghi gọi `SaveChangesAsync` **đúng một lần, ở cuối**. Cần Id vừa sinh để gắn quan hệ thì dùng navigation property, EF tự điền FK trong cùng lần save. |
| 3.3 | **[BẮT BUỘC]** Không gọi `SaveChanges()` đồng bộ; không `.Result`/`.Wait()`; luôn truyền `CancellationToken`. |
| 3.4 | **[BẮT BUỘC]** Query chỉ đọc dùng `AsNoTracking()` + projection `Select` sang DTO. **Không** `Include` rồi map trong bộ nhớ ở màn danh sách. Có nhiều collection thì tách thành nhiều query projection. |
| 3.5 | **[BẮT BUỘC]** Không N+1: không query DB trong vòng lặp `foreach` theo từng dòng. Kiểm tra bằng SQL log (`Microsoft.EntityFrameworkCore.Database.Command = Information` ở Development). |
| 3.6 | **[BẮT BUỘC]** Phân trang dùng `PagedQuery` + `PagedResult<T>.CreateAsync` (`pageSize` ≤ 100). Không trả danh sách không giới hạn. |
| 3.7 | **[BẮT BUỘC]** Stored procedure chỉ gọi qua `unitOfWork.ExecuteStoreProcedureGetMultiTables("[dbo].[usp_X]", new Hashtable { ["@Param"] = v })`, đọc bằng `.ToDataSetSimpleRead().TryRead<T>()`. SP được tạo bằng migration (`migrationBuilder.Sql`), không tạo tay trên DB. |
| 3.8 | **[BẮT BUỘC]** Không nối chuỗi SQL với input người dùng. Raw SQL phải dùng `SqlParameter`. |
| 3.9 | **[BẮT BUỘC]** EF không dịch được `OrderBy`/`Where` trên record DTO tạo trong `Select`. Muốn sắp xếp/lọc trên kết quả group thì project ra anonymous type trước, `ToListAsync`, rồi mới map sang DTO. |
| 3.10 | **[BẮT BUỘC]** Stored procedure cho màn danh sách lọc động phải: (a) dùng **dynamic SQL có tham số** `sp_executesql`, chỉ ghép điều kiện được truyền vào, `ORDER BY` lấy từ whitelist; **không** `SELECT` toàn bộ dòng vào bảng tạm rồi mới phân trang (đã đo: chậm hơn LINQ gấp 8 lần). (b) Tự lọc `IsDeleted = 0` trên mọi bảng, vì SP không có global query filter của EF. (c) Escape `[ % _` khi dùng `LIKE`. (d) Trả bảng 1 là `TotalCount`, bảng 2 là dữ liệu trang. (e) File `.sql` đặt ở `Persistence/Sql/` (EmbeddedResource, `CREATE OR ALTER`) và cài bằng migration gọi `SqlScripts.Read(...)`. (f) Có integration test chạy SP trên SQL Server thật. Mẫu: `usp_Ticket_Search`, `usp_Report_Summary` trong Helpdesk. |
| 3.11 | **[BẮT BUỘC]** Trước khi chuyển một query sang SP hoặc thêm/sửa index phải **đo trên dữ liệu lớn** (≥ vài chục nghìn dòng), cả trước lẫn sau, rồi giữ cách nhanh hơn. Không tối ưu theo cảm tính. |

## 4. Entity, database, migration

| # | Quy tắc |
|---|---|
| 4.1 | **[BẮT BUỘC]** Entity nghiệp vụ kế thừa `BaseEntity`. **Không gán tay** `Created*`/`Updated*`, vì `AuditSaveChangesInterceptor` tự điền. |
| 4.2 | **[BẮT BUỘC]** Xóa = xóa mềm: gọi `Remove()` và interceptor tự đổi thành `IsDeleted = true`. Global query filter tự ẩn bản ghi đã xóa. Chỉ dùng `IgnoreQueryFilters()` khi có lý do và phải ghi comment. |
| 4.3 | **[BẮT BUỘC]** Cấu hình entity bằng `IEntityTypeConfiguration<T>` trong `Persistence/Configurations/<Area>/`, mỗi entity một lớp. Không dùng Data Annotation cho mapping DB. |
| 4.4 | **[BẮT BUỘC]** Index unique trên bảng có xóa mềm phải có filter `HasFilter("[IsDeleted] = 0")`. |
| 4.5 | **[BẮT BUỘC]** Đổi schema phải đi kèm migration: `dotnet ef migrations add <Tên> -p src/Inventory.Persistence -s src/Inventory.Persistence`. Không sửa migration đã merge; muốn sửa thì tạo migration mới. |
| 4.6 | **[BẮT BUỘC]** Thời gian lưu UTC, lấy qua `TimeProvider` (inject), không dùng `DateTime.Now`. API trả ISO-8601 có `Z` (nhờ `UtcDateTimeJsonConverter`, không được gỡ). |
| 4.7 | **[BẮT BUỘC]** Bảng có sửa đồng thời (tồn kho, trạng thái chứng từ...) phải có `byte[] RowVersion` + `.IsRowVersion()`; client gửi lại `rowVersion`. |
| 4.8 | **[NÊN]** Entity có nghiệp vụ dùng setter `private` + method domain (xem `StockDocument`, `StockLevel`). Bảng hệ thống / bảng đơn giản được phép dùng setter public. |
| 4.9 | **[BẮT BUỘC]** Entity tự quyết định người tạo (bảng lịch sử, "Hệ thống" = null) phải implement `IExplicitCreator`. |
| 4.10 | **[BẮT BUỘC]** Index phục vụ query EF trên bảng xóa mềm phải chứa `IsDeleted`, bằng `INCLUDE` hoặc filtered index `WHERE [IsDeleted] = 0`. Mọi query EF đều có điều kiện này (global filter); nếu index thiếu cột, SQL phải key lookup từng dòng. Ví dụ đo được: auto-assign 50–90 ms, còn 20 ms sau khi thêm `IsDeleted` vào `INCLUDE`. |

## 5. Phân quyền 6 bảng

| # | Quy tắc |
|---|---|
| 5.1 | **[BẮT BUỘC]** Mọi endpoint (trừ `auth/*` công khai) phải có một trong hai: `[HasPermission(ConstActivity.X, ActivityType.Y)]`, hoặc comment giải thích vì sao chỉ cần đăng nhập (ví dụ phân quyền theo dữ liệu nằm trong handler). |
| 5.2 | **[BẮT BUỘC]** Chức năng mới = thêm hằng số vào `ConstActivity` + một dòng trong `ConstActivity.All`. Seeder tự tạo `Sys_Activity`. **Không** insert tay vào DB. |
| 5.3 | **[BẮT BUỘC]** Không kiểm tra quyền theo **tên role** (`if (role == "Admin")`). Luôn kiểm tra theo quyền: `[HasPermission]` ở controller, `await currentUser.HasPermissionAsync(code, type, ct)` trong handler. |
| 5.4 | **[BẮT BUỘC]** Phân quyền theo dữ liệu (chủ bản ghi hoặc có quyền R) kiểm tra trong handler **trước** khi đọc/ghi. Không đủ quyền thì ném `ForbiddenException`. |
| 5.5 | **[BẮT BUỘC]** Đổi quyền thì phải vô hiệu cache: đổi role hoặc quyền riêng của user → `permissionService.Invalidate(userId)`; sửa quyền của role → `permissionService.InvalidateAll()`. |
| 5.6 | **[BẮT BUỘC]** JWT chỉ chứa định danh (`sub`, `email`, `name`). Không nhét danh sách quyền vào token. |
| 5.7 | **[BẮT BUỘC]** Role Admin (`RoleType = 1`) không sửa/xóa được. Không ai tự khóa mình hoặc tự gỡ Admin của mình. Hệ thống luôn còn ít nhất một Admin active. |

## 6. Lỗi & response

| # | Quy tắc |
|---|---|
| 6.1 | **[BẮT BUỘC]** Không `try/catch` để trả lỗi trong controller/handler. Hãy **ném exception chuẩn**; `GlobalExceptionHandler` sẽ chuyển thành ProblemDetails: `ValidationException` 400 · `UnauthorizedException` 401 · `ForbiddenException` 403 · `NotFoundException` 404 · `ConflictException`/`DomainException`/`DbUpdateConcurrencyException` 409. |
| 6.2 | **[BẮT BUỘC]** Không nuốt exception (`catch { }`). Chỉ được bắt khi xử lý thật (retry, bù trừ) hoặc ở hạ tầng nền (log writer, background job), và phải log cảnh báo. |
| 6.3 | **[BẮT BUỘC]** Thành công trả DTO trực tiếp (không bọc `BaseResponse`). Tạo mới → `201`, không có body → `204`. JSON camelCase, enum dạng string. |
| 6.4 | **[BẮT BUỘC]** Không trả entity EF ra API. Luôn trả DTO. |
| 6.5 | **[BẮT BUỘC]** Lỗi 500 không được lộ stack trace hay message nội bộ ra client. Client chỉ nhận `traceId`. |

## 7. Hằng số & cấu hình

| # | Quy tắc |
|---|---|
| 7.1 | **[BẮT BUỘC]** Mã nghiệp vụ (activity, role, trạng thái, key cache, tên bảng hệ thống) khai trong `Domain/Constants/Const*.cs`. Không viết chuỗi trần trong handler. |
| 7.2 | **[BẮT BUỘC]** Cấu hình đọc qua `IOptions<T>` (lớp `XxxOptions` có `SectionName`). Không đọc `IConfiguration["..."]` rải rác trong handler. |
| 7.3 | **[BẮT BUỘC]** Không commit secret: `Jwt:Key`, connection string production, mật khẩu admin. Dùng user-secrets / biến môi trường. `Jwt:Key` phải dài ≥ 32 ký tự (app từ chối khởi động nếu ngắn hơn). |
| 7.4 | **[BẮT BUỘC]** Không hard-code email, tên người, mã khách hàng hay mã chứng từ cụ thể trong code. |

## 8. Logging & debug

| # | Quy tắc |
|---|---|
| 8.1 | **[BẮT BUỘC]** Log qua `ILogger<T>` (Serilog) với **message template**: `logger.LogInformation("Order {OrderId} created", id)`. Không nối chuỗi, không `Console.WriteLine`. |
| 8.2 | **[BẮT BUỘC]** Không tắt `ApiLoggingMiddleware`. Nó phải đứng **đầu pipeline**, bọc ngoài `UseExceptionHandler`. |
| 8.3 | **[BẮT BUỘC]** API nhận hoặc trả dữ liệu nhạy cảm mới (mật khẩu, OTP, token, số thẻ...) thì thêm tên field vào `ApiLogging:SensitiveFields`. |
| 8.4 | **[NÊN]** API polling hoặc trả payload lớn thì thêm vào `ApiLogging:ExcludedPaths`. |
| 8.5 | **[BẮT BUỘC]** `HttpClient` gọi hệ thống ngoài phải gắn `.AddHttpMessageHandler<LoggingDelegatingHandler>()`. |
| 8.6 | **[BẮT BUỘC]** Không log mật khẩu, token hay dữ liệu cá nhân nhạy cảm ở bất kỳ level nào. |
| 8.7 | Quy trình debug: lấy `traceId` trong ProblemDetails → `GET /api/v1/api-logs?traceId=...` → xem request/response → tìm tiếp `traceId` đó trong `logs/*.log` để xem stack trace. |

## 9. Dependency Injection

| # | Quy tắc |
|---|---|
| 9.1 | **[BẮT BUỘC]** `IUnitOfWork<>` và `IRepository<,>` đăng ký **open generic, Scoped**. Không đăng ký từng `IUnitOfWork<XContext>`, không dùng Singleton/Transient cho UnitOfWork. |
| 9.2 | **[BẮT BUỘC]** Singleton không được inject service Scoped. Background job lấy service Scoped qua `IServiceScopeFactory.CreateScope()`. |
| 9.3 | **[BẮT BUỘC]** Không tạo vòng phụ thuộc qua factory lambda. Ví dụ: `IAuditUser` (interceptor dùng) chỉ đọc claim, **không** được phụ thuộc gì dẫn tới DbContext. Vòng phụ thuộc kiểu này làm app treo khi khởi động mà không báo lỗi. |
| 9.4 | **[NÊN]** Handler, validator, mapping được quét tự động theo assembly nên không đăng ký tay. Chỉ service dùng chung mới đăng ký trong `DependencyInjection.cs` của layer tương ứng. |

## 10. Test & chất lượng

| # | Quy tắc |
|---|---|
| 10.1 | **[BẮT BUỘC]** `dotnet build` 0 error trước khi mở PR. Không thêm warning mới (trừ NU* của package). |
| 10.2 | **[BẮT BUỘC]** Rule nghiệp vụ mới (máy trạng thái, tính toán, phân quyền theo dữ liệu) phải có unit test. Dùng `TestDb` (InMemory + UnitOfWork thật + interceptor). |
| 10.3 | **[BẮT BUỘC]** Endpoint mới có yêu cầu quyền phải có ít nhất một integration test cho case **403** (thiếu quyền). |
| 10.4 | **[BẮT BUỘC]** Test dùng `FakeTimeProvider` khi logic phụ thuộc thời gian. Không dùng `Thread.Sleep`. |
| 10.5 | **[NÊN]** Integration test chạy với SQL Server thật (Testcontainers; hoặc local qua `TEST_SQL_CONNECTION`). |

## 11. Khi sửa code có sẵn

| # | Quy tắc |
|---|---|
| 11.1 | **[BẮT BUỘC]** Trace code trước khi kết luận nguyên nhân, không sửa mò. Chưa đủ dữ kiện thì thêm log truy vết rồi báo. |
| 11.2 | **[NÊN]** Ưu tiên patch nhỏ nhất, tái sử dụng helper/service có sẵn. Không rewrite kiến trúc, không đổi API contract nếu không có yêu cầu. |
| 11.3 | **[BẮT BUỘC]** Không đổi phiên bản package lớn (major) trong PR sửa lỗi. Nâng package làm PR riêng. |

## 12. Tồn kho (riêng dự án Inventory)

| # | Quy tắc |
|---|---|
| 12.1 | **[BẮT BUỘC]** Chỉ `IStockLedger` được đổi `StockLevel.Quantity`. Mọi thay đổi tồn đều sinh `StockMovement` trong cùng lần `SaveChangesAsync`. Không `UPDATE StockLevels` bằng SQL tay, không sửa/xóa `StockMovement`. |
| 12.2 | **[BẮT BUỘC]** Tồn không bao giờ âm: kiểm tra trong `StockLedger` (báo đủ mọi dòng thiếu) + `StockLevel.Adjust` + CHECK constraint `CK_StockLevels_Quantity_NonNegative`. Không gỡ lớp nào. |
| 12.3 | **[BẮT BUỘC]** Command có ghi sổ tồn kho hoặc lấy số chứng từ phải implement `IRetryOnConflict`. Handler của nó phải chạy lại từ đầu được (đọc lại dữ liệu, không giữ trạng thái ngoài UnitOfWork). |
| 12.4 | **[BẮT BUỘC]** Lệnh tạo phiếu nhận header `Idempotency-Key` và đi qua `IStockDocumentWriter` (kiểm tra key trước, ghi key cùng lần save với phiếu). |
| 12.5 | **[BẮT BUỘC]** Phiếu đã ghi sổ (`Posted`) là bất biến. Sai thì lập phiếu điều chỉnh/phiếu ngược, không sửa phiếu cũ. |
| 12.6 | **[BẮT BUỘC]** Ghi sổ (duyệt) cần quyền `U` của loại phiếu; tạo phiếu kèm `post: true` cũng phải kiểm tra quyền `U` đó. |

---

## Checklist PR (copy vào mô tả PR)

```text
[ ] Feature đúng Features/V1/<Feature>/{Commands,Queries,DTOs}; một use case một Command/Query
[ ] Handler dùng IUnitOfWork<...>, SaveChangesAsync một lần ở cuối
[ ] Mọi Command có Validator
[ ] Endpoint có [HasPermission] (hoặc comment lý do); activity mới đã thêm ConstActivity.All
[ ] Query đọc: AsNoTracking + projection, không N+1 (đã xem SQL log); SP/index mới có số đo trước–sau
[ ] Không chuỗi mã nghiệp vụ trần; cấu hình qua IOptions
[ ] Đổi schema có migration
[ ] Field nhạy cảm mới đã thêm vào ApiLogging:SensitiveFields
[ ] Unit test cho rule mới; integration test case 403
[ ] dotnet build 0 error, dotnet test xanh
```

## Thêm một feature mới — các bước

1. **Domain:** entity kế thừa `BaseEntity` (+ enum, rule nếu có) trong `Domain/Entities/<Area>/`.
2. **Persistence:** thêm `XxxConfiguration : IEntityTypeConfiguration<Xxx>` → tạo migration.
3. **Constants:** thêm activity vào `ConstActivity` + `ConstActivity.All` (+ quyền mặc định cho role trong `ConstRole.DefaultPermissions` nếu cần).
4. **Application:** `Features/V1/Xxx/DTOs`, `Commands/CreateXxx/CreateXxxCommand.cs` (command + validator + handler), `Queries/SearchXxx/SearchXxxQuery.cs`.
5. **Api:** `Controllers/V1/XxxController.cs` kế thừa `ApiControllerBase`, mỗi action có `[HasPermission]`.
6. **Test:** unit test cho rule; integration test cho happy path và 403.
7. Chạy `dotnet build`, `dotnet test`; mở Swagger thử; xem log API trong `Sys_LogApi`.
