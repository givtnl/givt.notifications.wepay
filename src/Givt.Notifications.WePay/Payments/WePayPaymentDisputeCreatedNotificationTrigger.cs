using System.Linq;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Infrastructure.AbstractClasses;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payments;

public class WePayPaymentDisputeCreatedNotificationTrigger : WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _context;

    public WePayPaymentDisputeCreatedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory, logger, notificationConfiguration)
    {
        _context = context;
    }
    
    [Function("WePayPaymentDisputeCreatedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult>() =>
        {
            var notification = await WePayNotification<ResultDisputesResponse>.FromHttpRequestData(req);
            
            var donations = await _context.Transactions.Where(x => x.PaymentProviderId == notification.Payload.Payment.Id).ToListAsync();

            if (donations.Any())
            {
                var message = $"A dispute has been created for transaction with id {notification.Payload.Payment.Id} for amount {notification.Payload.Amount}";
                SlackLogger.Information(message);
                Logger.Information(message);
            }
            else
            {
                var message = $"A dispute has been created for transaction with id {notification.Payload.Payment.Id} but no donations were found in our system";
                SlackLogger.Error(message);
                Logger.Error(message);
            }
        
            return new OkResult();
        });
    }
}