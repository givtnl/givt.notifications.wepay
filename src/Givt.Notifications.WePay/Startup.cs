using System;
using System.IO;
using Givt.Business.Infrastructure.Factories;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Integrations.Logging.Loggers;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.Http.Logger;
using MediatR;
using Givt.Business.Infrastructure.Behaviors;
using Givt.Business.Payments.Commands;
using Givt.Integrations.Email.Postmark.Configuration;
using Givt.Business.Infrastructure.Configuration;
using Givt.Integrations.Interfaces;
using Givt.Integrations.Email.Postmark;
using Givt.Notifications.WePay.Infrastructure.Wrappers;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay;
public class Startup : FunctionsStartup
{
    private IConfiguration Configuration { get; set; }
    
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddSingleton<ISlackLoggerFactory, SlackLoggerFactory>();
        builder.Services.AddSingleton<ILog, LogitHttpLogger>(x => new LogitHttpLogger(Configuration["LogitConfiguration:Tag"], Configuration["LogitConfiguration:Key"]));
        builder.Services.AddSingleton(Configuration.GetSection(nameof(WePayConfiguration)).Get<WePayConfiguration>());
        builder.Services.AddSingleton(Configuration.GetSection(nameof(WePayNotificationConfiguration)).Get<WePayNotificationConfiguration>());
        builder.Services.AddSingleton<WePayGeneratedConfigurationWrapper>();
        builder.Services.AddDbContextPool<GivtDatabaseContext>(dbContextOptions => dbContextOptions.UseSqlServer(Configuration.GetConnectionString("GivtDbConnection")));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetrySqlExceptionBehavior<,>));
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        builder.Services.AddMediatR(typeof(CreateCollectGroupPaymentCommand).Assembly);
        builder.Services.Configure<PostmarkOptions>(Configuration.GetSection(nameof(PostmarkOptions)))
                        .Configure<PostmarkOptions>(opts => opts.EnvironmentName = Configuration.GetSection(nameof(GivtConfiguration))
                            .Get<GivtConfiguration>().DeploymentType == "Debug" ? "Development" : "Production");
        builder.Services.AddSingleton<IEmailService, PostmarkEmailService>();
    }

    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        builder.ConfigurationBuilder
            .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("CUSTOMCONNSTR_AzureAppConfig"))
            .AddJsonFile(Path.Combine(Environment.CurrentDirectory, "local.settings.json"), true);
        Configuration = builder.ConfigurationBuilder.Build();
    }
}