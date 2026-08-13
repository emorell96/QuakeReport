using System.Net.Http.Json;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;
using QuakeReport.Web.Services;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class QuakeReportApiClientTests
{
    [TestMethod]
    public async Task SearchReportsPostsPagingFilterAndSortAndDeserializesResponse()
    {
        var report = new DamageReportSummaryResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Report",
            SeverityLevel.Major,
            DamageSign.Cracks,
            null,
            null,
            4.5,
            -74.3,
            "Main Street",
            DateTimeOffset.UtcNow);
        var expected = new { Results = new[] { report }, PageNumber = 2, PageSize = 10, TotalMatches = 21, TotalPages = 3 };
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected),
        });
        var client = new QuakeReportApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test"),
        });

        var earthquakeId = Guid.NewGuid();
        var request = new PagedRequest<DamageReportSearchFilter>
        {
            PageNumber = 2,
            PageSize = 10,
            Filter = new DamageReportSearchFilter
            {
                EarthquakeId = earthquakeId,
                Severity = SeverityLevel.Major,
                Sort = ReportSortOption.HighestSeverity,
            },
        };
        var result = await client.SearchReportsAsync(request);

        Assert.AreEqual(HttpMethod.Post, handler.RequestMethod);
        Assert.AreEqual("/api/reports/search", handler.RequestUri?.PathAndQuery);
        StringAssert.Contains(handler.RequestBody, "\"pageNumber\":2");
        StringAssert.Contains(handler.RequestBody, "\"pageSize\":10");
        StringAssert.Contains(handler.RequestBody, earthquakeId.ToString());
        StringAssert.Contains(handler.RequestBody, "\"severity\":3");
        StringAssert.Contains(handler.RequestBody, "\"sort\":2");
        Assert.AreEqual(2, result.PageNumber);
        Assert.AreEqual(10, result.PageSize);
        Assert.AreEqual(21, result.TotalMatches);
        Assert.AreEqual(3, result.TotalPages);
        Assert.AreEqual(report.Id, result.Results.Single().Id);
    }

    [TestMethod]
    public async Task GetAllReportsSendsOnlyPaginationQueryParameters()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Results = Array.Empty<DamageReportSummaryResponse>(), PageNumber = 1, PageSize = 20, TotalMatches = 0, TotalPages = 0 }),
        });
        var client = new QuakeReportApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test"),
        });

        await client.GetAllReportsAsync();

        Assert.AreEqual(HttpMethod.Get, handler.RequestMethod);
        Assert.AreEqual("/api/reports?page=1&pageSize=20", handler.RequestUri?.PathAndQuery);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public HttpMethod? RequestMethod { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestMethod = request.Method;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }
}
