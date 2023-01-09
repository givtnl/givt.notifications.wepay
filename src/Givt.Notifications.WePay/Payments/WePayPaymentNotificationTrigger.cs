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
using Givt.Business.Donations.Commands.Models;
using Givt.Business.Donations.Commands.SendPaymentFailedMail;
using Givt.Business.Donations.Queries;
using Givt.Business.Payments.Commands.SendPaymentMail;
using Givt.Business.Users.Queries.GetDetail;
using Givt.Models.Enums;
using WePay.Clear.Generated.Model;
using Givt.Notifications.WePay.Infrastructure.Helpers;

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
            var notification = await WePayNotification<PaymentsResponse>.FromHttpRequestData(req);
            
            var payment = notification.Payload;

            var updateResponse = await _mediator.Send(new UpdateTransactionStatusCommand
            {
                PaymentProviderId = payment.Id.ToString(),
                NewTransactionStatus = TransactionStatusHelper.FromWePayTransactionStatus(payment.Status)
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
                
                if (TransactionStatusHelper.FromWePayTransactionStatus(payment.Status) == TransactionStatus.Processed)
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
                            CollectGroupName = $"{x.OrgName}, {x.OrganisationAddressLine1}, {x.OrganisationAddressLine3}, {x.OrganisationPostalCode}, US, {x.OrganisationPhoneNumber}",
                            Amount = x.Amount,
                            DateTime = x.Timestamp
                        }).ToList(),
                        CardNetwork = user.DetailLineThree,
                        PAN = user.DetailLineOne,
                        AuthorizationCode = payment.AuthorizationCode
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
                            CollectGroupName = $"{x.OrgName}, {x.OrganisationAddressLine1}, {x.OrganisationAddressLine3}, {x.OrganisationPostalCode}, US, {x.OrganisationPhoneNumber}",
                            Amount = x.Amount,
                            DateTime = x.Timestamp
                        }).ToList(),
                        CardNetwork = user.DetailLineThree,
                        PAN = user.DetailLineOne,
                    };
                }
                // When the transaction is processed 
                
                await _mediator.Send(emailRequest);
            
                Logger.Information($"Payment with id {payment.Id} from {payment.CreateTime} has been updated to {payment.Status}");
                return new OkResult();
            }
            
            var logMessage = $"Received payment notification for id {payment.Id} but it's not updated in the database.";
            SlackLogger.Error(logMessage);
            Logger.Error(logMessage);

            return new OkResult();
        });
    }    
}