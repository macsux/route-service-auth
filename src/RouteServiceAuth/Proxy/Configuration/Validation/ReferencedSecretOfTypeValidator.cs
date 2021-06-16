using System;
using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    public class ReferencedSecretOfTypeValidator<TOptions> : PropertyValidator<TOptions, string?>
    {
        public Type Type { get; }

        public ReferencedSecretOfTypeValidator(Type type)
        {
            Type = type;
        }

        public override bool IsValid(ValidationContext<TOptions> context, string? value)
        {
            if (value == null) return false;
            var credentials = context.GetServiceProvider().GetRequiredService<IOptionsMonitor<Dictionary<string, Secret>>>();
            if (!credentials.CurrentValue.TryGetValue(value, out var credential))
                return true;
            return credential.GetType().IsAssignableTo(Type);
        }

        public override string Name => "SecretOfTypeValidator";
        protected override string GetDefaultMessageTemplate(string errorCode) => "Referenced secret is not of specified type";
    }
}