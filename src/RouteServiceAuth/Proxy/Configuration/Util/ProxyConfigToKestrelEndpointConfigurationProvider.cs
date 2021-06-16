using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace RouteServiceAuth.Proxy.Configuration.Util
{
    /// <summary>
    /// This configuration provider wraps the rest of the configuration stack to create shadow entries under Kestrel:Endpoints configuration key based on Proxy:[In|Eg]ress:ListenPort
    /// allowing hot reconfiguration of listening endpoints without restart
    /// </summary>
    public class ProxyConfigToKestrelEndpointConfigurationProvider : ConfigurationProvider, IConfigurationSource
    {
        readonly List<IConfigurationSource> _sources;

        IConfigurationRoot _config = new ConfigurationRoot(Array.Empty<IConfigurationProvider>());
        MappingProvider _mappedProvider = new(new MemoryConfigurationSource());

        public ProxyConfigToKestrelEndpointConfigurationProvider(IEnumerable<IConfigurationSource> configurationSources)
        {
            _sources = configurationSources.ToList();
        }


        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var providers = _sources.Select(x => x.Build(builder)).ToList();
            _mappedProvider = new MappingProvider(new MemoryConfigurationSource());
            providers.Add(_mappedProvider);
            _config = new ConfigurationRoot(providers);
            Load();
            return this;
        }


        public override bool TryGet(string key, out string value)
        {
            value = _config[key];
            return !string.IsNullOrEmpty(value);
        }

        public override void Set(string key, string value) => _mappedProvider.Data[key] = value;

        public override IEnumerable<string> GetChildKeys(
            IEnumerable<string> earlierKeys,
            string parentPath)
        {
            IConfiguration section = parentPath == null ? _config : _config.GetSection(parentPath);
            IEnumerable<IConfigurationSection> children = section.GetChildren();
            var keys = new List<string>();
            keys.AddRange(children.Select(c => c.Key));
            return keys.Concat(earlierKeys)
                .OrderBy(k => k, ConfigurationKeyComparer.Instance);
        }

        public override void Load()
        {
            _config.GetReloadToken().RegisterChangeCallback(_ => Reload(), null);
            var egress = _config.GetSection("Proxy:Egress").Get<List<ProxyEntry>>() ?? new List<ProxyEntry>();
            var ingress = _config.GetSection("Proxy:Ingress").Get<List<ProxyEntry>>() ?? new List<ProxyEntry>();
            // if (!ingress.Any() && Platform.IsCloudFoundry)
            // {
            //     if (!int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
            //         port = 8080;
            //     ingress.Add(new ProxyEntry()
            //     {
            //         CredentialsId = Credential.DefaultCredentialId,
            //         ListenPort = port,
            //         Routes = new()
            //         {
            //             new Configuration.Route() { Path = "/cloudfoundryapplication/**"}, // cloud foundry actuators are secured by jwt
            //             new Configuration.Route() { Path = "**/*.svc?wsdl", Methods = new() {"GET"}}, // wcf WSDLs
            //             Configuration.Route.Default
            //         }
            //     });
            // }
            _mappedProvider.Data = ingress
                .Union(egress)
                .Select(x => x.ListenPort)
                .Distinct()
                .ToDictionary(port => $"Kestrel:Endpoints:Http_{port}:Url", port => $"http://0.0.0.0:{port}");

        }

        private void Reload()
        {
            Load();
            OnReload();
        }

        private class MappingProvider : MemoryConfigurationProvider
        {
            public new IDictionary<string, string> Data
            {
                get => base.Data;
                set => base.Data = value;
            }
            public MappingProvider(MemoryConfigurationSource source) : base(source)
            {
            }
        }
    }
}