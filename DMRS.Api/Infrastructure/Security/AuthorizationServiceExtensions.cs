using Microsoft.AspNetCore.Authorization;

namespace DMRS.Api.Infrastructure.Security
{
    public static class AuthorizationServiceExtensions
    {
        public static IServiceCollection AddFhirAuthorization(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            services.AddScoped<ISmartAuthorizationService, SmartAuthorizationService>();
            services.AddScoped<IAuthorizationHandler, FhirScopeHandler>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("FhirScope", policy =>
                    policy.Requirements.Add(new FhirScopeRequirement()));
            });

            return services;
        }
    }
}
