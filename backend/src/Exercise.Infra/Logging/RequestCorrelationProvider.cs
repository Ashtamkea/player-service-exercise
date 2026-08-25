using Microsoft.AspNetCore.Http;

namespace Exercise.Infra.Logging
{
    public class RequestCorrelationProvider : ICorrelationCreator, ICorrelationProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RequestCorrelationProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Correlation CreateCorrelation(string? correlationId = null)
        {
            var correlation = new Correlation
            {
                Id = string.IsNullOrWhiteSpace(correlationId)
                ? Guid.NewGuid().ToString("N")
                : correlationId
            };

            return correlation;
        }

        public Correlation GetCorrelation()
        {
            var items = _httpContextAccessor.HttpContext?.Items;
            var correlation = items?[nameof(Correlation)] as Correlation;

            if (correlation is not null)
                return correlation;

            var newCorrelation = CreateCorrelation();
            SetCorrelation(newCorrelation);

            return newCorrelation;
        }

        public void SetCorrelation(Correlation correlation)
        {
            var items = _httpContextAccessor.HttpContext?.Items;

            if (items is null)
                return;

            items[nameof(Correlation)] = correlation;
        }
    }
}
