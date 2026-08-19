using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PopulationDataFacade.TestClient;

var builder = WebApplication.CreateBuilder(args);
var clientOptions = builder.Configuration.GetSection(TestClientOptions.SectionName).Get<TestClientOptions>() ?? new TestClientOptions();
builder.Services.AddOptions<TestClientOptions>()
    .Bind(builder.Configuration.GetSection(TestClientOptions.SectionName))
    .Validate(options => options.FacadeBaseUrl.IsAbsoluteUri && options.FacadeBaseUrl.Scheme == Uri.UriSchemeHttps,
        "TestClient:FacadeBaseUrl must be HTTPS.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "TestClient:ClientId is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientAssertionJwk), "TestClient:ClientAssertionJwk is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DPoPJwk), "TestClient:DPoPJwk is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.PatientContextHeaderName) &&
                         options.PatientContextHeaderName.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
        "TestClient:PatientContextHeaderName must be a non-empty HTTP header token containing letters, digits, or hyphens.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "PopulationDataFacade.TestClient";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = clientOptions.Authority.ToString().TrimEnd('/');
        options.ClientId = clientOptions.ClientId;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");
        options.Scope.Add(clientOptions.Scope);
        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("resource", clientOptions.Audience);
            return Task.CompletedTask;
        };
    });

builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
{
    options.DPoPJsonWebKey = DPoPProofKey.Parse(clientOptions.DPoPJwk);
});
builder.Services.AddTransient<IClientAssertionService, HelseIdClientAssertionService>();
builder.Services.AddUserAccessTokenHttpClient("facade", configureClient: client =>
{
    client.BaseAddress = clientOptions.FacadeBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store, private";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'");
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", () => Results.Challenge(
    new AuthenticationProperties { RedirectUri = "/" },
    [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/logout", () => Results.SignOut(
    new AuthenticationProperties { RedirectUri = "/" },
    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/", (HttpContext context) => Results.Content(Page(
    context.User.Identity?.IsAuthenticated == true,
    clientOptions.DefaultPatientAlias), "text/html; charset=utf-8"));

app.MapGet("/inspect", async (
    string alias,
    string resource,
    string? code,
    IHttpClientFactory clients,
    CancellationToken cancellationToken) =>
{
    var client = clients.CreateClient("facade");
    using var contextResponse = await client.PostAsync($"test/patient-context/{Uri.EscapeDataString(alias)}", null, cancellationToken);
    var contextJson = await contextResponse.Content.ReadAsStringAsync(cancellationToken);
    if (!contextResponse.IsSuccessStatusCode)
        return Results.Content(ResultPage((int)contextResponse.StatusCode, TimeSpan.Zero, contextJson, "Patient context could not be issued."), "text/html; charset=utf-8");

    using var contextDocument = JsonDocument.Parse(contextJson);
    var patientId = contextDocument.RootElement.GetProperty("patientId").GetString()!;
    var patientContext = contextDocument.RootElement.GetProperty("patientContext").GetString()!;
    var path = resource switch
    {
        "Patient" => $"fhir/Patient/{Uri.EscapeDataString(patientId)}",
        "Encounter" => $"fhir/Encounter?patient={Uri.EscapeDataString(patientId)}",
        _ => $"fhir/Observation?patient={Uri.EscapeDataString(patientId)}" +
             (string.IsNullOrWhiteSpace(code) ? string.Empty : $"&code={Uri.EscapeDataString(code)}")
    };

    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.TryAddWithoutValidation(clientOptions.PatientContextHeaderName, patientContext);
    var stopwatch = Stopwatch.StartNew();
    using var response = await client.SendAsync(request, cancellationToken);
    var json = await response.Content.ReadAsStringAsync(cancellationToken);
    stopwatch.Stop();
    return Results.Content(ResultPage((int)response.StatusCode, stopwatch.Elapsed, json, HumanSummary(json)), "text/html; charset=utf-8");
}).RequireAuthorization();

app.Run();

static string Page(bool authenticated, string defaultAlias)
{
    var auth = authenticated
        ? "<span class=ok>Innlogget med HelseID</span> · <a href=/logout>Logg ut</a>"
        : "<a class=button href=/login>Logg inn med HelseID</a>";
    return Shell($"""
        <header><h1>FHIR Population Data Facade</h1><p>Testklient for DHG Test via fasaden</p></header>
        <section class=card><p>{auth}</p></section>
        <section class=card>
          <form action=/inspect method=get>
            <label>Testperson-alias<input name=alias value="{E(defaultAlias)}" required></label>
            <label>FHIR-ressurs<select name=resource><option>Patient</option><option selected>Observation</option><option>Encounter</option></select></label>
            <label>Kodefilter <small>system|code, valgfritt</small><input name=code placeholder="urn:nhn:population-data|pre-pregnancy-bmi"></label>
            <button type=submit {(authenticated ? string.Empty : "disabled")}>Hent fra fasaden</button>
          </form>
        </section>
        """);
}

static string ResultPage(int status, TimeSpan elapsed, string raw, string human)
{
    var meta = "Ikke tilgjengelig";
    try
    {
        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.TryGetProperty("meta", out var value)) meta = value.GetRawText();
        else if (document.RootElement.TryGetProperty("entry", out var entries)) meta = $"{entries.GetArrayLength()} treff";
    }
    catch (JsonException) { }

    return Shell($"""
        <nav><a href=/>← Ny forespørsel</a></nav>
        <header><h1>Resultat</h1><p><strong>HTTP {status}</strong> · {elapsed.TotalMilliseconds:N0} ms · meta: {E(meta)}</p></header>
        <section class=card><h2>Menneskelesbar visning</h2><pre>{E(human)}</pre></section>
        <section class=card><h2>Rå FHIR JSON</h2><pre>{E(Pretty(raw))}</pre></section>
        """);
}

static string HumanSummary(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var resourceType = root.TryGetProperty("resourceType", out var type) ? type.GetString() : "Ukjent";
        if (resourceType == "Bundle" && root.TryGetProperty("entry", out var entries))
        {
            var lines = new List<string> { $"Bundle med {entries.GetArrayLength()} treff" };
            foreach (var entry in entries.EnumerateArray().Take(50))
            {
                var resource = entry.GetProperty("resource");
                var code = resource.TryGetProperty("code", out var codeObject) &&
                           codeObject.TryGetProperty("coding", out var coding) && coding.GetArrayLength() > 0
                    ? coding[0].TryGetProperty("display", out var display) ? display.GetString() : coding[0].GetProperty("code").GetString()
                    : resource.TryGetProperty("id", out var id) ? id.GetString() : "ressurs";
                var value = resource.EnumerateObject().FirstOrDefault(property => property.Name.StartsWith("value", StringComparison.Ordinal));
                lines.Add(value.Name is null ? $"• {code}" : $"• {code}: {value.Value}");
            }
            return string.Join(Environment.NewLine, lines);
        }
        return $"{resourceType} {(root.TryGetProperty("id", out var resourceId) ? resourceId.GetString() : string.Empty)}";
    }
    catch (JsonException)
    {
        return "Responsen var ikke gyldig JSON.";
    }
}

static string Pretty(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
    catch (JsonException) { return json; }
}

static string Shell(string body) => $$"""
    <!doctype html><html lang=no><head><meta charset=utf-8><meta name=viewport content="width=device-width,initial-scale=1">
    <title>FHIR Population Data Facade</title><style>
    :root{font-family:system-ui,sans-serif;color:#17212b;background:#eef3f5}body{max-width:1040px;margin:3rem auto;padding:0 1rem}
    header{margin-bottom:1.4rem}h1{margin-bottom:.25rem}.card{background:white;border:1px solid #d6e0e4;border-radius:12px;padding:1.25rem;margin:1rem 0;box-shadow:0 4px 16px #18313b0d}
    form{display:grid;gap:1rem;max-width:700px}label{display:grid;gap:.35rem;font-weight:650}input,select,button{font:inherit;padding:.7rem;border:1px solid #8da3ad;border-radius:7px}
    button,.button{background:#006b75;color:white;border:0;text-decoration:none;padding:.7rem 1rem;border-radius:7px;width:max-content}button:disabled{opacity:.45}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f6f8f9;padding:1rem;border-radius:7px}.ok{color:#08733c;font-weight:700}small{font-weight:400}
    </style></head><body>{{body}}</body></html>
    """;

static string E(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);

public partial class Program;
