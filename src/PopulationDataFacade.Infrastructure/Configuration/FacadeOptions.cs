using Duende.AccessTokenManagement.DPoP;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PopulationDataFacade.Infrastructure.Configuration;

public sealed class DhgOptions
{
    public const string SectionName = "Dhg";
    public string Environment { get; set; } = "Test";
    public Uri BaseUrl { get; set; } = new("https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/");
    public string SourceSystem { get; set; } = "PopulationDataFacade/1.0";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan PooledConnectionLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxTransientRetries { get; set; } = 3;
}

public sealed class HelseIdOptions
{
    public const string SectionName = "HelseId";
    public Uri Authority { get; set; } = new("https://helseid-sts.test.nhn.no");
    public string FacadeAudience { get; set; } = "nhn:population-data-facade";
    public string FacadeScope { get; set; } = "nhn:population-data-facade/read";
    public string DhgAudience { get; set; } = "nhn:maternity-record";
    public string DhgScope { get; set; } = "nhn:maternity-record/api";
    public string ClientId { get; set; } = string.Empty;
    public string ClientAssertionJwk { get; set; } = string.Empty;
    public string DPoPJwk { get; set; } = string.Empty;
}

public sealed class DevelopmentTestModeOptions
{
    public const string SectionName = "DevelopmentTestMode";
    public bool Enabled { get; set; }
    public string Subject { get; set; } = "swagger-dhg-test-user";
}

public sealed class HelseIdTestTokenOptions
{
    public const string SectionName = "HelseIdTestToken";
    public bool Enabled { get; set; }
    public Uri Endpoint { get; set; } = new("https://helseid-ttt.test.nhn.no/v2/create-test-token-with-key");
    public string AuthKey { get; set; } = string.Empty;
    public string Audience { get; set; } = "nhn:maternity-record";
    public string Scope { get; set; } = "nhn:maternity-record/api";
    public string OrgnrParent { get; set; } = string.Empty;
    public string OrgnrChild { get; set; } = string.Empty;
    public bool ClientTenancy { get; set; } = true;
    public int? ClientTenancyType { get; set; }
    public string ClientName { get; set; } = "PopulationDataFacade";
    public string PractitionerNationalIdentityNumber { get; set; } = string.Empty;
    public string PractitionerHprNumber { get; set; } = string.Empty;
    public string PractitionerName { get; set; } = string.Empty;
    public string UserRoleSystem { get; set; } = "urn:oid:2.16.578.1.12.4.1.1.9060";
    public string UserRoleCode { get; set; } = string.Empty;
    public string TreatmentFacilityName { get; set; } = string.Empty;
}

public sealed class DhgOptionsValidator(IOptions<HelseIdOptions> helseIdOptions) : IValidateOptions<DhgOptions>
{
    public ValidateOptionsResult Validate(string? name, DhgOptions options)
    {
        var failures = new List<string>();
        if (!options.BaseUrl.IsAbsoluteUri || options.BaseUrl.Scheme != Uri.UriSchemeHttps)
            failures.Add("Dhg:BaseUrl must be an absolute HTTPS URL.");
        if (options.SourceSystem.Length is < 3 or > 512)
            failures.Add("Dhg:SourceSystem must contain 3-512 characters.");
        if (options.RequestTimeout <= TimeSpan.Zero || options.ConnectTimeout <= TimeSpan.Zero)
            failures.Add("DHG timeouts must be positive.");
        if (options.MaxTransientRetries is < 0 or > 5)
            failures.Add("Dhg:MaxTransientRetries must be between 0 and 5.");

        var supportedEnvironment = options.Environment.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
                                   options.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        if (!supportedEnvironment)
            failures.Add("Dhg:Environment must be Test or Production.");

        var authority = helseIdOptions.Value.Authority.Host;
        if (options.Environment.Equals("Test", StringComparison.OrdinalIgnoreCase) &&
            (!options.BaseUrl.Host.EndsWith(".test.nhn.no", StringComparison.OrdinalIgnoreCase) ||
             !authority.EndsWith(".test.nhn.no", StringComparison.OrdinalIgnoreCase)))
            failures.Add("DHG Test must be paired with HelseID Test.");
        if (options.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
            (options.BaseUrl.Host.Contains(".test.", StringComparison.OrdinalIgnoreCase) ||
             authority.Contains(".test.", StringComparison.OrdinalIgnoreCase)))
            failures.Add("Production must not be paired with a Test endpoint.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public sealed class HelseIdOptionsValidator : IValidateOptions<HelseIdOptions>
{
    private readonly Func<bool> useTestTokenUtility;

    public HelseIdOptionsValidator(bool useTestTokenUtility = false)
        : this(() => useTestTokenUtility)
    {
    }

    public HelseIdOptionsValidator(Func<bool> useTestTokenUtility) =>
        this.useTestTokenUtility = useTestTokenUtility;

    public ValidateOptionsResult Validate(string? name, HelseIdOptions options)
    {
        var failures = new List<string>();
        if (!options.Authority.IsAbsoluteUri || options.Authority.Scheme != Uri.UriSchemeHttps)
            failures.Add("HelseId:Authority must be an absolute HTTPS URL.");
        if (string.IsNullOrWhiteSpace(options.FacadeAudience)) failures.Add("HelseId:FacadeAudience is required.");
        if (string.IsNullOrWhiteSpace(options.FacadeScope)) failures.Add("HelseId:FacadeScope is required.");
        if (options.DhgAudience != "nhn:maternity-record") failures.Add("HelseId:DhgAudience must be nhn:maternity-record.");
        if (options.DhgScope != "nhn:maternity-record/api") failures.Add("HelseId:DhgScope must be nhn:maternity-record/api.");
        if (string.IsNullOrWhiteSpace(options.ClientId)) failures.Add("HelseId:ClientId must be supplied from secure configuration.");
        if (!useTestTokenUtility() && !HasPrivateSigningKey(options.ClientAssertionJwk))
            failures.Add("HelseId:ClientAssertionJwk must be a valid asymmetric private JWK supplied from a secret store.");
        if (!useTestTokenUtility() && !HasPrivateDPoPKey(options.DPoPJwk))
            failures.Add("HelseId:DPoPJwk must be a valid private DPoP JWK supplied from a secret store.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool HasPrivateSigningKey(string value)
    {
        try
        {
            var key = new JsonWebKey(value);
            return key.HasPrivateKey && key.Kty is "RSA" or "EC";
        }
        catch
        {
            return false;
        }
    }

    private static bool HasPrivateDPoPKey(string value)
    {
        try
        {
            return DPoPProofKey.Parse(value).ToJsonWebKey().HasPrivateKey;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class HelseIdTestTokenOptionsValidator : IValidateOptions<HelseIdTestTokenOptions>
{
    private readonly Func<bool> developmentTestModeEnabled;
    private readonly Func<string> dhgEnvironment;

    public HelseIdTestTokenOptionsValidator(bool developmentTestModeEnabled, string dhgEnvironment)
        : this(() => developmentTestModeEnabled, () => dhgEnvironment)
    {
    }

    public HelseIdTestTokenOptionsValidator(
        Func<bool> developmentTestModeEnabled,
        Func<string> dhgEnvironment)
    {
        this.developmentTestModeEnabled = developmentTestModeEnabled;
        this.dhgEnvironment = dhgEnvironment;
    }

    public ValidateOptionsResult Validate(string? name, HelseIdTestTokenOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;

        var failures = new List<string>();
        if (!developmentTestModeEnabled())
            failures.Add("HelseIdTestToken can be enabled only together with DevelopmentTestMode.");
        if (!dhgEnvironment().Equals("Test", StringComparison.OrdinalIgnoreCase))
            failures.Add("HelseIdTestToken can be enabled only for DHG Test.");
        if (options.Endpoint is null || !options.Endpoint.IsAbsoluteUri || options.Endpoint.Scheme != Uri.UriSchemeHttps ||
            options.Endpoint.UserInfo.Length != 0 || options.Endpoint.Query.Length != 0 ||
            options.Endpoint.Fragment.Length != 0 ||
            !options.Endpoint.Host.EndsWith(".test.nhn.no", StringComparison.OrdinalIgnoreCase))
            failures.Add("HelseIdTestToken:Endpoint must be a credential-free HelseID Test HTTPS URL without query or fragment.");
        if (string.IsNullOrWhiteSpace(options.AuthKey))
            failures.Add("HelseIdTestToken:AuthKey must be supplied from secure configuration.");
        if (options.Audience != "nhn:maternity-record")
            failures.Add("HelseIdTestToken:Audience must be nhn:maternity-record.");
        if (options.Scope != "nhn:maternity-record/api")
            failures.Add("HelseIdTestToken:Scope must be nhn:maternity-record/api.");
        if (options.OrgnrParent.Length != 9 || options.OrgnrParent.Any(character => !char.IsAsciiDigit(character)))
            failures.Add("HelseIdTestToken:OrgnrParent must contain exactly nine digits.");
        if (options.OrgnrChild.Length != 9 || options.OrgnrChild.Any(character => !char.IsAsciiDigit(character)))
            failures.Add("HelseIdTestToken:OrgnrChild must contain exactly nine digits.");
        if (!options.ClientTenancy)
            failures.Add("HelseIdTestToken:ClientTenancy must be true.");
        if (options.ClientTenancyType is < 0 or > 2)
            failures.Add("HelseIdTestToken:ClientTenancyType must be 0, 1, or 2 when supplied.");
        if (string.IsNullOrWhiteSpace(options.ClientName))
            failures.Add("HelseIdTestToken:ClientName is required.");
        if (options.PractitionerNationalIdentityNumber.Length != 11 ||
            options.PractitionerNationalIdentityNumber.Any(character => !char.IsAsciiDigit(character)))
            failures.Add("HelseIdTestToken:PractitionerNationalIdentityNumber must contain exactly eleven digits.");
        if (options.PractitionerHprNumber.Length is < 7 or > 9 ||
            options.PractitionerHprNumber.Any(character => !char.IsAsciiDigit(character)))
            failures.Add("HelseIdTestToken:PractitionerHprNumber must contain seven to nine digits.");
        if (string.IsNullOrWhiteSpace(options.PractitionerName) || options.PractitionerName.Length > 255)
            failures.Add("HelseIdTestToken:PractitionerName must contain 1-255 characters.");
        if (options.UserRoleSystem != "urn:oid:2.16.578.1.12.4.1.1.9060")
            failures.Add("HelseIdTestToken:UserRoleSystem must be the Volven 9060 coding-system OID.");
        if (options.UserRoleCode.Length is < 2 or > 4 ||
            options.UserRoleCode.Any(character => !char.IsAsciiLetterOrDigit(character)))
            failures.Add("HelseIdTestToken:UserRoleCode must contain 2-4 ASCII letters or digits.");
        if (string.IsNullOrWhiteSpace(options.TreatmentFacilityName) || options.TreatmentFacilityName.Length > 255)
            failures.Add("HelseIdTestToken:TreatmentFacilityName must contain 1-255 characters.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
