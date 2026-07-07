using Microsoft.AspNetCore.Authorization;

namespace DMRS.Api.Infrastructure.Security
{
    public class FhirScopeHandler : AuthorizationHandler<FhirScopeRequirement>
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly ISmartAuthorizationService _authorizationService;

        public FhirScopeHandler(IHttpContextAccessor contextAccessor, ISmartAuthorizationService authorizationService)
        {
            _contextAccessor = contextAccessor;
            _authorizationService = authorizationService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, FhirScopeRequirement requirement)
        {
            var httpContext = _contextAccessor.HttpContext;
            if (httpContext == null)
            {
                return;
            }

            var action = MapAction(httpContext.Request.Method);
            var resourceType = httpContext.Request.RouteValues["controller"]?.ToString();

            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(resourceType))
            {
                return;
            }

            var accessLevel = _authorizationService.GetAccessLevel(context.User, resourceType, action);
            if (accessLevel == SmartAccessLevel.None)
            {
                return;
            }

            var id = httpContext.Request.RouteValues["id"]?.ToString();
            var isInstanceRequest = !string.IsNullOrWhiteSpace(id) && httpContext.Request.Method != HttpMethods.Post;
            var isHistoryRequest = httpContext.Request.Path.HasValue
                && httpContext.Request.Path.Value!.Contains("/_history", StringComparison.OrdinalIgnoreCase);
            var isVersionedRequest = isHistoryRequest || httpContext.Request.RouteValues.ContainsKey("vid");

            if (accessLevel == SmartAccessLevel.Patient)
            {
                var patientId = _authorizationService.ResolvePatientId(context.User);
                if (string.IsNullOrWhiteSpace(patientId))
                {
                    return;
                }

                if (isInstanceRequest && !isVersionedRequest)
                {
                    var isOwned = await _authorizationService.IsResourceOwnedByPatientAsync(resourceType, id!, patientId);
                    if (!isOwned)
                    {
                        return;
                    }
                }
            }

            if (accessLevel == SmartAccessLevel.User && isInstanceRequest && !isVersionedRequest)
            {
                // Staff may READ any patient and their clinical record across organizations, and may
                // CREATE/UPDATE patient-owned clinical resources across organizations (the controller's
                // CanUpdateResource performs the finer-grained check). Only delete and administrative
                // writes stay org-scoped, so skip this gate for the cross-org read/write cases only.
                var isCrossOrgRead = action == "read"
                    && _authorizationService.IsCrossOrganizationReadableType(resourceType);
                var isCrossOrgWrite = action == "write"
                    && _authorizationService.IsCrossOrganizationWritableType(resourceType);

                if (!isCrossOrgRead && !isCrossOrgWrite)
                {
                    var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(context.User);
                    var isOwned = await _authorizationService.IsResourceOwnedByOrganizationsAsync(resourceType, id!, organizationIds);
                    if (!isOwned)
                    {
                        return;
                    }
                }
            }

            context.Succeed(requirement);
        }

        private static string MapAction(string method)
        {
            return method switch
            {
                "GET" => "read",
                "POST" or "PUT" or "PATCH" => "write",
                "DELETE" => "delete",
                _ => string.Empty
            };
        }
    }
}
