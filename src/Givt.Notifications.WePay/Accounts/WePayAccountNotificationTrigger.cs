using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.DBModels.Domain;
using Givt.Models.Enums;
using Givt.Models.Exceptions;
using Givt.Notifications.WePay.Models;
using Givt.Notifications.WePay.Wrappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Api;
using WePay.Clear.Generated.Client;
using WePay.Clear.Generated.Model;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Accounts;
public class WePayAccountNotificationTrigger: WePayNotificationTrigger
{
    private readonly Configuration _configuration;
    private readonly GivtDatabaseContext _context;

    public WePayAccountNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayGeneratedConfigurationWrapper configuration, 
        WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context) : base(loggerFactory, logger, notificationConfiguration)
    {
        _configuration = configuration.Configuration;
        _context = context;
    }


    // topics : accounts.created
    [Function("WePayAccountNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult> () =>
        {
            var notification = await WePayNotification<AccountsResponse>.FromHttpRequestData(req);
            var ownerPaymentProviderId = notification.Payload.Owner.Id;

            var givtOrganisation = _context.Organisations
                .Include(x => x.CollectGroups)
                .FirstOrDefault(x => x.PaymentProviderIdentification == ownerPaymentProviderId.ToString());

            if (givtOrganisation == default)
                throw new InvalidRequestException($"No organisation found for account with id {ownerPaymentProviderId}");

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
            var merchantOnboardingApi = new MerchantOnboardingApi(_configuration);

            // Create the account in our database
            var capabilities = await merchantOnboardingApi.GetcapabilitiesAsync(notification.Payload.Id.ToString(), "3.0");


            foreach (var collectGroup in givtOrganisation.CollectGroups)
            {
                collectGroup.AccountId = account.Id;
                collectGroup.Active = capabilities.Payments.Enabled;
            }

            await _context.SaveChangesAsync();

            var logMessage = $"Account with id {notification.Payload.Id} from owner with id {notification.Payload.Owner.Id} ({givtOrganisation.Name}) has been created, payments: {capabilities.Payments.Enabled}";
            SlackLogger.Information(logMessage);
            Logger.Information(logMessage);

            return new OkResult();
        });
    }
}