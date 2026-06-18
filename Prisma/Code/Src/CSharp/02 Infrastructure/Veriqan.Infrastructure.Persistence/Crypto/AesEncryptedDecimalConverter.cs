using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Crypto;

/// <summary>
/// EF Core value converter that AES-256-CBC encrypts a <see cref="decimal"/> value on write
/// and decrypts it on read. The persisted column stores ciphertext as a Base64 string so it
/// fits a standard <c>nvarchar</c> column.
/// </summary>
/// <remarks>
/// <para>
/// Each encryption call generates a fresh 16-byte IV, which is prepended to the ciphertext
/// bytes before Base64 encoding: <c>stored = Base64(IV || ciphertext)</c>.
/// Decryption reads the first 16 bytes as the IV and the rest as ciphertext.
/// </para>
/// <para>
/// PKCS7 padding is used; the AES key is 32 bytes (AES-256).
/// </para>
/// </remarks>
public sealed class AesEncryptedDecimalConverter : ValueConverter<decimal, string>
{
    /// <summary>
    /// Initializes a new <see cref="AesEncryptedDecimalConverter"/> with the given key.
    /// </summary>
    /// <param name="key">32-byte AES-256 key material.</param>
    public AesEncryptedDecimalConverter(byte[] key)
        : base(
            plaintext => Encrypt(plaintext, key),
            cipherBase64 => Decrypt(cipherBase64, key))
    {
    }

    internal static string Encrypt(decimal value, byte[] key)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV

        using var encryptor = aes.CreateEncryptor();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(plainBytes, 0, plainBytes.Length);
        cs.FlushFinalBlock();

        return Convert.ToBase64String(ms.ToArray());
    }

    internal static decimal Decrypt(string cipherBase64, byte[] key)
    {
        var allBytes = Convert.FromBase64String(cipherBase64);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = new byte[16];
        Array.Copy(allBytes, 0, iv, 0, 16);
        aes.IV = iv;

        var cipherBytes = new byte[allBytes.Length - 16];
        Array.Copy(allBytes, 16, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs, System.Text.Encoding.UTF8);
        var plainText = reader.ReadToEnd();

        return decimal.Parse(plainText, System.Globalization.CultureInfo.InvariantCulture);
    }
}
