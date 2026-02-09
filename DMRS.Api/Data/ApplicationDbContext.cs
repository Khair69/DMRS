using DMRS.Api.Models.Patient;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Patient> Patients => Set<Patient>();
    }
}
