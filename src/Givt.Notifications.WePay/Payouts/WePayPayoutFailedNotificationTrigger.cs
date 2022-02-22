using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Givt.Business.Accounts.Queries;
using Givt.Business.Infrastructure.Interfaces;
using Givt.Business.Organisations.Queries.GetDetail;
using Givt.DatabaseAccess;
using Givt.Notifications.WePay.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog.Sinks.Http.Logger;
using WePay.Clear.Generated.Model;

namespace Givt.Notifications.WePay.Payouts;

public class WePayPayoutFailedNotificationTrigger: WePayNotificationTrigger
{
    private readonly GivtDatabaseContext _context;
    private readonly IMediator _mediator;

    public WePayPayoutFailedNotificationTrigger(ISlackLoggerFactory loggerFactory, ILog logger, WePayNotificationConfiguration notificationConfiguration, GivtDatabaseContext context, IMediator mediator) : base(loggerFactory, logger, notificationConfiguration)
    {
        _context = context;
        _mediator = mediator;
    }
    
    [Function("WePayPayoutFailedNotificationTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var notification = JsonConvert.DeserializeObject<WePayNotification<PayoutsResponseGetapayout>>(await req.ReadAsStringAsync() ?? string.Empty);
        var account = await _mediator.Send(new GetAccountDetailQuery { PaymentProviderId = notification.Payload.Owner.Id });
        if (account.OrganisationId != null)
        {
            var org = await _mediator.Send(new GetOrganisationDetailQuery(account.OrganisationId.Value));
            SlackLogger.Error($"Received notification on a failed payout {notification.Payload.Id} from WePay for organisation {org.Name}!");
        }

        return new OkResult();
    }
}