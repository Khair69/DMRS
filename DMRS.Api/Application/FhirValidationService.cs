using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Validation;
namespace DMRS.Api.Application
{
    public class FhirValidationService
    {
        //private readonly Validator _validator;

        //public FhirValidationService()
        //{
        //    // Load standard FHIR validation rules
        //    var source = new CachedResolver(new MultiResolver(
        //        new DirectorySource("path_to_fhir_spec_files"),
        //        ZipSource.CreateValidationSource()
        //    ));

        //    var settings = new ValidationSettings
        //    {
        //        ResourceResolver = source
        //    };

        //    _validator = new Validator(settings);
        //}

        //public OperationOutcome Validate(Resource resource)
        //{
        //    return _validator.Validate(resource);
        //}
    }
}
