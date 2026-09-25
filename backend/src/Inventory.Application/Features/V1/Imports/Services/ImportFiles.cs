using FluentValidation;
using Inventory.Application.Features.V1.Imports.DTOs;

namespace Inventory.Application.Features.V1.Imports.Services;

/// <summary>Tên cột trong file mẫu. Khi đọc, so khớp không phân biệt hoa thường / dấu (xem <see cref="SheetHeader"/>).</summary>
public static class ImportColumns
{
    public const string Sku = "SKU";
    public const string Name = "Tên sản phẩm";
    public const string Unit = "Đơn vị";
    public const string Group = "Nhóm hàng";
    public const string Cost = "Giá vốn";
    public const string Price = "Giá bán";
    public const string IsActive = "Đang kinh doanh";

    public const string Quantity = "Số lượng";
    public const string UnitCost = "Đơn giá";
    public const string Note = "Ghi chú";

    public static readonly IReadOnlyList<string> ProductsRequired = [Sku, Name, Unit, Group];
    public static readonly IReadOnlyList<string> LinesRequired = [Sku, Quantity];

    /// <summary>Ném lỗi 400 liệt kê các cột bắt buộc bị thiếu (thường do dùng sai file).</summary>
    public static void EnsurePresent(SheetReadResult sheet, IReadOnlyList<string> required)
    {
        var present = sheet.Headers.Select(SheetHeader.Normalize).ToHashSet();
        var missing = required.Where(r => !present.Contains(SheetHeader.Normalize(r))).ToList();
        if (missing.Count > 0)
            throw new ValidationException("file", $"Thiếu cột: {string.Join(", ", missing)}. Hãy dùng file mẫu.");
    }
}

public static class ImportLimits
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxProductRows = 20_000;

    /// <summary>Mỗi lô ghi DB: đủ lớn để EF gom INSERT/UPDATE, đủ nhỏ để change tracker không phình và transaction ngắn.</summary>
    public const int ProductBatchSize = 500;
}

/// <summary>Validator dùng chung cho mọi lệnh nhận file Excel.</summary>
public static class ImportFileRules
{
    public static void AddFileRules<T>(this AbstractValidator<T> validator, Func<T, string> fileName, Func<T, long> size)
    {
        validator.RuleFor(x => fileName(x)).Must(n => string.Equals(Path.GetExtension(n), ".xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ nhận file .xlsx.").OverridePropertyName("file");
        validator.RuleFor(x => size(x)).GreaterThan(0).WithMessage("File rỗng.")
            .LessThanOrEqualTo(ImportLimits.MaxFileBytes).WithMessage($"File tối đa {ImportLimits.MaxFileBytes / 1024 / 1024}MB.")
            .OverridePropertyName("file");
    }
}

/// <summary>Gom lỗi theo dòng trong lúc đọc file.</summary>
public sealed class RowErrors
{
    private readonly List<RowErrorDto> _errors = [];
    private readonly HashSet<int> _badRows = [];

    public IReadOnlyList<RowErrorDto> All => _errors;

    public bool HasErrors(int row) => _badRows.Contains(row);

    public void Add(int row, string? column, string message)
    {
        _errors.Add(new RowErrorDto(row, column, message));
        _badRows.Add(row);
    }
}
