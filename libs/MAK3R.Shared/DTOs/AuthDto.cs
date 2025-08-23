using System.ComponentModel.DataAnnotations;

namespace MAK3R.Shared.DTOs;

public record LoginDto(
    [Required] [EmailAddress] string Email,
    [Required] [MinLength(6)] string Password
);

public record RegisterDto(
    [Required] [EmailAddress] string Email,
    [Required] [MinLength(6)] string Password,
    [Required] string ConfirmPassword
);

public record AuthResponseDto(
    string Token,
    string Email,
    DateTime ExpiresAt
);