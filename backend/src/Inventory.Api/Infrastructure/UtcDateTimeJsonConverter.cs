using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventory.Api.Infrastructure;

/// <summary>
/// DB lưu UTC nhưng SQL Server trả DateTime Kind=Unspecified → serialize thiếu "Z" và FE hiểu nhầm là giờ local.
/// Converter này luôn ghi ISO-8601 có "Z" và chuẩn hóa giá trị đọc vào về UTC.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
    }
}
