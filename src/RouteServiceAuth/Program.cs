using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RouteServiceAuth.Proxy;
using Steeltoe.Common.Configuration;
using Steeltoe.Extensions.Configuration;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Extensions.Configuration.ConfigServer;
using Steeltoe.Extensions.Configuration.Placeholder;

namespace RouteServiceAuth
{
    public class Program
    {
        public static void Main(string[] args)
        {

            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .AddCloudFoundry()
                .ConfigureAppConfiguration(cfg =>
                {
                    // cfg.AddJsonFile("appsettings.CloudFoundry.json");
                    if (Environment.GetEnvironmentVariable("ENABLE_CONFIG_SERVER")?.ToLower() != "false")
                    {
                        cfg = cfg.AddConfigServer();
                    }

                    cfg
                        .AddPlaceholderResolver()
                        .AddProxyConfig();
                })
                .UseStartup<Startup>();
            
        }
        
        
    }
    
    
  
}
