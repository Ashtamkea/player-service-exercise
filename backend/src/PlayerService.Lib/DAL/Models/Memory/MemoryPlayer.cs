using System.Collections.Concurrent;

namespace PlayerService.Lib.DAL.Models.Memory
{
    public class MemoryPlayer
    {
        private long _lastActiveUtcTicks;

        public required string PlayerId { get; set; }
        public required long GiftsSent { get; set; }
        public required long GiftsReceived { get; set; }
        public required DateTime LastActiveUtc
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastActiveUtcTicks);
                var value = new DateTime(ticks, DateTimeKind.Utc);

                return value;
            }
            init
            {
                Interlocked.Exchange(ref _lastActiveUtcTicks, value.Ticks);
            }
        }
        public required ConcurrentDictionary<string, MemorySession> ActiveSessionsByDeviceId { get; init; }
        public required ConcurrentDictionary<Guid, MemoryScoreRequest> ScoreRequestsByRequestId { get; init; }
        public required ConcurrentDictionary<Guid, MemoryGiftRequest> GiftRequestsByRequestId { get; init; }
        public MemoryGiftRateLimit? GiftRateLimit { get; set; }
        public required SemaphoreSlim PlayerLock { get; init; }

        public void RecordActivity(DateTime activityUtc)
        {
            var activityTicks = activityUtc.Ticks;
            var observedTicks = Interlocked.Read(ref _lastActiveUtcTicks);

            while (activityTicks > observedTicks)
            {
                var previousTicks = Interlocked.CompareExchange(
                    ref _lastActiveUtcTicks,
                    activityTicks,
                    observedTicks
                );

                if (previousTicks == observedTicks)
                    return;

                observedTicks = previousTicks;
            }
        }
    }
}
