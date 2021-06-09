using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Steeltoe.Common;

namespace RouteServiceAuth
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection ConfigureProxyOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<ProxyOptions>(config.GetSection("Proxy"));
            services.AddOptions<ProxyOptions>()
                .Configure(opt =>
                {
                    if (Platform.IsCloudFoundry)
                    {
                        opt.DestinationHeaderName = KnownHeaders.X_CF_Forwarded_Url;
                    }
                })
                .PostConfigure(opt =>
                {
                    opt.IdentityHttpHeaderName ??= KnownHeaders.X_CF_Identity;
                    opt.RolesHttpHeaderName ??= KnownHeaders.X_CF_Roles;
                });
            return services;
        }
    }
}