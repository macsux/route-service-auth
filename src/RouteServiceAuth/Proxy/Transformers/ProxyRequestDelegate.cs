using System.Threading.Tasks;
using ProxyKit;

namespace RouteServiceAuth.Proxy.Transformers
{
    public delegate Task ProxyRequestDelegate(ForwardContext forwardContext);
}