using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            var webHost = WebHost.CreateDefaultBuilder(args)
                .AddConfigServer()
                .UseStartup<Startup>()
                .Build();
            var addressFeature = webHost.ServerFeatures.Get<IServerAddressesFeature>();
            var config = webHost.Services.GetRequiredService<IConfiguration>();
            var proxyMap = new ProxyMap().BindFrom(config);

            var urls = proxyMap.Entries
                .Select(x => $"http://0.0.0.0:{x.Key}")
                .Union(addressFeature.Addresses)
                .ToArray();
            
            return WebHost.CreateDefaultBuilder(args)
                .AddCloudFoundry()
                .AddConfigServer()
                .UseUrls(urls)
                .UseStartup<Startup>();
            
        }
    }
}
