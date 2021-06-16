using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProxyKit;
using RouteServiceAuth.Proxy.Configuration;
using RouteServiceAuth.Proxy.Transformers;
using ProxyOptions = RouteServiceAuth.Proxy.Configuration.ProxyOptions;

namespace RouteServiceAuth.Proxy.Reverse
{
    public class ReverseProxyIdentityMiddleware : ProxyMiddlewareBase
    {
        private readonly ILogger _logger;
        private readonly IOptionsMonitor<ProxyOptions> _proxyOptions;
        private readonly IServiceProvider _serviceProvider;

        public ReverseProxyIdentityMiddleware(RequestDelegate next, 
            ILogger<ReverseProxyIdentityMiddleware> logger, 
            IOptionsMonitor<ProxyOptions> proxyOptions, 
            IServiceProvider serviceProvider) : base(next)
        {
            _logger = logger;
            _proxyOptions = proxyOptions;
            _serviceProvider = serviceProvider;
        }

        protected override async Task<HttpResponseMessage> ProxyRequest(HttpContext context)
        {
            var proxyOptions = _proxyOptions.CurrentValue;
            HttpResponseMessage response;
            var config = context.GetProxyEntry();
            var forwardTo = config.TargetUrl;
            var destinationHeaderName = proxyOptions.DestinationHeaderName;
            if (forwardTo == null && !context.Request.Headers.TryGetForwardingAddressFromHeader(destinationHeaderName, out forwardTo)) 
            {
                response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent($"Destination route cannot be determined")
                };
                _logger.LogDebug("Received request without {DestinationHeaderName} header and no static forwarding route is set", destinationHeaderName);
                return response;
            }

            var forwardContext = context.ForwardTo(forwardTo);
            forwardContext.UpstreamRequest.RequestUri = new Uri(forwardContext.UpstreamRequest.RequestUri!.ToString().TrimEnd('/'));
            var pipeline = new ProxyForwardingPipelineBuilder(_serviceProvider);
            switch (proxyOptions.PrincipalForwardingMode)
            {
                case PrincipalForwardingMode.Headers:
                    pipeline.AddMiddleware<ClaimsAsHeaderAppender>();
                    break;
                case PrincipalForwardingMode.Jwt:
                    pipeline.AddMiddleware<ClaimsAsJwtHeaderAppender>();
                    break;
            }

            await pipeline.Build().Invoke(forwardContext);
            

            var headersString = forwardContext.UpstreamRequest.Headers.Aggregate(new StringBuilder(), (sb, kv) => sb.AppendLine($"  {kv.Key}: {string.Join(",", kv.Value)}"));
            _logger.LogTrace("Headers sent downstream\n{Headers}",headersString);

            response = await forwardContext.Send();
            _logger.LogDebug("Downstream responded with: {StatusCode}", response.StatusCode);

            // merge cookie header set at the proxy level with headers from downstream request 
            foreach (var cookie in context.Response.Headers["Set-Cookie"])
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
            }

            return response;
        }
    }
}