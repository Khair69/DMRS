using DMRS.Client;
using DMRS.Client.Services;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.UserOptions.RoleClaim = "role";
});

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

await builder.Build().RunAsync();
