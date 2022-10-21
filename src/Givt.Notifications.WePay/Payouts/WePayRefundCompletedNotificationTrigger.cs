using Givt.Business.Infrastructure.Interfaces;
using Givt.DatabaseAccess;
using Givt.Models;
using Givt.Notifications.WePay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Serilog.Sinks.Http.Logger;
using System;
using System.Linq;
using System.Threading.Tasks;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payouts;

public class WePayRefundCompletedNotificationTrigger : WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _dbContext;

    public WePayRefundCompletedNotificationTrigger(
        GivtDatabaseContext dbContext,
        ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration) :
        base(loggerFactory, logger, notificationConfiguration)
    {
        _dbContext = dbContext;
    }

    private static string CreateMsg(string notificationId, string paymentId, string accountId, string info)
    {
        return $"Received WePay refund notification {notificationId} on payment {paymentId} for account {accountId}: {info}";
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

            if (!notification.Payload.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                msg = CreateMsg(notificationId, paymentId, accountId, $"invalid status ({notification.Payload.Status}, expected: completed)");
                SlackLogger.Error(msg);
                Logger.Error(msg);
                return new OkResult();
            }

            var amount = notification.Payload.Amounts.TotalAmount / 100.0M;

            // fetch related transactions from DB
            var transactions = await _dbContext.Transactions
                .Where(x => x.PaymentProviderId == paymentId)
                .ToListAsync();
            if (!transactions.Any())
            {
                msg = CreateMsg(notificationId, paymentId, accountId, "no donations found");
                SlackLogger.Error(msg);
                Logger.Error(msg);
                return new OkResult();
            }

            var totalAmount = decimal.Round(transactions.Sum(x => x.Amount));
            if (totalAmount != amount)
            {
                msg = CreateMsg(notificationId, paymentId, accountId, $"sum of related transactions {totalAmount} is not the same as WePay refund amount {amount}.");
                SlackLogger.Error(msg);
                Logger.Error(msg);
            }

            foreach (var transaction in transactions)
                transaction.Status = TransactionStatus.Cancelled;
            await _dbContext.SaveChangesAsync();

            msg = CreateMsg(notificationId, paymentId, accountId, $"{transactions.Count} transactions cancelled");
            Logger.Information(msg);

            return new OkResult();
        });
    }
}