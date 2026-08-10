using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Services;

/// <summary>Typed client for the QuakeReport API (apiservice).</summary>
public class QuakeReportApiClient(HttpClient httpClient)
{
    public async Task<EarthquakeResponse?> GetActiveEarthquakeAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("/api/earthquakes/active", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EarthquakeResponse>(cancellationToken);
    }

    public async Task<PagedResponse<DamageReportSummaryResponse>> GetReportsAsync(
        int page = 1,
        int pageSize = 20,
        SeverityLevel? severity = null,
        ReportSortOption sort = ReportSortOption.Newest,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(page)] = page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(pageSize)] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(sort)] = sort.ToString(),
        };

        if (severity.HasValue)
        {
            query[nameof(severity)] = severity.Value.ToString();
        }

        var uri = QueryHelpers.AddQueryString("/api/reports", query);
        return await httpClient.GetFromJsonAsync<PagedResponse<DamageReportSummaryResponse>>(uri, cancellationToken)
            ?? new PagedResponse<DamageReportSummaryResponse>([], page, pageSize, 0, 0);
    }

    public async Task<DamageReportResponse?> GetReportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/reports/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DamageReportResponse>(cancellationToken);
    }

    public async Task<DamageReportResponse> CreateReportAsync(CreateDamageReportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/reports", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DamageReportResponse>(cancellationToken))!;
    }

    public async Task<ReportMediaResponse> UploadMediaAsync(
        Guid reportId,
        string fileName,
        string contentType,
        MediaType mediaType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(mediaType.ToString()), "MediaType");

        var response = await httpClient.PostAsync($"/api/reports/{reportId}/media", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportMediaResponse>(cancellationToken))!;
    }
}
