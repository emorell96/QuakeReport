using Microsoft.AspNetCore.Http.Extensions;

namespace QuakeReport.Web.Infrastructure;

public sealed class CanonicalDomainRedirectMiddleware(RequestDelegate next)
{
    public const string CanonicalHost = "terremoto.com.co";
    public const string RedirectHost = "www.terremoto.com.co";

    public async Task InvokeAsync(HttpContext context)
    {
        if (string.Equals(context.Request.Host.Host, RedirectHost, StringComparison.OrdinalIgnoreCase))
        {
            var destination = UriHelper.BuildAbsolute(
                "https",
                new HostString(CanonicalHost),
                context.Request.PathBase,
                context.Request.Path,
                context.Request.QueryString);

            context.Response.Redirect(destination, permanent: true, preserveMethod: true);
            return;
        }

        await next(context);
    }
}
