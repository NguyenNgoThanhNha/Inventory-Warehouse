using System.Collections;
using System.Data;
using System.Reflection;

namespace Inventory.Application.Common.Data;

/// <summary>
/// Đọc kết quả stored procedure theo thứ tự bảng — port từ Backend_Api_Template:
/// <c>var ds = unitOfWork.ExecuteStoreProcedureGetMultiTables(...).ToDataSetSimpleRead();
/// var total = ds.TryRead&lt;int&gt;()?.FirstOrDefault() ?? 0; var rows = ds.TryRead&lt;Dto&gt;() ?? [];</c>
/// </summary>
public static class DataSetExtensions
{
    public static DataSetSimpleRead ToDataSetSimpleRead(this DataSet dataSet) => new(dataSet);

    public static List<T> ToList<T>(this DataTable table)
    {
        if (typeof(T).IsValueType || typeof(T) == typeof(string))
            return table.Rows.Cast<DataRow>().Select(r => r[0] == DBNull.Value ? default! : (T)Convert.ChangeType(r[0], Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T))).ToList();

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && table.Columns.Contains(p.Name))
            .ToList();

        var result = new List<T>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            var item = Activator.CreateInstance<T>();
            foreach (var property in properties)
            {
                var value = row[property.Name];
                if (value == DBNull.Value) continue;
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                property.SetValue(item, targetType.IsEnum ? Enum.ToObject(targetType, value) : Convert.ChangeType(value, targetType));
            }
            result.Add(item);
        }
        return result;
    }
}

public sealed class DataSetSimpleRead(DataSet dataSet)
{
    private readonly IEnumerator _enumerator = dataSet.Tables.GetEnumerator();

    /// <summary>Đọc bảng hiện tại rồi chuyển sang bảng kế tiếp. Ném lỗi nếu hết bảng.</summary>
    public List<T> Read<T>() =>
        _enumerator.MoveNext() ? ((DataTable)_enumerator.Current!).ToList<T>() : throw new InvalidOperationException("Index of table out of range");

    /// <summary>Như <see cref="Read{T}"/> nhưng trả null thay vì ném lỗi.</summary>
    public List<T>? TryRead<T>()
    {
        try { return Read<T>(); }
        catch (InvalidOperationException) { return null; }
    }
}
