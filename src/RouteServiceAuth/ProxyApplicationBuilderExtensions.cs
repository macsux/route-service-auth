using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public static class ProxyApplicationBuilderExtensions
    {
        #region Service Collection
        public static IServiceCollection AddKerberosClient(this IServiceCollection services)
        {
            services.AddSingleton<KerberosClientProvider>();
            services.AddScoped(ctx => ctx.GetRequiredService<KerberosClientProvider>().Client);
            return services;
        }
        
        public static IServiceCollection AddClaimTransformer<T>(this IServiceCollection services) where T : class, IClaimsTransformation
        {
            services.AddSingleton<T>();
            services.AddSingleton<IClaimsTransformation>(ctx => ctx.GetRequiredService<T>());
            if (typeof(IStartupFilter).IsAssignableFrom(typeof(T)))
            {
                services.AddSingleton<IStartupFilter>(ctx => (IStartupFilter)ctx.GetRequiredService<T>());
            }
            return services;
        }
        #endregion

        #region HttpContext
        public static string GetAuthenticationSchemeName(this HttpContext ctx)
        {
            return $"{SpnegoProxyAuthenticationDefaults.AuthenticationScheme}-{ctx.Connection.LocalPort}";
        }

        public static ProxyEntry GetProxyEntry(this HttpContext ctx)
        {
            var direction = ctx.GetProxyDirection();
            var options = ctx.RequestServices.GetRequiredService<IOptionsSnapshot<KerberosProxyOptions>>().Value;
            switch (direction)
            {
                case ProxyDirection.Egress:
                    return options.Egress[ctx.Connection.LocalPort];
                case ProxyDirection.Ingress:
                    return options.Ingress[ctx.Connection.LocalPort];
                default:
                    throw new InvalidOperationException("Proxy config cannot be found for current port");
            }
        }

        public static ProxyDirection GetProxyDirection(this HttpContext ctx)
        {
            var options = ctx.RequestServices.GetRequiredService<IOptionsSnapshot<KerberosProxyOptions>>().Value;
            if (options.Ingress.ContainsKey(ctx.Connection.LocalPort))
                return ProxyDirection.Ingress;
            if (options.Egress.ContainsKey(ctx.Connection.LocalPort))
                return ProxyDirection.Egress;
            return 0;
        }
        #endregion
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
        public static IApplicationBuilder UseKerberosIngressProxy(this IApplicationBuilder app) =>
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
        public static IApplicationBuilder UseKerberosEgressProxy(this IApplicationBuilder app) =>
            app.MapWhen(
                ctx => ctx.GetProxyDirection() == ProxyDirection.Egress,
                egressApp => egressApp.UseMiddleware<AttachKerberosTicketProxyMiddleware>());
        
    }
}