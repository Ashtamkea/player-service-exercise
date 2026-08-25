using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftRequest
    {
        [Required]
        [JsonPropertyName("toPlayerId")]
        public required string RecipientPlayerId { get; set; }

        [Range(1, int.MaxValue)]
        public required int Points { get; set; }

        public required Guid RequestId { get; set; }
    }
}
