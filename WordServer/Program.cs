// WordServer.Program.cs
// K.Hira, R.Sweet
// April 4, 2025
// Entry point for the WordServer application, which sets up and runs the gRPC service.
namespace WordServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Specify the URLs for the server to listen on
        // Ensures compatibility when running as an executable (.exe)
        builder.WebHost.UseUrls("https://localhost:7181", "http://localhost:5109");

        // Add services to the container.
        builder.Services.AddGrpc();

        var app = builder.Build();

        // Map the DailyWord gRPC service to handle requests
        app.MapGrpcService<DailyWordService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}