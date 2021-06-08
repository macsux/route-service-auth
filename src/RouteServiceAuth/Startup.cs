using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;

        private readonly IConfiguration _configuration;
        private readonly KerberosProxyOptions _proxySettings;

        public Startup(ILogger<Startup> logger, IConfiguration configuration, IOptions<KerberosProxyOptions> options)
        {
            _logger = logger;
            _configuration = configuration;
            _proxySettings = options.Value;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // services.AddOptions<>()
            services.AddAuthentication().AddJwtBearer().AddSpnegoProxy(_proxySettings, _configuration);
            services.AddAuthorization(cfg =>
            {
                cfg.AddPolicy(AuthorizationPolicies.RequireAuthenticatedUser, policy => policy.RequireRole().RequireAuthenticatedUser());
            });

            services.Configure<KerberosProxyOptions>(_configuration);
            services.Configure<KerberosOptions>(_configuration.GetSection("Kerberos"));
            services.Configure<LdapOptions>(_configuration.GetSection("Ldap"));
            services.AddClaimTransformer<LdapRolesClaimsTransformer>();
            services.AddKerberosClient();
            services.AddProxy();
            
        }
        
        public void Configure(IApplicationBuilder app)
        {
            var ctx = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            
            app.UseKerberosIngressProxy();
            app.UseKerberosEgressProxy();
        }
    }
}