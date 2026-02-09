using DMRS.Api.Application.Patients;
using DMRS.Api.Data;
using DMRS.Api.Models.Patient;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("api/patients")]
    public class PatientsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PatientsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePatientRequest request)
        {
            var patient = new Patient(
                request.NationalId,
                request.FullName,
                request.BirthDate,
                request.Gender
                );

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return Ok(patient.Id);
        }
    }
}
