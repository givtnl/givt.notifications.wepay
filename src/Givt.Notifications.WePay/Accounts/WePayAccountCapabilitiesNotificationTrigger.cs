using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Business.Organisations.Commands;
using Givt.DatabaseAccess;
using Givt.DBModels.Domain;
using Givt.Notifications.WePay.Models;
using Givt.Notifications.WePay.Wrappers;
using Givt.PaymentProviders.V2.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Api;
using WePay.Clear.Generated.Client;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Accounts;

public class WePayAccountCapabilitiesNotificationTrigger: WePayNotificationTrigger
{
    private readonly Configuration _configuration;
    private readonly GivtDatabaseContext _context;

    public WePayAccountCapabilitiesNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger,  WePayGeneratedConfigurationWrapper configuration, WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory, logger, notificationConfiguration)
    {
        _configuration = configuration.Configuration;
        _context = context;
    }
    
    // accounts.capabilities.updated
    [Function("WePayAccountCapabilitiesNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    { 
        
        var bodyString = await req.ReadAsStringAsync();
        var notification = JsonConvert.DeserializeObject<WePayNotification<AccountsCapabilitiesResponse>>(bodyString);

        if (notification != null)
        {
            var ownerId = notification.Payload.Owner.Id;

            // request the organistion which is owner of the current account with incoming account id
            var givtOrganisation = await _context.Organisations
                .Include(x => x.CollectGroups)
                .Include(x => x.Accounts)
                .ThenInclude(x => x.BankAccountMandates)
                .FirstOrDefaultAsync(x => x.Accounts.First().PaymentProviderId == ownerId);

            if (givtOrganisation == null)
            {
                SlackLogger.Information($"No organisation found for account with id {ownerId}");
                return new OkResult();
            }

            var merchantOnboardingApi = new MerchantOnboardingApi(_configuration);
            
            var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(ownerId, "3.0");
            var currentBankAccount = givtOrganisation.Accounts.FirstOrDefault(account => account.PaymentProviderId == ownerId);

            if (capabilities.Payouts.Enabled && currentBankAccount != null && currentBankAccount.BankAccountMandates.All(x => x.Status != "closed.completed"))
            {
                var payoutMethods = await merchantOnboardingApi.GetacollectionofpayoutmethodsAsync("3.0", ownerId:givtOrganisation.PaymentProviderIdentification, type:TypeGetacollectionofpayoutmethods.PayoutBankUs);
                var payoutMethodToUse = payoutMethods.Results.FirstOrDefault();

                if (payoutMethodToUse != null)
                {
                    givtOrganisation.Accounts.FirstOrDefault()?.BankAccountMandates.Add( new DomainBankAccountMandate
                    {
                        Reference = payoutMethodToUse.Id,
                        Status = "closed.completed",
                        CreationDateTime = DateTimeOffset.FromUnixTimeSeconds(payoutMethodToUse.CreateTime).DateTime, // Datetime when the payoutmethod has been added in the WePay system
                        StartDateTime = DateTime.UtcNow // Time when created in our system and we started using it 
                    });
                    SlackLogger.Information($"Mandate for org with id {givtOrganisation.Id} has been added with reference {payoutMethods.Results.FirstOrDefault()?.Id} and name {payoutMethods.Results.FirstOrDefault()?.Nickname} ");
                }
                Logger.Information(payoutMethods.ToString());
            }
            
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