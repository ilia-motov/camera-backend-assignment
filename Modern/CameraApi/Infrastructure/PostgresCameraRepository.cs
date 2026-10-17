using CameraApi.Application;
using Microsoft.EntityFrameworkCore;

namespace CameraApi.Infrastructure;

public sealed class PostgresCameraRepository(CameraDbContext context) : ICameraRepository
{
    public async Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken)
    {
        return await context.Cameras
            .AsNoTracking()
            .Where(camera => camera.IsActive)
            .Select(camera => new Camera(camera.Id, camera.Name,
                new GeoPoint(camera.Latitude, camera.Longitude)))
            .ToListAsync(cancellationToken);
    }
}
