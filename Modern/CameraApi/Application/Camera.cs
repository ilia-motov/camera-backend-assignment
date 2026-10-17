namespace CameraApi.Application;

public sealed record Camera(int Id, string Name, GeoPoint Location);
public sealed record NearestCamera(int Id, string Name, double DistanceKm);
