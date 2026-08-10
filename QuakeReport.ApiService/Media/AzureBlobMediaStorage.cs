using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace QuakeReport.ApiService.Media;

public class AzureBlobMediaStorage(BlobServiceClient blobServiceClient) : IMediaStorage
{
    private const string ContainerName = "report-media";

    public async Task<string> UploadAsync(
        Guid reportId,
        Guid mediaId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var extension = Path.GetExtension(fileName);
        var blobName = $"{reportId}/{mediaId}{extension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);

        return blobClient.Uri.ToString();
    }
}
