namespace PlayerService.Shared.Configuration
{
    public static class ConstantValues
    {
        public const string ServiceName = "PlayerService";
        public const string DeviceIdHeader = "X-Device-Id";
        public const string SessionContextItemName = "SessionContext";
        public const string SessionBearerSecurityScheme = "SessionBearer";
        public const string SessionDeviceSecurityScheme = "SessionDevice";
        public const long InitialPlayerScore = 1000;
        public const string GiftSucceededOutcome = "succeeded";
        public const string GiftRecipientOfflineOutcome = "recipientOffline";
        public const string GiftInsufficientPointsOutcome = "insufficientPoints";
        public const string GiftRecipientScoreLimitExceededOutcome = "recipientScoreLimitExceeded";
    }
}
