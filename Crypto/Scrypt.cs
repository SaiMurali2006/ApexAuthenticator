using System.Buffers.Binary;
using System.Security.Cryptography;
using System;

namespace ApexAuth.Crypto;

public static class Scrypt
{
    public static byte[] DeriveKey(string password, byte[] salt, int n = 32768, int r = 8, int p = 1, int dkLen = 32)
    {
        if (n <= 1 || (n & (n - 1)) != 0) throw new ArgumentException("N must be a power of two greater than 1.");
        if (r <= 0 || p <= 0 || dkLen <= 0) throw new ArgumentOutOfRangeException();

        var passBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var b = Pbkdf2(passBytes, salt, p * 128 * r);
        var block = new byte[128 * r];

        for (var i = 0; i < p; i++)
        {
            Buffer.BlockCopy(b, i * block.Length, block, 0, block.Length);
            Smix(block, r, n);
            Buffer.BlockCopy(block, 0, b, i * block.Length, block.Length);
        }

        return Pbkdf2(passBytes, b, dkLen);
    }

    private static byte[] Pbkdf2(byte[] password, byte[] salt, int length)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 1, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(length);
    }

    private static void Smix(byte[] b, int r, int n)
    {
        var x = (byte[])b.Clone();
        var v = new byte[n * b.Length];

        for (var i = 0; i < n; i++)
        {
            Buffer.BlockCopy(x, 0, v, i * b.Length, b.Length);
            BlockMix(x, r);
        }

        for (var i = 0; i < n; i++)
        {
            var j = Integerify(x, r) & (ulong)(n - 1);
            XorBlock(x, 0, v, (int)j * b.Length, b.Length);
            BlockMix(x, r);
        }

        Buffer.BlockCopy(x, 0, b, 0, b.Length);
        CryptographicOperations.ZeroMemory(x);
        CryptographicOperations.ZeroMemory(v);
    }

    private static ulong Integerify(byte[] b, int r)
    {
        var offset = (2 * r - 1) * 64;
        return BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(offset, 8));
    }

    private static void BlockMix(byte[] b, int r)
    {
        var x = new byte[64];
        var y = new byte[b.Length];
        Buffer.BlockCopy(b, (2 * r - 1) * 64, x, 0, 64);

        for (var i = 0; i < 2 * r; i++)
        {
            XorBlock(x, 0, b, i * 64, 64);
            Salsa208(x);
            Buffer.BlockCopy(x, 0, y, i * 64, 64);
        }

        for (var i = 0; i < r; i++)
            Buffer.BlockCopy(y, i * 2 * 64, b, i * 64, 64);
        for (var i = 0; i < r; i++)
            Buffer.BlockCopy(y, (i * 2 + 1) * 64, b, (i + r) * 64, 64);

        CryptographicOperations.ZeroMemory(x);
        CryptographicOperations.ZeroMemory(y);
    }

    private static void XorBlock(byte[] target, int targetOffset, byte[] source, int sourceOffset, int count)
    {
        for (var i = 0; i < count; i++)
            target[targetOffset + i] ^= source[sourceOffset + i];
    }

    private static uint Rotate(uint value, int count) => (value << count) | (value >> (32 - count));

    private static void Salsa208(byte[] block)
    {
        Span<uint> x = stackalloc uint[16];
        Span<uint> original = stackalloc uint[16];
        for (var i = 0; i < 16; i++)
            x[i] = original[i] = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(i * 4, 4));

        for (var i = 0; i < 8; i += 2)
        {
            x[4] ^= Rotate(x[0] + x[12], 7); x[8] ^= Rotate(x[4] + x[0], 9);
            x[12] ^= Rotate(x[8] + x[4], 13); x[0] ^= Rotate(x[12] + x[8], 18);
            x[9] ^= Rotate(x[5] + x[1], 7); x[13] ^= Rotate(x[9] + x[5], 9);
            x[1] ^= Rotate(x[13] + x[9], 13); x[5] ^= Rotate(x[1] + x[13], 18);
            x[14] ^= Rotate(x[10] + x[6], 7); x[2] ^= Rotate(x[14] + x[10], 9);
            x[6] ^= Rotate(x[2] + x[14], 13); x[10] ^= Rotate(x[6] + x[2], 18);
            x[3] ^= Rotate(x[15] + x[11], 7); x[7] ^= Rotate(x[3] + x[15], 9);
            x[11] ^= Rotate(x[7] + x[3], 13); x[15] ^= Rotate(x[11] + x[7], 18);
            x[1] ^= Rotate(x[0] + x[3], 7); x[2] ^= Rotate(x[1] + x[0], 9);
            x[3] ^= Rotate(x[2] + x[1], 13); x[0] ^= Rotate(x[3] + x[2], 18);
            x[6] ^= Rotate(x[5] + x[4], 7); x[7] ^= Rotate(x[6] + x[5], 9);
            x[4] ^= Rotate(x[7] + x[6], 13); x[5] ^= Rotate(x[4] + x[7], 18);
            x[11] ^= Rotate(x[10] + x[9], 7); x[8] ^= Rotate(x[11] + x[10], 9);
            x[9] ^= Rotate(x[8] + x[11], 13); x[10] ^= Rotate(x[9] + x[8], 18);
            x[12] ^= Rotate(x[15] + x[14], 7); x[13] ^= Rotate(x[12] + x[15], 9);
            x[14] ^= Rotate(x[13] + x[12], 13); x[15] ^= Rotate(x[14] + x[13], 18);
        }

        for (var i = 0; i < 16; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(i * 4, 4), x[i] + original[i]);
    }
}
