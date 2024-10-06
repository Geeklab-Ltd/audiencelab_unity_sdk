namespace Geeklab.AudiencelabSDK
{
    public class MetricsModel
    {
        public int DaysLoggedIn { get; set; }
        public float SessionTime { get; set; }
        public int LevelPassed { get; set; }

        public float ValueOfPurchase { get; set; }
        public string IdOfPurchasedItem { get; set; }
        public string IdOfAdWatched { get; set; }
        public int WatchedSeconds { get; set; }
    }
}