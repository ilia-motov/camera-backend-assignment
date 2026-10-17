namespace CameraApi.Application;

public sealed class HaversineDistanceCalculator : IDistanceCalculator
{
    private const double EarthRadiusKm = 6371.0088;
    private const double DegreesToRadians = Math.PI / 180;

    public double CalculateKm(GeoPoint from, GeoPoint to)
    {
        var latitudeDelta = (to.Latitude - from.Latitude) * DegreesToRadians;
        var longitudeDelta = (to.Longitude - from.Longitude) * DegreesToRadians;
        var sinLatitude = Math.Sin(latitudeDelta / 2);
        var sinLongitude = Math.Sin(longitudeDelta / 2);

        var a = sinLatitude * sinLatitude
            + Math.Cos(from.Latitude * DegreesToRadians)
            * Math.Cos(to.Latitude * DegreesToRadians)
            * sinLongitude * sinLongitude;

        // Floating-point rounding can put a just outside [0, 1] near antipodes.
        a = Math.Clamp(a, 0, 1);
        return 2 * EarthRadiusKm * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
