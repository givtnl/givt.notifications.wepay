using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Notifications.WePay.Infrastructure.AbstractClasses;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.PayoutMethods;

public class WePayPayoutMethodsCreatedNotificationTrigger : WePayNotificationTrigger
{
    public WePayPayoutMethodsCreatedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger,
        WePayNotificationConfiguration notificationConfiguration) : base(loggerFactory, logger, notificationConfiguration)
    {

    }

    // accounts.capabilities.updated
    [Function("WePayPayoutMethodCreatedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult> () =>
        {
            var notification = await WePayNotification<PayoutMethodsResponse>.FromHttpRequestData(req);

            var ownerId = notification.Payload.Owner.Id;

            var msg = $"Payout method created with id: {ownerId}";
            Logger.Information(msg);
            SlackLogger.Information(msg);

            return new OkResult();
        });
    }
}