using Platform.Shared.Kernel.Exceptions;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Recordings;

public class DetectedDialectsService(IDetectedDialectsRepository detectedDialects, IUnitOfWork unitOfWork)
{
    public Task<DetectedDialect[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        detectedDialects.GetAllAsync(cancellationToken);

    public async Task<DetectedDialect> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await detectedDialects.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(DetectedDialect), id);

    public async Task<int> CreateAsync(DetectedDialectUploadRequest request, CancellationToken cancellationToken = default)
    {
        var detectedDialect = new DetectedDialect
        {
            FilteredRecordingPartId = request.FilteredPartId,
            UserGuessDialectId = request.UserGuessDialectId,
            ConfirmedDialectId = request.ConfirmedDialectId,
            PredictedDialectId = request.PredictedDialectId,
        };

        detectedDialects.Add(detectedDialect);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return detectedDialect.Id;
    }

    public async Task UpdateAsync(UpdateDetectedDialectRequest request, CancellationToken cancellationToken = default)
    {
        var detectedDialect = await detectedDialects.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DetectedDialect), request.Id);

        detectedDialect.UserGuessDialectId = request.UserGuessDialectId ?? detectedDialect.UserGuessDialectId;
        detectedDialect.ConfirmedDialectId = request.ConfirmedDialectId ?? detectedDialect.ConfirmedDialectId;
        detectedDialect.PredictedDialectId = request.PredictedDialectId ?? detectedDialect.PredictedDialectId;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var detectedDialect = await detectedDialects.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(DetectedDialect), id);

        detectedDialects.Remove(detectedDialect);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
