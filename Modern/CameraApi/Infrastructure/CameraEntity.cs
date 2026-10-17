namespace CameraApi.Infrastructure;

public sealed class CameraEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; }
}
