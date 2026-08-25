using System.ComponentModel.DataAnnotations;

namespace PlayerService.Shared.Models.Sessions
{
    public class LoginRequest
    {
        [Required]
        [MinLength(1)]
        public required string PlayerId { get; set; }

        [Required]
        [MinLength(1)]
        public required string DeviceId { get; set; }
    }
}
