using System;
using System.IO;
using FluentValidation;
using JetBrains.Annotations;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class KerberosOptions
    {
        public string? Realm { get; set; } = Environment.GetEnvironmentVariable("REALM") ?? Environment.GetEnvironmentVariable("DOMAIN")?.ToUpper();
        public string? Kdc { get; set; } = Environment.GetEnvironmentVariable("KDC");
        public string? Krb5ConfigPath { get; set; }

        [UsedImplicitly]
        public class Validator : AbstractValidator<KerberosOptions>
        {
            public Validator()
            {
                RuleFor(x => x.Realm).NotEmpty().Unless(x => !string.IsNullOrEmpty(x.Krb5ConfigPath));
                RuleFor(x => x.Kdc).NotEmpty().Unless(x => !string.IsNullOrEmpty(x.Krb5ConfigPath));
                RuleFor(x => x.Krb5ConfigPath).Must(path => string.IsNullOrEmpty(path) || File.Exists(path));
            }
        }
    }
}