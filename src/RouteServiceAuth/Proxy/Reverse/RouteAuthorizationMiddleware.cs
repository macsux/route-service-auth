using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RouteServiceAuth.Proxy.Route;

namespace RouteServiceAuth.Proxy.Reverse
{
    public class RouteAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;
        private readonly IAuthorizationPolicyProvider _policyProvider;
        private readonly IPolicyEvaluator _policyEvaluator;

        public RouteAuthorizationMiddleware(RequestDelegate next,
            ILogger<RouteAuthorizationMiddleware> logger,
            IAuthenticationSchemeProvider authenticationSchemeProvider,
            IAuthorizationPolicyProvider policyProvider,
            IPolicyEvaluator policyEvaluator)
        {
            _next = next;
            _logger = logger;
            _authenticationSchemeProvider = authenticationSchemeProvider;
            _policyProvider = policyProvider;
            _policyEvaluator = policyEvaluator;
        }

        public async Task Invoke(HttpContext context)
        {
            var proxyConfig = context.GetProxyEntry();
            

            bool IsRouteMatchesRequest(Configuration.Route route)
            {
                return new Ant(route.Path).IsMatch(context.Request.Path) && (route.Methods.Contains(context.Request.Method) || !route.Methods.Any());
            }
            var matchingRoute = proxyConfig.Routes?.FirstOrDefault(IsRouteMatchesRequest) ?? Configuration.Route.Default;
            
            _logger.LogTrace("Using route ID {RouteID}", matchingRoute.Id);

            if (matchingRoute.PolicyName != null)
            {
               
                var policy = await _policyProvider.GetPolicyAsync(matchingRoute.PolicyName) 
                             ?? throw new InvalidOperationException($"Authorization policy {matchingRoute.PolicyName} referenced by route {matchingRoute.Id} is not found");
                var authenticationScheme = policy.AuthenticationSchemes.FirstOrDefault();
               

                try
                {
                    var authenticationResult = await _policyEvaluator.AuthenticateAsync(policy, context);
                    var authorizationResult = await _policyEvaluator.AuthorizeAsync(policy, authenticationResult, context, null);

                    if (authorizationResult.Challenged)
                    {
                        _logger.LogTrace("Issuing authorization challenge");

                        await context.ChallengeAsync(authenticationScheme, new AuthenticationProperties());
                        return;
                    }
                    else if (authorizationResult.Forbidden)
                    {
                        _logger.LogTrace("Authorization failed");
                        await context.ForbidAsync();
                        return;
                    }
                }
                catch
                {
                    await context.ForbidAsync();
                    return;
                }
            }
           

            await _next(context);
        }
    }
}