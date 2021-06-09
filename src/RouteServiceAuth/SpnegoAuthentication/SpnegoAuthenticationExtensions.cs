using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.ConfigurationExtensions;

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
            builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(authenticationScheme, configureOptions);
            return builder;
        }

        public static AuthenticationBuilder AddSpnegoProxy(
            this AuthenticationBuilder builder)
        {
            var credentialsSectionName = "Proxy:Credentials";
            var services = builder.Services;
            services.AddSingleton<LdapRolesClaimsTransformer>();
            services.AddOptions();
            services.AddTransient<SpnegoAuthenticationHandler>();

            services.AddOptions<Credential>().BindNameConfiguration(credentialsSectionName);
            // new OptionsBuilder<Credential>(services, null).BindNameConfiguration(credentialsSectionName);
            services.AddTransient(svc => svc
                .GetRequiredService<IConfiguration>()
                .GetSection(credentialsSectionName)
                .Get<Dictionary<string, Credential>>());

            services.AddOptions<AuthenticationOptions>().Configure<IConfiguration>((options, configuration) =>
            {
                var credentialNames = configuration.GetSection(credentialsSectionName)
                    .GetChildren()
                    .Select(x => x.Key)
                    .ToList();
                foreach (var authenticationScheme in credentialNames)
                {
                    options.AddScheme(authenticationScheme, scheme =>
                    {
                        scheme.HandlerType = typeof(SpnegoAuthenticationHandler);
                        scheme.DisplayName = $"Spnego-{authenticationScheme}";
                    });
                }
            });
            
            services.AddSingleton<IConfigureOptions<SpnegoAuthenticationOptions>>(sp =>
            {
                var credentials = sp.GetRequiredService<IOptionsMonitor<Credential>>();
                return new LinkedNamedConfigurationOptions<SpnegoAuthenticationOptions,Credential>(credentials, (credential, spnegoOptions) =>
                {
                    spnegoOptions.PrincipalName = credential.UserAccount;
                    spnegoOptions.PrincipalPassword = credential.Password;
                });
            });
            services.AddSingleton<IValidateOptions<SpnegoAuthenticationOptions>>(new ValidateOptions<SpnegoAuthenticationOptions>(null, opt =>
            {
                opt.Validate();
                return true;
            }, null));
            
 
            //
            // // register authentication scheme for each credential listed in config
            // foreach (var (credsId, creds) in options.Credentials)
            // {
            //     var schemeName = credsId;
            //     builder.Services.Configure<SpnegoAuthenticationOptions>(schemeName, configuration);
            //     Action<SpnegoAuthenticationOptions> schemeOptionsConfigurer = opt =>
            //     {
            //         opt.PrincipalName = creds.UserAccount;
            //         opt.PrincipalPassword = creds.Password;
            //     };
            //     builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(schemeName, schemeOptionsConfigurer);
            // }
            return builder;
        }

    }
}