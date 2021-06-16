using System;
using System.Collections.Generic;
using FluentValidation;
using JetBrains.Annotations;
using RouteServiceAuth.Proxy.Configuration.Validation;

namespace RouteServiceAuth.Proxy.Configuration
{
    [PublicAPI]
    public class ProxyEntry
    {
        private string? _id;

        public string Id
        {
            get => _id ?? $"[FromPort={ListenPort},To=[{TargetUrl ?? "*"},Credentials={CredentialsId}]]";
            set => _id = value;
        }
        public int ListenPort { get; set; }
        public string? TargetUrl { get; set; }
        public string CredentialsId { get; set; } = WindowsCredential.DefaultCredentialId;
        public string? Spn { get; set; }
        public List<Route>? Routes { get; set; }

        public class Validator : AbstractValidator<ProxyEntry>
        {
            public static string EgressRules = "egress";
            public Validator()
            {
                RuleSet(EgressRules, () =>
                {
                    RuleFor(x => x.TargetUrl).NotEmpty();
                });
            }
        }
    }
}