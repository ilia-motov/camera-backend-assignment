namespace CameraApi.Application;

public interface ICameraRepository
{
    Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NearestCamera>> GetNearestActiveAsync(
        GeoPoint point, int limit, CancellationToken cancellationToken);
}
