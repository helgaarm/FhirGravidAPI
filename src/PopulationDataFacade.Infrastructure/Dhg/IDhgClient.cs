using PopulationDataFacade.Core;

namespace PopulationDataFacade.Infrastructure.Dhg;

public interface IDhgClient
{
    Task<DhgStatusResponse> GetStatusAsync(PatientRequestContext context, CancellationToken cancellationToken);
    Task<DhgMaternityRecord> GetRecordAsync(string recordId, PatientRequestContext context, CancellationToken cancellationToken);
}

public sealed record DhgAuthorization(string AccessToken, string DPoPProof);

public interface IDhgAuthorizationProvider
{
    Task<DhgAuthorization> AuthorizeAsync(
        string subjectToken,
        HttpMethod method,
        Uri destination,
        string? dPoPNonce,
        CancellationToken cancellationToken);
}
