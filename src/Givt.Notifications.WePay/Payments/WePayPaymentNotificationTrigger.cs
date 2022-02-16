using System;
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
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay.Payments;
public class WePayPaymentNotificationTrigger: WePayNotificationTrigger
{
    private readonly IMediator _mediator;

    public WePayPaymentNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, IMediator mediator) : base(loggerFactory, logger)
    {
        _mediator = mediator;
    }

    [Function("WePayPaymentNotificationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayPayment>>(bodyString);
        
        var payment = notification.Payload;

        await _mediator.Send(new UpdateTransactionStatusCommand
        {
            PaymentProviderId = payment.Id.ToString(),
            NewTransactionStatus = convertWepayStatusToTransactionStatus(payment)
        });

        if (convertWepayStatusToTransactionStatus(payment) == TransactionStatus.Processed) return new OkResult();
        
        var logMessage = $"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}";
        SlackLogger.Information(logMessage);
        Logger.Information(logMessage);

        return new OkResult();
    }


    private TransactionStatus convertWepayStatusToTransactionStatus(WePayPayment payment)
    {
        var status = TransactionStatus.Entered;
        switch (payment.Status)
        {
            case "completed":
                status = TransactionStatus.Processed;
                break;
            case "failed":
            case "cancelled":
                status = TransactionStatus.Cancelled;
                break;
            default:
                status = TransactionStatus.All;
                break;
        }

        return status;
    }
}