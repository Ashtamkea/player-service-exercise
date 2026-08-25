namespace PlayerService.Shared.Configuration
{
    public static class ConfigurationKeys
    {
        public const string PlayerServiceSection = "PlayerService";
        public const string SessionTtlInSeconds = "sessionTtlInSeconds";
        public const string SessionCleanupIntervalInSeconds = "sessionCleanupIntervalInSeconds";
        public const string ScoreRequestTtlInSeconds = "scoreRequestTtlInSeconds";
        public const string GiftRequestTtlInSeconds = "giftRequestTtlInSeconds";
        public const string GiftRateLimitWindowInSeconds = "giftRateLimitWindowInSeconds";
        public const string GiftRateLimitMaxRequests = "giftRateLimitMaxRequests";
        public const string LeaderboardTopSize = "leaderboardTopSize";
        public const string LeaderboardPollIntervalInSeconds = "leaderboardPollIntervalInSeconds";
    }
}
