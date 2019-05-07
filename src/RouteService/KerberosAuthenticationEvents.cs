using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pivotal.IWA.ServiceLightCore;

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

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            if (!context.Principal.Identity.IsAuthenticated)
            {
                
                await context.HttpContext.ChallengeAsync(SpnegoAuthenticationDefaults.AuthenticationScheme);
            }
            
        }
    }
}