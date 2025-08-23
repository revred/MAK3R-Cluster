namespace MAK3R.Shared.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    string? RegistrationId,
    string? TaxId,
    string? Industry,
    string? Website,
    DateTime CreatedUtc,
    DateTime UpdatedUtc
);

public record CreateCompanyRequest(
    string Name,
    string? RegistrationId,
    string? TaxId,
    string? Industry,
    string? Website
);

public record UpdateCompanyRequest(
    string Name,
    string? RegistrationId,
    string? TaxId,
    string? Industry,
    string? Website
);