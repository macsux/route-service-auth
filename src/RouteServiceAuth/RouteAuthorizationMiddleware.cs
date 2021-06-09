using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth
{
    public class RouteAuthorizationMiddleware
    {
        private static Route _defaultRoute = new Route
        {
            Id = "Default",
            PolicyName = AuthorizationPolicies.RequireAuthenticatedUser,
            Path = "/**"
        };
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IAuthorizationPolicyProvider _policyProvider;
        private readonly IPolicyEvaluator _policyEvaluator;

        public RouteAuthorizationMiddleware(RequestDelegate next,
            ILogger<RouteAuthorizationMiddleware> logger,
            IAuthorizationPolicyProvider policyProvider,
            IPolicyEvaluator policyEvaluator)
        {
            _next = next;
            _logger = logger;
            _policyProvider = policyProvider;
            _policyEvaluator = policyEvaluator;
        }

        public async Task Invoke(HttpContext context)
        {
            var proxyConfig = context.GetProxyEntry();
            var authenticationScheme = proxyConfig.CredentialsId;
            
            var matchingRoute = proxyConfig.Routes?
                .FirstOrDefault(x => new Ant(x.Path).IsMatch(context.Request.Path)) ?? _defaultRoute;

            
            _logger.LogTrace($"Using route ID {matchingRoute.Id}");

            if (matchingRoute.PolicyName != null)
            {
                // authorization policies are not tied to specific authentication scheme, since those are registered per credential
                // instead we construct it dynamically here by matching it to required authentication scheme preconfigured with specific creds
                // (authentication schemes are registered with same name as CredentialsId on ProxyConfig)
                var policy = await _policyProvider.GetPolicyAsync(matchingRoute.PolicyName);
                policy = new AuthorizationPolicy(policy.Requirements, new []{ authenticationScheme });
                _logger.LogTrace($"Applying '{matchingRoute.PolicyName}' authorization policy with '{authenticationScheme}' authentication");
                
                var authenticationResult = await _policyEvaluator.AuthenticateAsync(policy, context);
                var authorizationResult = await _policyEvaluator.AuthorizeAsync(policy, authenticationResult, context, null);

                if (authorizationResult.Challenged)
                {
                    _logger.LogTrace($"Issuing authorization challange");
                    await context.ChallengeAsync(authenticationScheme, new AuthenticationProperties());
                    return;
                }
                else if(authorizationResult.Forbidden)
                {
                    _logger.LogTrace($"Authorization failed");
                    await context.ForbidAsync();
                    return;
                }
            }
           

            await _next(context);
        }
    }
}