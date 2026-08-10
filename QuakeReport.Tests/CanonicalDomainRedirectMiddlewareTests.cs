using Microsoft.AspNetCore.Http;
using QuakeReport.Web.Infrastructure;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class CanonicalDomainRedirectMiddlewareTests
{
    [TestMethod]
    public async Task WwwHostRedirectsPermanentlyAndPreservesPathAndQuery()
    {
        var nextCalled = false;
        var middleware = new CanonicalDomainRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("WWW.TERREMOTO.COM.CO");
        context.Request.Path = "/reports/42";
        context.Request.QueryString = new QueryString("?source=alert");

        await middleware.InvokeAsync(context);

        Assert.IsFalse(nextCalled);
        Assert.AreEqual(StatusCodes.Status308PermanentRedirect, context.Response.StatusCode);
        Assert.AreEqual(
            "https://terremoto.com.co/reports/42?source=alert",
            context.Response.Headers.Location.ToString());
    }

    [TestMethod]
    public async Task CanonicalHostContinuesThroughPipeline()
    {
        var nextCalled = false;
        var middleware = new CanonicalDomainRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(CanonicalDomainRedirectMiddleware.CanonicalHost);

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
        Assert.AreEqual(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.IsFalse(context.Response.Headers.ContainsKey("Location"));
    }
}
