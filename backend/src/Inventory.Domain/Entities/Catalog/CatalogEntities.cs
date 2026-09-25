using Inventory.Domain.Common;

namespace Inventory.Domain.Entities.Catalog;

public class ProductGroup : BaseEntity
{
    private ProductGroup() { }

    public ProductGroup(string name) => Rename(name);

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;

    public void Rename(string name) => Name = name.Trim();
}

public class Product : BaseEntity
{
    private Product() { }

    public Product(string sku, string name, string unit, int groupId, decimal cost, decimal price, string? imageUrl = null)
    {
        Sku = NormalizeSku(sku);
        Update(name, unit, groupId, cost, price, imageUrl, isActive: true);
    }

    public int Id { get; private set; }
    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Unit { get; private set; } = null!;
    public int GroupId { get; private set; }
    public ProductGroup Group { get; private set; } = null!;

    /// <summary>Giá vốn — dùng tính giá trị tồn.</summary>
    public decimal Cost { get; private set; }

    /// <summary>Giá bán tham khảo.</summary>
    public decimal Price { get; private set; }

    public string? ImageUrl { get; private set; }

    /// <summary>Ngừng kinh doanh: không chọn được trong phiếu mới, vẫn giữ lịch sử.</summary>
    public bool IsActive { get; private set; }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public void Update(string name, string unit, int groupId, decimal cost, decimal price, string? imageUrl, bool isActive)
    {
        Name = name.Trim();
        Unit = unit.Trim();
        GroupId = groupId;
        Cost = cost;
        Price = price;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        IsActive = isActive;
    }
}

public class Warehouse : BaseEntity
{
    private Warehouse() { }

    public Warehouse(string code, string name, string? address)
    {
        Code = NormalizeCode(code);
        Update(name, address, isActive: true);
    }

    public int Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public void Update(string name, string? address, bool isActive)
    {
        Name = name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        IsActive = isActive;
    }
}

public class Supplier : BaseEntity
{
    private Supplier() { }

    public Supplier(string name, string? phone, string? email, string? address) => Update(name, phone, email, address);

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }

    public void Update(string name, string? phone, string? email, string? address)
    {
        Name = name.Trim();
        Phone = Clean(phone);
        Email = Clean(email);
        Address = Clean(address);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
