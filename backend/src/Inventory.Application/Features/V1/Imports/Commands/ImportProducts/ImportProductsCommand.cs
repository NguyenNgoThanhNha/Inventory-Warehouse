using System.Text.RegularExpressions;
using FluentValidation;
using Inventory.Application.Features.V1.Imports.DTOs;
using Inventory.Application.Features.V1.Imports.Services;
using Inventory.Domain.Entities.Catalog;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Features.V1.Imports.Commands.ImportProducts;

/// <summary>
/// Import sản phẩm từ Excel (spec §3.5): thêm mới theo SKU chưa có, cập nhật SKU đã có. Nhóm hàng chưa có thì tạo.
/// Dòng lỗi được bỏ qua và báo lại (số dòng + cột + lý do); dòng hợp lệ vẫn được ghi.
/// <see cref="DryRun"/> = true: chỉ kiểm tra, không ghi gì — FE cho xem trước lỗi rồi mới import thật.
/// </summary>
public sealed record ImportProductsCommand(Stream Content, string FileName, long Size, bool DryRun) : IRequest<ImportProductsResultDto>;

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator() => this.AddFileRules(x => x.FileName, x => x.Size);
}

/// <summary>
/// Ngoại lệ có chủ đích với RULES 3.2 (một SaveChanges): file tới 20.000 dòng được ghi theo LÔ 500 dòng,
/// mỗi lô một SaveChanges rồi xóa change tracker — giữ bộ nhớ và transaction nhỏ, không timeout.
/// An toàn khi đứt giữa chừng: import theo SKU là upsert, chạy lại cùng file cho cùng kết quả.
/// </summary>
public sealed partial class ImportProductsCommandHandler(
    IUnitOfWork<InventoryDbContext> unitOfWork,
    ISpreadsheetService spreadsheet,
    ICatalogCache catalogCache,
    ILogger<ImportProductsCommandHandler> logger) : IRequestHandler<ImportProductsCommand, ImportProductsResultDto>
{
    private sealed record ProductRow(int Row, string Sku, string Name, string Unit, string Group, decimal Cost, decimal Price, bool? IsActive);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SkuPattern();

    public async Task<ImportProductsResultDto> Handle(ImportProductsCommand request, CancellationToken ct)
    {
        var sheet = spreadsheet.Read(request.Content, ImportLimits.MaxProductRows);
        ImportColumns.EnsurePresent(sheet, ImportColumns.ProductsRequired);

        var errors = new RowErrors();
        var rows = Parse(sheet, errors);

        var groups = await unitOfWork.Repository<ProductGroup>().AsNoTracking()
            .ToDictionaryAsync(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase, ct);
        var newGroups = rows.Select(r => r.Group).Where(g => !groups.ContainsKey(g))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingSkus = await ExistingSkusAsync(rows.Select(r => r.Sku).ToList(), ct);
        var updated = rows.Count(r => existingSkus.Contains(r.Sku));

        var result = new ImportProductsResultDto(request.DryRun, sheet.Rows.Count, rows.Count, rows.Count - updated, updated,
            newGroups, errors.All.OrderBy(e => e.Row).ToList());
        if (request.DryRun || rows.Count == 0) return result;

        if (newGroups.Count > 0)
        {
            var created = newGroups.Select(n => new ProductGroup(n)).ToList();
            unitOfWork.Repository<ProductGroup>().AddRange(created);
            await unitOfWork.SaveChangesAsync(ct);
            foreach (var g in created) groups[g.Name] = g.Id;
        }

        foreach (var batch in rows.Chunk(ImportLimits.ProductBatchSize))
        {
            var skus = batch.Select(r => r.Sku).ToList();
            var existing = await unitOfWork.Repository<Product>().Where(p => skus.Contains(p.Sku)).ToDictionaryAsync(p => p.Sku, ct);
            foreach (var r in batch)
            {
                var groupId = groups[r.Group];
                if (existing.TryGetValue(r.Sku, out var product))
                    product.Update(r.Name, r.Unit, groupId, r.Cost, r.Price, product.ImageUrl, r.IsActive ?? product.IsActive);
                else
                    unitOfWork.Repository<Product>().Add(new Product(r.Sku, r.Name, r.Unit, groupId, r.Cost, r.Price));
            }
            await unitOfWork.SaveChangesAsync(ct);
            unitOfWork.ClearChangeTracker();
        }

        await catalogCache.InvalidateAsync(ct);
        logger.LogInformation("Imported products from {FileName}: {Created} created, {Updated} updated, {Errors} rows rejected",
            request.FileName, result.Created, result.Updated, result.Errors.Select(e => e.Row).Distinct().Count());
        return result;
    }

    private static List<ProductRow> Parse(SheetReadResult sheet, RowErrors errors)
    {
        var rows = new List<ProductRow>(sheet.Rows.Count);
        var firstRowOfSku = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in sheet.Rows)
        {
            var n = row.RowNumber;
            var rawSku = row.Get(ImportColumns.Sku);
            var sku = rawSku is null ? null : Product.NormalizeSku(rawSku);
            var name = row.Get(ImportColumns.Name);
            var unit = row.Get(ImportColumns.Unit);
            var group = row.Get(ImportColumns.Group);

            if (sku is null) errors.Add(n, ImportColumns.Sku, "Thiếu SKU.");
            else if (sku.Length > 50 || !SkuPattern().IsMatch(sku)) errors.Add(n, ImportColumns.Sku, "SKU tối đa 50 ký tự, chỉ gồm chữ, số và . _ -");
            else if (firstRowOfSku.TryGetValue(sku, out var first)) errors.Add(n, ImportColumns.Sku, $"SKU trùng với dòng {first}.");
            else firstRowOfSku[sku] = n;

            if (name is null) errors.Add(n, ImportColumns.Name, "Thiếu tên sản phẩm.");
            else if (name.Length > 200) errors.Add(n, ImportColumns.Name, "Tên tối đa 200 ký tự.");
            if (unit is null) errors.Add(n, ImportColumns.Unit, "Thiếu đơn vị.");
            else if (unit.Length > 20) errors.Add(n, ImportColumns.Unit, "Đơn vị tối đa 20 ký tự.");
            if (group is null) errors.Add(n, ImportColumns.Group, "Thiếu nhóm hàng.");
            else if (group.Length > 100) errors.Add(n, ImportColumns.Group, "Nhóm hàng tối đa 100 ký tự.");

            var cost = Money(row, ImportColumns.Cost, n, errors);
            var price = Money(row, ImportColumns.Price, n, errors);

            bool? isActive = null;
            if (row.Get(ImportColumns.IsActive) is not null && (isActive = SheetValue.TryBool(row.GetRaw(ImportColumns.IsActive))) is null)
                errors.Add(n, ImportColumns.IsActive, "Ghi \"có\" hoặc \"không\".");

            if (!errors.HasErrors(n))
                rows.Add(new ProductRow(n, sku!, name!, unit!, group!, cost, price, isActive));
        }
        return rows;
    }

    /// <summary>Ô trống = 0; có giá trị thì phải là số ≥ 0.</summary>
    private static decimal Money(SheetRow row, string column, int n, RowErrors errors)
    {
        if (row.Get(column) is null) return 0;
        if (row.TryGetDecimal(column, out var value) && value >= 0 && value < 1_000_000_000_000) return Math.Round(value, 2);
        errors.Add(n, column, $"{column} phải là số ≥ 0.");
        return 0;
    }

    private async Task<HashSet<string>> ExistingSkusAsync(List<string> skus, CancellationToken ct)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in skus.Chunk(2000))
            found.UnionWith(await unitOfWork.Repository<Product>().AsNoTracking()
                .Where(p => chunk.Contains(p.Sku)).Select(p => p.Sku).ToListAsync(ct));
        return found;
    }
}
