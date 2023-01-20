using System.Collections.Generic;
using Givt.PaymentProviders.V2.Configuration;
using WePay.Clear.Generated.Client;

namespace Givt.Notifications.WePay.Wrappers;

public class WePayGeneratedConfigurationWrapper
{
    public Configuration Configuration { get; internal set; }

    public WePayGeneratedConfigurationWrapper(WePayConfiguration configuration)
    {
        Configuration = new()
        {
            ApiKey = new Dictionary<string, string>() {
                { "App-Id", configuration.AppId },
                { "App-Token", configuration.AppToken }
            },
            BasePath = configuration.Url,
        };
    }
}