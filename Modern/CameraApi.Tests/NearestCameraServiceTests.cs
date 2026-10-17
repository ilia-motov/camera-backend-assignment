using CameraApi.Application;

namespace CameraApi.Tests;

public sealed class NearestCameraServiceTests
{
    [Fact]
    public async Task SelectsNearestAndBreaksExactTiesById()
    {
        var repository = new StubRepository([
            new(9, "Far", new GeoPoint(10, 10)),
            new(3, "Same-3", new GeoPoint(0, 0)),
            new(1, "Same-1", new GeoPoint(0, 0)),
            new(2, "Near", new GeoPoint(0, 0.001))
        ]);
        var service = new NearestCameraService(repository, new HaversineDistanceCalculator());
        var result = await service.FindAsync(new GeoPoint(0, 0), 3, default);
        Assert.Equal(new[] { 1, 3, 2 }, result.Select(camera => camera.Id));
    }

    [Fact]
    public async Task ReturnsAvailableCamerasWhenFewerThanLimit()
    {
        var repository = new StubRepository([new(7, "Camera", new GeoPoint(0, 0))]);
        var service = new NearestCameraService(repository, new HaversineDistanceCalculator());
        var result = await service.FindAsync(new GeoPoint(0, 0), 3, default);
        Assert.Equal(7, Assert.Single(result).Id);
    }
}

internal sealed class StubRepository(IReadOnlyList<Camera> cameras) : ICameraRepository
{
    public int Calls { get; private set; }

    public Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(cameras);
    }
}
