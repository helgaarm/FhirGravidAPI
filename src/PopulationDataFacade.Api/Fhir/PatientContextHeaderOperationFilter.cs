using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using PopulationDataFacade.Api.Security;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PopulationDataFacade.Api.Fhir;

public sealed class PatientContextHeaderOperationFilter(
    IOptions<PatientContextOptions> options) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        var ninSearch =
            context.ApiDescription.HttpMethod?.Equals("POST", StringComparison.OrdinalIgnoreCase) == true &&
            path?.EndsWith("/_search", StringComparison.OrdinalIgnoreCase) == true;
        if (ninSearch)
        {
            var identifierName = path!.StartsWith("fhir/Patient/", StringComparison.OrdinalIgnoreCase)
                ? "identifier"
                : "patient.identifier";
            var properties = new Dictionary<string, OpenApiSchema>
            {
                [identifierName] = new()
                {
                    Type = "string",
                    Description = "NIN sendes i form body og returneres aldri. Krever HelseID utenfor lokal DevelopmentTestMode."
                }
            };
            if (path.StartsWith("fhir/Observation/", StringComparison.OrdinalIgnoreCase))
            {
                properties["code"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Valgfritt system|code Observation filter."
                };
            }

            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/x-www-form-urlencoded"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = properties,
                            Required = new HashSet<string> { identifierName }
                        }
                    }
                }
            };
            return;
        }

        if (path is null ||
            (!path.StartsWith("fhir/Patient/", StringComparison.OrdinalIgnoreCase) &&
             !path.StartsWith("fhir/Observation", StringComparison.OrdinalIgnoreCase) &&
             !path.StartsWith("fhir/Encounter", StringComparison.OrdinalIgnoreCase)))
            return;

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = options.Value.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Protected patient context returnert av POST /test/patient-context/{alias}.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
