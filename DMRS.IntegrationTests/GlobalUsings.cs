// FHIR defines a "Task" resource (Hl7.Fhir.Model.Task) that collides with System.Threading.Tasks.Task
// throughout async code. Alias Task to the threading type project-wide; reference the FHIR resource by
// its full name on the rare occasion it is needed.
global using Task = System.Threading.Tasks.Task;
