using CameraApi.Application;

namespace CameraApi.Tests;

public sealed class NearestCameraServiceTests
{
    [Fact]
    public async Task PassesPointLimitAndCancellationToDatabaseSearch()
    {
        var repository = new StubRepository([new(7, "Camera", 1.25)]);
        var service = new NearestCameraService(repository);
        var point = new GeoPoint(55.7558, 37.6173);
        using var cts = new CancellationTokenSource();

        var result = await service.FindAsync(point, 3, cts.Token);

        Assert.Equal(new NearestCamera(7, "Camera", 1.25), Assert.Single(result));
        Assert.Equal(point, repository.Point);
        Assert.Equal(3, repository.Limit);
        Assert.Equal(cts.Token, repository.Token);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task CancelledRequestDoesNotCallRepository()
    {
        var repository = new StubRepository([]);
        var service = new NearestCameraService(repository);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.FindAsync(new GeoPoint(0, 0), 3, cts.Token));
        Assert.Equal(0, repository.Calls);
    }
}

internal sealed class StubRepository(IReadOnlyList<NearestCamera> cameras) : ICameraRepository
{
    public int Calls { get; private set; }
    public GeoPoint Point { get; private set; }
    public int Limit { get; private set; }
    public CancellationToken Token { get; private set; }

    public Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("The endpoint must not load all cameras.");

    public Task<IReadOnlyList<NearestCamera>> GetNearestActiveAsync(
        GeoPoint point, int limit, CancellationToken cancellationToken)
    {
        Calls++;
        Point = point;
        Limit = limit;
        Token = cancellationToken;
        return Task.FromResult(cameras);
    }
}
