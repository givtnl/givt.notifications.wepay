namespace Givt.Notifications.WePay.Models;

public class WePayNotification<T>
{
    public string Id { get; set; }
    public string Resource { get; set; }
    public T Payload { get; set; }
}