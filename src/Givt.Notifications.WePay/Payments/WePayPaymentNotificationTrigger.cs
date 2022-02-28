using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Givt.Business.Donations.Commands.UpdateTransactionStatusCommand;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Models;
using Givt.Notifications.WePay.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Serilog.Sinks.Http.Logger;
using System.Text.Json;
using Givt.Business.Donations.Commands.Models;
using Givt.Business.Donations.Commands.SendPaymentFailedMail;
using Givt.Business.Donations.Models;
using Givt.Business.Donations.Queries;
using Givt.Business.Payments.Commands.SendPaymentMail;
using Givt.Business.Transactions.Queries;
using Givt.Business.Users.Queries.GetDetail;
using Givt.Models.Enums;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Payments;
public class WePayPaymentNotificationTrigger: WePayNotificationTrigger
{
    private readonly IMediator _mediator;

    public WePayPaymentNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration, IMediator mediator) : base(loggerFactory, logger, notificationConfiguration)
    {
        _mediator = mediator;
    }

    [Function("WePayPaymentNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
    {
        return await WithExceptionHandler(async Task<IActionResult>() => 
        {
            var notification = await WePayNotification<WePayPayment>.FromHttpRequestData(req);
            
            var payment = notification.Payload;

            var updateResponse = await _mediator.Send(new UpdateTransactionStatusCommand
            {
                PaymentProviderId = payment.Id.ToString(),
                NewTransactionStatus = payment.TransactionStatus
            });

            if (updateResponse.TransactionIds.Any() && updateResponse.HasUserTransactions)
            {
                
                var transactions = await _mediator.Send(new GetDonationDetailQuery
                {
                    TransactionIds = new List<string> {payment.Id.ToString()}
                });
                
                var user = await _mediator.Send(new GetUserDetailQuery
                {
                    Id = updateResponse.UserId
                });

                IRequest emailRequest;
                
                if (payment.TransactionStatus == TransactionStatus.Processed)
                {
                    emailRequest  = new SendPaymentCreatedMailCommand()
                    {
                        PaymentType = PaymentType.CreditCard,
                        FirstName = user.FirstName,
                        EmailAddress = user.Email,
                        Amount = transactions.Sum(x => x.Amount),
                        Language = "en",
                        GiftOverview = transactions.Select(x => new PaymentMailListItem
                        {
                            TransactionId = x.Id,
                            CollectGroupName = x.OrgName,
                            Amount = x.Amount,
                            DateTime = x.Timestamp
                        }).ToList()
                    };
                }
                else
                {
                    emailRequest  = new SendPaymentFailedMailCommand()
                    {
                        FirstName = user.FirstName,
                        EmailAddress = user.Email,
                        Amount = transactions.Sum(x => x.Amount),
                        Language = "en",
                        GiftOverview = transactions.Select(x => new PaymentMailListItem
                        {
                            TransactionId = x.Id,
                            CollectGroupName = x.OrgName,
                            Amount = x.Amount,
                            DateTime = x.Timestamp
                        }).ToList()
                    };
                }
                // When the transaction is processed 
                
                await _mediator.Send(emailRequest);
            
                Logger.Information($"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}");
                return new OkResult();
            }
            
            var logMessage = $"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}";
            SlackLogger.Information(logMessage);
            Logger.Information(logMessage);

            return new OkResult();
        });
    }    
}