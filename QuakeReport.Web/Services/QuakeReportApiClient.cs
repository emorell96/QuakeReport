using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Services;

/// <summary>Typed client for the QuakeReport API (apiservice).</summary>
public class QuakeReportApiClient(HttpClient httpClient)
{
    public async Task<PagedResponse<MissingPersonSummaryResponse>> GetMissingPeopleAsync(string? query = null, MissingPersonStatus status = MissingPersonStatus.Missing, MissingPersonSortOption sort = MissingPersonSortOption.Newest, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?> { ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString(), ["status"] = status.ToString(), ["sort"] = sort.ToString() };
        if (!string.IsNullOrWhiteSpace(query)) parameters["query"] = query;
        var uri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/api/missing-people", parameters);
        return await httpClient.GetFromJsonAsync<PagedResponse<MissingPersonSummaryResponse>>(uri, cancellationToken) ?? new([], page, pageSize, 0, 0);
    }

    public async Task<MissingPersonResponse?> GetMissingPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/missing-people/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MissingPersonResponse>(cancellationToken);
    }

    public async Task<CreateMissingPersonResponse> CreateMissingPersonAsync(CreateMissingPersonRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/missing-people", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateMissingPersonResponse>(cancellationToken))!;
    }

    public async Task<MissingPersonResponse?> LookupMissingPersonAsync(IdentificationLookupRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/missing-people/lookup-by-identification", request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MissingPersonResponse>(cancellationToken);
    }

    public async Task<MissingPersonTipResponse> CreateMissingPersonTipAsync(Guid id, CreateMissingPersonTipRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/missing-people/{id}/tips", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MissingPersonTipResponse>(cancellationToken))!;
    }

    public async Task<PagedResponse<MissingPersonTipResponse>> GetMissingPersonTipsAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var uri = QueryHelpers.AddQueryString($"/api/missing-people/{id}/tips", new Dictionary<string, string?>
        {
            [nameof(page)] = page.ToString(), [nameof(pageSize)] = pageSize.ToString()
        });
        return await httpClient.GetFromJsonAsync<PagedResponse<MissingPersonTipResponse>>(uri, cancellationToken)
            ?? new([], page, pageSize, 0, 0);
    }

    public async Task<string> UploadMissingPersonPhotoAsync(Guid id, string managementCode, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(10 * 1024 * 1024, cancellationToken);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        form.Add(content, "file", file.Name);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/missing-people/{id}/photo") { Content = form };
        request.Headers.Add("X-Management-Code", managementCode);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PhotoResponse>(cancellationToken))!.PhotoUrl;
    }

    public async Task<MissingPersonResponse> UpdateMissingPersonStatusAsync(Guid id, string code, MissingPersonStatus status, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/missing-people/{id}/status") { Content = JsonContent.Create(new UpdateMissingPersonStatusRequest(status)) };
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MissingPersonResponse>(cancellationToken))!;
    }

    public async Task<IReadOnlyList<PrivateMissingPersonTipResponse>> GetPrivateMissingPersonTipsAsync(Guid id, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/missing-people/{id}/management/tips");
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PrivateMissingPersonTipResponse>>(cancellationToken) ?? [];
    }

    public async Task HideMissingPersonTipAsync(Guid id, Guid tipId, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/missing-people/{id}/tips/{tipId}/visibility");
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

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

    private sealed record PhotoResponse(string PhotoUrl);
}
