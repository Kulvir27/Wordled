using WordServer;


namespace WordleGameServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseUrls("https://localhost:7275", "http://localhost:5228");

        // Add services to the container.
        builder.Services.AddGrpc();
        builder.Services.AddGrpcClient<DailyWord.DailyWordClient>(o =>  { o.Address = new Uri("http://localhost:5109"); });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.MapGrpcService<DailyWordleService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}