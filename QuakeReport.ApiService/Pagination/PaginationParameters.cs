namespace QuakeReport.ApiService.Pagination;

public static class PaginationParameters
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValid(int page, int pageSize) =>
        page >= 1 && pageSize is >= 1 and <= MaxPageSize;
}
