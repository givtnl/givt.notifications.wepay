using Givt.Notifications.WePay.Models;

namespace Givt.Notifications.WePay.Accounts;

public class AccountCapabilitiesUpdatedNotification
{
    public PaymentCapabilities Payments { get; set; }
    public PayoutCapabilities Payouts { get; set; }
    public WePayAccount Owner { get; set; }
}

public class PaymentCapabilities
{
    public bool Enabled { get; set; }
}

public class PayoutCapabilities
{
    public bool Enabled { get; set; }
}