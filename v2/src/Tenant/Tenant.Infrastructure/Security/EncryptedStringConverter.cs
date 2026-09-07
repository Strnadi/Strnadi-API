using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tenant.Domain.Services;

namespace Tenant.Infrastructure.Security;

public class EncryptedStringConverter(IEncryptionService encryption)
    : ValueConverter<string, string>(
        plainText => encryption.Encrypt(plainText),
        cipherText => encryption.Decrypt(cipherText));