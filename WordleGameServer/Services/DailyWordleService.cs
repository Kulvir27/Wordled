﻿using Grpc.Core;
using WordServer;
using System.Text.Json;

namespace WordleGameServer
{
    public class DailyWordleService : DailyWordle.DailyWordleBase
    {
        // Class members
        private readonly DailyWord.DailyWordClient _wordClient;
        private static readonly string StatsDirectory = "stats";
        private static readonly object FileLock = new();


        // Constructor to initialize the client instance
        public DailyWordleService(DailyWord.DailyWordClient wordClient)
        {
            _wordClient = wordClient;
        }


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

            // Process the request stream
            await foreach (var request in requestStream.ReadAllAsync())
            {
                if (won || turns >= 6)
                    break;

                string userGuess = request.Word.ToLower();

                // Validate guess length (must be 5 letters)
                if (userGuess.Length == 5)
                {
                    // Create a ValidateWordRequest object with the user's guess
                    var validateRequest = new ValidateWordRequest { Word = userGuess };

                    // Pass the request to our client instance (DailyWordleService property)
                    var validateResponse = await _wordClient.ValidateWordAsync(validateRequest);


                    // Word not in the list
                    if (!validateResponse.Correct)
                    {
                        // Send a PlayResponse back to the user with all fields populated (this is how it's always done)
                        var response = new PlayResponse
                        {
                            Correct = false,
                            GameOver = false,
                            Guesses = turns,
                            Letters = { },
                            Message = $"'{userGuess}' is not a valid word."
                        };

                        await responseStream.WriteAsync(response);
                        continue;
                    }
                    else
                    {
                        // Valid turn
                        turns++;

                        // Winning logic
                        if (userGuess == wordToGuess)
                        {
                            // Set the loop condition to break out
                            won = true;

                            // Process the letters and return a list
                            var feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                            var response = new PlayResponse
                            {
                                Correct = true,
                                GameOver = true,
                                Guesses = turns,
                                Letters = { feedback },
                                Message = $"Congratulations! You've correctly guessed today's word, '{wordToGuess}'!"
                            };

                            await responseStream.WriteAsync(response);
                            break;
                        }
                        // Incorrect letters or placements
                        else
                        {
                            // Process the letters
                            var feedback = GenerateLetterFeedback(userGuess, wordToGuess);

                            var response = new PlayResponse
                            {
                                Correct = false,
                                GameOver = false,
                                Guesses = turns,
                                Letters = { feedback },
                                Message = ""
                            };

                            await responseStream.WriteAsync(response);
                        }
                    }
                }
            }

            // Write valid games to json file
            if (turns > 0)
            {
                // Lock the file so only one game can update at a time 
                lock (FileLock)
                {
                    // Use Path.Combine() to get a properly formatted path
                    string statsPath = Path.Combine("stats", $"{wordDate}.json");

                    DailyStats stats;

                    // Create directory if it doesn't exist
                    Directory.CreateDirectory("stats");

                    // If today's stats exist, load into a DailyStats object
                    if (File.Exists(statsPath))
                    {
                        string json = File.ReadAllText(statsPath);
                        stats = JsonSerializer.Deserialize<DailyStats>(json)!;

                    }
                    // Else, create a new stats object and assign it to stats
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

                    // Write to file
                    string updatedJson = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(statsPath, updatedJson);

                }
            }
        }


        // Pulls the stats that are saved as DailyStats from the JSON and serves up statistics.
        // This reports on but does not modify the JSON.
        public override Task<StatsResponse> GetStats(StatsRequest request, ServerCallContext context)
        {
            // Grab the date to find or name the json
            string wordDate = DateTime.Now.ToString("yyyy-MM-dd");
            string statsPath = Path.Combine("stats", $"{wordDate}.json");

            lock (FileLock)
            {
                if (!File.Exists(statsPath))
                {
                    // Return zeroed out stats for days not yet recorded
                    return Task.FromResult(new StatsResponse
                    {
                        TotalPlayers = 0,
                        WinPercentage = 0,
                        AverageGuesses = 0
                    });
                }

                string json = File.ReadAllText(statsPath);
                var stats = JsonSerializer.Deserialize<DailyStats>(json)!;


                // Calculate
                double winPercentage = stats.TotalPlayers > 0 ? (double)stats.WinCount / stats.TotalPlayers * 100 : 0;
                double averageGuesses = stats.WinCount > 0 ? stats.GuessDistribution.Sum(g => g.Key * g.Value) / (double)stats.WinCount : 0;

                // Return found stats
                return Task.FromResult(new StatsResponse
                {
                    TotalPlayers = stats.TotalPlayers,
                    WinPercentage = winPercentage,
                    AverageGuesses = averageGuesses
                });
            }
        }


        // Helper methods

        // Processes each letter and returns as a list of Letterfeedback objects: {letter, FeedbackType enum}
        private List<LetterFeedback> GenerateLetterFeedback(string guess, string wordToGuess)
        {
            // Create an array so we can use the index for easy comparison
            var feedback = new LetterFeedback[5];

            // Track how many times the letter has matched so far
            var matchCounts = new Dictionary<char, int>();

            // Find correct letters in correct positions
            for (int i = 0; i < 5; i++)
            {
                if (guess[i] == wordToGuess[i])
                {
                    // Create the feedback object defined in the proto
                    feedback[i] = new LetterFeedback
                    {
                        Letter = guess[i].ToString(),
                        Feedback = FeedbackType.CorrectPosition
                    };

                    // Track the matches (or populate the first appearance)
                    if (!matchCounts.ContainsKey(guess[i]))
                        matchCounts[guess[i]] = 1;
                    else
                        matchCounts[guess[i]]++;
                }
            }

            // Then, check for correct letters in wrong positions, or incorrect letters
            for (int i = 0; i < 5; i++)
            {
                // Skip matches that were handled in the first loop
                if (feedback[i] != null) continue;

                char letter = guess[i];

                // Grab the count of letter instances in correct word
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
                    // Letter is in word but in the wrong position
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
