using System;
using System.IO;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth
{
    
    /// <summary>
    /// Provides an instance of Kerberos client which is rebuilt if there are any changes to the kerberos options
    /// </summary>
    public class KerberosClientProvider
    {
        private readonly FileSystemWatcher _fileWatcher = new FileSystemWatcher();
        public KerberosClient Client { get; private set; }

        public KerberosClientProvider(IOptionsMonitor<KerberosOptions> options)
        {
            CreateClient(options.CurrentValue);
            options.OnChange(CreateClient);
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
            _fileWatcher.Changed += (sender, args) => CreateClient(options.CurrentValue);
        }

        private void CreateClient(KerberosOptions options)
        {
            // var krb5Config = Krb5Config.CurrentUser(options.Krb5ConfigPath);
            var krb5Config = Krb5Config.Default();
            if (options.Kdc != null && options.Realm != null)
            {
                krb5Config.Realms[options.Realm].Kdc.Add(options.Kdc);
            }
            if (File.Exists(options.Krb5ConfigPath))
            {
                _fileWatcher.EnableRaisingEvents = true;
                _fileWatcher.Path = Path.GetDirectoryName(options.Krb5ConfigPath);
                _fileWatcher.Filter = Path.GetFileName(options.Krb5ConfigPath);
            }
            else
            {
                _fileWatcher.EnableRaisingEvents = false;
            }
            Client = new KerberosClient(krb5Config);
        }


   

    }
}