using Microsoft.AspNetCore.Authorization;

namespace DMRS.Api.Infrastructure.Security
{
    public static class AuthorizationServiceExtensions
    {
        public static IServiceCollection AddFhirAuthorization(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddScoped<IResourceOwnershipResolver, ResourceOwnershipResolver>();
            services.AddScoped<ISmartAuthorizationService, SmartAuthorizationService>();
            services.AddScoped<IAuthorizationHandler, FhirScopeHandler>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("FhirScope", policy =>
                    policy.Requirements.Add(new FhirScopeRequirement()));

                // CDS administration (rule authoring, medicine knowledge management) is an
                // administrative function, not a FHIR resource operation. It must not be routed
                // through FhirScope: that policy keys off the controller name as if it were a FHIR
                // resource type and runs an org-ownership gate that has no meaning for CDS records,
                // which 403s legitimate admins on instance operations. Gate it on admin roles instead.
                options.AddPolicy("CdsAdmin", policy =>
                    policy.RequireRole("ROLE_SYSTEM_ADMIN", "ROLE_ORG_ADMIN"));
            });

            return services;
        }
    }
}
