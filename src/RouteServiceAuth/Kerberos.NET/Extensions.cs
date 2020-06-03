using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RouteServiceAuth.Kerberos.NET
{
    public static class Extensions
    {
        public static IServiceCollection AddWhitelist(this IServiceCollection services, IConfiguration configuration, string sectionName = "Whitelist")
        {
            services.Configure<WhitelistOptions>(configuration.GetSection(sectionName));
            services.AddSingleton<IWhitelist, Whitelist>();
            return services;
        }

        public static bool TryGetForwardAddress(this IHeaderDictionary headers, out string forwardAddress)
        {
            forwardAddress = null;
            if(headers.TryGetValue(Constants.X_CF_Forwarded_Url, out var values))
            {
                forwardAddress = values.ToString();
            }
            return null != forwardAddress;
        }
    }
}