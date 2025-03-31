// WordleGameServer.Program.cs
// K.Hira, R.Sweet
// April 4, 2025
// Configures and runs the WordleGameServer, a gRPC-based service that supports Wordle gameplay functionality.

using WordServer;
namespace WordleGameServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Specify the URLs for the server to listen on
        // Ensures compatibility when running as an executable (.exe)
        builder.WebHost.UseUrls("https://localhost:7275", "http://localhost:5228");

        // Add services to the container.
        builder.Services.AddGrpc();
        builder.Services.AddGrpcClient<DailyWord.DailyWordClient>(o =>  { o.Address = new Uri("http://localhost:5109"); });

        var app = builder.Build();

        // Map the gRPC service to the application's pipeline
        app.MapGrpcService<DailyWordleService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}