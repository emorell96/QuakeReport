using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Geospatial;

public static class GeoPoint
{
    public const int Wgs84Srid = 4326;

    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    public static Point? FromCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null) return null;
        if (latitude is null || longitude is null || !IsValid(latitude.Value, longitude.Value))
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude and longitude must be supplied together and be valid WGS84 coordinates.");
        return FromCoordinates(latitude.Value, longitude.Value);
    }

    public static Point FromCoordinates(double latitude, double longitude)
    {
        if (!IsValid(latitude, longitude))
            throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates must be valid WGS84 coordinates.");
        return new Point(longitude, latitude) { SRID = Wgs84Srid };
    }

    public static double? Latitude(Point? point) => point?.Y;
    public static double? Longitude(Point? point) => point?.X;
}
