using System.Security.Cryptography;
using System.Text;

namespace Inventory.Application.Common.Security;

/// <summary>Chỉ lưu hash SHA-256 của refresh/reset token trong DB, không lưu bản gốc.</summary>
public static class TokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
