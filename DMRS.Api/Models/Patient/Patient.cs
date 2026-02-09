namespace DMRS.Api.Models.Patient
{
    public class Patient
    {
        public Guid Id { get; private set; }
        public string NationalId { get; private set; }
        public string FullName { get; private set; }
        public DateOnly BirthDate { get; private set; }
        public string Gender { get; private set; }

        private Patient() { }

        public Patient(string nationalId, string fullName, DateOnly birthDate, string gender)
        {
            Id = Guid.NewGuid();
            NationalId = nationalId;
            FullName = fullName;
            BirthDate = birthDate;
            Gender = gender;
        }
    }
}
