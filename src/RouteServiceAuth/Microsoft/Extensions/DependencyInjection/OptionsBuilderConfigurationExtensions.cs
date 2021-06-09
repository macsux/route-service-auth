using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options.ConfigurationExtensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding configuration related options services to the DI container via <see cref="OptionsBuilder{TOptions}"/>.
    /// </summary>
    public static class OptionsBuilderConfigurationExtensions
    {
        
        public static OptionsBuilder<TOptions> BindNameConfiguration<TOptions>(
            this OptionsBuilder<TOptions> optionsBuilder,
            string configSectionPath)
            where TOptions : class
        {
            _ = optionsBuilder ?? throw new ArgumentNullException(nameof(optionsBuilder));
            _ = configSectionPath ?? throw new ArgumentNullException(nameof(configSectionPath));

            optionsBuilder.Services.AddSingleton<IConfigureOptions<TOptions>>(sp =>
            {
                IConfiguration config = sp.GetRequiredService<IConfiguration>();
                IConfiguration section = string.Equals("", configSectionPath, StringComparison.OrdinalIgnoreCase)
                    ? config
                    : config.GetSection(configSectionPath);
                return new BindNameConfigurationOptions<TOptions>(section);
            });
            // optionsBuilder.Name = null;
            // temporary hack until this gets added to the BCL: https://github.com/dotnet/runtime/issues/45294
            var setName = optionsBuilder.GetType().GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            setName.SetValue(optionsBuilder, null);
            
            return optionsBuilder;
        }
    }
}