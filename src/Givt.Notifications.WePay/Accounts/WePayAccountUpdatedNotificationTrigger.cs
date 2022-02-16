using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Api;
using WePay.Clear.Generated.Client;

namespace Givt.Notifications.WePay.Accounts;

public class WePayAccountUpdatedNotificationTrigger: WePayNotificationTrigger
{
    private readonly WePayConfiguration _configuration;
    private readonly GivtDatabaseContext _context;

    public WePayAccountUpdatedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayConfiguration configuration, GivtDatabaseContext context) : base(loggerFactory, logger)
    {
        _configuration = configuration;
        _context = context;
    }
    
    // topics : accounts.updated
    [Function("WePayAccountUpdatedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonSerializer.Deserialize<WePayNotification<WePayAccount>>(bodyString);

        if (notification != null)
        {
            var ownerPaymentProviderId = notification.Payload.Owner.Id;

            var givtOrganisation = _context.Organisations
                .Include(x => x.CollectGroups)
                .FirstOrDefault(x => x.PaymentProviderIdentification == ownerPaymentProviderId.ToString());

            if (givtOrganisation != null)
            {
                // TODO what about the httpclient? @maarten
                var merchantOnboardingApi = new MerchantOnboardingApi();
                merchantOnboardingApi.Configuration = new Configuration
                {
                    ApiKey = new Dictionary<string, string>() {
                        { "App-Id", _configuration.AppId },
                        { "App-Token", _configuration.AppToken }
                    },
                    BasePath = _configuration.Url,
                };
                
                var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(notification.Payload.Id.ToString(), "3.0");
                
                foreach (var collectGroup in givtOrganisation.CollectGroups)
                {
                    collectGroup.Active = capabilities.Payments.Enabled;
                }
                
                await _context.SaveChangesAsync();

                var logMessage = $"Account with id {notification.Payload.Id} from owner with id {notification.Payload.Owner.Id} ({givtOrganisation.Name}) has been updated, payments: {capabilities.Payments.Enabled}";

                SlackLogger.Information(logMessage);
                Logger.Information(logMessage);
            }

        }
        return new OkResult();
        
    }
}