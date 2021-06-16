using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RouteServiceAuth.LdapGroups;
using RouteServiceAuth.Proxy.Configuration;
using RouteServiceAuth.Proxy.Configuration.Util;
using Steeltoe.Extensions.Configuration.CloudFoundry;

namespace RouteServiceAuth.Authentication.Spnego
{
    public static class SpnegoAuthenticationExtensions
    {
        private const string Scheme = SpnegoAuthenticationDefaults.AuthenticationScheme;

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder)
        {
            return builder.AddSpnego(Scheme);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme)
        {

            builder.AddSpnego(authenticationScheme, null);
            builder.Services.AddCredentials();
            // trigger rebuild of SpnegoAuthenticationOptions whenever there's a change to Dictionary<string, Credential> options 
            builder.Services.AddSingleton<IOptionsChangeTokenSource<SpnegoAuthenticationOptions>>(svc =>
            {
                var linkedOptions = svc.GetRequiredService<IOptionsMonitor<Dictionary<string, Secret>>>();
                return new LinkedOptionsChangeTrackingSource<SpnegoAuthenticationOptions,Dictionary<string, Secret>>(authenticationScheme, linkedOptions);
            });

            // make spnego authentication handler try every Windows credential defined in config for decrypting incoming ticket  
            builder.Services
                .AddOptions<SpnegoAuthenticationOptions>(authenticationScheme)
                .PostConfigure<IOptionsMonitor<Dictionary<string, Secret>>>((options, credentials) =>
                {
                    foreach (var cred in credentials.CurrentValue.Values.OfType<WindowsCredential>())
                    {
                        options.Credentials.Add(cred);
                    }
                });

            return builder;
        }

        // public static AuthenticationBuilder AddSpnego(
        //     this AuthenticationBuilder builder)
        // {
        //     
        //     
        // }
        
        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            Action<SpnegoAuthenticationOptions> configureOptions)
        {
            return builder.AddSpnego(Scheme, configureOptions);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme,
            Action<SpnegoAuthenticationOptions>? configureOptions)
        {
            
            builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(authenticationScheme, configureOptions);
            return builder;
        }
        
        
        /// <summary>
        /// Creates an authentication scheme for each credential listed in configuration
        /// </summary>
        public static IServiceCollection AddCredentials(this IServiceCollection services)
        {
            // void ConfigureFromConfig(IConfiguration config, Dictionary<string, Secret> opt)
            // {
            //     foreach (var credSection in config.GetChildren())
            //     {
            //         var type = credSection.GetValue<Secret.CredentialType>("Type").GetCredentialClassType();
            //         if (type == null)
            //             continue;
            //         var cred = (Secret)credSection.Get(type);
            //         cred.Id = credSection.Key;
            //         opt[credSection.Key] = cred;
            //     }
            // }
            var credentialsSectionName = "Secrets";
            services.AddTransient<SpnegoAuthenticationHandler>();
            services.AddOptions();
            services.AddOptions<Dictionary<string, Secret>>()
                .Configure<IConfiguration>((opt, config) =>
                {
                    void Merge<T>(Dictionary<string, T>? secrets) where T : Secret
                    {
                        if (secrets == null)
                            return;
                        foreach ((var id, Secret secret) in secrets)
                        {
                            secret.Id = id;
                            opt[id] = secret;
                        }
                    }

                    void MergeAll(IConfiguration section)
                    {
                        Merge(section.GetSection("WindowsCredentials").Get<Dictionary<string, WindowsCredential>>());
                        Merge(section.GetSection("SecurityKeys").Get<Dictionary<string, SecurityKeySecret>>());
                        Merge(section.GetSection("OAuthClients").Get<Dictionary<string, OAuthClient>>());
                    }

                    MergeAll(config.GetSection(credentialsSectionName));
                    // var credsSections = config.GetSection(credentialsSectionName);
                    // ConfigureFromConfig(credsSections, opt);
                    var cf = new CloudFoundryServicesOptions((IConfigurationRoot)config);

                    string? GetCredentialSectionName(HashSet<string> tags)
                    {
                        if (tags.Contains("windows-credential"))
                            return "WindowsCredentials";
                        if (tags.Contains("security-key"))
                            return "SecurityKeys";
                        if (tags.Contains("oauth2-client"))
                            return "OAuthClients";
                        return null;
                    }
                    var credentialBindings = cf.ServicesList
                        .Where(x => x.Label == "user-provided")
                        .Select(service => new
                        {
                            Type = GetCredentialSectionName(service.Tags.ToHashSet()),
                            Service = service
                        })
                        .Where(x => x.Type != null)
                        .SelectMany(x => x.Service.Credentials.ToDictionary(c => $"{x.Type}:{x.Service.Name}:{c.Key}", c => c.Value.Value))
                        .ToList();
                    var bindingConfigs = new ConfigurationBuilder().AddInMemoryCollection(credentialBindings).Build();
                    MergeAll(bindingConfigs);
                })
                .PostConfigure(credentials =>
                {
                    var envPrincipal = Environment.GetEnvironmentVariable("PRINCIPAL_NAME");
                    var envPassword = Environment.GetEnvironmentVariable("PRINCIPAL_PASSWORD");
                    if (!credentials.Any() && envPrincipal != null && envPassword != null)
                    {
                        credentials.Add(WindowsCredential.DefaultCredentialId, new WindowsCredential() {UserAccount = envPrincipal, Password = envPassword});
                    }
                });
                
            // rebuild authentication schemes if credentials section changes
            services.AddSingleton<IOptionsChangeTokenSource<AuthenticationOptions>>(svc => 
            new ConfigurationChangeTokenSource<AuthenticationOptions>(svc.GetRequiredService<IConfiguration>().GetSection(credentialsSectionName)));
            // services.AddSingleton<IConfigureOptions<SpnegoAuthenticationOptions>, SpnegoHandlerOptionsConfigurator>();
            // services.AddSingleton<IStartupFilter, CredentialsToSpnegoAuthenticationSchemeSynchronizer>();
            // services.AddSingleton<IValidateOptions<SpnegoAuthenticationOptions>>(new ValidateOptions<SpnegoAuthenticationOptions>(null, opt =>
            // {
            //     opt.Validate();
            //     return true;
            // }, null));
            return services;
        }

    }
}