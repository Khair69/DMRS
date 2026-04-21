using DMRS.Api.Application;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure;
using DMRS.Api.Infrastructure.ClinicalDecisionSupport;
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
using Microsoft.OpenApi;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;

//to prevent mapping of standard claims to Microsoft-specific claim types
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logsPath);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Digital Medical Records System (DMRS) API",
        Version = "v1",
        Description = "API for managing patient records, encounters, and clinical data."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/DMRS";
        options.Audience = builder.Configuration["Keycloak:ClientId"] ?? "dmrs-api";
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
builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();
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
builder.Services.AddScoped<ProvenanceIndexer>();
builder.Services.AddSingleton<IFhirValidatorService, FhirValidatorService>();

builder.Services.Configure<KnowledgeCacheOptions>(builder.Configuration.GetSection("Cds:Knowledge"));
builder.Services.Configure<RxNormOptions>(builder.Configuration.GetSection("Cds:Knowledge:RxNorm"));
builder.Services.Configure<MockMedicineApiOptions>(builder.Configuration.GetSection("Cds:Knowledge:MockMedicineApi"));

builder.Services.AddSingleton<ICdsServiceRegistry, CdsServiceRegistry>();
builder.Services.AddScoped<ICdsHookService, CdsHookService>();
builder.Services.AddScoped<ICdsContextBuilder, CdsContextBuilder>();
builder.Services.AddScoped<IRuleEngine, RuleEngine>();
builder.Services.AddScoped<IRuleFactory, RuleFactory>();
builder.Services.AddScoped<IRuleExpressionEvaluator, SimpleJsonLogicEvaluator>();
builder.Services.AddScoped<ICardTemplateRenderer, CardTemplateRenderer>();
builder.Services.AddScoped<IRuleDefinitionRepository, EfRuleDefinitionRepository>();
builder.Services.AddScoped<IRuleManagementService, RuleManagementService>();
builder.Services.AddScoped<IKnowledgeCache, KnowledgeCache>();
builder.Services.AddScoped<IClinicalKnowledgeService, ClinicalKnowledgeService>();
builder.Services.AddScoped<IMedicationRequestKnowledgeWarmup, MedicationRequestKnowledgeWarmup>();

var knowledgeProvider = builder.Configuration["Cds:Knowledge:Provider"];
if (string.Equals(knowledgeProvider, "RxNorm", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IKnowledgeProvider, RxNormKnowledgeProvider>();
}
else
{
    builder.Services.AddHttpClient<IKnowledgeProvider, MockMedicineKnowledgeProvider>();
}

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

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.Run();
