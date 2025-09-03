using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FileUploader.Worker
{
    public class Startup
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

            var config = builder.Configuration;
            var redisConn = config.GetValue<string>("Redis:Configuration") ?? "localhost:6379";
            var mux = await ConnectionMultiplexer.ConnectAsync(redisConn);
            builder.Services.AddSingleton<IConnectionMultiplexer>(mux);

            builder.Services.AddHostedService<WorkerService>();

            await builder.Build().RunAsync();
        }
    }
}