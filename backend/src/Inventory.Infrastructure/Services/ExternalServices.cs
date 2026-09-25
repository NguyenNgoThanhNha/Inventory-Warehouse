using Inventory.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Services;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Thư mục gốc lưu file (tương đối với ContentRoot hoặc tuyệt đối).</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
}

/// <summary>Lưu file trên đĩa. Thay bằng Azure Blob / S3 chỉ cần viết implementation IFileStorage khác.</summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default)
    {
        var relative = Path.Combine(folder, $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}").Replace('\\', '/');
        var fullPath = ResolveSafe(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
        return relative;
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        var fullPath = ResolveSafe(storagePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("File không tồn tại trên storage.", storagePath);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    /// <summary>Chặn path traversal (../).</summary>
    private string ResolveSafe(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Đường dẫn file không hợp lệ.");
        return full;
    }
}

/// <summary>Dev: không gửi mail thật, chỉ ghi log (xem link reset mật khẩu trong console/file log).</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[EMAIL] To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
