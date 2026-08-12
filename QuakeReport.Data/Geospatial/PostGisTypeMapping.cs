using Npgsql;

namespace QuakeReport.Data.Geospatial;

/// <summary>
/// Configures the low-level Npgsql spatial mapper before Aspire creates its
/// Azure authentication-aware data source. EF's UseNetTopologySuite configures
/// query translation, but an externally-created data source also needs this
/// ADO.NET mapping in order to read PostGIS values.
/// </summary>
public static class PostGisTypeMapping
{
    public static void Configure()
    {
#pragma warning disable CS0618 // Required for data sources created internally by the Aspire Azure integration.
        NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite();
#pragma warning restore CS0618
    }
}
