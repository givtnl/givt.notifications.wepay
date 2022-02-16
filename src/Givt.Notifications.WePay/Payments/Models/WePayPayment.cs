using System;
using Givt.Models;
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
    
    public TransactionStatus TransactionStatus
    {
        get
        {
            var status = TransactionStatus.Entered;
            switch (Status)
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
}