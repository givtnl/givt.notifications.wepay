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
        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonSerializer.Deserialize<WePayNotification<WePayPayment>>(bodyString);
        
        var payment = notification.Payload;

        await _mediator.Send(new UpdateTransactionStatusCommand
        {
            PaymentProviderId = payment.Id.ToString(),
            NewTransactionStatus = payment.TransactionStatus
        });

        if (payment.TransactionStatus == TransactionStatus.Processed) return new OkResult();
        
        var logMessage = $"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}";
        SlackLogger.Information(logMessage);
        Logger.Information(logMessage);

        return new OkResult();
    }


    
}