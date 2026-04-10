using DMRS.MedicineInfo.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// --- THE SEEDING MAGIC ---
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(context);
}

// --- THE ENDPOINT ---
app.MapGet("/api/medications/{rxCui}", async (string rxCui, AppDbContext db) =>
{
    var medicine = await db.Medicines
        .Include(m => m.Ingredients) // Ensure ingredients are loaded
        .FirstOrDefaultAsync(m => m.RxCui == rxCui);

    return medicine is not null ? Results.Ok(medicine) : Results.NotFound();
});

app.MapGet("/", () => "Welcome to the DMRS Medicine Info API! Use /api/medications/{rxCui} to get medicine details.");

app.Run();
