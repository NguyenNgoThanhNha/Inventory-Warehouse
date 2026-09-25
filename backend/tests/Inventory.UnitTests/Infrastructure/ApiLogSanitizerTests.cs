using Inventory.Infrastructure.Logging;

namespace Inventory.UnitTests.Infrastructure;

public class ApiLogSanitizerTests
{
    private static readonly ApiLoggingOptions Options = new() { MaxBodyLength = 200 };

    [Fact]
    public void Masks_sensitive_fields_case_insensitive_and_nested()
    {
        const string body = """{"email":"a@x.vn","Password":"Secret@1","user":{"refreshToken":"abc"},"items":[{"token":"t"}]}""";

        var result = ApiLogSanitizer.Sanitize(body, "application/json", Options)!;

        Assert.DoesNotContain("Secret@1", result);
        Assert.DoesNotContain("\"abc\"", result);
        Assert.Contains("\"Password\":\"***\"", result);
        Assert.Contains("\"refreshToken\":\"***\"", result);
        Assert.Contains("\"token\":\"***\"", result);
        Assert.Contains("a@x.vn", result);
    }

    [Fact]
    public void Truncates_long_body()
    {
        var result = ApiLogSanitizer.Sanitize(new string('x', 500), "text/plain", Options)!;

        Assert.EndsWith(ApiLogSanitizer.TruncatedSuffix, result);
        Assert.Equal(200 + ApiLogSanitizer.TruncatedSuffix.Length, result.Length);
    }

    [Fact]
    public void Keeps_invalid_json_as_is() =>
        Assert.Equal("{not json", ApiLogSanitizer.Sanitize("{not json", "application/json", Options));
}
