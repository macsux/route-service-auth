using System.Threading.Tasks;
using ProxyKit;

namespace RouteServiceAuth.Proxy.Transformers
{
    public interface IProxyMiddleware
    {
        Task Invoke(ForwardContext forwardContext);
    }
}