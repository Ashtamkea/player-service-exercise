using System.ComponentModel.DataAnnotations;

namespace PlayerService.Shared.Models.PlayerStats
{
    public class AddScoreRequest
    {
        [Range(1, int.MaxValue)]
        public required int Points { get; set; }

        public required Guid RequestId { get; set; }
    }
}
