using CameraApi.Application;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CameraApi.Infrastructure;

public sealed class PostgresCameraRepository(CameraDbContext context) : ICameraRepository
{
    public async Task<IReadOnlyList<NearestCamera>> GetNearestActiveAsync(
        GeoPoint point, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var location = new Point(point.Longitude, point.Latitude) { SRID = 4326 };
        // A secondary Id ordering in the KNN query can prevent a GiST scan on PostgreSQL 16.
        // Keep all boundary ties, then resolve them by Id outside the indexed search.
        return await context.Database.SqlQuery<NearestCamera>($"""
            SELECT nearest.id AS "Id", nearest.name AS "Name",
                   ST_Distance(nearest.location, {location}::geography, false) / 1000 AS "DistanceKm"
            FROM (
                SELECT id, name, location, location <-> {location}::geography AS distance_m
                FROM cameras
                WHERE is_active
                ORDER BY location <-> {location}::geography
                FETCH FIRST {limit} ROWS WITH TIES
            ) AS nearest
            ORDER BY nearest.distance_m, nearest.id
            LIMIT {limit}
            """)
            .ToListAsync(cancellationToken);
    }

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
