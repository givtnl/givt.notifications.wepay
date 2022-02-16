using Givt.Business.Infrastructure.Interfaces;
using Serilog;
using Serilog.Sinks.Http.Logger;

namespace Givt.Notifications.WePay;

public abstract class WePayNotificationTrigger
{
    internal readonly ILogger SlackLogger;
    internal readonly ILog Logger;

    public WePayNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger)
    {
        SlackLogger = loggerFactory.Create();
        Logger = logger;
    }
}