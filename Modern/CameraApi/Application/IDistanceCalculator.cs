namespace CameraApi.Application;

public interface IDistanceCalculator
{
    double CalculateKm(GeoPoint from, GeoPoint to);
}
