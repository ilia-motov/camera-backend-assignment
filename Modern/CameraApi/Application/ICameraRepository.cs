namespace CameraApi.Application;

public interface ICameraRepository
{
    Task<IReadOnlyList<Camera>> GetActiveAsync(CancellationToken cancellationToken);
}
