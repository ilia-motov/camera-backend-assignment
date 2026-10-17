using CameraApi.Application;

namespace CameraApi.Tests;

public sealed class HaversineDistanceCalculatorTests
{
    private readonly HaversineDistanceCalculator calculator = new();

    [Fact]
    public void IdenticalPointsHaveZeroDistance()
    {
        var point = new GeoPoint(55.7558, 37.6173);
        Assert.Equal(0, calculator.CalculateKm(point, point));
    }

    [Fact]
    public void DistanceIsSymmetric()
    {
        var moscow = new GeoPoint(55.7558, 37.6173);
        var london = new GeoPoint(51.5074, -0.1278);
        Assert.Equal(calculator.CalculateKm(moscow, london),
            calculator.CalculateKm(london, moscow), precision: 8);
    }

    [Fact]
    public void LondonToNewYorkIsApproximately5570Km()
    {
        var distance = calculator.CalculateKm(new GeoPoint(51.5074, -0.1278),
            new GeoPoint(40.7128, -74.0060));
        Assert.InRange(distance, 5560, 5580);
    }
}
