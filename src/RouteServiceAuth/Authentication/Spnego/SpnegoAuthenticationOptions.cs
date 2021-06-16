using System;
using System.Collections.Generic;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using RouteServiceAuth.Proxy.Configuration;

namespace RouteServiceAuth.Authentication.Spnego
{
    [PublicAPI]
    public class SpnegoAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// List of credentials that will be attempted
        /// </summary>
        public List<WindowsCredential> Credentials { get; set; } = new();

        /// <summary>
        ///     The location of the keytab containing service credentials
        /// </summary>
        // ReSharper disable once StringLiteralTypo
        [Obsolete("Not used")]
        public string? KeytabFile { get; set; } = Environment.GetEnvironmentVariable("KRB5_CLIENT_KTNAME");

        public override void Validate() => new Validator().ValidateAndThrow(this);

        public class Validator : AbstractValidator<SpnegoAuthenticationOptions>
        {
            
            public Validator()
            {
                RuleForEach(x => x.Credentials).SetValidator(new WindowsCredential.Validator());
                RuleFor(x => x.Credentials).NotEmpty();
                    // .Unless(x => x.KeytabFile != null);
                // RuleFor(x => x.KeytabFile).NotEmpty().Unless(x => x.Credentials.Any());
            }
        }
    }
}