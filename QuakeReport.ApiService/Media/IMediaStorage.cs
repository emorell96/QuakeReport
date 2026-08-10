namespace QuakeReport.ApiService.Media;

/// <summary>
/// Uploads report media to blob storage. Abstracted so the API/controller
/// layer doesn't depend on the Azure SDK directly.
/// </summary>
public interface IMediaStorage
{
    /// <summary>Uploads a file and returns its public URL.</summary>
    Task<string> UploadAsync(Guid reportId, Guid mediaId, string fileName, string contentType, Stream content, CancellationToken cancellationToken);
}
