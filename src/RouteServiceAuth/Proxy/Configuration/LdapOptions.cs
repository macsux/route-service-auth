using System;
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Results;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using RouteServiceAuth.Proxy.Configuration.Validation;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class LdapOptions
    {
        public string? Server { get; set; }
        public int Port { get; set; } = 389;
        public string CredentialId { get; set; } = WindowsCredential.DefaultCredentialId;
        public string Filter { get; set; } = "(objectClass=group)";
        public string? GroupsQuery { get; set; }
        public TimeSpan RefreshFrequency { get; set; } = TimeSpan.FromMinutes(1);

        public class Validator : AbstractValidator<LdapOptions>
        {
            public Validator()
            {
                RuleFor(x => x.Server).NotEmpty();
                RuleFor(x => x.Port).GreaterThan(0);
                RuleFor(x => x.GroupsQuery).NotEmpty();
                RuleFor(x => x.CredentialId).ReferencesValidCredentials().IsSecretOfType<LdapOptions, WindowsCredential>();
            }
        }
    }
}