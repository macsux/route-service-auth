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
using Microsoft.AspNetCore.Hosting.Server.Features;
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
            // return options.Egress.Union(options.Ingress).FirstOrDefault(x => x.ListenPort == ctx.Connection.LocalPort) ?? throw NotFound();
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
        
        public static IApplicationBuilder MapProxyPorts(this IApplicationBuilder app)
        {
            var proxyOptions = app.ApplicationServices.GetRequiredService<IOptionsMonitor<ProxyOptions>>();
            var appConfig = proxyOptions.CurrentValue;
            var urls = 
                appConfig.Egress
                    .Union(appConfig.Ingress)
                    .Select(entry => $"http://0.0.0.0:{entry.ListenPort}")
                    .ToArray();
            var address = app.ServerFeatures.Get<IServerAddressesFeature>();
            address.Addresses.Clear();
            foreach (var url in urls)
            {
                address.Addresses.Add(url);
            }

            return app;
        }
    }
}