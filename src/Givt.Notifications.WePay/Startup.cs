using System;
using System.IO;
using Givt.Business.Infrastructure.Factories;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Integrations.Logging;
using Givt.Integrations.Logging.Loggers;
using Givt.Integrations.Logging.Models;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.Http.Logger;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay;
public class Startup : FunctionsStartup
{
    private IConfiguration Configuration { get; set; }
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddSingleton<ISlackLoggerFactory, SlackLoggerFactory>();

    }

    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        builder.ConfigurationBuilder
            .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("CUSTOMCONNSTR_AzureAppConfig"))
            .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "local.settings.json"), true);
        Configuration = builder.ConfigurationBuilder.Build();
    }
}