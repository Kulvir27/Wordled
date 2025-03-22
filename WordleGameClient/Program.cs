using Grpc.Net.Client;
using WordServer;

namespace WordleGameClient
{
    internal class Program
    {
        static void Main(string[] args)
        {

            // Connect to the service
            var channel = GrpcChannel.ForAddress("https://localhost:7112");
            var word = new DailyWord.DailyWordClient(channel);



            // hold list of letters or dictionary to count multiple occurrences of same letter?





            // TODO: game logic


        }



        // function to print menu screen


    }
}



