using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Business.Organisations.Commands;
using Givt.DatabaseAccess;
using Givt.DBModels.Domain;
using Givt.Models.Exceptions;
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
        return await WithExceptionHandler(async Task<IActionResult> () =>
        {
            var notification = await WePayNotification<AccountsCapabilitiesResponse>.FromHttpRequestData(req);

            var ownerId = notification.Payload.Owner.Id;

            // request the organistion which is owner of the current account with incoming account id
            var givtOrganisation = await _context.Organisations
                .Include(x => x.CollectGroups)
                .Include(x => x.Accounts)
                .ThenInclude(x => x.BankAccountMandates)
                .FirstOrDefaultAsync(x => x.Accounts.Any(y => y.PaymentProviderId == ownerId));

            if (givtOrganisation == default)
                throw new InvalidRequestException(nameof(ownerId), ownerId);

            var merchantOnboardingApi = new MerchantOnboardingApi(_configuration);

            var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(ownerId, "3.0");
            var currentBankAccount = givtOrganisation.Accounts.FirstOrDefault(account => account.PaymentProviderId == ownerId);

            if (notification.Payload.Payments.Enabled && !givtOrganisation.Accounts.First(x => x.PaymentProviderId == ownerId).Active)
            {
                //Handle when payments became active but the account is inactive
            } else if (!notification.Payload.Payments.Enabled && givtOrganisation.Accounts.First(x => x.PaymentProviderId == ownerId).Active)
            {
                //Handle when payments became inactive but the account is active
            }

            if (notification.Payload.Payouts.Enabled && !givtOrganisation.Accounts.Any(x => x.Active && x.BankAccountMandates.Any(y => y.Status == "closed.completed")))
            {
                //Handle when payouts become active but there's no mandate yet
            } else if (!notification.Payload.Payouts.Enabled && givtOrganisation.Accounts.Any(x => x.Active && x.BankAccountMandates.Any(y => y.Status == "closed.completed")))
            {
                //Handle when payouts become inactive but there's an active mandate
            }


            if (capabilities.Payouts.Enabled && currentBankAccount != null && currentBankAccount.BankAccountMandates.All(x => x.Status != "closed.completed"))
            {
                var payoutMethods = await merchantOnboardingApi.GetacollectionofpayoutmethodsAsync("3.0", ownerId: givtOrganisation.PaymentProviderIdentification, type: TypeGetacollectionofpayoutmethods.PayoutBankUs);
                var payoutMethodToUse = payoutMethods.Results.FirstOrDefault();

                if (payoutMethodToUse != null)
                {
                    givtOrganisation.Accounts.FirstOrDefault()?.BankAccountMandates.Add(new DomainBankAccountMandate
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
            var msg = $"Account capabilities from account {notification.Payload.Owner.Id} ({givtOrganisation.Name}), Payments : {notification.Payload.Payments.Enabled} , Payouts : {notification.Payload.Payouts.Enabled}";
            Logger.Information(msg);
            SlackLogger.Information(msg);

            return new OkResult();
        });
    }
}