namespace QuakeReport.Core.Models.API;

public class PagedRequest<T> where T : class
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public T? Filter { get; set; }
}
