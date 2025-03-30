using Grpc.Net.Client;
using Grpc.Core;
using WordleGameServer;

namespace WordleGameClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            bool PrintStats = true;

            // Server is reachable, connect to the gRPC service
            var channel = GrpcChannel.ForAddress("https://localhost:7275");
            var gameClient = new DailyWordle.DailyWordleClient(channel);

            // Display game rules
            DisplayRules();

            // Initialize letter trackers
            var availableLetters = new HashSet<char>("abcdefghijklmnopqrstuvwxyz".ToCharArray());
            var includedLetters = new HashSet<char>();
            var excludedLetters = new HashSet<char>();

            try
            {
                // Start game
                using var call = gameClient.Play();                

                // Loop for user guesses
                for (int turn = 0; turn < 6; turn++)
                {
                    Console.Write($"({turn+1}): ");
                    var guess = (Console.ReadLine() ?? string.Empty);

                    // Validate user guess length, invalid length does not count as a turn
                    if (guess.Length != 5)
                    {
                        Console.WriteLine("Invalid word length. Try again.");
                        turn--; // redo guess
                        continue;
                    }

                    try
                    {
                        // Pass next word guessed to WordleGameService
                        await call.RequestStream.WriteAsync(new PlayRequest { Word = guess });

                        // Process the server's response
                        if (await call.ResponseStream.MoveNext()) // Await the MoveNext() to ensure we dont access stale ResponseStream.Current
                        {
                            var playResponse = call.ResponseStream.Current;

                            if (playResponse != null)
                            {
                                // If invalid guess (does not exist in wordle.json), decrement and redo turn
                                if (!playResponse.ValidWord)
                                {
                                    turn--; // redo turn
                                    Console.WriteLine(playResponse.Message);
                                    continue;
                                }

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
                                if (playResponse.Correct)
                                {
                                    Console.WriteLine(playResponse.Message);
                                    break;
                                }
                                else if (playResponse.GameOver)
                                {
                                    Console.WriteLine(playResponse.Message);
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
                    catch (RpcException)
                    {
                        Console.WriteLine("\nERROR: Unable to connect to the Wordle Game Service.");
                        Console.WriteLine("Please check your connection and try again.");
                        PrintStats = false;
                        break;
                    }
                }

                // If game concludes safely
                if (PrintStats)
                {
                    // Display statistics
                    Console.WriteLine("\nStatistics");
                    Console.WriteLine("----------");
                    StatsRequest statsRequest = new() { };
                    try { 
                        StatsResponse stats = gameClient.GetStats(statsRequest);
                        Console.WriteLine("Players:          " + stats.TotalPlayers);
                        Console.WriteLine("Winners:          " + Math.Round(stats.WinPercentage) + "%");
                        Console.WriteLine("Average Guesses:  " + stats.AverageGuesses);
                    }
                    catch (RpcException)
                    {
                        Console.WriteLine("ERROR: Could not retrieve statistics due to a connection error.");
                        Console.WriteLine("Please check your connection and try again.");
                    }
                }

                // Wrap up the game session
                await call.RequestStream.CompleteAsync();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nUNEXPECTED ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
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
                    // Correct position and wrong position are both "included"
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