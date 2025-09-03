using Microsoft.AspNetCore.Http.Features;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// 1. Configure Services
// ----------------------------

// Allow large file uploads (up to 1 GB)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 1024L * 1024L * 1024L;
});

builder.Services.AddEndpointsApiExplorer();

// Redis Connection
var redisConn = builder.Configuration.GetValue<string>("Redis:Configuration") ?? "localhost:6379";
var mux = await ConnectionMultiplexer.ConnectAsync(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(mux);

// Add Controllers
builder.Services.AddControllers();

var app = builder.Build();


app.UseHttpsRedirection();
app.MapControllers();

app.Run();
