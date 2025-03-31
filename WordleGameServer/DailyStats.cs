// WordleGameServer.DailyStats.cs
// K.Hira, R.Sweet
// April 4, 2025
// Class for storing and managing daily Wordle gameplay statistics.

namespace WordleGameServer
{
    /// <summary>
    /// Represents the daily statistics for Wordle gameplay.
    /// </summary>
    public class DailyStats
    {
        // Members

        // <summary>
        /// The date for which these statistics apply (formatted as YYYY-MM-DD).
        /// </summary>
        public string Date { get; set; }
        /// <summary>
        /// The total number of players who attempted the Wordle.
        /// </summary>
        public int TotalPlayers { get; set; }
        /// <summary>
        /// The total number of players who successfully guessed the Wordle.
        /// </summary>
        public int WinCount { get; set; }
        /// <summary>
        /// Distribution of guesses required to win (key = number of guesses, value = frequency).
        /// </summary>
        public Dictionary<int, int> GuessDistribution { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DailyStats"/> class with default values.
        /// </summary>
        public DailyStats()
        {
            Date = "";
            GuessDistribution = new Dictionary<int, int>();
        }

        /// <summary>
        /// Records a completed game attempt, updating player counts and guess distribution.
        /// </summary>
        /// <param name="won">Indicates if the player won the game.</param>
        /// <param name="numGuesses">The number of guesses taken by the player.</param>
        public void RecordGame(bool won, int numGuesses)
        {
            TotalPlayers++;

            if (won)
            {
                WinCount++;

                // If the number of guesses (ie. 1-6) exists, increment
                if (GuessDistribution.ContainsKey(numGuesses))
                {
                    GuessDistribution[numGuesses]++;
                }
                // Else create it and give it an initial value
                else
                {
                    GuessDistribution[numGuesses] = 1;
                }
            }
        }
    }
}
