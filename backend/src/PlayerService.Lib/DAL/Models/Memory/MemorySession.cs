namespace PlayerService.Lib.DAL.Models.Memory
{
    public class MemorySession
    {
        private long _lastActiveUtcTicks;

        public required string SessionId { get; init; }
        public required string PlayerId { get; init; }
        public required string DeviceId { get; init; }
        public required DateTime LastActiveUtc
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastActiveUtcTicks);
                var value = new DateTime(ticks, DateTimeKind.Utc);

                return value;
            }
            set
            {
                Interlocked.Exchange(ref _lastActiveUtcTicks, value.Ticks);
            }
        }
    }
}
