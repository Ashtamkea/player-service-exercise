namespace PlayerService.Shared.Models.PlayerGifts.Enums
{
    public enum GiftOperationStatus
    {
        Succeeded,
        RecipientOffline,
        InsufficientPoints,
        RecipientScoreLimitExceeded,
        RateLimited,
        TemporarilyUnavailable
    }
}
