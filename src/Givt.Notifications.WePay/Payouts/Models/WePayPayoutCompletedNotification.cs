using System;
using Givt.Notifications.WePay.Models;

namespace Givt.Notifications.WePay.Payouts.Models;

public class WePayPayoutCompletedNotification
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public Owner Owner { get; set; }
}
