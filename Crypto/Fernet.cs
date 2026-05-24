using System.Security.Cryptography;
using System;

namespace ApexAuth.Crypto;

public static class Fernet
{
    private const byte Version = 0x80;

    public static string Encrypt(byte[] key, byte[] plaintext)
    {
        if (key.Length != 32) throw new ArgumentException("Fernet key must be 32 bytes.");

        var signingKey = key[..16];
        var encryptionKey = key[16..];
        var iv = RandomNumberGenerator.GetBytes(16);
        byte[] ciphertext;

        using (var aes = Aes.Create())
        {
            aes.Key = encryptionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var encryptor = aes.CreateEncryptor();
            ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = new byte[1 + 8 + iv.Length + ciphertext.Length];
        body[0] = Version;
        WriteBigEndian(timestamp, body.AsSpan(1, 8));
        Buffer.BlockCopy(iv, 0, body, 9, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, body, 25, ciphertext.Length);

        using var hmac = new HMACSHA256(signingKey);
        var tag = hmac.ComputeHash(body);
        var token = new byte[body.Length + tag.Length];
        Buffer.BlockCopy(body, 0, token, 0, body.Length);
        Buffer.BlockCopy(tag, 0, token, body.Length, tag.Length);
        return Base64UrlEncode(token);
    }

    public static byte[] Decrypt(byte[] key, string token)
    {
        if (key.Length != 32) throw new ArgumentException("Fernet key must be 32 bytes.");

        var raw = Base64UrlDecode(token);
        if (raw.Length < 1 + 8 + 16 + 32 || raw[0] != Version)
            throw new CryptographicException("Invalid vault token.");

        var signingKey = key[..16];
        var encryptionKey = key[16..];
        var bodyLength = raw.Length - 32;
        var body = raw.AsSpan(0, bodyLength).ToArray();
        var expectedTag = raw.AsSpan(bodyLength, 32).ToArray();

        using (var hmac = new HMACSHA256(signingKey))
        {
            var actualTag = hmac.ComputeHash(body);
            if (!CryptographicOperations.FixedTimeEquals(actualTag, expectedTag))
                throw new CryptographicException("Invalid password or corrupted vault.");
        }

        var iv = raw.AsSpan(9, 16).ToArray();
        var ciphertext = raw.AsSpan(25, bodyLength - 25).ToArray();
        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private static void WriteBigEndian(long value, Span<byte> target)
    {
        for (var i = 7; i >= 0; i--)
        {
            target[i] = (byte)(value & 0xff);
            value >>= 8;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
