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

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Payments;
public class WePayPaymentNotificationTrigger
{
    private readonly ISlackLoggerFactory _loggerFactory;

    public WePayPaymentNotificationTrigger(ISlackLoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    [Function("WePayPaymentNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        var log = _loggerFactory.Create();

        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayPayment>>(bodyString);

        log.Information($"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}");

        return new OkResult();
    }
}