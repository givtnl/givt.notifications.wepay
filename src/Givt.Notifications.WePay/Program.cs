using System;
using System.IO;
using Givt.Business.Infrastructure.Factories;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace givt.notifications.wepay
{
    public class Program
    {
        internal class FunctionsConfigurationBuilder : IFunctionsConfigurationBuilder
        {
            public IConfigurationBuilder ConfigurationBuilder { get; }
            public FunctionsConfigurationBuilder(IConfigurationBuilder builder) { ConfigurationBuilder = builder; }
        };

        internal class FunctionsHostBuilder : IFunctionsHostBuilder
        {
            public IServiceCollection Services { get; }
            public FunctionsHostBuilder(IServiceCollection services) { Services = services; }
        }

        public static void Main()
        {
            var startup = new Startup();
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration(builder => startup.ConfigureAppConfiguration(new FunctionsConfigurationBuilder(builder)))
                .ConfigureServices((context, collection) => startup.Configure(new FunctionsHostBuilder(collection)))
                .Build();

            host.Run();
        }
    }
}