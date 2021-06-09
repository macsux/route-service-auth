using System;
using Microsoft.Extensions.Configuration;
using Options = Microsoft.Extensions.Options.Options;

namespace Microsoft.Extensions.Options.ConfigurationExtensions
{
    public class BindNameConfigurationOptions<TOptions> : IConfigureNamedOptions<TOptions>
        where TOptions : class
    {
        public IConfiguration Config { get; }

        public BindNameConfigurationOptions(IConfiguration config)
        {
            Config = config;
        }
        public void Configure(string name, TOptions options)
        {
            Config.GetSection(name).Bind(options);
        }

        public void Configure(TOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
    }
    
}