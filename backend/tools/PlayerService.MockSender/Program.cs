using System.Text.Json;

namespace PlayerService.MockSender
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var options = BenchmarkOptions.Parse(args);
                using var client = new HttpClient
                {
                    BaseAddress = options.BaseUrl,
                    Timeout = TimeSpan.FromMinutes(5)
                };
                var benchmark = new HotPlayerBenchmark(client, options);
                var result = await benchmark.RunAsync();

                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                ));

                return result.ActualSystemScore == result.ExpectedSystemScore
                    && result.ActualHotPlayerScore
                    == result.ExpectedHotPlayerScore
                    && result.MinimumPlayerScore >= 0
                    && result.ActualGiftsSentCount
                    == result.ExpectedGiftsCount
                    && result.ActualGiftsReceivedCount
                    == result.ExpectedGiftsCount
                    && result.StatusCounts.Count == 1
                    && result.StatusCounts.ContainsKey(200)
                    ? 0
                    : 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
