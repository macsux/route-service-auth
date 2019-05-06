using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using RouteService;

namespace Pivotal.IWA.ServiceLightCore
{
    public static class SpnegoAuthenticationExtensions
    {
        private const string scheme = SpnegoAuthenticationDefaults.AuthenticationScheme;

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder)
        {
            return builder.AddSpnego(scheme);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme)
        {
            return builder.AddSpnego(authenticationScheme, null);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            Action<SpnegoAuthenticationOptions> configureOptions)
        {
            return builder.AddSpnego(scheme, configureOptions);
        }

        public static AuthenticationBuilder AddSpnego(
            this AuthenticationBuilder builder,
            string authenticationScheme,
            Action<SpnegoAuthenticationOptions> configureOptions)
        {
            return builder.AddScheme<SpnegoAuthenticationOptions, SpnegoAuthenticationHandler>(authenticationScheme, configureOptions);
        }

        public static IApplicationBuilder ForbidAnonymous(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    await context.ChallengeAsync(SpnegoAuthenticationDefaults.AuthenticationScheme
                        , new AuthenticationProperties());
                    return;
                }

                await next();
            });
        }

    }
}