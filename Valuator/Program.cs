using StackExchange.Redis;
using Valuator.Services;

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

        builder.Services.AddSingleton<RankTaskPublisher>( sp =>
            new RankTaskPublisher( sp.GetRequiredService<IConfiguration>() ) );
        builder.Services.AddSingleton<EventsPublisher>( sp =>
            new EventsPublisher( sp.GetRequiredService<IConfiguration>() ) );

        builder.Services.AddSingleton<UserService>();

        builder.Services.AddAuthentication("Cookies")
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/AccessDenied";  
                options.ExpireTimeSpan = TimeSpan.FromHours(2);
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
