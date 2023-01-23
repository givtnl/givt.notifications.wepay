using Givt.Business.Donations.Commands.UpdateTransactionStatusCommand;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Business.Transactions.Queries;
using Givt.Models;
using Givt.Notifications.WePay.Infrastructure.AbstractClasses;
using Givt.Notifications.WePay.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog.Sinks.Http.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payments;

public class WePayRefundCompletedNotificationTrigger : WePayNotificationTrigger
{
    private readonly IMediator _mediator;

    public WePayRefundCompletedNotificationTrigger(
        IMediator mediator,
        ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration) :
        base(loggerFactory, logger, notificationConfiguration)
    {
        _mediator = mediator;
    }

    private static string CreateMsg(string notificationId, string paymentId, string accountId, decimal amount, string reason, string info)
    {
        return $"Received WePay refund notification {notificationId} on payment {paymentId} for account {accountId}, reason='{reason}', amount = {amount}: {info}";
    }

    [Function("WePayRefundCompletedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData request)
    {
        return await WithExceptionHandler(async Task<IActionResult> () =>
        {
            string msg;

            var notification = await WePayNotification<RefundsResponseGetarefund>.FromHttpRequestData(request);
            var notificationId = notification.Id;
            var paymentId = notification.Payload.Payment.Id;
            var accountId = notification.Payload.Owner.Id;
            var amount = notification.Payload.Amounts.TotalAmount / 100.0M;
            var reason = notification.Payload.RefundReason;

            if (!notification.Payload.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                msg = CreateMsg(notificationId, paymentId, accountId, amount, reason, $"invalid status ({notification.Payload.Status}, expected: completed)");
                SlackLogger.Error(msg);
                Logger.Error(msg);
                return new OkResult();
            }

            // fetch related transactions from DB. Transaction = aggregated DomainTransaction records per payment provider
            var transactions = await _mediator.Send(new GetTransactionListQuery { FilterDonations = new List<string> { paymentId } });

            if (!transactions.Any())
            {
                msg = CreateMsg(notificationId, paymentId, accountId, amount, reason, "no donations found");
                SlackLogger.Error(msg);
                Logger.Error(msg);
                return new OkResult();
            }
            // check amount
            var totalAmount = decimal.Round(transactions.Sum(x => x.SumAmounts));
            if (totalAmount != amount)
            {
                msg = CreateMsg(notificationId, paymentId, accountId, amount, reason, $"sum of related transactions {totalAmount} is not the same as WePay refund amount {amount}.");
                SlackLogger.Error(msg);
                Logger.Error(msg);
            }

            // update transactions in database
            var result = await _mediator.Send(new UpdateTransactionStatusCommand
            {
                PaymentProviderId = paymentId,
                NewTransactionStatus = TransactionStatus.Cancelled,
                Reason = $"{reason} PaymentId: {paymentId}",
                ReasonCode = "Refund"
            });

            msg = CreateMsg(notificationId, paymentId, accountId, amount, reason, $"{result.TransactionIds.Count()} transactions cancelled");
            Logger.Information(msg);

            return new OkResult();
        });
    }
}