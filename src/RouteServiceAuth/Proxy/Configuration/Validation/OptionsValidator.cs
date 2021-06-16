using System;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    public class OptionsValidator<TOptions> : IValidateOptions<TOptions> where TOptions : class
    {
        public OptionsValidator(string name, IValidator<TOptions> validator)
        {
            Validator = validator;
            Name = name;
        }

        /// <summary>
        /// The options name.
        /// </summary>
        public string Name { get; }

        public IValidator<TOptions> Validator { get; }

        /// <summary>
        /// Validates a specific named options instance (or all when <paramref name="name"/> is null).
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance.</param>
        /// <returns>The <see cref="ValidateOptionsResult"/> result.</returns>
        public ValidateOptionsResult Validate(string name, TOptions options)
        {
            // null name is used to configure all named options
            if (Name == null || name == Name)
            {
                var result = Validator.Validate(options);
                if(result.IsValid)
                    return ValidateOptionsResult.Success;
                return ValidateOptionsResult.Fail(result.ToString());
            }

            // ignored if not validating this instance
            return ValidateOptionsResult.Skip;
        }
    }
}