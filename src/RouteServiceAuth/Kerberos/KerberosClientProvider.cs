using System.Collections.Concurrent;
using System.IO;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Microsoft.Extensions.Options;
using RouteServiceAuth.Proxy.Configuration;

namespace RouteServiceAuth.Kerberos
{
    /// <summary>
    /// Provides lazy creation of Kerberos client tied to credentials using them.
    /// Clients are rebuilt if there are any changes to the kerberos options
    /// </summary>
    public class KerberosClientProvider : IKerberosClientProvider
    {
        private readonly IOptionsMonitor<KerberosOptions> _options;
        private readonly FileSystemWatcher _fileWatcher = new();
        readonly ConcurrentDictionary<Secret, KerberosClient> _clientsCache = new();
        private Krb5Config _krb5Config = Krb5Config.Default();


        public KerberosClientProvider(IOptionsMonitor<KerberosOptions> options)
        {
            _options = options;
            options.OnChange(CreateKrb5Config);
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileWatcher.Changed += (_, _) => CreateKrb5Config(options.CurrentValue);
        }

        public KerberosClient GetClient(Secret secret)
        {
            var client = _clientsCache.GetOrAdd(secret, _ => CreateKerberosClient());
            return client;
        }

        private void CreateKrb5Config(KerberosOptions options)
        {
            if (File.Exists(options.Krb5ConfigPath))
            {
                _krb5Config = Krb5Config.Parse(File.ReadAllText(options.Krb5ConfigPath!));
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
            var krb5Config = Krb5Config.Parse(_krb5Config.Serialize()); // clone
            if (options.Kdc != null && options.Realm != null)
            {
                krb5Config.Realms[options.Realm].Kdc.Add(options.Kdc);
            }
            var client = new KerberosClient(krb5Config);
            return client;
        }
    }
}