namespace PlayerService.MockSender
{
    public class BenchmarkOptions
    {
        public Uri BaseUrl { get; private set; } = new("http://127.0.0.1:51983");
        public int BackgroundPlayerCount { get; private set; } = 128;
        public int Concurrency { get; private set; } = 64;
        public int PointsPerRequest { get; private set; } = 1;
        public int RequestCount { get; private set; } = 10000;
        public int WarmupRequestCount { get; private set; } = 1000;

        public static BenchmarkOptions Parse(string[] args)
        {
            var options = new BenchmarkOptions();

            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException($"A value is required for '{args[index]}'.");

                var name = args[index];
                var value = args[index + 1];

                switch (name)
                {
                    case "--background-players":
                        options.BackgroundPlayerCount = ParsePositiveInt(
                            name,
                            value
                        );
                        break;
                    case "--base-url":
                        options.BaseUrl = new Uri(value, UriKind.Absolute);
                        break;
                    case "--concurrency":
                        options.Concurrency = ParsePositiveInt(name, value);
                        break;
                    case "--points":
                        options.PointsPerRequest = ParsePositiveInt(name, value);
                        break;
                    case "--requests":
                        options.RequestCount = ParsePositiveInt(name, value);
                        break;
                    case "--warmup":
                        options.WarmupRequestCount = ParseNonNegativeInt(name, value);
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{name}'.");
                }
            }

            if (options.BackgroundPlayerCount < 2)
            {
                throw new ArgumentException(
                    "'--background-players' must be at least two."
                );
            }

            return options;
        }

        private static int ParseNonNegativeInt(string name, string value)
        {
            if (int.TryParse(value, out var parsedValue) && parsedValue >= 0)
                return parsedValue;

            throw new ArgumentException($"'{name}' must be a non-negative integer.");
        }

        private static int ParsePositiveInt(string name, string value)
        {
            if (int.TryParse(value, out var parsedValue) && parsedValue > 0)
                return parsedValue;

            throw new ArgumentException($"'{name}' must be a positive integer.");
        }
    }
}
