using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerStats;

namespace PlayerService.Lib.DAL.Providers.Memory
{
    public class PlayerStatsMemoryProvider : IPlayerStatsProvider
    {
        private readonly MemoryGameDataSource _gameDataSource;
        private readonly TimeSpan _scoreRequestTtl;

        public PlayerStatsMemoryProvider(
            MemoryGameDataSource gameDataSource,
            IConfiguration configuration
        )
        {
            _gameDataSource = gameDataSource;
            _scoreRequestTtl = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.ScoreRequestTtlInSeconds
                )
            );

            if (_scoreRequestTtl <= TimeSpan.Zero)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Score request TTL must be greater than zero.",
                    new
                    {
                        ScoreRequestTtl = _scoreRequestTtl
                    }
                );

                #endregion
            }
        }

        public async Task<ScoreUpdateResult> AddScoreAsync(
            string playerId,
            int points,
            Guid requestId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var player = _gameDataSource.GetRequiredPlayer(playerId);

            await player.PlayerLock.WaitAsync(cancellationToken);

            try
            {
                _gameDataSource.ScoresByPlayerId.TryGetValue(
                    playerId,
                    out var currentScore
                );
                var applied = false;

                if (!player.ScoreRequestsByRequestId.ContainsKey(requestId))
                {
                    currentScore = checked(currentScore + points);
                    var request = new MemoryScoreRequest
                    {
                        ExpiresAtUtc = DateTime.UtcNow.Add(_scoreRequestTtl)
                    };

                    _gameDataSource.ScoresByPlayerId[playerId] = currentScore;
                    player.ScoreRequestsByRequestId[requestId] = request;
                    applied = true;
                }

                var result = new ScoreUpdateResult
                {
                    Stats = CreatePlayerStats(player, currentScore),
                    Applied = applied
                };

                return result;
            }
            finally
            {
                player.PlayerLock.Release();
            }
        }

        public Task<List<ScoreRequestCleanupCandidate>> GetExpiredScoreRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var candidates = _gameDataSource.PlayersById.Values
            .SelectMany(player => player.ScoreRequestsByRequestId
                .Where(entry => entry.Value.ExpiresAtUtc <= now)
                .Select(entry => new ScoreRequestCleanupCandidate
                {
                    PlayerId = player.PlayerId,
                    RequestId = entry.Key
                })
            )
            .ToList();
            return Task.FromResult(candidates);
        }

        public async Task<int> DeleteExpiredScoreRequestsAsync(
            List<ScoreRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deletedCount = 0;
            var candidatesByPlayer = candidates
            .GroupBy(candidate => candidate.PlayerId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

            foreach (var playerCandidates in candidatesByPlayer)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (
                    !_gameDataSource.PlayersById.TryGetValue(
                        playerCandidates.Key,
                        out var player
                    )
                )
                    continue;

                await player.PlayerLock.WaitAsync(cancellationToken);

                try
                {
                    var requestIds = playerCandidates
                    .Select(candidate => candidate.RequestId)
                    .Distinct()
                    .ToList();

                    foreach (var requestId in requestIds)
                    {
                        if (
                            !player.ScoreRequestsByRequestId.TryGetValue(
                                requestId,
                                out var request
                            )
                            || request.ExpiresAtUtc > DateTime.UtcNow
                        )
                            continue;

                        if (
                            player.ScoreRequestsByRequestId.TryRemove(
                                new KeyValuePair<Guid, MemoryScoreRequest>(
                                    requestId,
                                    request
                                )
                            )
                        )
                            deletedCount++;
                    }
                }
                finally
                {
                    player.PlayerLock.Release();
                }
            }

            return deletedCount;
        }

        public Task<PlayerStats?> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!
                _gameDataSource.PlayersById.TryGetValue(
                    playerId,
                    out var player
                )
            )
                return Task.FromResult<PlayerStats?>(null);

            if (!
                _gameDataSource.ScoresByPlayerId.TryGetValue(
                    playerId,
                    out var score
                )
            )
                return Task.FromResult<PlayerStats?>(null);

            var playerStats = CreatePlayerStats(player, score);
            return Task.FromResult<PlayerStats?>(playerStats);
        }

        private static PlayerStats CreatePlayerStats(
            MemoryPlayer player,
            long score
        )
        {
            var playerStats = new PlayerStats
            {
                Score = score,
                GiftsSent = player.GiftsSent,
                GiftsReceived = player.GiftsReceived,
                LastActiveUtc = player.LastActiveUtc
            };

            return playerStats;
        }
    }
}
