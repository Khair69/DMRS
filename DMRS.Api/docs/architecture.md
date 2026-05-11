# Architecture Notes

The detailed CDS architecture is documented in [cds-system.md](/D:/Code/ASP/DMRS/DMRS.Api/docs/cds-system.md).

For the current CDS work, the most important boundaries are:

- `DMRS.MedicineInfo.Api` is the upstream medicine source
- `DMRS.Api` owns CDS rule storage, normalized medicine knowledge, context enrichment, and rule execution
- FHIR resources inside `DMRS.Api` provide patient-linked context such as allergies

If you are trying to understand the CDS feature specifically, start with `cds-system.md` first and then use `cds-testing.md` to validate the behavior step by step.
