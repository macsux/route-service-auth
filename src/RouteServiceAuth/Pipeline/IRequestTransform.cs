using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RouteServiceAuth.Pipeline
{
    public interface IRequestTransform
    {
        Task Transform(HttpContext context, HttpRequestMessage request);
    }
}