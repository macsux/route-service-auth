using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public class ReverseProxyIdentityMiddleware : ProxyMiddlewareBase
    {
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ProxyOptions> _proxyOptions;

        public ReverseProxyIdentityMiddleware(RequestDelegate next, 
            ILogger<ReverseProxyIdentityMiddleware> logger, 
            IOptionsMonitor<ProxyOptions> proxyOptions) : base(next)
        {
            _logger = logger;
            _proxyOptions = proxyOptions;
        }

        protected override async Task<HttpResponseMessage> ProxyRequest(HttpContext context)
        {
            HttpResponseMessage response;
            var config = context.GetProxyEntry();
            var forwardTo = config.TargetUrl;
            var isRunningInCloudFoundry = Environment.GetEnvironmentVariable("VCAP_APPLICATION") != null;
            var destinationHeaderName = _proxyOptions.CurrentValue.DestinationHeaderName;
            if (destinationHeaderName == null && isRunningInCloudFoundry)
                destinationHeaderName = KnownHeaders.X_CF_Forwarded_Url;
            if (forwardTo == null && !context.Request.Headers.TryGetForwardingAddressFromHeader(destinationHeaderName, out forwardTo)) 
            {
                response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent($"Destination route cannot be determined")
                };
                _logger.LogDebug($"Received request without {KnownHeaders.X_CF_Forwarded_Url} header and no static forwarding route is set");
                return response;
            }

            var forwardContext = context.ForwardTo(forwardTo);
            // forwardContext.UpstreamRequest.RequestUri = new Uri(forwardTo);

            if (context.User.Identity.IsAuthenticated)
            {
                forwardContext.UpstreamRequest.Headers.Add(KnownHeaders.X_CF_Identity, context.User.Identity.Name);
                var roles = string.Join(",", context.User.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value));
                if (!string.IsNullOrEmpty(roles))
                {
                    forwardContext.UpstreamRequest.Headers.Add(KnownHeaders.X_CF_Roles, roles);
                }
            }

            forwardContext.UpstreamRequest.Headers.Remove("Authorization");


            _logger.LogTrace("Headers sent downstream");
            _logger.LogTrace("-----------------------");
            foreach (var header in forwardContext.UpstreamRequest.Headers)
            {
                _logger.LogTrace($"  {header.Key}: {header.Value.FirstOrDefault()}");
            }

            response = await forwardContext.Send();
            _logger.LogDebug($"Downstream responded with: {response.StatusCode}");

            // merge cookie header set at the proxy level with headers from downstream request 
            foreach (var cookie in context.Response.Headers["Set-Cookie"])
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
            }

            return response;
        }
    }
}