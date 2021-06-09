using System;
using System.Collections.Concurrent;
using System.IO;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Kerberos.NET.Credentials;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth
{
    
    /// <summary>
    /// Provides lazy creation of Kerberos client tied to credentials using them.
    /// Clients are rebuilt if there are any changes to the kerberos options
    /// </summary>
    public class KerberosClientProvider
    {
        private readonly IOptionsMonitor<KerberosOptions> _options;
        private readonly FileSystemWatcher _fileWatcher = new();
        ConcurrentDictionary<Credential, KerberosClient> _clientsCache = new();
        private Krb5Config _krb5config = Krb5Config.Default();


        public KerberosClientProvider(IOptionsMonitor<KerberosOptions> options)
        {
            _options = options;
            options.OnChange(CreateKrb5Config);
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileWatcher.Changed += (sender, args) => CreateKrb5Config(options.CurrentValue);
        }

        public KerberosClient GetClient(Credential credential)
        {
            var client = _clientsCache.GetOrAdd(credential, credential => CreateKerberosClient());
            return client;
        }

        private void CreateKrb5Config(KerberosOptions options)
        {
            if (File.Exists(options.Krb5ConfigPath))
            {
                _krb5config = Krb5Config.Parse(File.ReadAllText(options.Krb5ConfigPath));
                _fileWatcher.EnableRaisingEvents = true;
                _fileWatcher.Path = Path.GetDirectoryName(options.Krb5ConfigPath)!;
                _fileWatcher.Filter = Path.GetFileName(options.Krb5ConfigPath)!;
            }
            else
            {
                _fileWatcher.EnableRaisingEvents = false;
            }

            _clientsCache.Clear();
        }

        private KerberosClient CreateKerberosClient()
        {
            
            var options = _options.CurrentValue;
            var krb5Config = Krb5Config.Parse(_krb5config.Serialize()); // clone
            if (options.Kdc != null && options.Realm != null)
            {
                krb5Config.Realms[options.Realm].Kdc.Add(options.Kdc);
            }
            var client = new KerberosClient(krb5Config);
            return client;
        }
    }
}