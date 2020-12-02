using Microsoft.AspNetCore.Http;

namespace RouteServiceAuth.Kerberos.NET
{
    public static class RouteServiceExtensions
    {
        public static bool TryGetCfRouteServiceForwardAddress(this IHeaderDictionary headers, out string forwardAddress)
        {
            forwardAddress = null;
            
            if(headers.TryGetValue(Constants.X_CF_Forwarded_Url, out var values))
            {
                forwardAddress = values.ToString();
            }
            return null != forwardAddress;
        }
    }
}