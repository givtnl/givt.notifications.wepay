using Microsoft.Extensions.Hosting;

namespace givt.notifications.wepay
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .Build();
 
            host.Run();
        }
    }
}