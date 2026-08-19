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
    public bool AllowRemoteStaging { get; set; }
    public string Subject { get; set; } = "swagger-dhg-test-user";
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
        if (!HasPrivateSigningKey(options.ClientAssertionJwk))
            failures.Add("HelseId:ClientAssertionJwk must be a valid asymmetric private JWK supplied from a secret store.");
        if (!HasPrivateDPoPKey(options.DPoPJwk))
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
