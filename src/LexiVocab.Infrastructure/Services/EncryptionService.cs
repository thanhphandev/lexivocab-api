using System.Security.Cryptography;
using System.Text;
using LexiVocab.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace LexiVocab.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["ENCRYPTION_KEY"] 
            ?? throw new InvalidOperationException("Encryption key is missing in configuration.");
        
        // Ensure the key is 32 bytes (256 bits) for AES-256
        var fullKey = Encoding.UTF8.GetBytes(keyString);
        _key = new byte[32];
        Array.Copy(fullKey, _key, Math.Min(fullKey.Length, 32));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
        }

        var iv = Convert.ToBase64String(aes.IV);
        var cipherText = Convert.ToBase64String(ms.ToArray());

        return $"{iv}:{cipherText}";
    }

    public string? Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            var parts = cipherText.Split(':');
            if (parts.Length != 2) return null; // Not encrypted with this service or invalid format

            var iv = Convert.FromBase64String(parts[0]);
            var buffer = Convert.FromBase64String(parts[1]);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
        catch
        {
            return null; // Decryption failed (wrong key or corrupted data)
        }
    }
}
