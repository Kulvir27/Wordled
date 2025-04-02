// WordleGameServer.Services.DailyWordleService.cs
// K. Hira, R. Sweet
// April 4, 2025
// A gRPC service that implements the DailyWordle game logic, including handling
// bidirectional streaming for gameplay and retrieving user statistics.
// This service interacts with the WordServer's DailyWord service to validate
// words and fetch the daily word.

using Grpc.Core;
using WordServer;
using System.Text.Json;

namespace WordleGameServer
{
    /// <summary>
    /// Implementation of the DailyWordle gRPC service.
    /// This service allows clients to play a Wordle-like game through bidirectional streaming
    /// and retrieve daily statistics through a unary RPC.
    /// </summary>
    public class DailyWordleService : DailyWordle.DailyWordleBase
    {
        private readonly DailyWord.DailyWordClient _wordClient;
        private static readonly string StatsDirectory = "stats";
        private static readonly object FileLock = new();

        /// <summary>
        /// Constructor to initialize the DailyWordleService with a DailyWord client.
        /// </summary>
        /// <param name="wordClient">Instance of DailyWord.DailyWordClient to fetch and validate words</param>
        public DailyWordleService(DailyWord.DailyWordClient wordClient)
        {
            _wordClient = wordClient;
        }

        /// <summary>
        /// Implementation of the Play RPC, which uses a bidirectional stream to allow the client to send
        /// word guesses and receive feedback from the server in real-time. The method asynchronously processes 
        /// the user's guesses, checks their validity, and provides feedback on each guess until the game is won or lost.
        /// </summary>
        /// <param name="requestStream">A reference to the incoming request stream, which receives PlayRequest objects containing user guesses.</param>
        /// <param name="responseStream">A reference to the outgoing response stream, which sends PlayResponse objects with feedback on each guess.</param>
        /// <param name="context">Provides metadata, deadlines, and cancellation support for the gRPC call.</param>
        /// <returns>A Task representing the asynchronous execution of the method.</returns>
        public override async Task Play(
            IAsyncStreamReader<PlayRequest> requestStream,
            IServerStreamWriter<PlayResponse> responseStream,
            ServerCallContext context)
        {
            int turns = 0;
            bool won = false;
            var wordResponse = await _wordClient.GetWordAsync(new GetWordRequest());
            string wordToGuess = wordResponse.Word.ToLower();
            string wordDate = DateTime.Now.ToString("yyyy-MM-dd");
            var feedback = new List<LetterFeedback>();

            // Process the request stream
            await foreach (var request in requestStream.ReadAllAsync())
            {
                string userGuess = request.Word.ToLower();

                if (won) break;

                if (userGuess.Length == 5)
                {
                    var validateRequest = new ValidateWordRequest { Word = userGuess };
                    var validateResponse = await _wordClient.ValidateWordAsync(validateRequest);

                    if (!validateResponse.Correct)
                    {
                        feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                        // Send a PlayResponse back to the user
                        var response = new PlayResponse
                        {
                            Correct = false,
                            GameOver = false,
                            ValidWord = false,
                            Guesses = turns,
                            Letters = { feedback },
                            Message = "Word does not exist in wordle.json"
                        };

                        await responseStream.WriteAsync(response);
                        continue;
                    }

                    turns++;

                    // Check if the user has won
                    if (userGuess == wordToGuess)
                    {
                        won = true;

                        feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                        var response = new PlayResponse
                        {
                            Correct = true,
                            GameOver = true,
                            ValidWord = true,
                            Guesses = turns,
                            Letters = { feedback },
                            Message = $"You win! You've correctly guessed today's word, '{wordToGuess}'!"
                        };

                        await responseStream.WriteAsync(response);
                        break;
                    }

                    // Check if user has used all their turns (Game Over)
                    if (turns > 5)
                    {
                        feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                        var response = new PlayResponse
                        {
                            Correct = false,
                            GameOver = true,
                            ValidWord = true,
                            Guesses = turns,
                            Letters = { feedback },
                            Message = $"You lose!"
                        };

                        await responseStream.WriteAsync(response);
                        break;
                    }
                    // Incorrect letters or placements
                    else
                    {
                        feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                        var response = new PlayResponse
                        {
                            Correct = false,
                            GameOver = false,
                            ValidWord = true,
                            Guesses = turns,
                            Letters = { feedback },
                            Message = ""
                        };

                        await responseStream.WriteAsync(response);
                    }

                }
            }

            // Write valid games to json file
            if (turns > 0)
            {
                // Lock the file so only one game can update at a time 
                lock (FileLock)
                {
                    string statsPath = Path.Combine(StatsDirectory, $"{wordDate}.json");
                    DailyStats stats;

                    // Ensure stats directory exists
                    Directory.CreateDirectory(StatsDirectory);

                    if (File.Exists(statsPath))
                    {
                        string json = File.ReadAllText(statsPath);
                        stats = JsonSerializer.Deserialize<DailyStats>(json)!;

                    }
                    else
                    {
                        stats = new DailyStats
                        {
                            Date = wordDate,
                            TotalPlayers = 0,
                            WinCount = 0,
                            GuessDistribution = new Dictionary<int, int>()
                        };
                    }

                    // Update the stats with class method
                    stats.RecordGame(won, turns);

                    string updatedJson = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(statsPath, updatedJson);

                }
            }
        }

        /// <summary>
        /// Implementation of the GetStats RPC, which retrieves and returns user game statistics 
        /// for the current day's Wordle game. This method reads from a JSON file that stores game statistics, 
        /// calculates the win percentage and average number of guesses, and sends the data back to the client.
        /// </summary>
        /// <param name="request">An empty StatsRequest object, stats are retrieved based on the current date.</param>
        /// <param name="context">Provides metadata, deadlines, and cancellation support for the gRPC call.</param>
        /// <returns>A Task containing a StatsResponse object with the total players, win percentage, and average guesses.</returns>
        public override Task<StatsResponse> GetStats(StatsRequest request, ServerCallContext context)
        {
            string wordDate = DateTime.Now.ToString("yyyy-MM-dd");
            string statsPath = Path.Combine(StatsDirectory, $"{wordDate}.json");

            lock (FileLock)
            {
                if (!File.Exists(statsPath))
                {
                    return Task.FromResult(new StatsResponse
                    {
                        TotalPlayers = 0,
                        WinPercentage = 0,
                        AverageGuesses = 0
                    });
                }

                string json = File.ReadAllText(statsPath);
                var stats = JsonSerializer.Deserialize<DailyStats>(json)!;

                double winPercentage = stats.TotalPlayers > 0 ? (double)stats.WinCount / stats.TotalPlayers * 100 : 0;
                double averageGuesses = stats.WinCount > 0 ? stats.GuessDistribution.Sum(g => g.Key * g.Value) / (double)stats.WinCount : 0;

                return Task.FromResult(new StatsResponse
                {
                    TotalPlayers = stats.TotalPlayers,
                    WinPercentage = winPercentage,
                    AverageGuesses = averageGuesses
                });
            }
        }

        /// <summary>
        /// Analyzes the player's guessed word and returns feedback for each letter, indicating 
        /// whether it is in the correct position, in the word but misplaced, or not in the word at all.
        /// </summary>
        /// <param name="guess">The player's guessed word, converted to lowercase.</param>
        /// <param name="wordToGuess">The correct word of the day, also in lowercase.</param>
        /// <returns>A list of LetterFeedback objects, each containing a letter and its corresponding feedback type.</returns>
        private List<LetterFeedback> GenerateLetterFeedback(string guess, string wordToGuess)
        {
            // Create an array so we can use the index for easy comparison
            var feedback = new LetterFeedback[5];

            var matchCounts = new Dictionary<char, int>();

            for (int i = 0; i < 5; i++)
            {
                if (guess[i] == wordToGuess[i])
                {
                    feedback[i] = new LetterFeedback
                    {
                        Letter = guess[i].ToString(),
                        Feedback = FeedbackType.CorrectPosition
                    };

                    // Track the matches
                    if (!matchCounts.ContainsKey(guess[i]))
                        matchCounts[guess[i]] = 1;
                    else
                        matchCounts[guess[i]]++;
                }
            }

            // Check for correct letters in wrong positions, or incorrect letters
            for (int i = 0; i < 5; i++)
            {
                // Skip matches that were handled in the first loop
                if (feedback[i] != null) continue;

                char letter = guess[i];

                int totalInWord = wordToGuess.Count(c => c == letter);
                int usedSoFar = matchCounts.ContainsKey(letter) ? matchCounts[letter] : 0;

                // Not in word, or already matched all occurrences
                if (totalInWord == 0 || usedSoFar >= totalInWord)
                {
                    feedback[i] = new LetterFeedback
                    {
                        Letter = letter.ToString(),
                        Feedback = FeedbackType.NotInWord
                    };
                }
                else
                {
                    // Wrong position
                    feedback[i] = new LetterFeedback
                    {
                        Letter = letter.ToString(),
                        Feedback = FeedbackType.WrongPosition
                    };

                    matchCounts[letter] = usedSoFar + 1;
                }
            }

            return feedback.ToList();
        }
    }
}
