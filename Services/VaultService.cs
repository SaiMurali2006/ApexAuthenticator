using System.Security.Cryptography;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApexAuth.Crypto;
using ApexAuth.Models;

namespace ApexAuth.Services;

public sealed class VaultService
{
    private readonly string _vaultPath;
    private byte[]? _sessionKey;

    public VaultPayload Payload { get; private set; } = new();
    public bool Exists => File.Exists(_vaultPath);

    public VaultService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexAuth");
        Directory.CreateDirectory(dir);
        _vaultPath = Path.Combine(dir, "data.vault");
    }

    public string VaultPath => _vaultPath;

    private const int NewVaultN = 131072;

    public void Create(string masterPassword)
    {
        Payload = new VaultPayload();
        var salt = RandomNumberGenerator.GetBytes(16);
        _sessionKey = Scrypt.DeriveKey(masterPassword, salt, NewVaultN);
        SaveWithSalt(salt, NewVaultN);
    }

    public void Unlock(string masterPassword)
    {
        var envelope = LoadEnvelope(_vaultPath);
        var salt = Convert.FromBase64String(envelope.Salt);
        var key = Scrypt.DeriveKey(masterPassword, salt, envelope.N, envelope.R, envelope.P);
        byte[]? json = null;
        try
        {
            json = Fernet.Decrypt(key, envelope.Token);
            Payload = JsonSerializer.Deserialize<VaultPayload>(json) ?? new VaultPayload();
            _sessionKey = key;
        }
        finally
        {
            if (json is not null) CryptographicOperations.ZeroMemory(json);
        }
    }

    public void Save()
    {
        EnsureUnlocked();
        var envelope = LoadEnvelope(_vaultPath);
        SaveWithSalt(Convert.FromBase64String(envelope.Salt), envelope.N);
    }

    public void ExportBackup(string path, string password)
    {
        EnsureUnlocked();
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Scrypt.DeriveKey(password, salt, NewVaultN);
        var json = JsonSerializer.SerializeToUtf8Bytes(Payload);
        try
        {
            var envelope = new VaultEnvelope
            {
                Salt = Convert.ToBase64String(salt),
                N = NewVaultN,
                Token = Fernet.Encrypt(key, json)
            };
            WriteEnvelope(path, envelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public int ImportBackup(string path, string password)
    {
        EnsureUnlocked();
        var envelope = LoadEnvelope(path);
        var salt = Convert.FromBase64String(envelope.Salt);
        var key = Scrypt.DeriveKey(password, salt, envelope.N, envelope.R, envelope.P);
        byte[]? json = null;
        try
        {
            json = Fernet.Decrypt(key, envelope.Token, TimeSpan.FromDays(365 * 5));
            var incoming = JsonSerializer.Deserialize<VaultPayload>(json) ?? new VaultPayload();
            var existing = Payload.Accounts.ToDictionary(x => x.Id);
            var imported = 0;

            foreach (var account in incoming.Accounts)
            {
                if (existing.ContainsKey(account.Id))
                {
                    account.Id = Guid.NewGuid();
                }

                Payload.Accounts.Add(account);
                imported++;
            }

            Save();
            return imported;
        }
        finally
        {
            if (json is not null) CryptographicOperations.ZeroMemory(json);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public void Lock()
    {
        Payload = new VaultPayload();
        if (_sessionKey is not null)
            CryptographicOperations.ZeroMemory(_sessionKey);
        _sessionKey = null;
        TotpService.ClearCache();
    }

    private void SaveWithSalt(byte[] salt, int n)
    {
        EnsureUnlocked();
        var json = JsonSerializer.SerializeToUtf8Bytes(Payload);
        try
        {
            var envelope = new VaultEnvelope
            {
                Salt = Convert.ToBase64String(salt),
                N = n,
                Token = Fernet.Encrypt(_sessionKey!, json)
            };
            WriteEnvelope(_vaultPath, envelope);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    private static VaultEnvelope LoadEnvelope(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<VaultEnvelope>(json) ?? throw new InvalidDataException("Invalid vault file.");
    }

    private static void WriteEnvelope(string path, VaultEnvelope envelope)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope);
        var tmpPath = path + ".tmp";

        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(payload, 0, payload.Length);
            fs.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            var backupPath = path + ".bak";
            File.Replace(tmpPath, path, backupPath);
        }
        else
        {
            File.Move(tmpPath, path);
        }
    }

    private void EnsureUnlocked()
    {
        if (_sessionKey is null) throw new InvalidOperationException("Vault is locked.");
    }
}
