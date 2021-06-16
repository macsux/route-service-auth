using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using FluentValidation;
using IdentityServer4;
using IdentityServer4.Configuration;
using IdentityServer4.Endpoints;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProxyKit;
using RouteServiceAuth.Authentication.Spnego;
using RouteServiceAuth.Kerberos;
using RouteServiceAuth.LdapGroups;
using RouteServiceAuth.Proxy;
using RouteServiceAuth.Proxy.Configuration;
using RouteServiceAuth.Proxy.Configuration.Validation;
using Steeltoe.Common;
using ProxyOptions = RouteServiceAuth.Proxy.Configuration.ProxyOptions;

namespace RouteServiceAuth
{
    public class Startup
    {

        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;

        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var identityServerBuilder = services.AddJwtIssuing();
            
            if (_environment.IsDevelopment())
            {
                identityServerBuilder.AddDeveloperSigningCredential();
            }
            else
            {
                services.AddSingleton<ISigningCredentialStore, CertificateStore>();
                services.AddSingleton<IValidationKeysStore, CertificateStore>();
            }
            
            services.AddProxy();
            services.AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = SpnegoAuthenticationDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = SpnegoAuthenticationDefaults.AuthenticationScheme;
                })
                .AddSpnego();
            
            services.AddAuthorization(cfg => cfg.AddPolicy(AuthorizationPolicies.RequireAuthenticatedUser, policy => policy.RequireAuthenticatedUser()));
            services.AddClaimTransformer<LdapRolesClaimsTransformer>();
            services.AddSingleton<KerberosClientProvider>();

            services.AddOptions<ProxyOptions>().BindConfiguration("Proxy").Validate();
            services.AddOptions<KerberosOptions>().BindConfiguration("Kerberos").Validate();
            services.AddOptions<LdapOptions>().BindConfiguration("Ldap").Validate();
            
            services.AddSingleton<ValidatorFactory>();
            services.AddTransient(typeof(IValidator<>), typeof(ValidatorFactory.ServiceProviderWrappedValidator<>));
        }
        
        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions() { ForwardedHeaders = ForwardedHeaders.All});
            app.UseIdentityServer();
            app.UseSpnegoIngressProxy();
            app.UseSpnegoEgressProxy();
            
        }
    }

    
}