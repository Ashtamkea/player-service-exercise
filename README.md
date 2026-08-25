# Player Service Exercise

## Concurrency and session policy

I avoid locks for ordinary business-data reads and introduce them only where an
operation must coordinate state changes. Each device has its own lock, which
serializes session creation, authentication refresh, replacement after expiration,
and cleanup for that device. The same player may have active sessions on multiple
devices, so concurrent logins from different devices are allowed. Two concurrent
logins for the same device still result in one successful session and one conflict.

Each player also has a lock protecting score updates, idempotency records, gift
counters, session indexes, and gifting. A gift acquires both player locks using
ordinal lock ordering, so reverse gifts and overlapping player pairs cannot
deadlock. Protected reads briefly acquire their device lock because authentication
refreshes session activity, but the underlying stats and leaderboard reads do not
acquire player locks.

## Point conservation

I verify point conservation with a system test that records the initial sum of all
player balances and then executes thousands of randomized concurrent gifts across
reverse and overlapping player pairs. The workload includes repeated request IDs
and retries to prove that every gift is applied at most once.

After the workload completes, the test verifies that the final sum equals the
initial sum, no balance is negative, and the total number of gifts sent equals the
total number received. Score-addition requests are excluded from the conservation
phase because they intentionally create points.

## Leaderboard design and staleness

The leaderboard is an immutable snapshot containing at most the configured `N`
players, already ordered by score. Every three minutes, a background worker scans
all `P` player scores and selects the Top N using a bounded minimum heap. Selection
costs `O(P log N)`, and ordering the selected entries costs `O(N log N)`. Reading
the cached snapshot reference is `O(1)`.

A reader-writer score-snapshot gate ensures that snapshot capture cannot observe
only the debit or credit side of a gift. The previous snapshot remains available
until a successful background refresh atomically replaces it. The cache has no
request-triggered regeneration or expiration, which avoids a cache stampede, also
known as a thundering herd.

Under normal operation, a score change appears within three minutes plus snapshot
generation time. I consider this acceptable for a global leaderboard, where small
temporary differences are less noticeable than in a small social leaderboard. The
interval can be shortened or extended based on user research. Repeated refresh
failures can extend staleness because the previous valid snapshot is intentionally
retained.

## Operational trade-offs

Gifting has a configurable per-sender rate limit, currently set to 50 unique
attempts per minute. Expiration uses hosted workers that periodically discover and
delete expired sessions and idempotency records. This creates periodic processing
pressure. Native TTL-based deletion would be cleaner in a distributed store, but I
kept the explicit cleanup design to avoid gold-plating the in-memory assignment.

I initially explored Redis. Sharding player scores would improve scalability, but
it conflicts with the requirement to update two independently placed players in
one strictly transactional gift. I therefore redesigned the service around a
single-process in-memory store to demonstrate the required concurrency,
transactionality, and conservation semantics clearly. The trade-off is that state
and idempotency are process-local, so multi-instance consistency is not supported.

I also added basic structured, correlation-aware logging for debugging and as a
foundation for operational counters and metrics.

## Benchmark

I ran the API locally in Release mode with 64 concurrent workers, one hot player,
and 128 less-active background players. Each run began with 1,000 warmup requests,
followed by 10,000 measured requests. Every measured run executed 5,000 score
updates on the hot player, 3,500 gifts into the hot player, 1,000 background-player
score updates, 475 gifts between background players, and 25 gifts from the hot
player. Every request used a unique request ID.

The three runs achieved 18,654, 33,854, and 48,618 requests per second. Median
throughput was 33,854 requests per second, with median latency of 1.8 ms at p50,
3.6 ms at p95, and 4.9 ms at p99.

All 30,000 measured requests succeeded. Every run preserved the expected total
points, produced the expected hot-player score, kept every balance nonnegative,
and recorded matching totals of 4,320 gifts sent and received. These are localhost,
single-process synthetic results and should not be treated as a production capacity
claim.
