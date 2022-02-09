using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Accounts;
public class WePayAccountNotificationTrigger
{
    private readonly ISlackLoggerFactory _loggerFactory;

    public WePayAccountNotificationTrigger(ISlackLoggerFactory loggerFactory)
    {        
        _loggerFactory = loggerFactory;
    }
    
    [Function("WePayAccountNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        var slackLogger = _loggerFactory.Create();
        var logger = context.GetLogger("HttpFunction");
        logger.LogInformation("Logging the log out of it");

        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayAccount>>(bodyString);

        slackLogger.Information($"Account with id {notification.Payload.Id} has been updated");

        return new OkResult();
    }
}