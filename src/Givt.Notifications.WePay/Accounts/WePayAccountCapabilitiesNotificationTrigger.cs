using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.DBModels.Domain;
using Givt.Integrations.Interfaces;
using Givt.Models.Exceptions;
using Givt.Notifications.WePay.Accounts.Models;
using Givt.Notifications.WePay.Infrastructure.AbstractClasses;
using Givt.Notifications.WePay.Infrastructure.Wrappers;
using Givt.Notifications.WePay.Models;
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
    private readonly IEmailService _emailService;

    public WePayAccountCapabilitiesNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger,  WePayGeneratedConfigurationWrapper configuration, 
        WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context, IEmailService emailService) : base(loggerFactory, logger, notificationConfiguration)
    {
        _configuration = configuration.Configuration;
        _context = context;
        _emailService = emailService;
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

            var currentBankAccount = givtOrganisation.Accounts.First(account => account.PaymentProviderId == ownerId);

            var merchantOnboardingApi = new MerchantOnboardingApi(_configuration);
            merchantOnboardingApi.ApiClient.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

            Func<List<CurrentIssue>, Task> sendAccountIssuesEmail = async Task (List<CurrentIssue> issues) =>
            {
                var legalEntity = await merchantOnboardingApi.GetalegalentityAsync(
                    id: givtOrganisation.PaymentProviderIdentification,
                    apiVersion: "3.0",
                    uniqueKey: Guid.NewGuid().ToString()
                );
                var email = new OrganizationAccountProblemEmail("US","en") { AccountIssues = JsonConvert.SerializeObject(issues) };
                await _emailService.SendTemplateMail(legalEntity.Controller.Email, "OrganizationAccountProblem", email);
            };

            if (notification.Payload.Payments.Enabled)
            {
                //Handle when payments became active but the account is inactive
                currentBankAccount.Active = true;
                foreach (var collectGroup in givtOrganisation.CollectGroups)
                    collectGroup.Active = true;

            } else if (!notification.Payload.Payments.Enabled && currentBankAccount.Active)
            {
                //Handle when payments became inactive but the account is active
                currentBankAccount.Active = false;
                foreach (var collectGroup in givtOrganisation.CollectGroups)
                    collectGroup.Active = false;

                SlackLogger.Error($"The WePay account {currentBankAccount.PaymentProviderId} has issues");

                //This is also the place where we let the account holder know about the issues
                await sendAccountIssuesEmail.Invoke(notification.Payload.Payments.CurrentIssues);
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

                //This is also the place where we let the account holder know about the issues
                await sendAccountIssuesEmail.Invoke(notification.Payload.Payouts.CurrentIssues);
            }

            await _context.SaveChangesAsync();
            
            var msg = $"Account capabilities from account {notification.Payload.Owner.Id} ({givtOrganisation.Name}), Payments : {notification.Payload.Payments.Enabled} , Payouts : {notification.Payload.Payouts.Enabled}";
            Logger.Information(msg);
            SlackLogger.Information(msg);

            return new OkResult();
        });
    }
}