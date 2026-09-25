using Inventory.Domain.Entities.Sys;

namespace Inventory.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(SysAccount user, string password);

    bool Verify(SysAccount user, string password);
}

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(SysAccount user);

    /// <summary>Sinh chuỗi ngẫu nhiên an toàn (refresh token / reset token).</summary>
    string GenerateSecureToken();

    TimeSpan RefreshTokenLifetime { get; }
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface IFileStorage
{
    /// <returns>Đường dẫn lưu trữ tương đối (lưu vào Attachment.StoragePath).</returns>
    Task<string> SaveAsync(Stream content, string fileName, string folder, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
}
