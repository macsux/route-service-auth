using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    public class ReferencedSecretExistValidator<TOptions> : PropertyValidator<TOptions, string?>
    {
        public override bool IsValid(ValidationContext<TOptions> context, string? value)
        {
            if (value == null) return false;
            var secret = context.GetServiceProvider().GetRequiredService<IOptionsMonitor<Dictionary<string, Secret>>>();
            return secret.CurrentValue.ContainsKey(value);
        }

        public override string Name => "ReferencedSecretExist";
        protected override string GetDefaultMessageTemplate(string errorCode) => "Secret ID does not point to a valid secret instance";
    }
}