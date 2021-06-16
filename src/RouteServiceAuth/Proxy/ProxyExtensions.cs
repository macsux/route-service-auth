using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using IdentityServer4.Configuration;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RouteServiceAuth.Proxy.Configuration;
using RouteServiceAuth.Proxy.Configuration.Util;
using RouteServiceAuth.Proxy.Configuration.Validation;
using RouteServiceAuth.Proxy.Forward;
using RouteServiceAuth.Proxy.Reverse;
using ProxyOptions = RouteServiceAuth.Proxy.Configuration.ProxyOptions;
using Secret = RouteServiceAuth.Proxy.Configuration.Secret;

namespace RouteServiceAuth.Proxy
{
    public static class ProxyExtensions
    {
        public static bool TryGetSecretOfType<T>(this Dictionary<string, Secret> credentials, string key, [MaybeNullWhen(false)] out T value) where T : Secret
        {
            value = null;
            if (!credentials.TryGetValue(key, out var cred) || cred is not T tValue) return false;
            value = tValue;
            return true;
        }
        public static OptionsBuilder<TOptions> Validate<TOptions>(this OptionsBuilder<TOptions> builder) where TOptions : class
        {
            
            builder.Services.AddTransient<IValidateOptions<TOptions>>(sp =>
                new OptionsValidator<TOptions>(builder.Name,
                    sp.GetRequiredService<IValidator<TOptions>>()));
            return builder;
        }

        public static IConfigurationBuilder AddProxyConfig(this IConfigurationBuilder builder)
        {
            var remappingConfigurationSource = new ProxyConfigToKestrelEndpointConfigurationProvider(builder.Sources);
            builder.Sources.Clear();
            builder.Sources.Add(remappingConfigurationSource);
            return builder;
        }
        
        
        #region Service Collection
       
        
        public static IServiceCollection AddClaimTransformer<T>(this IServiceCollection services) where T : class, IClaimsTransformation
        {
            services.AddSingleton<T>();
            services.AddSingleton<IClaimsTransformation>(ctx => ctx.GetRequiredService<T>());
            if (typeof(IStartupFilter).IsAssignableFrom(typeof(T)))
            {
                services.AddSingleton(ctx => (IStartupFilter)ctx.GetRequiredService<T>());
            }
            return services;
        }
        

        #endregion

        #region HttpContext

        public static ProxyEntry GetProxyEntry(this HttpContext ctx)
        {
            
            var direction = ctx.GetProxyDirection();
            var options = ctx.RequestServices.GetRequiredService<IOptionsSnapshot<ProxyOptions>>().Value;
            
            Exception NotFound() => new InvalidOperationException("Proxy config cannot be found for current port");
            switch (direction)
            {
                case ProxyDirection.Egress:
                    return options.Egress.FirstOrDefault(x => x.ListenPort == ctx.Connection.LocalPort) ?? throw NotFound();
                case ProxyDirection.Ingress:
                    return options.Ingress.FirstOrDefault(x => x.ListenPort == ctx.Connection.LocalPort) ?? throw NotFound();
                default:
                    throw NotFound();
            }
        }

        public static ProxyDirection GetProxyDirection(this HttpContext ctx)
        {
            var options = ctx.RequestServices.GetRequiredService<IOptionsSnapshot<ProxyOptions>>().Value;
            if (options.Ingress.Any(x => x.ListenPort == ctx.Connection.LocalPort))
                return ProxyDirection.Ingress;
            if (options.Egress.Any(x => x.ListenPort == ctx.Connection.LocalPort))
                return ProxyDirection.Egress;
            return 0;
        }
        #endregion

        #region Application Builder

        

        /// <summary>
        /// Authorizes ingress requests against associated authorization policy as defined on the matching proxy route
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseRouteAuthorization(this IApplicationBuilder app) => app.UseMiddleware<RouteAuthorizationMiddleware>();

        /// <summary>
        /// Proxies requests downstream and appends current Principal identity and roles as http headers  
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseIdentityIngressProxy(this IApplicationBuilder app) => app.UseMiddleware<ReverseProxyIdentityMiddleware>();

        /// <summary>
        /// Configures ingress proxy pipeline which uses Kerberos to authenticate incoming requests and propagate identity downstream as http header
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSpnegoIngressProxy(this IApplicationBuilder app) =>
            app.MapWhen(ctx => ctx.GetProxyDirection() == ProxyDirection.Ingress,
                ingressApp => ingressApp
                    .UseAuthentication()
                    .UseRouteAuthorization()
                    .UseIdentityIngressProxy());


        /// <summary>
        /// Configures egress pipeline that proxies requests to configured destination while obtaining and attaching SPNEGO (Kerberos) tickets as HTTP headers 
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSpnegoEgressProxy(this IApplicationBuilder app) =>
            app.MapWhen(
                ctx => ctx.GetProxyDirection() == ProxyDirection.Egress,
                egressApp => egressApp.UseMiddleware<AttachKerberosTicketMiddleware>());

        #endregion

        #region Headers

        public static bool TryGetForwardingAddressFromHeader(this IHeaderDictionary headers, string? forwardHeaderName, out string? forwardAddress)
        {
            forwardAddress = null;
            if (forwardHeaderName == null)
                return false;
            
            if(headers.TryGetValue(forwardHeaderName, out var values))
            {
                forwardAddress = values.ToString();
            }
            return null != forwardAddress;
        }

        #endregion

        public static IIdentityServerBuilder AddJwtIssuing(this IServiceCollection services)
        {
            return services.AddIdentityServer(opt =>
                {
                    opt.Endpoints = new EndpointsOptions()
                    {
                        EnableAuthorizeEndpoint = false,
                        EnableIntrospectionEndpoint = false,
                        EnableTokenEndpoint = false,
                        EnableCheckSessionEndpoint = false,
                        EnableDeviceAuthorizationEndpoint = false,
                        EnableEndSessionEndpoint = false,
                        EnableJwtRequestUri = false,
                        EnableTokenRevocationEndpoint = false,
                        EnableUserInfoEndpoint = false
                    };
                    opt.Discovery = new DiscoveryOptions()
                    {
                        ShowClaims = false,
                        ShowApiScopes = false,
                        ShowGrantTypes = false,
                        ShowIdentityScopes = false,
                        ShowResponseModes = false,
                        ShowResponseTypes = false,
                        ShowExtensionGrantTypes = false,
                        ShowTokenEndpointAuthenticationMethods = false
                    };
                    
                })
                .AddInMemoryClients(Enumerable.Empty<Client>())
                .AddInMemoryIdentityResources(Enumerable.Empty<IdentityResource>())
                .AddInMemoryCaching()
                .AddInMemoryApiResources(Enumerable.Empty<ApiResource>())
                .AddInMemoryApiScopes(Enumerable.Empty<ApiScope>());
        }
    }
}