using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.DBModels.Domain;
using Givt.Models.Enums;
using Givt.Notifications.WePay.Models;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Api;
using WePay.Clear.Generated.Client;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Accounts;
public class WePayAccountNotificationTrigger: WePayNotificationTrigger
{
    private readonly WePayConfiguration _configuration;
    private readonly GivtDatabaseContext _context;

    public WePayAccountNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayConfiguration configuration, GivtDatabaseContext context) : base(loggerFactory, logger)
    {
        _configuration = configuration;
        _context = context;
    }
    
    [Function("WePayAccountNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayAccount>>(bodyString);

        if (notification != null)
        {
            var ownerPaymentProviderID = notification.Payload.Owner.Id;

            var givtOrganisation = _context.Organisations.FirstOrDefault(x => x.PaymentProviderId == ownerPaymentProviderID.ToString());

            if (givtOrganisation != null)
            {
                var account = new DomainAccount
                {
                    Active = true,
                    Created = DateTime.UtcNow,
                    OrganisationId = givtOrganisation.Id,
                    PaymentProviderId = notification.Payload.Id.ToString(),
                    Primary = true,
                    Verified = true,
                    AccountName = notification.Payload.Name,
                    PaymentProvider = PaymentProvider.WePay,
                    PaymentType = PaymentType.CreditCard,
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
        
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
            
                // Create the account in our database
      
                var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(notification.Payload.Id.ToString(), "3.0");

                var logMessage = $"Account with id {notification.Payload.Id} from owner with id {notification.Payload.Owner.Id} has been updated, payments: {capabilities.Payments.Enabled}";

        
                SlackLogger.Information(logMessage);
                Logger.Information(logMessage);
            }

        }
       
        return new OkResult();
    }
}