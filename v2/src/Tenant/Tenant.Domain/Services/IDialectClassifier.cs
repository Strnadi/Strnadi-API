namespace Tenant.Domain.Services;

public record DialectPrediction(double StartSeconds, double EndSeconds, string? DialectCode, bool IsRepresentant);

public interface IDialectClassifier
{
    Task<DialectPrediction[]?> ClassifyAsync(byte[] normalizedAudio, string fileName, CancellationToken cancellationToken = default);
}