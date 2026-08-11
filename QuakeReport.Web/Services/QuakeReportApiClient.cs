using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Services;

/// <summary>Typed client for the QuakeReport API (apiservice).</summary>
public class QuakeReportApiClient(HttpClient httpClient, IConfiguration? configuration = null)
{
    private string ModerationKey => configuration?["Moderation:ApiKey"] ?? string.Empty;

    public async Task<PagedResponse<BloodDonationCenterSummaryResponse>> GetBloodDonationCentersAsync(string? query=null, BloodDonationCenterType? centerType=null, BloodDonationOperationalStatus? operationalStatus=null, BloodDonationModerationStatus? moderationStatus=null, BloodTypeFlags? bloodTypes=null, BloodComponentFlags? components=null, BloodDonationSortOption sort=BloodDonationSortOption.Newest, int page=1, int pageSize=20, CancellationToken cancellationToken=default) { var p=new Dictionary<string,string?>{["page"]=page.ToString(),["pageSize"]=pageSize.ToString(),["sort"]=sort.ToString()}; if(!string.IsNullOrWhiteSpace(query))p["query"]=query;if(centerType is not null)p["centerType"]=centerType.ToString();if(operationalStatus is not null)p["operationalStatus"]=operationalStatus.ToString();if(moderationStatus is not null)p["moderationStatus"]=moderationStatus.ToString();if(bloodTypes is not null)p["bloodTypes"]=bloodTypes.ToString();if(components is not null)p["components"]=components.ToString();var uri=QueryHelpers.AddQueryString("/api/blood-donation-centers",p);return await httpClient.GetFromJsonAsync<PagedResponse<BloodDonationCenterSummaryResponse>>(uri,cancellationToken)??new([],page,pageSize,0,0); }
    public async Task<BloodDonationCenterResponse?> GetBloodDonationCenterAsync(Guid id,CancellationToken cancellationToken=default){var r=await httpClient.GetAsync($"/api/blood-donation-centers/{id}",cancellationToken);if(r.StatusCode==System.Net.HttpStatusCode.NotFound)return null;r.EnsureSuccessStatusCode();return await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken);}
    public async Task<CreateBloodDonationCenterResponse> CreateBloodDonationCenterAsync(CreateBloodDonationCenterRequest request,CancellationToken cancellationToken=default){var r=await httpClient.PostAsJsonAsync("/api/blood-donation-centers",request,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<CreateBloodDonationCenterResponse>(cancellationToken))!;}
    public async Task<BloodDonationCenterResponse?> LookupBloodDonationCenterByManagementCodeAsync(string code,CancellationToken cancellationToken=default){var r=await httpClient.PostAsJsonAsync("/api/blood-donation-centers/management/lookup",new BloodDonationCenterManagementCodeRequest(code),cancellationToken);if(r.StatusCode==System.Net.HttpStatusCode.NotFound)return null;r.EnsureSuccessStatusCode();return await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken);}
    public async Task<BloodDonationCenterResponse> UpdateBloodDonationCenterAsync(Guid id,string code,UpdateBloodDonationCenterRequest request,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Put,$"/api/blood-donation-centers/{id}"){Content=JsonContent.Create(request)};m.Headers.Add("X-Management-Code",code);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;}
    public async Task<BloodDonationCenterResponse> UpdateBloodDonationCenterStatusAsync(Guid id,string code,BloodDonationOperationalStatus status,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Patch,$"/api/blood-donation-centers/{id}/status"){Content=JsonContent.Create(new UpdateBloodDonationCenterStatusRequest(status))};m.Headers.Add("X-Management-Code",code);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;}
    public async Task<BloodDonationCenterCommentResponse> CreateBloodDonationCenterCommentAsync(Guid id,CreateBloodDonationCenterCommentRequest request,CancellationToken cancellationToken=default){var r=await httpClient.PostAsJsonAsync($"/api/blood-donation-centers/{id}/comments",request,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterCommentResponse>(cancellationToken))!;}
    public async Task<PagedResponse<BloodDonationCenterSummaryResponse>> GetPendingBloodDonationCentersAsync(int page=1,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Get,$"/api/blood-donation-centers/moderation/pending?page={page}&pageSize=20");m.Headers.Add("X-Moderation-Service-Key",ModerationKey);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<PagedResponse<BloodDonationCenterSummaryResponse>>(cancellationToken))!;}
    public async Task<BloodDonationCenterResponse> ModerateBloodDonationCenterAsync(Guid id,BloodDonationModerationStatus status,string? email=null,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Patch,$"/api/blood-donation-centers/moderation/{id}"){Content=JsonContent.Create(new UpdateBloodDonationCenterModerationRequest(status))};m.Headers.Add("X-Moderation-Service-Key",ModerationKey);if(!string.IsNullOrWhiteSpace(email))m.Headers.Add("X-Moderator-Email",email);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;}
    public async Task<BloodDonationCenterResponse> ModeratorUpdateBloodDonationCenterAsync(Guid id,UpdateBloodDonationCenterRequest request,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Put,$"/api/blood-donation-centers/moderation/{id}"){Content=JsonContent.Create(request)};m.Headers.Add("X-Moderation-Service-Key",ModerationKey);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;}
    public async Task<BloodDonationCenterResponse> CreateOfficialBloodDonationCenterAsync(CreateBloodDonationCenterRequest request,string? email=null,CancellationToken cancellationToken=default){using var m=new HttpRequestMessage(HttpMethod.Post,"/api/blood-donation-centers/moderation/official"){Content=JsonContent.Create(request)};m.Headers.Add("X-Moderation-Service-Key",ModerationKey);if(!string.IsNullOrWhiteSpace(email))m.Headers.Add("X-Moderator-Email",email);var r=await httpClient.SendAsync(m,cancellationToken);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<BloodDonationCenterResponse>(cancellationToken))!;}

    public async Task<PagedResponse<HelpRequestSummaryResponse>> GetHelpRequestsAsync(string? query = null, HelpRequestPriority? priority = null, HelpNeedCategory? category = null, HelpRequestStatus? status = null, HelpRequestModerationStatus? moderationStatus = null, HelpRequestSortOption sort = HelpRequestSortOption.HighestPriority, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?> { ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString(), ["sort"] = sort.ToString() };
        if (!string.IsNullOrWhiteSpace(query)) parameters["query"] = query;
        if (priority is not null) parameters["priority"] = priority.ToString();
        if (category is not null) parameters["category"] = category.ToString();
        if (status is not null) parameters["status"] = status.ToString();
        if (moderationStatus is not null) parameters["moderationStatus"] = moderationStatus.ToString();
        var uri = QueryHelpers.AddQueryString("/api/help-requests", parameters);
        return await httpClient.GetFromJsonAsync<PagedResponse<HelpRequestSummaryResponse>>(uri, cancellationToken) ?? new([], page, pageSize, 0, 0);
    }

    public async Task<HelpRequestResponse?> GetHelpRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/help-requests/{id}", cancellationToken); if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken);
    }

    public async Task<CreateHelpRequestResponse> CreateHelpRequestAsync(CreateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/help-requests", request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<CreateHelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse?> LookupHelpRequestByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/help-requests/management/lookup", new HelpRequestManagementCodeRequest(code), cancellationToken); if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken);
    }

    public async Task<HelpRequestResponse> UpdateHelpRequestAsync(Guid id, string code, UpdateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/help-requests/{id}") { Content = JsonContent.Create(request) }; message.Headers.Add("X-Management-Code", code); var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> UpdateHelpRequestStatusAsync(Guid id, string code, HelpRequestStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/{id}/status") { Content = JsonContent.Create(new UpdateHelpRequestStatusRequest(status)) }; message.Headers.Add("X-Management-Code", code); var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestCommentResponse> CreateHelpRequestCommentAsync(Guid id, CreateHelpRequestCommentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/help-requests/{id}/comments", request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestCommentResponse>(cancellationToken))!;
    }

    public async Task HideHelpRequestCommentAsync(Guid id, Guid commentId, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/{id}/comments/{commentId}/visibility"); request.Headers.Add("X-Management-Code", code); var response = await httpClient.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResponse<HelpRequestSummaryResponse>> GetPendingHelpRequestsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/help-requests/moderation/pending?page={page}&pageSize=20"); request.Headers.Add("X-Moderation-Service-Key", ModerationKey); var response = await httpClient.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<PagedResponse<HelpRequestSummaryResponse>>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModerateHelpRequestAsync(Guid id, HelpRequestModerationStatus status, string? email = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/moderation/{id}") { Content = JsonContent.Create(new UpdateHelpRequestModerationRequest(status)) }; request.Headers.Add("X-Moderation-Service-Key", ModerationKey); if (!string.IsNullOrWhiteSpace(email)) request.Headers.Add("X-Moderator-Email", email); var response = await httpClient.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModeratorUpdateHelpRequestAsync(Guid id, UpdateHelpRequestRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/help-requests/moderation/{id}") { Content = JsonContent.Create(request) }; message.Headers.Add("X-Moderation-Service-Key", ModerationKey); var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> ModeratorUpdateHelpRequestStatusAsync(Guid id, HelpRequestStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/help-requests/moderation/{id}/status") { Content = JsonContent.Create(new UpdateHelpRequestStatusRequest(status)) }; message.Headers.Add("X-Moderation-Service-Key", ModerationKey); var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<HelpRequestResponse> CreateOfficialHelpRequestAsync(CreateHelpRequestRequest request, string? email = null, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/help-requests/moderation/official") { Content = JsonContent.Create(request) }; message.Headers.Add("X-Moderation-Service-Key", ModerationKey); if (!string.IsNullOrWhiteSpace(email)) message.Headers.Add("X-Moderator-Email", email); var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HelpRequestResponse>(cancellationToken))!;
    }

    public async Task<PagedResponse<ShelterSummaryResponse>> GetSheltersAsync(string? query = null, ShelterOperationalStatus? operationalStatus = null, ShelterModerationStatus? moderationStatus = null, ShelterSortOption sort = ShelterSortOption.Newest, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?> { ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString(), ["sort"] = sort.ToString() };
        if (!string.IsNullOrWhiteSpace(query)) parameters["query"] = query;
        if (operationalStatus is not null) parameters["operationalStatus"] = operationalStatus.ToString();
        if (moderationStatus is not null) parameters["moderationStatus"] = moderationStatus.ToString();
        var uri = QueryHelpers.AddQueryString("/api/shelters", parameters);
        return await httpClient.GetFromJsonAsync<PagedResponse<ShelterSummaryResponse>>(uri, cancellationToken) ?? new([], page, pageSize, 0, 0);
    }

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

    public async Task<PagedResponse<ShelterSummaryResponse>> GetPendingSheltersAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/shelters/moderation/pending?page={page}&pageSize=20");
        request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResponse<ShelterSummaryResponse>>(cancellationToken))!;
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

    public async Task<PagedResponse<CollectionPointSummaryResponse>> GetCollectionPointsAsync(string? query = null, CollectionPointOperationalStatus? operationalStatus = null, CollectionPointModerationStatus? moderationStatus = null, CollectionPointSortOption sort = CollectionPointSortOption.Newest, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?> { ["page"] = page.ToString(), ["pageSize"] = pageSize.ToString(), ["sort"] = sort.ToString() };
        if (!string.IsNullOrWhiteSpace(query)) parameters["query"] = query;
        if (operationalStatus is not null) parameters["operationalStatus"] = operationalStatus.ToString();
        if (moderationStatus is not null) parameters["moderationStatus"] = moderationStatus.ToString();
        var uri = QueryHelpers.AddQueryString("/api/collection-points", parameters);
        return await httpClient.GetFromJsonAsync<PagedResponse<CollectionPointSummaryResponse>>(uri, cancellationToken) ?? new([], page, pageSize, 0, 0);
    }

    public async Task<CollectionPointResponse?> GetCollectionPointAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/collection-points/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken);
    }

    public async Task<CreateCollectionPointResponse> CreateCollectionPointAsync(CreateCollectionPointRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/collection-points", request, cancellationToken); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateCollectionPointResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointCommentResponse> CreateCollectionPointCommentAsync(Guid id, CreateCollectionPointCommentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/api/collection-points/{id}/comments", request, cancellationToken); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionPointCommentResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse?> LookupCollectionPointByManagementCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/collection-points/management/lookup", new CollectionPointManagementCodeRequest(code), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken);
    }

    public async Task<CollectionPointResponse> UpdateCollectionPointAsync(Guid id, string code, UpdateCollectionPointRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"/api/collection-points/{id}") { Content = JsonContent.Create(request) }; message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse> UpdateCollectionPointStatusAsync(Guid id, string code, CollectionPointOperationalStatus status, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/{id}/status") { Content = JsonContent.Create(new UpdateCollectionPointStatusRequest(status)) }; message.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
    }

    public async Task HideCollectionPointCommentAsync(Guid id, Guid commentId, string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/{id}/comments/{commentId}/visibility");
        request.Headers.Add("X-Management-Code", code);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResponse<CollectionPointSummaryResponse>> GetPendingCollectionPointsAsync(int page = 1, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/collection-points/moderation/pending?page={page}&pageSize=20"); request.Headers.Add("X-Moderation-Service-Key", ModerationKey);
        var response = await httpClient.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<PagedResponse<CollectionPointSummaryResponse>>(cancellationToken))!;
    }

    public async Task<CollectionPointResponse> ModerateCollectionPointAsync(Guid id, CollectionPointModerationStatus status, string? email = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/collection-points/moderation/{id}") { Content = JsonContent.Create(new UpdateCollectionPointModerationRequest(status)) }; request.Headers.Add("X-Moderation-Service-Key", ModerationKey); if (!string.IsNullOrWhiteSpace(email)) request.Headers.Add("X-Moderator-Email", email);
        var response = await httpClient.SendAsync(request, cancellationToken); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<CollectionPointResponse>(cancellationToken))!;
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
