using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;

namespace Givt.Notifications.WePay;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
public class WePayAccountsNotificationTrigger
{
    private readonly ISlackLoggerFactory _loggerFactory;

    public WePayAccountsNotificationTrigger(ISlackLoggerFactory loggerFactory)
    {        
        _loggerFactory = loggerFactory;
    }
    
    [Function("WePayPaymentNotifcationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        var log = _loggerFactory.Create();

        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayAccount>>(bodyString);

        log.Information($"Account with id {notification.Payload.Id} has been updated");

        return new OkResult();
    }
}