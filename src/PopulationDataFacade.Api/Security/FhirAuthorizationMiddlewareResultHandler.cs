using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PopulationDataFacade.Api.Fhir;

namespace PopulationDataFacade.Api.Security;

public sealed class FhirAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            context.Response.Headers.WWWAuthenticate = "DPoP";
            await FhirHttp.Result(
                    FhirHttp.Outcome("security", "A valid HelseID DPoP access token is required."),
                    StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await FhirHttp.Result(
                    FhirHttp.Outcome("forbidden", "The token does not grant access to this operation."),
                    StatusCodes.Status403Forbidden)
                .ExecuteAsync(context);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
