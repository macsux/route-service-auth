using System;
using Microsoft.Extensions.Configuration;
using Options = Microsoft.Extensions.Options.Options;

namespace Microsoft.Extensions.Options.ConfigurationExtensions
{
    public class LinkedNamedConfigurationOptions<TOptions,TOther> : IConfigureNamedOptions<TOptions>
        where TOptions : class where TOther : class
    {
        public IOptionsMonitor<TOther> Other { get; }
        public Action<TOther, TOptions> Transform { get; }

        public LinkedNamedConfigurationOptions(IOptionsMonitor<TOther> other, Action<TOther, TOptions> transform)
        {
            Other = other;
            Transform = transform;
        }
        public void Configure(string name, TOptions options)
        {
            var other = Other.Get(name);
            Transform(other, options);
        }

        public void Configure(TOptions options) => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);
    }
    
}