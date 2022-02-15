using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;

namespace Givt.Notifications.WePay.Accounts;

public class WePayAccountCapabilitiesNotificationTrigger: WePayNotificationTrigger
{
    private readonly WePayConfiguration _configuration;
    private readonly GivtDatabaseContext _context;

    public WePayAccountCapabilitiesNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger,  WePayConfiguration configuration, GivtDatabaseContext context) : base(loggerFactory, logger)
    {
        _configuration = configuration;
        _context = context;
    }
    
    // accounts.capabilities.updated
    [Function("WePayAccountCapabilitiesNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    { 
        
        var bodyString = await req.ReadAsStringAsync();
        var notification = JsonConvert.DeserializeObject<WePayNotification<AccountCapabilitiesUpdatedNotification>>(bodyString);

        if (notification != null)
        {
            var ownerId = notification.Payload.Owner.Id;

            var givtOrganisation = await _context.Organisations
                .Include(x => x.Accounts)
                .Include(x => x.CollectGroups)
                .FirstOrDefaultAsync(x => x.PaymentProviderIdentification == ownerId.ToString());

            foreach (var collectGroup in givtOrganisation.CollectGroups)
            {
                collectGroup.Active = notification.Payload.Payments.Enabled;
            }

            await _context.SaveChangesAsync();
            
            Logger.Information("C# HTTP trigger function processed a request.");
            SlackLogger.Information($"Account capabilities from account {notification.Payload.Owner.Id} ({givtOrganisation.Name}), Payments : {notification.Payload.Payments.Enabled} , Payouts : {notification.Payload.Payouts.Enabled}");
        }

        return new OkResult();
    }
}