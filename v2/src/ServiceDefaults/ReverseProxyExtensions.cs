using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults;

public static class ReverseProxyExtensions
{
    public static TBuilder AddTrustedReverseProxy<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var proxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in proxies)
                options.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        return builder;
    }
}
