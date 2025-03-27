namespace WordleGameServer
{
    public class DailyStats
    {
        public string Date { get; set; }
        public int TotalPlayers { get; set; }
        public int WinCount { get; set; }
        public Dictionary<int, int> GuessDistribution { get; set; }

        public DailyStats()
        {
            Date = "";
            GuessDistribution = new Dictionary<int, int>();
        }

        // Updates Total Players, WinCount, and GuessDistribution information
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
