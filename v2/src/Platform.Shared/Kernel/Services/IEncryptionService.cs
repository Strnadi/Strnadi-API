namespace Platform.Shared.Kernel.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);
}
