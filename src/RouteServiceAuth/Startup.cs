using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Security.Claims;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Kerberos.NET;
using Steeltoe.Common;

namespace RouteServiceAuth
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;

        private readonly IConfiguration _configuration;

        public Startup(ILogger<Startup> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication()
                .AddSpnegoProxy();
            services.AddHttpContextAccessor();
            services.AddAuthorization(cfg =>
            {
                cfg.AddPolicy(AuthorizationPolicies.RequireAuthenticatedUser, policy => policy.RequireAuthenticatedUser());
            });

            services.ConfigureProxyOptions(_configuration);
            services.Configure<KerberosOptions>(_configuration.GetSection("Kerberos"));
            services.Configure<LdapOptions>(_configuration.GetSection("Ldap"));
            services.AddClaimTransformer<LdapRolesClaimsTransformer>();
            services.AddSingleton<KerberosClientProvider>();
            services.AddProxy();
            
        }
        
        public void Configure(IApplicationBuilder app)
        {
            app.UseKerberosIngressProxy();
            app.UseKerberosEgressProxy();

            app.MapProxyPorts();
        }
    }
}