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
        var notification = JsonSerializer.Deserialize<WePayNotification<AccountCapabilitiesUpdatedNotification>>(bodyString);

        if (notification != null)
        {
            var ownerId = notification.Payload.Owner.Id;

            var givtOrganisation = await _context.Organisations
                .Include(x => x.CollectGroups)
                .FirstOrDefaultAsync(x => x.Accounts.First().PaymentProviderId == ownerId.ToString());

            var merchantOnboardingApi = new MerchantOnboardingApi();
            merchantOnboardingApi.Configuration = new Configuration
            {
                ApiKey = new Dictionary<string, string>() {
                    { "App-Id", _configuration.AppId },
                    { "App-Token", _configuration.AppToken }
                },
                BasePath = _configuration.Url,
            };
            
            // Create the account in our database
      
            var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(ownerId.ToString(), "3.0");
            
            foreach (var collectGroup in givtOrganisation.CollectGroups)
            {
                collectGroup.Active = capabilities.Payments.Enabled;
            }

            await _context.SaveChangesAsync();
            
            Logger.Information("C# HTTP trigger function processed a request.");
            SlackLogger.Information($"Account capabilities from account {notification.Payload.Owner.Id} ({givtOrganisation.Name}), Payments : {notification.Payload.Payments.Enabled} , Payouts : {notification.Payload.Payouts.Enabled}");
        }

        return new OkResult();
    }
}