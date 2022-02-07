using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;

namespace givt.notifications.wepay;

public static class WepayPaymentNotificationTrigger
{
    [Function("WepayPaymentNotifcationTrigger")]
    public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        return new OkObjectResult($"Hello");
    }
}