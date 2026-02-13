using DMRS.Api.Application.Interfaces;
using Firely.Fhir.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Firely.Fhir.Packages;

namespace DMRS.Api.Application
{
    public class FhirValidatorService : IFhirValidatorService
    {
        private Validator? _validator;
        private readonly IAsyncResourceResolver _coreSource;
        public FhirValidatorService()
        {
            // 1) Package resolver for FHIR spec + IG packages
            var packageSource = FhirPackageSource.CreateCorePackageSource(ModelInfo.ModelInspector, FhirRelease.R5, "https://packages.simplifier.net");
            _coreSource = new CachedResolver(packageSource);

            // 2) Combine with local resolver (e.g., custom profiles)
            var folderResolver = new DirectorySource("Profiles");
            var resolver = new MultiResolver(folderResolver, _coreSource);

            // 3) Terminology service
            var termSvc = new LocalTerminologyService(resolver);

            // 4) Validator
            _validator = new Validator(resolver, termSvc);
        }
        public Task<OperationOutcome> ValidateAsync(Resource resource, string? profileUrl = null)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            // Validate by profile if supplied, otherwise use resource.Meta.Profile
            var result = profileUrl is not null
                ? _validator.Validate(resource, profileUrl)
                : _validator.Validate(resource);

            return System.Threading.Tasks.Task.FromResult(result);
        }
    }
}
