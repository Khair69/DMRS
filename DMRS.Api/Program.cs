using DMRS.Api.Application;
using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure;
using DMRS.Api.Infrastructure.Persistence;
using DMRS.Api.Infrastructure.Search;
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
            RoleClaimType = "roles",
            NameClaimType = "preferred_username"
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IFhirRepository, FhirRepository>();
builder.Services.AddScoped<ISearchIndexer, PatientIndexer>();
builder.Services.AddSingleton<IFhirValidatorService, FhirValidatorService>();

builder.Services.AddSingleton<FhirJsonSerializer>(new FhirJsonSerializer());
builder.Services.AddSingleton<FhirJsonDeserializer>(new FhirJsonDeserializer(new DeserializerSettings
{
    Validator = null // We will handle validation separately
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
