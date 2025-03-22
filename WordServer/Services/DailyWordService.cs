using Grpc.Core;
using System.Text.Json;

namespace WordServer
{
    public class DailyWordService : DailyWord.DailyWordBase
    {
        // Members
        private readonly string _wordleFilepath = "wordle.json";
        private readonly List<string> _words;
        private static readonly DateTime _referenceDate = new DateTime(2025, 1, 1);

        // Constructor loads the words from the JSON file
        public DailyWordService()
        {
            var json = File.ReadAllText(_wordleFilepath);
            _words = JsonSerializer.Deserialize<List<string>>(json) ?? 
                     throw new InvalidOperationException("Failed to load words.");
        }

        // GetWord method returns the word of the day based on the current date
        public override Task<GetWordResponse> GetWord(GetWordRequest request, ServerCallContext context)
        {
            var dateDifference = DateTime.Today - _referenceDate;
            var index = dateDifference.Days % _words.Count;

            return Task.FromResult(new GetWordResponse
            {
                Word = _words[index]
            });
        }

        // ValidateWord method checks if the user's guess is in the list of words
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
