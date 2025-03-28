using Grpc.Net.Client;
using Grpc.Core;
using WordleGameServer;

namespace WordleGameClient
{
    internal class Program
    {
        static async Task Main(string[] args) // made async here to allow for awaits in this method
        {
            // Connect to the gRPC service
            var channel = GrpcChannel.ForAddress("https://localhost:7275");
            var gameClient = new WordleGameService.WordleGameServiceClient(channel);

            // Display game rules
            DisplayRules();

            // Initialize letter trackers
            var availableLetters = new HashSet<char>("abcdefghijklmnopqrstuvwxyz".ToCharArray());
            var includedLetters = new HashSet<char>();
            var excludedLetters = new HashSet<char>();

            try
            {
                // start game
                using var call = gameClient.Play();

                // loop for user guesses
                for (int turn = 1; turn <= 5; turn++)
                {
                    Console.Write($"({turn}): ");
                    var guess = (Console.ReadLine() ?? string.Empty);

                    if (guess.Length != 5)
                    {
                        Console.WriteLine("Invalid word length. Try again.");
                        turn--; // user can retry their current turn
                        continue;
                    }

                    // pass next word guessed to WordleGameService
                    await call.RequestStream.WriteAsync(new PlayRequest { Word = guess });


                    // Process the server's response
                    if (await call.ResponseStream.MoveNext()) // await the MoveNext() to ensure we dont access stale ResponseStream.Current
                    {
                        var playResponse = call.ResponseStream.Current;

                        if (playResponse != null)
                        {
                            // Display feedback
                            Console.WriteLine($"     {string.Join("", playResponse.Letters.Select(l => MapFeedback(l.Feedback)))}");
                            Console.WriteLine();

                            // Update letter trackers
                            UpdateLetterSets(playResponse.Letters, availableLetters, includedLetters, excludedLetters);

                            // Display letter statuses
                            Console.WriteLine($"     Included:  {string.Join(", ", includedLetters)}");
                            Console.WriteLine($"     Available: {string.Join(", ", availableLetters)}");
                            Console.WriteLine($"     Excluded:  {string.Join(", ", excludedLetters)}");
                            Console.WriteLine();

                            // Check if the game is over
                            if (playResponse.GameOver)
                            {
                                Console.WriteLine("Game over! Thanks for playing.");
                                break;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Server ended the response stream unexpectedly.");
                        break;
                    }
                }

                // Wrap up the game session
                await call.RequestStream.CompleteAsync();
                Console.ReadKey();
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"\nERROR: Unable to connect to the Wordle Game Service. Details: {ex.Message}");
            }
        }

        static void DisplayRules()
        {
            Console.WriteLine("+-------------------+");
            Console.WriteLine("|   W O R D L E D   |");
            Console.WriteLine("+-------------------+\n");
            Console.WriteLine("You have 6 chances to guess a 5-letter word.");
            Console.WriteLine("Each guess must be a 'playable' 5-letter word.");
            Console.WriteLine("After a guess the game will display a series of");
            Console.WriteLine("characters to show you how good your guess was.");
            Console.WriteLine("x - means the letter above is not in the word.");
            Console.WriteLine("? - means the letter should be in another spot.");
            Console.WriteLine("* - means the letter is correct in this spot.\n");
            Console.WriteLine("     Available: a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p,q,r,s,t,u,v,w,x,y,z\n");
        }

        static string MapFeedback(FeedbackType feedback)
        {
            return feedback switch
            {
                FeedbackType.CorrectPosition => "*",
                FeedbackType.WrongPosition => "?",
                FeedbackType.NotInWord => "x",
                _ => " "
            };
        }

        static void UpdateLetterSets(IEnumerable<LetterFeedback> letters, HashSet<char> available, HashSet<char> included, HashSet<char> excluded)
        {
            foreach (var letterFeedback in letters)
            {
                char letter = char.ToLower(letterFeedback.Letter[0]);

                switch (letterFeedback.Feedback)
                {
                    case FeedbackType.CorrectPosition:
                    case FeedbackType.WrongPosition:
                        included.Add(letter);
                        break;
                    case FeedbackType.NotInWord:
                        excluded.Add(letter);
                        available.Remove(letter);
                        break;
                }
            }
        }
    }
}