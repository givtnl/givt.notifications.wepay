using System.Linq;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payments;

public class WePayPaymentDisputeResolvedNotificationTrigger : WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _context;

    public WePayPaymentDisputeResolvedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger,
        WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory,
        logger, notificationConfiguration)
    {
        _context = context;
    }

    [Function("WePayPaymentDisputeResolvedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var notification =
            JsonConvert.DeserializeObject<WePayNotification<ResultDisputesResponse>>(await req.ReadAsStringAsync() ??
                string.Empty);

        var donations = await _context.Transactions.Where(x => x.PaymentProviderId == notification.Payload.Payment.Id)
            .ToListAsync();

        if (donations.Any())
        {
            var message = $"A dispute has been resolved for transaction with id {notification.Payload.Payment.Id}";
            SlackLogger.Information(message);
            Logger.Information(message);
        }
        else
        {
            var message =
                $"A dispute has been resolved for transaction with id {notification.Payload.Payment.Id} but no donations were found in our system";
            SlackLogger.Error(message);
            Logger.Information(message);
        }

        return new OkResult();

    }
}