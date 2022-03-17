using System;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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

    protected async Task<IActionResult> WithExceptionHandler(Func<Task<IActionResult>> func)
    {
        try
        {
            return await func.Invoke();
        } catch (Exception e)
        {
            Logger.Error($"Received error while handling function" + JsonConvert.SerializeObject(new
            {
                Exception = e.ToString(),
                StackTrace = e.StackTrace
            }, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            }));
            var innerException = e.InnerException;
            while (innerException != null) 
            {
                Logger.Error($"Continued innerException of previous error" + JsonConvert.SerializeObject(new
                {
                    Exception = innerException.ToString(),
                    StackTrace = innerException.StackTrace
                }, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                }));
                innerException = innerException.InnerException;
            }
        }
        return new OkResult();
    } 
}