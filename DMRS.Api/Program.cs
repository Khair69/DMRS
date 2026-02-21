using DMRS.Api.Application;
using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure;
using DMRS.Api.Infrastructure.Persistence;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Search.Clinical;
using DMRS.Api.Infrastructure.Search.Medication;
using DMRS.Api.Infrastructure.Search.Scheduling;
using DMRS.Api.Infrastructure.Search.Security;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

//to prevent mapping of standard claims to Microsoft-specific claim types
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/DMRS";
        options.Audience = "dmrs-api";
        options.RequireHttpsMetadata = false; // DEV ONLY
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            RoleClaimType = "roles",
            NameClaimType = "preferred_username"
        };
    });

builder.Services.AddFhirAuthorization();

builder.Services.AddScoped<IFhirRepository, FhirRepository>();
builder.Services.AddScoped<PatientIndexer>();
builder.Services.AddScoped<PractitionerIndexer>();
builder.Services.AddScoped<PractitionerRoleIndexer>();
builder.Services.AddScoped<OrganizationIndexer>();
builder.Services.AddScoped<EncounterIndexer>();
builder.Services.AddScoped<LocationIndexer>();
builder.Services.AddScoped<AllergyIntoleranceIndexer>();
builder.Services.AddScoped<ConditionIndexer>();
builder.Services.AddScoped<ObservationIndexer>();
builder.Services.AddScoped<ProcedureIndexer>();
builder.Services.AddScoped<MedicationRequestIndexer>();
builder.Services.AddScoped<AppointmentIndexer>();
builder.Services.AddScoped<ServiceRequestIndexer>();
builder.Services.AddScoped<BundleIndexer>();
builder.Services.AddScoped<MetadataIndexer>();
builder.Services.AddSingleton<IFhirValidatorService, FhirValidatorService>();

builder.Services.AddSingleton<FhirJsonSerializer>(new FhirJsonSerializer());
builder.Services.AddSingleton<FhirJsonDeserializer>(new FhirJsonDeserializer(new DeserializerSettings
{
    Validator = null // We will handle validation separately
}));

var corsPolicy = "AllowBlazorClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        // Replace with the actual URL/Port your Blazor app runs on
        policy.WithOrigins("https://localhost:7099", "http://localhost:5155")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(corsPolicy);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
