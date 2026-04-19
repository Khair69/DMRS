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
app.MapGet("/api/medications/{value}", async (string value, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Results.NotFound();
    }

    var trimmedValue = value.Trim();
    var medicines = db.Medicines
        .Include(m => m.Ingredients);

    var medicine = trimmedValue.All(char.IsDigit)
        ? await medicines.FirstOrDefaultAsync(m => m.RxCui == trimmedValue)
        : await medicines
            .Where(m =>
                EF.Functions.ILike(m.Name, trimmedValue)
                || EF.Functions.ILike(m.Name, $"%{trimmedValue}%"))
            .OrderByDescending(m => EF.Functions.ILike(m.Name, trimmedValue))
            .ThenBy(m => m.Name)
            .FirstOrDefaultAsync();

    return medicine is not null ? Results.Ok(medicine) : Results.NotFound();
});

app.MapGet("/", () => "Welcome to the DMRS Medicine Info API! Use /api/medications/{value} to get medicine details by RxCUI or medicine name.");

app.Run();
