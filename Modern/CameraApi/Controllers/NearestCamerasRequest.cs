using System.ComponentModel.DataAnnotations;

namespace CameraApi.Controllers;

public sealed class NearestCamerasRequest
{
    [Required, Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Required, Range(-180d, 180d)]
    public double? Longitude { get; init; }

    [Required, Range(1, 100)]
    public int? Limit { get; init; }
}
