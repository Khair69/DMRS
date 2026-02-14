using Microsoft.AspNetCore.Authorization;

namespace DMRS.Api.Infrastructure.Security
{
    public class FhirScopeHandler : AuthorizationHandler<FhirScopeRequirement>
    {
        private readonly IHttpContextAccessor _contextAccessor;

        public FhirScopeHandler(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FhirScopeRequirement requirement)
        {
            var httpContext = _contextAccessor.HttpContext;
            if (httpContext == null) return Task.CompletedTask;

            var method = httpContext.Request.Method;
            string action = method switch
            {
                "GET" => "read",
                "POST" => "write",
                "PUT" or "PATCH" => "write",
                "DELETE" => "delete",
                _ => string.Empty
            };

            var resourceName = httpContext.Request.RouteValues["controller"]?.ToString();

            if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(resourceName))
            {
                return Task.CompletedTask;
            }

            var specificScope = $"user/{resourceName}.{action}";
            var wildcardScope = $"user/*.{action}";

            var scopeClaim = context.User.FindFirst("scope")?.Value;
            if (string.IsNullOrEmpty(scopeClaim)) return Task.CompletedTask;

            var userScopes = scopeClaim.Split(' ');

            if (userScopes.Contains(specificScope) || userScopes.Contains(wildcardScope))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
