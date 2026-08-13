using System.Linq.Expressions;

namespace QuakeReport.ApiService.Pagination;

public static class OrderedQueryExtensions
{
    public static IOrderedQueryable<TResult> SelectOrdered<TSource, TResult>(
        this IOrderedQueryable<TSource> query,
        Expression<Func<TSource, TResult>> selector) =>
        (IOrderedQueryable<TResult>)query.Select(selector);
}
