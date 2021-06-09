using Microsoft.AspNetCore.Http;

namespace RouteServiceAuth.Kerberos.NET
{
    public static class RouteServiceExtensions
    {
        public static bool TryGetCfRouteServiceForwardAddress(this IHeaderDictionary headers, out string forwardAddress)
        {
            forwardAddress = null;
            
            if(headers.TryGetValue(KnownHeaders.X_CF_Forwarded_Url, out var values))
            {
                forwardAddress = values.ToString();
            }
            return null != forwardAddress;
        }
        public static bool TryGetForwardingAddressFromHeader(this IHeaderDictionary headers, string forwardHeaderName, out string forwardAddress)
        {
            forwardAddress = null;
            if (forwardHeaderName == null)
                return false;
            
            if(headers.TryGetValue(forwardHeaderName, out var values))
            {
                forwardAddress = values.ToString();
            }
            return null != forwardAddress;
        }
    }
}