using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RouteServiceAuth
{
    public static class SpnegoAuthenticationExtensions
    {
        private const string scheme = SpnegoAuthenticationDefaults.AuthenticationScheme;

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder)
        {
            return builder.AddSpnego(scheme);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme)
        {
            return builder.AddSpnego(authenticationScheme, null);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            Action<SpnegoAuthenticationOptions> configureOptions)
        {
            return builder.AddSpnego(scheme, configureOptions);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme,
            Action<SpnegoAuthenticationOptions> configureOptions)
        {
            
            builder.Services.AddSingleton<LdapRolesClaimsTransformer>();
            builder.Services.AddSingleton<SpnegoAuthenticator>();
            builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(authenticationScheme, configureOptions);
            return builder;
        }

        public static AuthenticationBuilder AddSpnegoProxy(
            this AuthenticationBuilder builder, 
            KerberosProxyOptions options,
            IConfiguration configuration)
        {
            
            builder.Services.AddSingleton<LdapRolesClaimsTransformer>();
            builder.Services.AddSingleton<SpnegoAuthenticator>();
            foreach (var option in options.Ingress.Values)
            {
                var schemeName = $"{SpnegoProxyAuthenticationDefaults.AuthenticationScheme}-{option.ListenPort}";
                builder.Services.Configure<SpnegoAuthenticationOptions>(schemeName, configuration);
                Action<SpnegoAuthenticationOptions> schemeOptionsConfigurer = opt =>
                {
                    opt.PrincipalName = option.UserAccount;
                    opt.PrincipalPassword = option.Password;
                };
                builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(schemeName, schemeOptionsConfigurer);
            }
            return builder;
        }

    }
}