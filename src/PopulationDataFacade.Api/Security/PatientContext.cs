using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;

namespace PopulationDataFacade.Api.Security;

public sealed class PatientContextOptions
{
    public const string SectionName = "PatientContext";
    public string HeaderName { get; set; } = "X-Patient-Context";
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(10);
    public string PatientIdHmacKey { get; set; } = string.Empty;
    public Dictionary<string, SyntheticPatientOptions> TestAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsNationalIdentityNumberFormat(string value) =>
        value.Length == 11 && value.All(char.IsAsciiDigit);

    public static bool IsPatientIdHmacKeyValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var key = Convert.FromBase64String(value);
            var valid = key.Length >= 32;
            CryptographicOperations.ZeroMemory(key);
            return valid;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class SyntheticPatientOptions
{
    public string LogicalId { get; set; } = string.Empty;
    public string NationalIdentityNumber { get; set; } = string.Empty;
}

public interface IPatientContextTokenService
{
    string Issue(string alias, string authenticatedSubject);
    PatientContextPayload Read(string token);
}

public sealed record PatientContextPayload(
    string LogicalId,
    string NationalIdentityNumber,
    string AuthenticatedSubject,
    DateTimeOffset ExpiresAt);

public interface IPatientIdPseudonymizer
{
    string CreateLogicalId(string nationalIdentityNumber);
}

public sealed class PatientIdPseudonymizer : IPatientIdPseudonymizer, IDisposable
{
    private readonly byte[] key;

    public PatientIdPseudonymizer(IOptions<PatientContextOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.PatientIdHmacKey))
        {
            key = [];
            return;
        }

        try
        {
            key = Convert.FromBase64String(options.Value.PatientIdHmacKey);
        }
        catch (FormatException)
        {
            key = [];
        }
    }

    public string CreateLogicalId(string nationalIdentityNumber)
    {
        if (key.Length < 32)
            throw new InvalidOperationException(
                "PatientContext:PatientIdHmacKey must be a Base64-encoded secret of at least 32 bytes.");
        Span<byte> identifierBytes = stackalloc byte[nationalIdentityNumber.Length];
        for (var index = 0; index < nationalIdentityNumber.Length; index++)
            identifierBytes[index] = (byte)nationalIdentityNumber[index];

        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(key, identifierBytes, hash);
        CryptographicOperations.ZeroMemory(identifierBytes);
        var encoded = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '.');
        CryptographicOperations.ZeroMemory(hash);
        return $"patient-{encoded}";
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}

public sealed class PatientContextTokenService : IPatientContextTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly IOptions<PatientContextOptions> _options;
    private readonly TimeProvider _timeProvider;

    public PatientContextTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PatientContextOptions> options,
        TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("PopulationDataFacade.PatientContext.v1");
        _options = options;
        _timeProvider = timeProvider;
    }

    public string Issue(string alias, string authenticatedSubject)
    {
        if (string.IsNullOrWhiteSpace(authenticatedSubject))
            throw new PopulationDataException(PopulationErrorKind.Unauthorized, "An authenticated HelseID subject is required.");
        if (!_options.Value.TestAliases.TryGetValue(alias, out var patient) ||
            string.IsNullOrWhiteSpace(patient.LogicalId) ||
            string.IsNullOrWhiteSpace(patient.NationalIdentityNumber))
            throw new PopulationDataException(PopulationErrorKind.NotFound, "The configured synthetic patient alias was not found.");

        var payload = new PatientContextPayload(
            patient.LogicalId,
            patient.NationalIdentityNumber,
            authenticatedSubject,
            _timeProvider.GetUtcNow().Add(_options.Value.Lifetime));
        return _protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public PatientContextPayload Read(string token)
    {
        try
        {
            var json = _protector.Unprotect(token);
            var payload = JsonSerializer.Deserialize<PatientContextPayload>(json, JsonOptions);
            if (payload is null || payload.ExpiresAt <= _timeProvider.GetUtcNow() ||
                string.IsNullOrWhiteSpace(payload.LogicalId) ||
                string.IsNullOrWhiteSpace(payload.NationalIdentityNumber) ||
                string.IsNullOrWhiteSpace(payload.AuthenticatedSubject))
                throw new PopulationDataException(PopulationErrorKind.InvalidPatientContext, "The patient context is invalid or expired.");
            return payload;
        }
        catch (PopulationDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            throw new PopulationDataException(PopulationErrorKind.InvalidPatientContext, "The patient context is invalid or expired.", exception);
        }
    }
}

public sealed class PatientRequestContextFactory(
    IPatientContextTokenService tokenService,
    IPatientIdPseudonymizer patientIdPseudonymizer,
    IOptions<PatientContextOptions> options,
    IOptions<DevelopmentTestModeOptions> developmentTestMode,
    IHostEnvironment hostEnvironment)
{
    public PatientRequestContext Create(HttpContext httpContext, string requestedPatientId)
    {
        if (!httpContext.Request.Headers.TryGetValue(options.Value.HeaderName, out var values) ||
            values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            throw new PopulationDataException(PopulationErrorKind.InvalidPatientContext, "A protected patient context is required.");

        var payload = tokenService.Read(values[0]!);
        if (!string.Equals(payload.LogicalId, requestedPatientId, StringComparison.Ordinal))
            throw new PopulationDataException(PopulationErrorKind.NotFound, "The requested patient was not found in this context.");

        var authenticatedSubject = developmentTestMode.Value.Enabled
            ? developmentTestMode.Value.Subject
            : httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(authenticatedSubject) ||
            !string.Equals(payload.AuthenticatedSubject, authenticatedSubject, StringComparison.Ordinal))
            throw new PopulationDataException(PopulationErrorKind.Forbidden, "The patient context is not valid for this authenticated subject.");

        var subjectToken = string.Empty;
        if (!developmentTestMode.Value.Enabled)
            subjectToken = ReadSubjectToken(httpContext);

        return new PatientRequestContext(
            payload.LogicalId,
            payload.NationalIdentityNumber,
            subjectToken,
            httpContext.TraceIdentifier);
    }

    public PatientRequestContext CreateForNinSearch(
        HttpContext httpContext,
        string nationalIdentityNumber)
    {
        if (!PatientContextOptions.IsNationalIdentityNumberFormat(nationalIdentityNumber))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The patient identifier must contain exactly 11 digits.");

        if (developmentTestMode.Value.Enabled)
            return CreateForConfiguredTestNin(httpContext, nationalIdentityNumber);

        if (string.IsNullOrWhiteSpace(httpContext.User.FindFirst("sub")?.Value))
            throw new PopulationDataException(
                PopulationErrorKind.Unauthorized,
                "An authenticated HelseID subject is required.");

        return new PatientRequestContext(
            patientIdPseudonymizer.CreateLogicalId(nationalIdentityNumber),
            nationalIdentityNumber,
            ReadSubjectToken(httpContext),
            httpContext.TraceIdentifier);
    }

    private PatientRequestContext CreateForConfiguredTestNin(
        HttpContext httpContext,
        string nationalIdentityNumber)
    {
        if (!hostEnvironment.IsDevelopment())
            throw new PopulationDataException(
                PopulationErrorKind.Forbidden,
                "Anonymous NIN-based form search is available only in local Development Test mode.");

        var patient = options.Value.TestAliases.Values.SingleOrDefault(candidate =>
            string.Equals(
                candidate.NationalIdentityNumber,
                nationalIdentityNumber,
                StringComparison.Ordinal));
        if (patient is null)
            throw new PopulationDataException(
                PopulationErrorKind.NotFound,
                "The configured synthetic patient identifier was not found.");

        return new PatientRequestContext(
            patient.LogicalId,
            patient.NationalIdentityNumber,
            string.Empty,
            httpContext.TraceIdentifier);
    }

    private static string ReadSubjectToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        var separator = authorization.IndexOf(' ');
        if (separator <= 0 || separator == authorization.Length - 1)
            throw new PopulationDataException(
                PopulationErrorKind.Unauthorized,
                "An incoming HelseID access token is required.");
        return authorization[(separator + 1)..];
    }
}
