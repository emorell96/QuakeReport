using System.Net.Http.Json;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Web.Services;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class QuakeReportApiClientTests
{
    [TestMethod]
    public async Task GetReportsSendsPagingFilterAndSortAndDeserializesResponse()
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

        var result = await client.GetReportsAsync(
            page: 2,
            pageSize: 10,
            severity: SeverityLevel.Major,
            sort: ReportSortOption.HighestSeverity);

        Assert.AreEqual(
            "/api/reports?page=2&pageSize=10&sort=HighestSeverity&severity=Major",
            handler.RequestUri?.PathAndQuery);
        Assert.AreEqual(2, result.PageNumber);
        Assert.AreEqual(10, result.PageSize);
        Assert.AreEqual(21, result.TotalMatches);
        Assert.AreEqual(3, result.TotalPages);
        Assert.AreEqual(report.Id, result.Results.Single().Id);
    }

    [TestMethod]
    public async Task GetReportsOmitsSeverityWhenNoFilterIsSelected()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { Results = Array.Empty<DamageReportSummaryResponse>(), PageNumber = 1, PageSize = 20, TotalMatches = 0, TotalPages = 0 }),
        });
        var client = new QuakeReportApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test"),
        });

        await client.GetReportsAsync();

        Assert.AreEqual("/api/reports?page=1&pageSize=20&sort=Newest", handler.RequestUri?.PathAndQuery);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(request));
        }
    }
}
