using System;
using System.IO;
using Givt.Business.Infrastructure.Configuration;
using Givt.Business.Infrastructure.Factories;
using Givt.Business.Infrastructure.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace givt.notifications.wepay
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration(builder =>
                {
                    builder
                        .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("CUSTOMCONNSTR_AzureAppConfig"))
                        .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "local.settings.json"), true);
                })
                .ConfigureServices(s =>
                {
                    s.AddSingleton<ISlackLoggerFactory, SlackLoggerFactory>();
                })
                .Build();
 
            host.Run();
        }
    }
}