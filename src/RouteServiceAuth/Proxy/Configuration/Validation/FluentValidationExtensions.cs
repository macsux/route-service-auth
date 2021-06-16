using FluentValidation;
using JetBrains.Annotations;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    [PublicAPI]
    public static class FluentValidationExtensions
    {
        public static IRuleBuilderOptions<T, string?> ReferencesValidCredentials<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
            ruleBuilder
                .SetValidator(new ReferencedSecretExistValidator<T>())
                .SetValidator(new ReferencedSecretValidator<T>());

        public static IRuleBuilderOptions<T, string?> IsSecretOfType<T, TSecret>(this IRuleBuilder<T, string?> ruleBuilder) =>
            ruleBuilder.SetValidator(new ReferencedSecretOfTypeValidator<T>(typeof(TSecret)));
        
        public static IRuleBuilderOptions<T, string?> RsaPrivateKey<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
            ruleBuilder.SetValidator(new RsaPrivateKeyValidator<T>());
    }
}