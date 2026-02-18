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

            if (accessLevel == SmartAccessLevel.Patient)
            {
                var patientId = _authorizationService.ResolvePatientId(context.User);
                if (string.IsNullOrWhiteSpace(patientId))
                {
                    return;
                }

                var id = httpContext.Request.RouteValues["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(id) && httpContext.Request.Method != HttpMethods.Post)
                {
                    var isOwned = await _authorizationService.IsResourceOwnedByPatientAsync(resourceType, id, patientId);
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
