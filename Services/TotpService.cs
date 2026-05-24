using System.Security.Cryptography;
using System;
using System.Collections.Generic;

namespace ApexAuth.Services;

public static class TotpService
{
    private const int StepSeconds = 30;

    public static string GetCode(string secret, DateTimeOffset? now = null)
    {
        var key = DecodeBase32(secret);
        var unix = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var counter = unix / StepSeconds;
        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6");
    }

    public static int SecondsRemaining(DateTimeOffset? now = null)
    {
        var seconds = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        return StepSeconds - (int)(seconds % StepSeconds);
    }

    public static double CycleProgress(DateTimeOffset? now = null)
    {
        var seconds = (now ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds() / 1000.0;
        return (seconds % StepSeconds) / StepSeconds;
    }

    public static string NormalizeSecret(string secret)
    {
        return secret.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
    }

    public static bool IsValidSecret(string secret)
    {
        try
        {
            return DecodeBase32(secret).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = NormalizeSecret(input).TrimEnd('=');
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in normalized)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0) throw new FormatException("Secret is not valid Base32.");
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
