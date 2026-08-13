using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;
using StorageGenerics.Core.Models;

namespace QuakeReport.Web.Services;

/// <summary>Typed client for the QuakeReport API (apiservice).</summary>
public class QuakeReportApiClient(HttpClient httpClient, IConfiguration? configuration = null)
{
    private string ModerationKey => configuration?["Moderation:ApiKey"] ?? string.Empty;

    public async Task<MapOverviewResponse> GetMapOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("/api/map", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MapOverviewResponse>(cancellationToken))!;
    }

    public async Task<IReadOnlyList<GeocodingReviewItemResponse>> GetGeocodingReviewItemsAsync(GeocodingReviewStatus status, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/geocoding-review?status={status}");
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<GeocodingReviewItemResponse>>(cancellationToken) ?? [];
    }

    public async Task RetryGeocodingReviewAsync(Guid id, CancellationToken cancellationToken = default) =>
        await SendGeocodingReviewAsync(HttpMethod.Post, $"/api/geocoding-review/{id}/retry", null, cancellationToken);

    public async Task ResolveGeocodingReviewAsync(Guid id, ResolveGeocodingReviewRequest payload, CancellationToken cancellationToken = default) =>
        await SendGeocodingReviewAsync(HttpMethod.Put, $"/api/geocoding-review/{id}/resolve", payload, cancellationToken);

    public async Task DismissGeocodingReviewAsync(Guid id, DismissGeocodingReviewRequest payload, CancellationToken cancellationToken = default) =>
        await SendGeocodingReviewAsync(HttpMethod.Post, $"/api/geocoding-review/{id}/dismiss", payload, cancellationToken);

    private async Task SendGeocodingReviewAsync(HttpMethod method, string uri, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<BloodDonationCenterSummaryResponse>> GetAllBloodDonationCentersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<BloodDonationCenterSummaryResponse>(
            "/api/blood-donation-centers",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<BloodDonationCenterSummaryResponse>> SearchBloodDonationCentersAsync(
        PagedRequest<BloodDonationCenterSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<BloodDonationCenterSummaryResponse>(
            "/api/blood-donation-centers/search",
            request,
            cancellationToken);

    public async Task<BloodDonationCenterResponse?> GetBloodDonationCenterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await httpClient.GetAsync($"/api/blood-donation-centers/{id}", cancellationToken);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken);
    }

    public async Task<CreateBloodDonationCenterResponse> CreateBloodDonationCenterAsync(CreateBloodDonationCenterRequest request, CancellationToken cancellationToken = default)
    {
        var r = await httpClient.PostAsJsonAsync("/api/blood-donation-centers", request, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<CreateBloodDonationCenterResponse>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterResponse?> LookupBloodDonationCenterByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var r = await httpClient.PostAsJsonAsync(
            "/api/blood-donation-centers/management/lookup",
            new BloodDonationCenterManagementCodeRequest(code),
            cancellationToken);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken);
    }

    public async Task<BloodDonationCenterResponse> UpdateBloodDonationCenterAsync(
        Guid id,
        string code,
        UpdateBloodDonationCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Put, $"/api/blood-donation-centers/{id}")
        {
            Content = JsonContent.Create(request)
        };
        m.Headers.Add("X-Management-Code", code);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterResponse> UpdateBloodDonationCenterStatusAsync(
        Guid id,
        string code,
        BloodDonationOperationalStatus status,
        CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Patch, $"/api/blood-donation-centers/{id}/status")
        {
            Content = JsonContent.Create(new UpdateBloodDonationCenterStatusRequest(status))
        };
        m.Headers.Add("X-Management-Code", code);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterCommentResponse> CreateBloodDonationCenterCommentAsync(
        Guid id,
        CreateBloodDonationCenterCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var r = await httpClient.PostAsJsonAsync($"/api/blood-donation-centers/{id}/comments", request, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterCommentResponse>(cancellationToken))!;
    }

    public async Task<PagedResult<BloodDonationCenterSummaryResponse>> GetPendingBloodDonationCentersAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Get, $"/api/blood-donation-centers/moderation/pending?page={page}&pageSize=20");
        m.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<PagedResult<BloodDonationCenterSummaryResponse>>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterResponse> ModerateBloodDonationCenterAsync(
        Guid id,
        BloodDonationModerationStatus status,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Patch, $"/api/blood-donation-centers/moderation/{id}")
        {
            Content = JsonContent.Create(new UpdateBloodDonationCenterModerationRequest(status))
        };
        m.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) m.Headers.Add("X-Moderator-Email", email);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterResponse> ModeratorUpdateBloodDonationCenterAsync(
        Guid id,
        UpdateBloodDonationCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Put, $"/api/blood-donation-centers/moderation/{id}")
        {
            Content = JsonContent.Create(request)
        };
        m.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;
    }

    public async Task<BloodDonationCenterResponse> CreateOfficialBloodDonationCenterAsync(
        CreateBloodDonationCenterRequest request,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        using var m = new HttpRequestMessage(HttpMethod.Post, "/api/blood-donation-centers/moderation/official")
        {
            Content = JsonContent.Create(request)
        };
        m.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) m.Headers.Add("X-Moderator-Email", email);
        var r = await httpClient.SendAsync(m, cancellationToken);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;
    }

    public Task<PagedResult<HelpRequestSummaryResponse>> GetAllHelpRequestsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<HelpRequestSummaryResponse>(
            "/api/help-requests",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<HelpRequestSummaryResponse>> SearchHelpRequestsAsync(
        PagedRequest<HelpRequestSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<HelpRequestSummaryResponse>(
            "/api/help-requests/search",
            request,
            cancellationToken);

    public async Task<HelpRequestResponse?> GetHelpRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/help-requests/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken);
    }

    public async Task<CreateHelpRequestResponse> CreateHelpRequestAsync(CreateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/help-requests", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateHelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse?> LookupHelpRequestByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/help-requests/management/lookup", new HelpRequestManagementCodeRequest(code), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken);
    }

    public async Task<HelpRequestResponse> UpdateHelpRequestAsync(Guid id, string code, UpdateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/help-requests/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> UpdateHelpRequestStatusAsync(Guid id, string code, HelpRequestStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/{id}/status") { Content = JsonContent.Create(new UpdateHelpRequestStatusRequest(status)) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestCommentResponse> CreateHelpRequestCommentAsync(Guid id, CreateHelpRequestCommentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/help-requests/{id}/comments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestCommentResponse>(cancellationToken))!;
    }

    public async Task HideHelpRequestCommentAsync(Guid id, Guid commentId, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/{id}/comments/{commentId}/visibility");
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResult<HelpRequestSummaryResponse>> GetPendingHelpRequestsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/help-requests/moderation/pending?page={page}&pageSize=20");
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<HelpRequestSummaryResponse>>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModerateHelpRequestAsync(Guid id, HelpRequestModerationStatus status, string? email = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/moderation/{id}") { Content = JsonContent.Create(new UpdateHelpRequestModerationRequest(status)) };
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) request.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModeratorUpdateHelpRequestAsync(Guid id, UpdateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/help-requests/moderation/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModeratorUpdateHelpRequestStatusAsync(Guid id, HelpRequestStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/moderation/{id}/status") { Content = JsonContent.Create(new UpdateHelpRequestStatusRequest(status)) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> CreateOfficialHelpRequestAsync(CreateHelpRequestRequest request, string? email = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/help-requests/moderation/official") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) message.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public Task<PagedResult<ShelterSummaryResponse>> GetAllSheltersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ShelterSummaryResponse>(
            "/api/shelters",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<ShelterSummaryResponse>> SearchSheltersAsync(
        PagedRequest<ShelterSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<ShelterSummaryResponse>(
            "/api/shelters/search",
            request,
            cancellationToken);

    public async Task<ShelterResponse?> GetShelterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/shelters/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken);
    }

    public async Task<CreateShelterResponse> CreateShelterAsync(CreateShelterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/shelters", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateShelterResponse>(cancellationToken))!;
    }

    public async Task<ShelterResponse?> LookupShelterByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/shelters/management/lookup", new ShelterManagementCodeRequest(code), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken);
    }

    public async Task<ShelterResponse> UpdateShelterAsync(Guid id, string code, UpdateShelterRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/shelters/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public async Task<ShelterResponse> UpdateShelterStatusAsync(Guid id, string code, ShelterOperationalStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/shelters/{id}/status") { Content = JsonContent.Create(new UpdateShelterStatusRequest(status)) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public async Task<PagedResult<ShelterSummaryResponse>> GetPendingSheltersAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/shelters/moderation/pending?page={page}&pageSize=20");
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<ShelterSummaryResponse>>(cancellationToken))!;
    }

    public async Task<ShelterResponse> ModerateShelterAsync(Guid id, ShelterModerationStatus status, string? email = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/shelters/moderation/{id}") { Content = JsonContent.Create(new UpdateShelterModerationRequest(status)) };
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) request.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public async Task<ShelterResponse> ModeratorUpdateShelterAsync(Guid id, UpdateShelterRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/shelters/moderation/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public async Task<ShelterResponse> ModeratorUpdateShelterStatusAsync(Guid id, ShelterOperationalStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/shelters/moderation/{id}/status") { Content = JsonContent.Create(new UpdateShelterStatusRequest(status)) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public async Task<ShelterResponse> CreateOfficialShelterAsync(CreateShelterRequest request, string? email = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/shelters/moderation/official") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) message.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(cancellationToken))!;
    }

    public Task<PagedResult<CollectionPointSummaryResponse>> GetAllCollectionPointsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<CollectionPointSummaryResponse>(
            "/api/collection-points",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<CollectionPointSummaryResponse>> SearchCollectionPointsAsync(
        PagedRequest<CollectionPointSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<CollectionPointSummaryResponse>(
            "/api/collection-points/search",
            request,
            cancellationToken);

    public async Task<CollectionPointResponse?> GetCollectionPointAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/collection-points/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken);
    }

    public async Task<CreateCollectionPointResponse> CreateCollectionPointAsync(CreateCollectionPointRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/collection-points", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateCollectionPointResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointCommentResponse> CreateCollectionPointCommentAsync(Guid id, CreateCollectionPointCommentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/collection-points/{id}/comments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointCommentResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse?> LookupCollectionPointByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/collection-points/management/lookup", new CollectionPointManagementCodeRequest(code), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken);
    }

    public async Task<CollectionPointResponse> UpdateCollectionPointAsync(Guid id, string code, UpdateCollectionPointRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/collection-points/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse> UpdateCollectionPointStatusAsync(Guid id, string code, CollectionPointOperationalStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/{id}/status") { Content = JsonContent.Create(new UpdateCollectionPointStatusRequest(status)) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }

    public async Task HideCollectionPointCommentAsync(Guid id, Guid commentId, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/{id}/comments/{commentId}/visibility");
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResult<CollectionPointSummaryResponse>> GetPendingCollectionPointsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/collection-points/moderation/pending?page={page}&pageSize=20");
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<CollectionPointSummaryResponse>>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse> ModerateCollectionPointAsync(Guid id, CollectionPointModerationStatus status, string? email = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/moderation/{id}") { Content = JsonContent.Create(new UpdateCollectionPointModerationRequest(status)) };
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) request.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse> CreateOfficialCollectionPointAsync(CreateCollectionPointRequest request, string? email = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/collection-points/moderation/official") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        if (!string.IsNullOrWhiteSpace(email)) message.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }
    public Task<PagedResult<MissingPersonSummaryResponse>> GetAllMissingPeopleAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<MissingPersonSummaryResponse>(
            "/api/missing-people",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<MissingPersonSummaryResponse>> SearchMissingPeopleAsync(
        PagedRequest<MissingPersonSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<MissingPersonSummaryResponse>(
            "/api/missing-people/search",
            request,
            cancellationToken);

    public async Task<MissingPersonResponse?> GetMissingPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/missing-people/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MissingPersonResponse>(cancellationToken);
    }

    public async Task<MissingPersonResponse?> LookupMissingPersonByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/missing-people/management/lookup", new ManagementCodeRequest(code), cancellationToken);
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

    public async Task<PagedResult<MissingPersonTipResponse>> GetMissingPersonTipsAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var uri = QueryHelpers.AddQueryString($"/api/missing-people/{id}/tips", new Dictionary<string, string?>
        {
            [nameof(page)] = page.ToString(), [nameof(pageSize)] = pageSize.ToString()
        });
        return await httpClient.GetFromJsonAsync<PagedResult<MissingPersonTipResponse>>(uri, cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty pagination response.");
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

    public async Task<MissingPersonResponse> UpdateMissingPersonAsync(Guid id, string code, UpdateMissingPersonRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/missing-people/{id}") { Content = JsonContent.Create(request) };
        message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken);
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

    public Task<PagedResult<DamageReportSummaryResponse>> GetAllReportsAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<DamageReportSummaryResponse>(
            "/api/reports",
            page,
            pageSize,
            cancellationToken);

    public Task<PagedResult<DamageReportSummaryResponse>> SearchReportsAsync(
        PagedRequest<DamageReportSearchFilter> request,
        CancellationToken cancellationToken = default) =>
        SearchPageAsync<DamageReportSummaryResponse>(
            "/api/reports/search",
            request,
            cancellationToken);

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

    private async Task<PagedResult<TResponse>> GetPageAsync<TResponse>(
        string resourceUri,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            [nameof(page)] = page.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(pageSize)] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var uri = QueryHelpers.AddQueryString(resourceUri, query);
        return await httpClient.GetFromJsonAsync<PagedResult<TResponse>>(uri, cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty pagination response.");
    }

    private async Task<PagedResult<TResponse>> SearchPageAsync<TResponse>(
        string resourceUri,
        object request,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            resourceUri,
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedResult<TResponse>>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty pagination response.");
    }

    private sealed record PhotoResponse(string PhotoUrl);
}
