using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QuakeReport.ApiService.Media;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;
using System.Reflection;

namespace QuakeReport.Tests;

internal static class TestDb
{
    public static QuakeReportDbContext Create()
    {
        var options = new DbContextOptionsBuilder<QuakeReportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new QuakeReportDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

internal static class TestRepository
{
    public static IQueryableRepositoryService<TEntity, Guid> Create<TEntity>(QuakeReportDbContext db)
        where TEntity : class, IEntity<Guid>
    {
        var repository = DispatchProxy.Create<IQueryableRepositoryService<TEntity, Guid>, QueryRepositoryProxy<TEntity>>();
        ((QueryRepositoryProxy<TEntity>)(object)repository).Db = db;
        return repository;
    }

    private class QueryRepositoryProxy<TEntity> : DispatchProxy
        where TEntity : class, IEntity<Guid>
    {
        public required QuakeReportDbContext Db { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                "QueryAll" => Db.Set<TEntity>().AsQueryable(),
                "SaveChangesAsync" => Db.SaveChangesAsync(args is [CancellationToken token] ? token : default),
                "Dispose" => null,
                _ => throw new NotSupportedException($"{targetMethod?.Name} is not used by these controller tests."),
            };
    }
}

internal static class TestAssert
{
    public static T InstanceOf<T>(object? value) where T : class
    {
        Assert.IsInstanceOfType(value, typeof(T));
        return (T)value!;
    }
}

internal sealed class RecordingMediaStorage : IMediaStorage
{
    public int CallCount { get; private set; }
    public Guid ReportId { get; private set; }
    public Guid MediaId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public byte[] UploadedContent { get; private set; } = [];
    public string Url { get; set; } = "https://storage.test/report-media/file";
    public Exception? ExceptionToThrow { get; set; }

    public async Task<string> UploadAsync(
        Guid reportId,
        Guid mediaId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        CallCount++;
        ReportId = reportId;
        MediaId = mediaId;
        FileName = fileName;
        ContentType = contentType;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        UploadedContent = copy.ToArray();
        return Url;
    }
}

internal sealed class TestFormFile : IFormFile
{
    private readonly byte[] content;

    public TestFormFile(long length, string fileName, string contentType, byte[]? content = null)
    {
        Length = length;
        FileName = fileName;
        ContentType = contentType;
        this.content = content ?? [];
    }

    public string ContentType { get; }
    public string ContentDisposition => string.Empty;
    public IHeaderDictionary Headers { get; } = new HeaderDictionary();
    public long Length { get; }
    public string Name => "File";
    public string FileName { get; }

    public Stream OpenReadStream() => new MemoryStream(content, writable: false);

    public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
        OpenReadStream().CopyToAsync(target, cancellationToken);
}

internal sealed class TestHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(response);
}
