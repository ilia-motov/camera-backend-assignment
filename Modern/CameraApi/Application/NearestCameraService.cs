namespace CameraApi.Application;

public sealed class NearestCameraService(ICameraRepository repository)
{
    public Task<IReadOnlyList<NearestCamera>> FindAsync(
        GeoPoint point, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));

        cancellationToken.ThrowIfCancellationRequested();
        return repository.GetNearestActiveAsync(point, limit, cancellationToken);
    }
}
