namespace CameraApi.Application;

public sealed class NearestCameraService(
    ICameraRepository repository,
    IDistanceCalculator distanceCalculator)
{
    public async Task<IReadOnlyList<NearestCamera>> FindAsync(
        GeoPoint point, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));

        cancellationToken.ThrowIfCancellationRequested();

        var cameras = await repository.GetActiveAsync(cancellationToken);
        var nearest = new List<NearestCamera>(cameras.Count);

        foreach (var camera in cameras)
        {
            cancellationToken.ThrowIfCancellationRequested();

            nearest.Add(new NearestCamera(camera.Id, camera.Name,
                distanceCalculator.CalculateKm(point, camera.Location)));
        }

        cancellationToken.ThrowIfCancellationRequested();

        nearest.Sort((left, right) =>
        {
            var distanceOrder = left.DistanceKm.CompareTo(right.DistanceKm);
            return distanceOrder != 0 ? distanceOrder : left.Id.CompareTo(right.Id);
        });

        cancellationToken.ThrowIfCancellationRequested();

        return nearest.Take(limit).ToArray();
    }
}
