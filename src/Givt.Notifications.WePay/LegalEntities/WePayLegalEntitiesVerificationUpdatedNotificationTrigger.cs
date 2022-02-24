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

namespace Givt.Notifications.WePay.LegalEntities;

public class WePayLegalEntitiesVerificationUpdatedNotificationTrigger: WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _context;

    public WePayLegalEntitiesVerificationUpdatedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory, logger, notificationConfiguration)
    {
        _context = context;
    }
    
    [Function("WePayLegalEntitiesVerificationUpdatedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult>() =>
        {
            var notification = await WePayNotification<LegalEntitiesVerificationsResponse>.FromHttpRequestData(req);
            var organisation = await _context.Organisations.FirstOrDefaultAsync(x => x.PaymentProviderIdentification == notification.Payload.Owner.Id);

            if (organisation == default)
            {
                SlackLogger.Error($"No organisation found for account with id {notification.Payload.Owner.Id}");
                Logger.Error($"No organisation found for account with id {notification.Payload.Owner.Id}");
                return new OkResult();
            }

            if (notification.Payload.EntityVerification.CurrentIssues.Any())
            {
                var message = $"The legal entity verification for {organisation.Name} with legal entity id {notification.Payload.Owner.Id} has been updated with issues";
                SlackLogger.Information(message);
                Logger.Information(message);
            }

            return new OkResult();
            
        });
    }
}