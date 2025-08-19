using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Binance;

public sealed class Signer
{
    private readonly byte[] _secret;
    public Signer(string secret) => _secret = Encoding.UTF8.GetBytes(secret);

    public string SignToHex(string message)
    {
        using var h = new HMACSHA256(_secret);
        var data = Encoding.UTF8.GetBytes(message);
        var hash = h.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2")); // lowercase hex
        return sb.ToString();
    }
}

