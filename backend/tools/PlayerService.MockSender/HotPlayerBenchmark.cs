using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;

namespace PlayerService.MockSender
{
    public class HotPlayerBenchmark
    {
        private const int InitialPlayerScore = 1000;
        private const int WorkloadPatternSize = 400;
        private readonly HttpClient _client;
        private readonly BenchmarkOptions _options;

        public HotPlayerBenchmark(HttpClient client, BenchmarkOptions options)
        {
            _client = client;
            _options = options;
        }

        public async Task<BenchmarkResult> RunAsync(
            CancellationToken cancellationToken = default
        )
        {
            var context = await CreateContextAsync(cancellationToken);
            var warmupResult = await ExecuteBatchAsync(
                context,
                _options.WarmupRequestCount,
                sequenceOffset: 0,
                captureLatency: false,
                cancellationToken
            );
            EnsureAllRequestsSucceeded(warmupResult.StatusCounts);

            var stopwatch = Stopwatch.StartNew();
            var measuredResult = await ExecuteBatchAsync(
                context,
                _options.RequestCount,
                sequenceOffset: _options.WarmupRequestCount,
                captureLatency: true,
                cancellationToken
            );
            stopwatch.Stop();

            var playerStats = await GetAllPlayerStatsAsync(
                context,
                cancellationToken
            );
            var combinedOperationCounts = CombineOperationCounts(
                warmupResult.OperationCounts,
                measuredResult.OperationCounts
            );
            var expectedScoreAdditions = checked(
                (long)(
                    GetOperationCount(
                        combinedOperationCounts,
                        BenchmarkOperationType.HotPlayerScore
                    )
                    + GetOperationCount(
                        combinedOperationCounts,
                        BenchmarkOperationType.BackgroundPlayerScore
                    )
                )
                * _options.PointsPerRequest
            );
            var expectedSystemScore = checked(
                (long)(_options.BackgroundPlayerCount + 1)
                * InitialPlayerScore
                + expectedScoreAdditions
            );
            var expectedHotPlayerScore = checked(
                InitialPlayerScore
                + (long)GetOperationCount(
                    combinedOperationCounts,
                    BenchmarkOperationType.HotPlayerScore
                )
                * _options.PointsPerRequest
                + (long)GetOperationCount(
                    combinedOperationCounts,
                    BenchmarkOperationType.BackgroundToHotPlayerGift
                )
                * _options.PointsPerRequest
                - (long)GetOperationCount(
                    combinedOperationCounts,
                    BenchmarkOperationType.HotPlayerToBackgroundGift
                )
                * _options.PointsPerRequest
            );
            var expectedGiftsCount = GetGiftCount(combinedOperationCounts);
            var sortedLatencies = measuredResult.LatenciesMilliseconds;
            Array.Sort(sortedLatencies);

            return new BenchmarkResult
            {
                Scenario = "Mixed traffic centered on one hot player",
                BaseUrl = _options.BaseUrl.ToString(),
                BackgroundPlayerCount = _options.BackgroundPlayerCount,
                RequestCount = _options.RequestCount,
                Concurrency = _options.Concurrency,
                WarmupRequestCount = _options.WarmupRequestCount,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                ThroughputRequestsPerSecond =
                    _options.RequestCount / stopwatch.Elapsed.TotalSeconds,
                LatencyP50Milliseconds = GetPercentile(sortedLatencies, 0.50),
                LatencyP95Milliseconds = GetPercentile(sortedLatencies, 0.95),
                LatencyP99Milliseconds = GetPercentile(sortedLatencies, 0.99),
                LatencyMaxMilliseconds = sortedLatencies[^1],
                StatusCounts = measuredResult.StatusCounts,
                OperationCounts = measuredResult.OperationCounts,
                ExpectedSystemScore = expectedSystemScore,
                ActualSystemScore = playerStats.Sum(stats => stats.Score),
                ExpectedHotPlayerScore = expectedHotPlayerScore,
                ActualHotPlayerScore = playerStats[0].Score,
                MinimumPlayerScore = playerStats.Min(stats => stats.Score),
                ExpectedGiftsCount = expectedGiftsCount,
                ActualGiftsSentCount = playerStats.Sum(stats => stats.GiftsSent),
                ActualGiftsReceivedCount = playerStats.Sum(
                    stats => stats.GiftsReceived
                ),
                LogicalProcessorCount = Environment.ProcessorCount,
                Framework = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription
            };
        }

        private async Task<BenchmarkContext> CreateContextAsync(
            CancellationToken cancellationToken
        )
        {
            var hotPlayerId = $"benchmark-hot-player-{Guid.NewGuid():N}";
            var hotPlayerSessions = await Task.WhenAll(
                Enumerable.Range(0, _options.Concurrency)
                .Select(index => LoginAsync(
                    hotPlayerId,
                    $"benchmark-hot-device-{index:D4}-{Guid.NewGuid():N}",
                    cancellationToken
                ))
            );
            var backgroundPlayers = await Task.WhenAll(
                Enumerable.Range(0, _options.BackgroundPlayerCount)
                .Select(index =>
                {
                    var playerId = $"benchmark-background-{index:D4}-{Guid.NewGuid():N}";

                    return LoginAsync(
                        playerId,
                        $"benchmark-background-device-{index:D4}-{Guid.NewGuid():N}",
                        cancellationToken
                    );
                })
            );

            return new BenchmarkContext(
                hotPlayerSessions,
                backgroundPlayers
            );
        }

        private async Task<BatchResult> ExecuteBatchAsync(
            BenchmarkContext context,
            int requestCount,
            int sequenceOffset,
            bool captureLatency,
            CancellationToken cancellationToken
        )
        {
            if (requestCount == 0)
            {
                return new BatchResult(
                    [],
                    new Dictionary<int, int>(),
                    new Dictionary<string, int>()
                );
            }

            var operations = Enumerable.Range(0, requestCount)
            .Select(index => CreateOperation(
                sequenceOffset + index,
                context.BackgroundPlayers.Length
            ))
            .ToArray();
            var latencies = captureLatency
                ? new double[requestCount]
                : [];
            var statusCounts = new ConcurrentDictionary<int, int>();
            var nextRequestIndex = -1;
            var workerTasks = context.HotPlayerSessions.Select(
                hotPlayerSession => ExecuteWorkerAsync(hotPlayerSession)
            );

            await Task.WhenAll(workerTasks);

            var operationCounts = operations
            .GroupBy(operation => operation.Type)
            .ToDictionary(
                group => group.Key.ToString(),
                group => group.Count(),
                StringComparer.Ordinal
            );

            return new BatchResult(
                latencies,
                new SortedDictionary<int, int>(statusCounts),
                new SortedDictionary<string, int>(
                    operationCounts,
                    StringComparer.Ordinal
                )
            );

            async Task ExecuteWorkerAsync(PlayerIdentity hotPlayerSession)
            {
                while (true)
                {
                    var requestIndex = Interlocked.Increment(
                        ref nextRequestIndex
                    );

                    if (requestIndex >= requestCount)
                        return;

                    cancellationToken.ThrowIfCancellationRequested();
                    var startedAt = Stopwatch.GetTimestamp();
                    var statusCode = await ExecuteOperationAsync(
                        operations[requestIndex],
                        context,
                        hotPlayerSession,
                        cancellationToken
                    );

                    if (captureLatency)
                    {
                        latencies[requestIndex] = Stopwatch
                        .GetElapsedTime(startedAt)
                        .TotalMilliseconds;
                    }

                    statusCounts.AddOrUpdate(
                        statusCode,
                        1,
                        (_, count) => count + 1
                    );
                }
            }
        }

        private Task<int> ExecuteOperationAsync(
            BenchmarkOperation operation,
            BenchmarkContext context,
            PlayerIdentity hotPlayerSession,
            CancellationToken cancellationToken
        )
        {
            var backgroundPlayer = context.BackgroundPlayers[
                operation.BackgroundPlayerIndex
            ];

            return operation.Type switch
            {
                BenchmarkOperationType.HotPlayerScore => AddScoreAsync(
                    hotPlayerSession,
                    cancellationToken
                ),
                BenchmarkOperationType.BackgroundPlayerScore => AddScoreAsync(
                    backgroundPlayer,
                    cancellationToken
                ),
                BenchmarkOperationType.BackgroundToHotPlayerGift => GiftAsync(
                    backgroundPlayer,
                    hotPlayerSession.PlayerId,
                    cancellationToken
                ),
                BenchmarkOperationType.HotPlayerToBackgroundGift => GiftAsync(
                    hotPlayerSession,
                    backgroundPlayer.PlayerId,
                    cancellationToken
                ),
                BenchmarkOperationType.BackgroundToBackgroundGift => GiftAsync(
                    backgroundPlayer,
                    context.BackgroundPlayers[
                        operation.RecipientBackgroundPlayerIndex
                    ].PlayerId,
                    cancellationToken
                ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(operation.Type)
                )
            };
        }

        private static BenchmarkOperation CreateOperation(
            int sequenceIndex,
            int backgroundPlayerCount
        )
        {
            var patternIndex = sequenceIndex % WorkloadPatternSize;
            var backgroundPlayerIndex = sequenceIndex % backgroundPlayerCount;
            var recipientBackgroundPlayerIndex =
                (backgroundPlayerIndex + 1) % backgroundPlayerCount;
            var operationType = patternIndex switch
            {
                < 200 => BenchmarkOperationType.HotPlayerScore,
                < 340 => BenchmarkOperationType.BackgroundToHotPlayerGift,
                < 380 => BenchmarkOperationType.BackgroundPlayerScore,
                < 399 => BenchmarkOperationType.BackgroundToBackgroundGift,
                _ => BenchmarkOperationType.HotPlayerToBackgroundGift
            };

            return new BenchmarkOperation(
                operationType,
                backgroundPlayerIndex,
                recipientBackgroundPlayerIndex
            );
        }

        private async Task<int> AddScoreAsync(
            PlayerIdentity player,
            CancellationToken cancellationToken
        )
        {
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Post,
                $"/players/{player.PlayerId}/stats/score",
                player
            );
            request.Content = JsonContent.Create(new AddScoreRequest
            {
                Points = _options.PointsPerRequest,
                RequestId = Guid.NewGuid()
            });

            return await SendAsync(request, cancellationToken);
        }

        private async Task<int> GiftAsync(
            PlayerIdentity sender,
            string recipientPlayerId,
            CancellationToken cancellationToken
        )
        {
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Post,
                $"/players/{sender.PlayerId}/gifts",
                sender
            );
            request.Content = JsonContent.Create(new GiftRequest
            {
                RecipientPlayerId = recipientPlayerId,
                Points = _options.PointsPerRequest,
                RequestId = Guid.NewGuid()
            });

            return await SendAsync(request, cancellationToken);
        }

        private async Task<int> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            using var response = await _client.SendAsync(
                request,
                cancellationToken
            );
            await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return (int)response.StatusCode;
        }

        private async Task<PlayerStats[]> GetAllPlayerStatsAsync(
            BenchmarkContext context,
            CancellationToken cancellationToken
        )
        {
            var playerIdentities = context.BackgroundPlayers
            .Prepend(context.HotPlayerSessions[0]);

            return await Task.WhenAll(
                playerIdentities.Select(player => GetStatsAsync(
                    player,
                    cancellationToken
                ))
            );
        }

        private async Task<PlayerStats> GetStatsAsync(
            PlayerIdentity player,
            CancellationToken cancellationToken
        )
        {
            using var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/players/{player.PlayerId}/stats",
                player
            );
            using var response = await _client.SendAsync(
                request,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<PlayerStats>(
                cancellationToken
            ))!;
        }

        private async Task<PlayerIdentity> LoginAsync(
            string playerId,
            string deviceId,
            CancellationToken cancellationToken
        )
        {
            using var response = await _client.PostAsJsonAsync(
                "/login",
                new LoginRequest
                {
                    PlayerId = playerId,
                    DeviceId = deviceId
                },
                cancellationToken
            );
            response.EnsureSuccessStatusCode();

            return new PlayerIdentity
            {
                PlayerId = playerId,
                DeviceId = deviceId,
                SessionId = await response.Content.ReadAsStringAsync(
                    cancellationToken
                )
            };
        }

        private static HttpRequestMessage CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            PlayerIdentity player
        )
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                player.SessionId
            );
            request.Headers.Add(
                ConstantValues.DeviceIdHeader,
                player.DeviceId
            );

            return request;
        }

        private static Dictionary<string, int> CombineOperationCounts(
            IReadOnlyDictionary<string, int> left,
            IReadOnlyDictionary<string, int> right
        )
        {
            var combined = new Dictionary<string, int>(
                left,
                StringComparer.Ordinal
            );

            foreach (var entry in right)
            {
                combined[entry.Key] = combined.GetValueOrDefault(entry.Key)
                    + entry.Value;
            }

            return combined;
        }

        private static int GetOperationCount(
            IReadOnlyDictionary<string, int> operationCounts,
            BenchmarkOperationType operationType
        )
        {
            return operationCounts.GetValueOrDefault(operationType.ToString());
        }

        private static int GetGiftCount(
            IReadOnlyDictionary<string, int> operationCounts
        )
        {
            return GetOperationCount(
                operationCounts,
                BenchmarkOperationType.BackgroundToHotPlayerGift
            )
            + GetOperationCount(
                operationCounts,
                BenchmarkOperationType.HotPlayerToBackgroundGift
            )
            + GetOperationCount(
                operationCounts,
                BenchmarkOperationType.BackgroundToBackgroundGift
            );
        }

        private static void EnsureAllRequestsSucceeded(
            IReadOnlyDictionary<int, int> statusCounts
        )
        {
            if (
                statusCounts.Count == 0
                || statusCounts.Count == 1
                && statusCounts.TryGetValue(200, out _)
            )
                return;

            var statusSummary = string.Join(
                ", ",
                statusCounts
                .OrderBy(entry => entry.Key)
                .Select(entry => $"{entry.Key}={entry.Value}")
            );
            throw new InvalidOperationException(
                $"The warmup received unsuccessful responses: {statusSummary}."
            );
        }

        private static double GetPercentile(
            double[] sortedValues,
            double percentile
        )
        {
            var index = Math.Max(
                0,
                (int)Math.Ceiling(percentile * sortedValues.Length) - 1
            );

            return sortedValues[index];
        }

        private enum BenchmarkOperationType
        {
            HotPlayerScore,
            BackgroundPlayerScore,
            BackgroundToHotPlayerGift,
            HotPlayerToBackgroundGift,
            BackgroundToBackgroundGift
        }

        private sealed record BenchmarkOperation(
            BenchmarkOperationType Type,
            int BackgroundPlayerIndex,
            int RecipientBackgroundPlayerIndex
        );

        private sealed record BenchmarkContext(
            PlayerIdentity[] HotPlayerSessions,
            PlayerIdentity[] BackgroundPlayers
        );

        private sealed record BatchResult(
            double[] LatenciesMilliseconds,
            IReadOnlyDictionary<int, int> StatusCounts,
            IReadOnlyDictionary<string, int> OperationCounts
        );
    }
}
