using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Dhg;
using Xunit;

namespace PopulationDataFacade.Tests;

public sealed class DhgPopulationDataServiceTests
{
    private const string RecordId = "0f0b2f66-34f2-490b-a089-aaa6aa4c9825";

    [Theory]
    [InlineData(false, false, false, RecordId, PopulationErrorKind.ConsentMissing)]
    [InlineData(true, true, true, RecordId, PopulationErrorKind.Forbidden)]
    [InlineData(true, false, false, RecordId, PopulationErrorKind.NoActiveMaternityRecord)]
    [InlineData(true, false, true, null, PopulationErrorKind.NoActiveMaternityRecord)]
    public async Task Status_gates_prevent_record_access(
        bool consent,
        bool deceased,
        bool active,
        string? latestRecordId,
        PopulationErrorKind expectedKind)
    {
        var client = new RecordingDhgClient
        {
            Status = new DhgStatusResponse
            {
                HasGivenConsent = consent,
                Deceased = deceased,
                HasActiveMaternityRecord = active,
                LatestRecordId = latestRecordId
            }
        };
        var service = new DhgPopulationDataService(client, new DhgPopulationSnapshotFactory());

        var error = await Assert.ThrowsAsync<PopulationDataException>(() =>
            service.GetSnapshotAsync(Context(), TestContext.Current.CancellationToken));

        Assert.Equal(expectedKind, error.Kind);
        Assert.Empty(client.RequestedRecordIds);
    }

    [Fact]
    public async Task Record_identity_must_match_the_status_latest_record_id()
    {
        var client = ActiveClient();
        client.Record.Metadata!.RecordId = "f6cd6638-21aa-42ca-a7f3-ff5aa7aab449";
        var service = new DhgPopulationDataService(client, new DhgPopulationSnapshotFactory());

        var error = await Assert.ThrowsAsync<PopulationDataException>(() =>
            service.GetSnapshotAsync(Context(), TestContext.Current.CancellationToken));

        Assert.Equal(PopulationErrorKind.SourceContractInvalid, error.Kind);
        Assert.Equal([RecordId], client.RequestedRecordIds);
    }

    [Fact]
    public async Task Only_the_current_active_record_reaches_the_snapshot_factory()
    {
        var client = ActiveClient();
        var service = new DhgPopulationDataService(client, new DhgPopulationSnapshotFactory());

        var snapshot = await service.GetSnapshotAsync(Context(), TestContext.Current.CancellationToken);

        Assert.True(snapshot.HasActiveMaternityRecord);
        Assert.Equal("patient-1", snapshot.Patient.LogicalId);
        Assert.Equal([RecordId], client.RequestedRecordIds);
    }

    private static RecordingDhgClient ActiveClient() => new()
    {
        Status = new DhgStatusResponse
        {
            HasGivenConsent = true,
            HasActiveMaternityRecord = true,
            LatestRecordId = RecordId,
            LastChangedDateTime = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00")
        },
        Record = new DhgMaternityRecord
        {
            Metadata = new DhgRecordMetadata
            {
                RecordId = RecordId,
                RecordStatus = new DhgRecordStatus { Status = "ACTIVE" }
            }
        }
    };

    private static PatientRequestContext Context() =>
        new("patient-1", "01019012345", "subject-token", "17a80c64-4592-48ad-ae7a-f537e8863dc1");

    private sealed class RecordingDhgClient : IDhgClient
    {
        public DhgStatusResponse Status { get; init; } = new();
        public DhgMaternityRecord Record { get; init; } = new();
        public List<string> RequestedRecordIds { get; } = [];

        public Task<DhgStatusResponse> GetStatusAsync(
            PatientRequestContext context,
            CancellationToken cancellationToken) => Task.FromResult(Status);

        public Task<DhgMaternityRecord> GetRecordAsync(
            string recordId,
            PatientRequestContext context,
            CancellationToken cancellationToken)
        {
            RequestedRecordIds.Add(recordId);
            return Task.FromResult(Record);
        }
    }
}
