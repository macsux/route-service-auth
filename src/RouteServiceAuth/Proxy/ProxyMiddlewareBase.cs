using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ProxyKit;

namespace RouteServiceAuth.Proxy
{
    public abstract class ProxyMiddlewareBase
    {
        protected ProxyMiddleware ProxyMiddleware { get; }

        public ProxyMiddlewareBase(RequestDelegate next)
        {
            var proxyOptions = new ProxyKit.ProxyOptions
            {
                HandleProxyRequest = ProxyRequest,
            };
            ProxyMiddleware = new ProxyMiddleware(next, proxyOptions);
        }
        public async Task Invoke(HttpContext context) => await ProxyMiddleware.Invoke(context);

        protected abstract Task<HttpResponseMessage> ProxyRequest(HttpContext context);
    }
}