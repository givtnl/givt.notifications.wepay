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

            var currentBankAccount = givtOrganisation.Accounts.First(account => account.PaymentProviderId == ownerId);

            if (notification.Payload.Payments.Enabled)
            {
                //Handle when payments became active but the account is inactive
                if (!currentBankAccount.Active)
                    currentBankAccount.Active = true;
                foreach (var collectGroup in givtOrganisation.CollectGroups)
                    collectGroup.Active = true;

            } else if (!notification.Payload.Payments.Enabled && currentBankAccount.Active)
            {
                //Handle when payments became inactive but the account is active
                currentBankAccount.Active = false;
                foreach (var collectGroup in givtOrganisation.CollectGroups)
                    collectGroup.Active = false;
            }

            if (notification.Payload.Payouts.Enabled && !currentBankAccount.BankAccountMandates.Any(y => y.Status == "closed.completed"))
            {
                //Handle when payouts become active but there's no mandate yet
                var payoutMethods = await merchantOnboardingApi.GetacollectionofpayoutmethodsAsync("3.0", ownerId: givtOrganisation.PaymentProviderIdentification, type: TypeGetacollectionofpayoutmethods.PayoutBankUs);
                var payoutMethodToUse = payoutMethods.Results.FirstOrDefault();

                if (payoutMethodToUse != default)
                {
                    currentBankAccount.DetailLineOne = $"****...{payoutMethodToUse.PayoutBankUs.LastFour}";
                    currentBankAccount?.BankAccountMandates.Add(new DomainBankAccountMandate
                    {
                        Reference = payoutMethodToUse.Id,
                        Status = "closed.completed",
                        CreationDateTime = DateTimeOffset.FromUnixTimeSeconds(payoutMethodToUse.CreateTime).DateTime, // Datetime when the payoutmethod has been added in the WePay system
                        StartDateTime = DateTime.UtcNow // Time when created in our system and we started using it 
                    });
                    SlackLogger.Information($"Mandate for org with id {givtOrganisation.Id} has been added with reference {payoutMethods.Results.FirstOrDefault()?.Id} and name {payoutMethods.Results.FirstOrDefault()?.Nickname} ");
                    Logger.Information($"Mandate for org with id {givtOrganisation.Id} has been added with reference {payoutMethods.Results.FirstOrDefault()?.Id} and name {payoutMethods.Results.FirstOrDefault()?.Nickname} ");
                } else
                    throw new InvalidRequestException($"Payouts for {currentBankAccount.PaymentProviderId} became active but payment method isn't found!");

            } else if (!notification.Payload.Payouts.Enabled && currentBankAccount.BankAccountMandates.Any(y => y.Status == "closed.completed"))
            {
                //Handle when payouts become inactive but there's an active mandate
                var mandate = currentBankAccount.BankAccountMandates.First(x => x.Status == "closed.completed");
                mandate.Status = "closed.revoked";
                mandate.EndDateTime = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            
            var msg = $"Account capabilities from account {notification.Payload.Owner.Id} ({givtOrganisation.Name}), Payments : {notification.Payload.Payments.Enabled} , Payouts : {notification.Payload.Payouts.Enabled}";
            Logger.Information(msg);
            SlackLogger.Information(msg);

            return new OkResult();
        });
    }
}