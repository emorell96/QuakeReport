using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Moq;
using QuakeReport.ApiService.Media;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class AzureBlobMediaStorageTests
{
    [TestMethod]
    public async Task UploadCreatesExpectedBlobAndReturnsItsUri()
    {
        var reportId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var fileName = "evidence.photo.jpg";
        var contentType = "image/jpeg";
        var cancellationToken = new CancellationTokenSource().Token;
        var blobUri = new Uri("https://storage.test/report-media/blob");
        var expectedBlobName = $"{reportId}/{mediaId}.jpg";
        var serviceClient = new Mock<BlobServiceClient>();
        var containerClient = new Mock<BlobContainerClient>();
        var blobClient = new Mock<BlobClient>();

        serviceClient
            .Setup(client => client.GetBlobContainerClient("report-media"))
            .Returns(containerClient.Object);
        containerClient
            .Setup(client => client.GetBlobClient(expectedBlobName))
            .Returns(blobClient.Object);
        containerClient
            .Setup(client => client.CreateIfNotExistsAsync(
                PublicAccessType.Blob,
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobContainerEncryptionScopeOptions>(),
                cancellationToken))
            .ReturnsAsync((Response<BlobContainerInfo>)null!);
        blobClient
            .SetupGet(client => client.Uri)
            .Returns(blobUri);
        blobClient
            .Setup(client => client.UploadAsync(
                It.IsAny<Stream>(),
                It.Is<BlobHttpHeaders>(headers => headers.ContentType == contentType),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<AccessTier?>(),
                It.IsAny<StorageTransferOptions>(),
                cancellationToken))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var storage = new AzureBlobMediaStorage(serviceClient.Object);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await storage.UploadAsync(reportId, mediaId, fileName, contentType, content, cancellationToken);

        Assert.AreEqual(blobUri.ToString(), result);
        serviceClient.Verify(client => client.GetBlobContainerClient("report-media"), Times.Once);
        containerClient.Verify(client => client.CreateIfNotExistsAsync(
            PublicAccessType.Blob,
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<BlobContainerEncryptionScopeOptions>(),
            cancellationToken), Times.Once);
        containerClient.Verify(client => client.GetBlobClient(expectedBlobName), Times.Once);
        blobClient.Verify(client => client.UploadAsync(
            It.IsAny<Stream>(),
            It.Is<BlobHttpHeaders>(headers => headers.ContentType == contentType),
            It.IsAny<IDictionary<string, string>>(),
            It.IsAny<BlobRequestConditions>(),
            It.IsAny<IProgress<long>>(),
            It.IsAny<AccessTier?>(),
            It.IsAny<StorageTransferOptions>(),
            cancellationToken), Times.Once);
    }
}
