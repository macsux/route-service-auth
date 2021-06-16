using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class OAuthClient : Secret
    {
        public string? ClientId { get; set; }
        public string? Secret { get; set; }
        
        public class Validator : AbstractValidator<OAuthClient>
        {
            public Validator()
            {
                RuleFor(x => x.ClientId).NotEmpty();
                RuleFor(x => x.Secret).NotEmpty();
            }
        }

    }
}