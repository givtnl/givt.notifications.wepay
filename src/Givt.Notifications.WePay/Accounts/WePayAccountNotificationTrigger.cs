using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Accounts;
public class WePayAccountNotificationTrigger: WePayNotificationTrigger
{
    public WePayAccountNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger) : base(loggerFactory, logger)
    {
    }
    
    [Function("WePayAccountNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayAccount>>(bodyString);

        var logMessage = $"Account with id {notification.Payload.Id} has been updated";
        
        SlackLogger.Information(logMessage);
        Logger.Information(logMessage);

        return new OkResult();
    }
}