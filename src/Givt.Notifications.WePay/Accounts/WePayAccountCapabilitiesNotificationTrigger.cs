using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;

namespace Givt.Notifications.WePay.Accounts;

public class WePayAccountCapabilitiesNotificationTrigger: WePayNotificationTrigger
{
    public WePayAccountCapabilitiesNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger) : base(loggerFactory, logger)
    {
    }
    
    [Function("WePayAccountCapabilitiesNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    { 
        
        var bodyString = await req.ReadAsStringAsync();
        var notification = JsonConvert.DeserializeObject<AccountCapabilitiesUpdatedNotification>(bodyString);

        Logger.Information("C# HTTP trigger function processed a request.");
        SlackLogger.Information($"Account capabilities from account {notification.Owner.Id}, Payments : {notification.Payments.Enabled} , Payouts : {notification.Payouts.Enabled}");

        return new OkResult();
    }
}