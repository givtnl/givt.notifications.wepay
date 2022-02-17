using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Serilog;
using Serilog.Sinks.Http.Logger;

namespace Givt.Notifications.WePay;

public abstract class WePayNotificationTrigger
{
    internal readonly ILogger SlackLogger;
    internal readonly ILog Logger;

    public WePayNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration)
    {
        SlackLogger = loggerFactory.Create(notificationConfiguration.SlackChannel, notificationConfiguration.SlackWebHook);
        Logger = logger;
    }
}