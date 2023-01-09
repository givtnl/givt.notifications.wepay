using System;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
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
            var organisationId = Guid.Parse((notification.Payload.CustomData as JObject)["GivtOrganisationId"].ToString());
            var organisation = await _context.Organisations.FirstOrDefaultAsync(x => x.Id == organisationId);

            if (organisation == default)
            {
                SlackLogger.Error($"Legal entity created with id {notification.Payload.Id} but organisation with Id {organisationId} not found");
                Logger.Error($"Legal entity created with id {notification.Payload.Id} but organisation with Id {organisationId} not found");
                return new OkResult();
            }

            organisation.PaymentProviderIdentification = notification.Payload.Id;
            await _context.SaveChangesAsync();

            SlackLogger.Information($"Legal entity created with id {notification.Payload.Id} and Givt's organisation id {organisationId}");
            Logger.Information($"Legal entity created with id {notification.Payload.Id} and Givt's organisation id {organisationId}");

            return new OkResult();
        });
    }
}
