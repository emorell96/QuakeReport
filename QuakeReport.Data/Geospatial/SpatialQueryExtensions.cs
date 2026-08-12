using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using QuakeReport.Data.Models;

namespace QuakeReport.Data.Geospatial;

public static class SpatialQueryExtensions
{
    public static IOrderedQueryable<TEntity> OrderByDistanceFrom<TEntity>(
        this IQueryable<TEntity> query,
        Point origin,
        bool useDatabaseKnn = true)
        where TEntity : class, IEntityWithLocation
    {
        var located = query.Where(entity => entity.Location != null);
        return useDatabaseKnn
            ? located.OrderBy(entity => EF.Functions.DistanceKnn(entity.Location!, origin))
            : located.OrderBy(entity => entity.Location!.Distance(origin));
    }

    public static IQueryable<TEntity> WithinDistanceOf<TEntity>(
        this IQueryable<TEntity> query,
        Point origin,
        double meters)
        where TEntity : class, IEntityWithLocation =>
        query.Where(entity => entity.Location != null && entity.Location.IsWithinDistance(origin, meters));
}
