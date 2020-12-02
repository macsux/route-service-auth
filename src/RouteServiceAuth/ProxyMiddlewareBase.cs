using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProxyKit;

namespace RouteServiceAuth
{
    public abstract class ProxyMiddlewareBase
    {
        protected ProxyMiddleware ProxyMiddleware { get; }

        public ProxyMiddlewareBase(RequestDelegate next)
        {
            var proxyOptions = new ProxyOptions
            {
                HandleProxyRequest = ProxyRequest,
            };
            ProxyMiddleware = new ProxyMiddleware(next, proxyOptions);
        }
        public async Task Invoke(HttpContext context) => await ProxyMiddleware.Invoke(context);

        protected abstract Task<HttpResponseMessage> ProxyRequest(HttpContext context);
    }
}