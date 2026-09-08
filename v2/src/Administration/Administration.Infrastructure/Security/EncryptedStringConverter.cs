using Administration.Domain.Services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Administration.Infrastructure.Security;

public class EncryptedStringConverter(IEncryptionService encryption)
    : ValueConverter<string, string>(
        plainText => encryption.Encrypt(plainText),
        cipherText => encryption.Decrypt(cipherText));
