using System.Collections.Generic;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.LegalEntities;

public class WePayLegalEntitiesCreatedNotificationTrigger : WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _context;

    public WePayLegalEntitiesCreatedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory, logger, notificationConfiguration)
    {
        _context = context;
    }
    
    [Function("WePayLegalEntitiesCreatedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult>() =>
        {
            var notification = await WePayNotification<LegalEntitiesResponse>.FromHttpRequestData(req);
            var organisationId = (notification.Payload.CustomData as Dictionary<string, string>)["OrganisationId"] ;
            var organisation = await _context.Organisations.FirstOrDefaultAsync(x => x.PaymentProviderIdentification == organisationId);

            if (organisation == default)
            {
                SlackLogger.Error($"Legal entity created with id {notification.Payload.Id} but organisation with Id {organisationId} not found");
                Logger.Error($"Legal entity created with id {notification.Payload.Id} but organisation with Id {organisationId} not found");
                return new OkResult();
            }

            organisation.PaymentProviderIdentification = notification.Payload.Id;
            await _context.SaveChangesAsync();

            return new OkResult();
        });
    }
}
