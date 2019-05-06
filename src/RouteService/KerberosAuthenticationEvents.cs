using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace RouteService
{
    public class KerberosAuthenticationEvents : CookieAuthenticationEvents
    {
        public KerberosAuthenticationEvents()
        {
        }

        public override Task SigningIn(CookieSigningInContext context)
        {
            return base.SigningIn(context);
        }

        public override Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            return base.ValidatePrincipal(context);
        }
    }
}