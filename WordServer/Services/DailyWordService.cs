// WordServer.Services.DailyWordService.cs
// K.Hira, R.Sweet
// April 4, 2025
// Implements the gRPC service for providing the daily Wordle word and validating user guesses.
using Grpc.Core;
using System.Text.Json;

namespace WordServer
{
    /// <summary>
    /// The DailyWordService class provides gRPC methods to serve the daily Wordle word
    /// and validate user guesses. It loads words from a JSON file and selects a word 
    /// based on the current date.
    /// </summary>
    public class DailyWordService : DailyWord.DailyWordBase
    {
        // Members

        /// <summary>
        /// File path to the JSON file containing the list of valid Wordle words.
        /// </summary>
        private readonly string _wordleFilepath = "wordle.json";

        /// <summary>
        /// List of words loaded from the JSON file.
        /// </summary>
        private readonly List<string> _words;

        /// <summary>
        /// A fixed reference date used for calculating the word of the day.
        /// </summary>
        private static readonly DateTime _referenceDate = new DateTime(2025, 1, 1);


        /// <summary>
        /// Constructor that initializes the DailyWordService by loading the list of words from the JSON file.
        /// Throws an exception if the file cannot be read or parsed.
        /// </summary>
        public DailyWordService()
        {
            var json = File.ReadAllText(_wordleFilepath);
            _words = JsonSerializer.Deserialize<List<string>>(json) ?? 
                     throw new InvalidOperationException("Failed to load words.");
        }

        /// <summary>
        /// Retrieves the word of the day based on the current date.
        /// The word is determined by calculating the number of days since the reference date
        /// and using modulo arithmetic to select a word from the list.
        /// </summary>
        /// <param name="request">An empty GetWordRequest object.</param>
        /// <param name="context">Provides metadata, deadlines, and cancellation support for the gRPC call.</param>
        /// <returns>A GetWordResponse containing the word of the day.</returns>
        public override Task<GetWordResponse> GetWord(GetWordRequest request, ServerCallContext context)
        {
            var dateDifference = DateTime.Today - _referenceDate;
            var index = dateDifference.Days % _words.Count;

            return Task.FromResult(new GetWordResponse
            {
                Word = _words[index]
            });
        }

        /// <summary>
        /// Validates whether a given word exists in the predefined list of words.
        /// </summary>
        /// <param name="request">A ValidateWordRequest containing the user's guessed word.</param>
        /// <param name="context">Provides metadata, deadlines, and cancellation support for the gRPC call.</param>
        /// <returns>A ValidateWordResponse indicating whether the word is valid.</returns>
        public override Task<ValidateWordResponse> ValidateWord(ValidateWordRequest request, ServerCallContext context)
        {
            var isValid = _words.Contains(request.Word);
            return Task.FromResult(new ValidateWordResponse
            {
                Correct = isValid
            });
        }
    }
}
