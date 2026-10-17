using CameraApi.Application;
using Microsoft.AspNetCore.Mvc;

namespace CameraApi.Controllers;

[ApiController]
[Route("api/cameras")]
public sealed class CamerasController(NearestCameraService service) : ControllerBase
{
    [HttpPost("nearest")]
    [ProducesResponseType<IReadOnlyList<NearestCamera>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NearestCamera>>> Nearest(
        [FromBody] NearestCamerasRequest request, CancellationToken cancellationToken)
    {
        var cameras = await service.FindAsync(
            new GeoPoint(request.Latitude!.Value, request.Longitude!.Value),
            request.Limit!.Value, cancellationToken);

        return Ok(cameras.Select(camera => camera with
        {
            DistanceKm = Math.Round(camera.DistanceKm, 2, MidpointRounding.AwayFromZero)
        }).ToArray());
    }
}
