using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Platform.Shared.Kernel.Services;

namespace Platform.Shared.Infrastructure.Security;

public class EncryptedStringConverter(IEncryptionService encryption)
    : ValueConverter<string, string>(
        plainText => encryption.Encrypt(plainText),
        cipherText => encryption.Decrypt(cipherText));
