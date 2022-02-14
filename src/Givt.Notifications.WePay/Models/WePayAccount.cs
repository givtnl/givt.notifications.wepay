using System;

namespace Givt.Notifications.WePay.Models;

public class WePayAccount
{
    public Guid Id { get; set; }
    public string resource { get; set; }
    public Owner Owner { get; set; }
    public string Description { get; set; }
    public string Name { get; set; }
}