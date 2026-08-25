namespace PlayerService.MockSender
{
    public class BenchmarkResult
    {
        public required string Scenario { get; set; }
        public required string BaseUrl { get; set; }
        public required int BackgroundPlayerCount { get; set; }
        public required int RequestCount { get; set; }
        public required int Concurrency { get; set; }
        public required int WarmupRequestCount { get; set; }
        public required double ElapsedMilliseconds { get; set; }
        public required double ThroughputRequestsPerSecond { get; set; }
        public required double LatencyP50Milliseconds { get; set; }
        public required double LatencyP95Milliseconds { get; set; }
        public required double LatencyP99Milliseconds { get; set; }
        public required double LatencyMaxMilliseconds { get; set; }
        public required IReadOnlyDictionary<int, int> StatusCounts { get; set; }
        public required IReadOnlyDictionary<string, int> OperationCounts { get; set; }
        public required long ExpectedSystemScore { get; set; }
        public required long ActualSystemScore { get; set; }
        public required long ExpectedHotPlayerScore { get; set; }
        public required long ActualHotPlayerScore { get; set; }
        public required long MinimumPlayerScore { get; set; }
        public required long ExpectedGiftsCount { get; set; }
        public required long ActualGiftsSentCount { get; set; }
        public required long ActualGiftsReceivedCount { get; set; }
        public required int LogicalProcessorCount { get; set; }
        public required string Framework { get; set; }
        public required string OperatingSystem { get; set; }
    }
}
