using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace QuakeReport.ApiService.Media;

public sealed class MissingPersonPhotoStorage(BlobServiceClient client) : IMissingPersonPhotoStorage
{
    public async Task<string> UploadAsync(Guid personId, string extension, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var container = client.GetBlobContainerClient("missing-person-media");
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        var blob = container.GetBlobClient($"{personId}/portrait{extension}");
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
        return blob.Uri.ToString();
    }
}

public interface IMissingPersonPhotoStorage
{
    Task<string> UploadAsync(Guid personId, string extension, Stream content, string contentType, CancellationToken cancellationToken);
}
