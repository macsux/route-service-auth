using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RouteServiceAuth
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddSidecarOptions(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<KerberosProxyOptions>(config.GetSection("Proxy"))
                .PostConfigure<KerberosProxyOptions>(x =>
                {
                    x.Egress = config.GetSection(nameof(x.Egress)).Get<List<ProxyEntry>>()?.ToDictionary(x => x.ListenPort, x => x) ?? new Dictionary<int, ProxyEntry>();
                    x.Ingress = config.GetSection(nameof(x.Ingress)).Get<List<ProxyEntry>>()?.ToDictionary(x => x.ListenPort, x => x) ?? new Dictionary<int, ProxyEntry>();
                });
            return services;
        }
    }
}