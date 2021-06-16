using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IdentityServer4;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProxyKit;
using ProxyOptions = RouteServiceAuth.Proxy.Configuration.ProxyOptions;

namespace RouteServiceAuth.Proxy.Transformers
{
    public class ClaimsAsJwtHeaderAppender : IProxyMiddleware
    {
        private readonly IOptionsSnapshot<ProxyOptions> _proxyOptions;
        private readonly IdentityServerTools _identityServerTools;
        private readonly ProxyRequestDelegate _next;

        public ClaimsAsJwtHeaderAppender(IOptionsSnapshot<ProxyOptions> proxyOptions, IdentityServerTools identityServerTools, ProxyRequestDelegate next)
        {
            _proxyOptions = proxyOptions;
            _identityServerTools = identityServerTools;
            _next = next;
        }
        public async Task Invoke(ForwardContext forwardContext)
        {
            if (forwardContext.HttpContext.User.Identity?.IsAuthenticated ?? false)
            {
                // var audience = forwardContext.UpstreamRequest.RequestUri?.ToString();
                // var issuer = "TBD";
                
                // var tokenHandler = new JwtSecurityTokenHandler();
                // var tokenDescriptor = new SecurityTokenDescriptor
                // {
                //     Subject = (ClaimsIdentity)forwardContext.HttpContext.User.Identity,
                //     Expires = DateTime.UtcNow.AddMinutes(1),
                //     Issuer = issuer,
                //     Audience = audience,
                //     SigningCredentials = new SigningCredentials(_proxyOptions.Value.GetSecurityKey(), SecurityAlgorithms.RsaSha256)
                // };
                //
                // var token = tokenHandler.CreateToken(tokenDescriptor);
                // var jwt = tokenHandler.WriteToken(token);
                var jwt = await _identityServerTools.IssueJwtAsync(60, forwardContext.HttpContext.User.Claims);
                forwardContext.UpstreamRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            }
            await _next(forwardContext);
        }
        
    }
}