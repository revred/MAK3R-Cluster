using MAK3R.Core;
using MAK3R.DigitalTwin.Entities;
using MAK3R.Shared.DTOs;

namespace MAK3R.DigitalTwin.Services;

public interface ITwinOrchestrator
{
    Task<Result<OnboardingResult>> CreateDigitalTwinAsync(OnboardingWizardDto wizardData, CancellationToken ct = default);
    Task<Result<Company>> GetCompanyTwinAsync(Guid companyId, CancellationToken ct = default);
    Task<Result<TwinValidationResult>> ValidateTwinAsync(Guid companyId, CancellationToken ct = default);
    Task<Result> UpdateTwinFromConnectorAsync(Guid companyId, string connectorId, CancellationToken ct = default);
}

public record TwinValidationResult(
    List<TwinGap> Gaps,
    double ConfidenceScore,
    List<string> Recommendations
);

public record TwinGap(
    string EntityType,
    string EntityId,
    string GapType,
    string Description,
    string Severity
);