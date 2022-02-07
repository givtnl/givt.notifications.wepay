using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(givt.notifications.wepay.Startup))]
namespace givt.notifications.wepay;
public class Startup: FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        throw new System.NotImplementedException();
    }
}