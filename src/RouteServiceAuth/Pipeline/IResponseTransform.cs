using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RouteServiceAuth.Pipeline
{
    public interface IResponseTransform
    {
        Task Transform(HttpContext context);
    }
}