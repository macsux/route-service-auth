using System.Collections.Generic;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    public class ReferencedSecretValidator<TOptions> : PropertyValidator<TOptions, string?>
    {
        public override bool IsValid(ValidationContext<TOptions> context, string? value)
        {
            if (value == null) return false;
            var credentials = context.GetServiceProvider().GetRequiredService<IOptionsMonitor<Dictionary<string, Secret>>>();
            var credentialsValidator = context.GetServiceProvider().GetRequiredService<IValidator<Secret>>();
            if (!credentials.CurrentValue.TryGetValue(value, out var credential))
                return false;
            return credentialsValidator.Validate(context.CloneForChildValidator(credential)).IsValid;
            // var credentialValidationResult = 
            // foreach (var error in credentialValidationResult.Errors)
            // {
            //     context.AddFailure(error);
            // }
            //
            // return credentialValidationResult.IsValid;
        }

        public override string Name => "SecretReferenceValidator";
        protected override string GetDefaultMessageTemplate(string errorCode) => "Referenced secret is not valid";
    }
}