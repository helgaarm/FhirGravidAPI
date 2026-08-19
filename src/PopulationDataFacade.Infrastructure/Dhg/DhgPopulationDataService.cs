using PopulationDataFacade.Core;

namespace PopulationDataFacade.Infrastructure.Dhg;

public sealed class DhgPopulationDataService(
    IDhgClient client,
    DhgPopulationSnapshotFactory snapshotFactory) : IPopulationDataService
{
    public async Task<PopulationSnapshot> GetSnapshotAsync(PatientRequestContext context, CancellationToken cancellationToken)
    {
        var status = await client.GetStatusAsync(context, cancellationToken);
        if (status.HasGivenConsent != true)
            throw new PopulationDataException(PopulationErrorKind.ConsentMissing, "The patient has not consented to DHG data sharing.");
        if (status.Deceased == true)
            throw new PopulationDataException(PopulationErrorKind.Forbidden, "DHG data is not available for this patient context.");
        if (status.HasActiveMaternityRecord != true || string.IsNullOrWhiteSpace(status.LatestRecordId))
            throw new PopulationDataException(PopulationErrorKind.NoActiveMaternityRecord, "No active maternity record exists.");

        var record = await client.GetRecordAsync(status.LatestRecordId, context, cancellationToken);
        if (!string.Equals(record.Metadata?.RecordId, status.LatestRecordId, StringComparison.OrdinalIgnoreCase))
            throw new PopulationDataException(PopulationErrorKind.SourceContractInvalid, "DHG record identity did not match latestRecordId.");
        if (!string.Equals(record.Metadata?.RecordStatus?.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new PopulationDataException(PopulationErrorKind.NoActiveMaternityRecord, "The latest DHG record is not active.");

        return snapshotFactory.Create(context.LogicalId, status, record);
    }
}
