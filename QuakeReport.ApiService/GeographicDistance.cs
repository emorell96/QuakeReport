namespace QuakeReport.ApiService;

internal static class GeographicDistance
{
    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    public static double Kilometers(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKilometers = 6371.0088;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var firstLatitude = DegreesToRadians(latitude1);
        var secondLatitude = DegreesToRadians(latitude2);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
                Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return earthRadiusKilometers * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
