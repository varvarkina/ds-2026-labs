using StackExchange.Redis;
using Valuator.Services;
using Valuator.Hubs;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        var redisConnectionString = builder.Configuration.GetConnectionString( "Redis" ) ?? "localhost:6379";
        builder.Services.AddSingleton<IConnectionMultiplexer>( _ => ConnectionMultiplexer.Connect( redisConnectionString ) );

        builder.Services.AddSingleton<RankTaskPublisher>();
        builder.Services.AddSingleton<EventsPublisher>();

        builder.Services.AddSignalR();
        builder.Services.AddHostedService<EventsConsumerService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();
        app.MapHub<RankHub>( "/rankHub" );

        app.Run();
    }
}
