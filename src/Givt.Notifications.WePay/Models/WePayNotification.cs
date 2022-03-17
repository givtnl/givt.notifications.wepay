using System.Threading.Tasks;
using Givt.Models.Exceptions;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
namespace Givt.Notifications.WePay.Models;

public class WePayNotification<T>
{
    public string Id { get; set; }
    public string Resource { get; set; }
    public T Payload { get; set; }

    public static async Task<WePayNotification<T>> FromHttpRequestData(HttpRequestData req)
    {
        var bodyString = await req.ReadAsStringAsync();
        if (bodyString == null)
            throw new InvalidRequestException("Couldn't read the body as string!");

        var obj = JsonConvert.DeserializeObject<WePayNotification<T>>(bodyString, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        if (obj == null)
            throw new InvalidRequestException(nameof(bodyString), bodyString);

        return obj;
    }
}