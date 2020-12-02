using System;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Steeltoe.Extensions.Configuration.CloudFoundry;
using Steeltoe.Extensions.Configuration.ConfigServer;

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
            // var webHost = WebHost.CreateDefaultBuilder(args)
            //     .AddConfigServer()
            //     .UseStartup<Startup>()
            //     .Build();

            
            // var addressFeature = webHost.ServerFeatures.Get<IServerAddressesFeature>();
            // var config = webHost.Services.GetRequiredService<IConfiguration>();
            // var proxyMap = new ProxyMap().BindFrom(config);
            var serviceCollection = new ServiceCollection();
            var environmentName = Environment.GetEnvironmentVariable("Hosting:Environment") ?? Environment.GetEnvironmentVariable("ASPNET_ENV");
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile("appsettings." + environmentName + ".json", true, true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .AddConfigServer()
                .Build();
            var container = serviceCollection
                .AddSidecarOptions(config)
                .BuildServiceProvider();
            var appConfig = container.GetRequiredService<IOptionsSnapshot<KerberosProxyOptions>>().Value;
            container.Dispose();
            
            
            
            var urls = 
                appConfig.Egress.Keys
                .Union(appConfig.Ingress.Keys)
                .Select(port => $"http://0.0.0.0:{port}")
                .ToArray();
            
            return WebHost.CreateDefaultBuilder(args)
                .AddCloudFoundry()
                .AddConfigServer()
                .UseUrls(urls)
                .ConfigureServices((ctx, svc) =>
                {
                    svc.AddSidecarOptions(ctx.Configuration);
                })
                .UseStartup<Startup>();
            
        }
    }
}
