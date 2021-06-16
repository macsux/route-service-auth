using Kerberos.NET.Client;
using RouteServiceAuth.Proxy.Configuration;

namespace RouteServiceAuth.Kerberos
{
    public interface IKerberosClientProvider
    {
        KerberosClient GetClient(Secret secret);
    }
}