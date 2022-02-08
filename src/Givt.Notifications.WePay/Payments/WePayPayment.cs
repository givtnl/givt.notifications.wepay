using System;
using Newtonsoft.Json;

namespace Givt.Notifications.WePay.Payments;

public class WePayPayment
{
    public Guid Id { get; set; }
    public string resource { get; set; }
    public DateTime CreationTime => DateTimeOffset.FromUnixTimeSeconds(CreationTimeInMS).DateTime;
    [JsonProperty("create_time")]
    public int CreationTimeInMS { get; set; }
    public string Status { get; set; }
}