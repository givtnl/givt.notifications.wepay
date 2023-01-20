using Givt.Business.Accounts.Queries;
using Givt.Business.CollectGroups.Queries.GetListByDonations;
using Givt.Business.Donations.Model;
using Givt.Business.Donations.Queries;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Business.Payments.Commands;
using Givt.Business.Payments.Commands.UpdatePayment;
using Givt.Business.Transactions.Models;
using Givt.Notifications.WePay.Models;
using Givt.Notifications.WePay.Wrappers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog.Sinks.Http.Logger;
using System;
using System.Linq;
using System.Threading.Tasks;
using WePay.Clear.Generated.Api;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payouts;

public class WePayPayoutCompletedNotificationTrigger : WePayNotificationTrigger
{
    private readonly IMediator _mediator;
    private readonly PaymentOperationsApi _paymentOperationsApi;
    private readonly PaymentsApi _paymentsApi;

    public WePayPayoutCompletedNotificationTrigger(
        WePayGeneratedConfigurationWrapper configuration, IMediator mediator, ISlackLoggerFactory loggerFactory,
        ILog logger, WePayNotificationConfiguration notificationConfiguration) : base(loggerFactory, logger,
        notificationConfiguration)
    {
        _mediator = mediator;
        _paymentOperationsApi = new PaymentOperationsApi(configuration.Configuration);
        _paymentOperationsApi.ApiClient.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        _paymentsApi = new PaymentsApi(configuration.Configuration);
        _paymentsApi.ApiClient.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

    }

    [Function("WePayPayoutCompletedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData request)
    {
        return await WithExceptionHandler(async Task<IActionResult> () =>
        {
            var notification = await WePayNotification<PayoutsResponseGetapayout>.FromHttpRequestData(request);

            if (notification.Payload.Status != StatusPayoutsResponse.Completed)
            {
                SlackLogger.Error($"Received notification on payout {notification.Payload.Id} from WePay without the expected status Completed!");
                return new OkResult();
            }

            //Get the transaction reports from WePay
            var wePayReports = await _paymentOperationsApi.GetacollectionoftransactionrecordsAsync(
                apiVersion: "3.0",
                uniqueKey: Guid.NewGuid().ToString(),
                accountId: notification.Payload.Owner.Id.ToString(),
                payoutId: notification.Payload.Id.ToString(),
                pageSize: 50
            );
            var transactionIds = wePayReports.Results.Where(x => x.Type == TypeResult6.MerchantPayment).Select(x => x.Owner.Id).ToList(); // owner is from payments
            var refundIds = wePayReports.Results.Where(x => x.Type == TypeResult6.MerchantPaymentRefund).Select(x => x.Owner.Id).ToList(); // owner is from refunds        
            while (!string.IsNullOrWhiteSpace(wePayReports.Next))
            {
                wePayReports = await _paymentOperationsApi.GetacollectionoftransactionrecordsAsync(
                    apiVersion: "3.0",
                    uniqueKey: Guid.NewGuid().ToString(),
                    page: wePayReports.Next.Split('=').Last(),
                    pageSize: 50
                );
                transactionIds.AddRange(wePayReports.Results.Where(x => x.Type == TypeResult6.MerchantPayment).Select(x => x.Owner.Id));
                refundIds.AddRange(wePayReports.Results.Where(x => x.Type == TypeResult6.MerchantPaymentRefund).Select(x => x.Owner.Id));
            }

            // get the payment ids belonging to refunds            
            foreach (var refundId in refundIds)
            {
                var wepayRefund = await _paymentsApi.GetarefundAsync(
                    id: refundId,
                    apiVersion: "3.0",
                    uniqueKey: Guid.NewGuid().ToString()
                );
                transactionIds.Add(wepayRefund.Payment.Id);
            }
            // let's lookup some information in the system
            var account = await _mediator.Send(new GetAccountDetailQuery { PaymentProviderId = notification.Payload.Owner.Id.ToString() });
            var donations = await _mediator.Send(new GetDonationDetailQuery { TransactionIds = transactionIds });
            var collectGroups = await _mediator.Send(new GetCollectGroupListByDonationsQuery { Donations = donations });

            if (!donations.Any())
            {
                SlackLogger.Error($"No donations found for WePay payout {notification.Payload.Id}");
                Logger.Error($"No donations found for WePay payout {notification.Payload.Id}");
                return new OkResult();
            }

            var totalAmount = 0M;

            foreach (var collectGroup in collectGroups)
            {
                var createPaymentCommand = new CreateCollectGroupPaymentCommand
                {
                    Account = account,
                    AccountId = account.Id,
                    CollectGroupId = collectGroup.Id,
                    DefaultAccountId = collectGroup.AccountId.Value,
                    Transactions = donations
                        .Where(x => x.CollectGroupId == collectGroup.Id)
                        .GroupBy(x => x.TransactionId)
                        .Select(x =>
                        {
                            return new TransactionModel
                            {
                                DonationIds = x.Select(y => y.Id),
                                PaymentProviderId = x.Key
                            };
                        }).ToList(),
                    Donations = donations
                        .Where(x => x.CollectGroupId == collectGroup.Id)
                        .Select(x => new DonationForPaymentModel
                        {
                            Id = x.Id,
                            TransactionId = x.TransactionId
                        }).ToList(),
                    TransactionsEndDate = new DateTime(1970, 1, 1).AddSeconds(notification.Payload.CreateTime)
                };

                var payment = await _mediator.Send(createPaymentCommand);
                await _mediator.Send(new UpdatePaymentCommand
                {
                    Id = payment.Id,
                    PaymentProviderExecutionDate = new DateTime(1970, 1, 1).AddSeconds(notification.Payload.CompleteTime.Value),
                    PaymentProviderId = notification.Payload.Id
                });
                SlackLogger.Information($"Payout created with id {payment.Id} for WePay account {notification.Payload.Owner.Id}");
                Logger.Information($"Payout created with id {payment.Id} for WePay account {notification.Payload.Owner.Id}");

                totalAmount += payment.TotalPaid;
            }

            if (notification.Payload.Amount != decimal.Round(totalAmount, 2) * 100)
            {
                SlackLogger.Error($"{decimal.Round(totalAmount, 2)} is not the same as WePay amount {notification.Payload.Amount / 100.0M}");
                Logger.Error($"{decimal.Round(totalAmount, 2)} is not the same as WePay amount {notification.Payload.Amount / 100.0M}");
            }
            return new OkResult();
        });
    }
}