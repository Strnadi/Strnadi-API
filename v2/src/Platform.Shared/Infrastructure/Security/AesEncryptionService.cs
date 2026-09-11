using System.Security.Cryptography;
using System.Text;
using Platform.Shared.Kernel.Configuration;
using Platform.Shared.Kernel.Services;

namespace Platform.Shared.Infrastructure.Security;

public class AesEncryptionService(IEncryptionSettings encryptionSettings) : IEncryptionService
{
    private readonly byte[] _key = Convert.FromBase64String(encryptionSettings.Key);

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    // Tolerates rows written before encryption was introduced: existing plaintext values are
    // read back unchanged (Encrypt is unconditional, so they self-heal into ciphertext on the
    // next write) instead of requiring a one-off backfill migration before this can be deployed.
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        return TryDecrypt(cipherText, out var plainText) ? plainText : cipherText;
    }

    private bool TryDecrypt(string cipherText, out string plainText)
    {
        plainText = string.Empty;

        byte[] fullBytes;
        try
        {
            fullBytes = Convert.FromBase64String(cipherText);
        }
        catch (FormatException)
        {
            return false; // not base64 at all - definitely legacy plaintext
        }

        using var aes = Aes.Create();
        aes.Key = _key;

        var ivLength = aes.IV.Length;
        if (fullBytes.Length <= ivLength || (fullBytes.Length - ivLength) % (aes.BlockSize / 8) != 0)
            return false; // wrong shape to be IV + PKCS7-padded ciphertext

        var iv = new byte[ivLength];
        var cipherBytes = new byte[fullBytes.Length - ivLength];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, ivLength);
        Buffer.BlockCopy(fullBytes, ivLength, cipherBytes, 0, cipherBytes.Length);
        aes.IV = iv;

        try
        {
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            plainText = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(plainBytes);
            return true;
        }
        catch (Exception e) when (e is CryptographicException or DecoderFallbackException)
        {
            return false;
        }
    }
}
