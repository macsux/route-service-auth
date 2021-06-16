using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Kerberos.NET.Entities.Pac;
using Microsoft.Extensions.Options;
using ProxyKit;
using ProxyOptions = RouteServiceAuth.Proxy.Configuration.ProxyOptions;

namespace RouteServiceAuth.Proxy.Transformers
{
    public class ClaimsAsHeaderAppender : IProxyMiddleware
    {
        private readonly IOptionsSnapshot<ProxyOptions> _proxyOptions;
        private readonly ProxyRequestDelegate _next;

        public ClaimsAsHeaderAppender(IOptionsSnapshot<ProxyOptions> proxyOptions, ProxyRequestDelegate next)
        {
            _proxyOptions = proxyOptions;
            _next = next;
        }

        public async Task Invoke(ForwardContext forwardContext)
        {
            forwardContext.UpstreamRequest.Headers.Remove(_proxyOptions.Value.IdentityHttpHeaderName);
            forwardContext.UpstreamRequest.Headers.Remove(_proxyOptions.Value.RoleHttpHeaderName);
            if (forwardContext.HttpContext.User.Identity?.IsAuthenticated ?? false)
            {
                forwardContext.UpstreamRequest.Headers.Add(_proxyOptions.Value.IdentityHttpHeaderName, forwardContext.HttpContext.User.Identity.Name);
                var roles = forwardContext.HttpContext.User.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToArray();
                if (roles.Any())
                {
                    forwardContext.UpstreamRequest.Headers.Add(_proxyOptions.Value.RoleHttpHeaderName, roles);
                }
            }

            forwardContext.UpstreamRequest.Headers.Remove("Authorization");
            await _next(forwardContext);
        }
    }
}