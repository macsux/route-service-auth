using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;

namespace RouteServiceAuth.Proxy.Configuration.Validation
{
    /// <summary>
    /// Registers all validators in current assembly and allows them to be resolved from DI container
    /// </summary>
    public class ValidatorFactory : ValidatorFactoryBase
    {
        private readonly IServiceProvider _serviceProvider;
        public Dictionary<Type, Type> Validators { get; }

        public ValidatorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Validators = AssemblyScanner.FindValidatorsInAssembly(GetType().Assembly, true)
                .GroupBy(x => x.InterfaceType)
                .Select(x => (x.Key, Value: x.Last()))
                .ToDictionary(x => x.Key, x => x.Value.ValidatorType);
            
        }

        public override IValidator CreateInstance(Type serviceType)
        {
            
            if (!Validators.TryGetValue(serviceType, out var validator))
                throw new InvalidOperationException($"Service {serviceType} is not registered");
            return (IValidator) ActivatorUtilities.CreateInstance(_serviceProvider.CreateScope().ServiceProvider, validator);
        }


        /// <summary>
        /// Wraps real validator and configures validation context with ServiceProvider to allow any chaining validators to be resolved from DI container
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class ServiceProviderWrappedValidator<T> : IValidator<T>
        {
            private readonly IValidator<T> _innerValidator;
            private readonly IServiceProvider _serviceProvider;
            public ServiceProviderWrappedValidator(ValidatorFactory factory, IServiceProvider serviceProvider)
            {
                _innerValidator = factory.Validators.ContainsKey(typeof(IValidator<T>)) ? factory.GetValidator<T>() : new InlineValidator<T>();
                _serviceProvider = serviceProvider;
            }

            public ValidationResult Validate(IValidationContext context) => _innerValidator.Validate(context);

            public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = new ()) 
                => _innerValidator.ValidateAsync(context, cancellation);

            public IValidatorDescriptor CreateDescriptor() => _innerValidator.CreateDescriptor();

            public bool CanValidateInstancesOfType(Type type) => _innerValidator.CanValidateInstancesOfType(type);

            public ValidationResult Validate(T instance)
            {
                return _innerValidator.Validate(GetContext(instance));
            }

            public Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellation = new CancellationToken())
            {
                return _innerValidator.ValidateAsync(GetContext(instance), cancellation);
            }

            private ValidationContext<T> GetContext(T instance)
            {
                var context = new ValidationContext<T>(instance, new PropertyChain(), ValidatorOptions.Global.ValidatorSelectors.DefaultValidatorSelectorFactory());
                context.SetServiceProvider(_serviceProvider);
                return context;
            }
        }
    }
}