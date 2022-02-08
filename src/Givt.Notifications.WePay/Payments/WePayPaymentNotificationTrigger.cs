using System;
using System.Threading.Tasks;
using Givt.Business.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

[assembly: FunctionsStartup(typeof(Givt.Notifications.WePay.Startup))]
namespace Givt.Notifications.WePay;
public class WePayPaymentNotificationTrigger
{
    private readonly ISlackLoggerFactory _loggerFactory;

    public WePayPaymentNotificationTrigger(ISlackLoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    [Function("WePayPaymentNotifcationTrigger")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        var log = _loggerFactory.Create();

        var bodyString = await req.ReadAsStringAsync();

        var notification = JsonConvert.DeserializeObject<WePayNotification<WePayPayment>>(bodyString);

        log.Information($"Payment with id {notification.Payload.Id} from {notification.Payload.CreationTime} has been updated to {notification.Payload.Status}");

        return new OkResult();
    }

    public class WePayNotification<T>
    {
        public string Id { get; set; }
        public string Resource { get; set; }
        public T Payload { get; set; }
    }

    public class WePayPayment
    {
        public Guid Id { get; set; }
        public string resource { get; set; }
        public DateTime CreationTime => DateTimeOffset.FromUnixTimeSeconds(CreationTimeInMS).DateTime;
        [JsonProperty("create_time")]
        public int CreationTimeInMS { get; set; }

        public string Status { get; set; }
    }
}