namespace Tenant.Domain.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    
    string Decrypt(string cipherText);
}