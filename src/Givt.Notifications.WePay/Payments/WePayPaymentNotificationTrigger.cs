using System;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Payments;
public class WePayPaymentNotificationTrigger: WePayNotificationTrigger
{

    [Function("WePayPaymentNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayPayment>>(bodyString);

        var logMessage = $"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}";
        
        SlackLogger.Information(logMessage);
        Logger.Information(logMessage);

        return new OkResult();
    }

    public WePayPaymentNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger) : base(loggerFactory, logger)
    {
    }
}