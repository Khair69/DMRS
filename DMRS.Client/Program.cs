using DMRS.Client;
using DMRS.Client.Features.AllergyIntolerances.Services;
using DMRS.Client.Features.Appointments.Services;
using DMRS.Client.Features.Conditions.Services;
using DMRS.Client.Features.Encounters.Services;
using DMRS.Client.Features.Locations.Services;
using DMRS.Client.Features.MedicationRequests.Services;
using DMRS.Client.Features.Observations.Services;
using DMRS.Client.Features.Organizations.Services;
using DMRS.Client.Features.Patients.Services;
using DMRS.Client.Features.Procedures.Services;
using DMRS.Client.Features.ServiceRequests.Services;
using DMRS.Client.Features.Staff.Services;
using DMRS.Client.Services;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
//using System.IdentityModel.Tokens.Jwt;
//if not remember to delete nugut package
//JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
//Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.UserOptions.RoleClaim = "roles";
})
    .AddAccountClaimsPrincipalFactory<KeycloakClaimsPrincipalFactory>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7029/";
builder.Services.AddHttpClient<FhirApiService>(client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp =>
    {
        return sp.GetRequiredService<AuthorizationMessageHandler>()
            .ConfigureHandler(
                authorizedUrls: new[] { apiBaseUrl }, // Only send tokens to this URL
                scopes: new[] { "openid", "profile" } // Match your Keycloak scopes
            );
    });

builder.Services.AddSingleton<FhirJsonSerializer>(new FhirJsonSerializer());
builder.Services.AddSingleton<FhirJsonDeserializer>(new FhirJsonDeserializer(new DeserializerSettings
{
    Validator = null // We will handle validation separately
}));

builder.Services.AddScoped<PatientFeatureService>();
builder.Services.AddScoped<OrganizationFeatureService>();
builder.Services.AddScoped<OrganizationAdminFeatureService>();
builder.Services.AddScoped<StaffFeatureService>();
builder.Services.AddScoped<OrganizationContextService>();
builder.Services.AddScoped<ConditionFeatureService>();
builder.Services.AddScoped<ObservationFeatureService>();
builder.Services.AddScoped<ProcedureFeatureService>();
builder.Services.AddScoped<AllergyIntoleranceFeatureService>();
builder.Services.AddScoped<MedicationRequestFeatureService>();
builder.Services.AddScoped<ServiceRequestFeatureService>();
builder.Services.AddScoped<AppointmentFeatureService>();
builder.Services.AddScoped<EncounterFeatureService>();
builder.Services.AddScoped<LocationFeatureService>();

await builder.Build().RunAsync();
