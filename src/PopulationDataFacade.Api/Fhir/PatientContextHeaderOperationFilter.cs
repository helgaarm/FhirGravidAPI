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
            Description = "Protected patient context returned by POST /test/patient-context/{alias}.",
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}
