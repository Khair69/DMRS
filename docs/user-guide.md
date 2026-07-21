# Nabd User & Admin Guide

A practical walkthrough of the Nabd web app for the people who use it. For HTTP/API details see the
[API reference](api-reference.md); for setup see the [README](../README.md).

## Roles

What you can see and do depends on your role (assigned in Keycloak):

| Role | Can do |
|---|---|
| **System Admin** (`ROLE_SYSTEM_ADMIN`) | Everything, including Organizations and CDS Admin. |
| **Org Admin** (`ROLE_ORG_ADMIN`) | Clinical + scheduling work for their organization; onboard staff/patients. |
| **Practitioner** (`ROLE_PRACTITIONER`) | Clinical workspace — patients, charts, orders, AI insights. |
| **Patient** (`ROLE_PATIENT`) | Their own record (after claiming an invite). |

## Signing in

1. Open the client at `https://localhost:7099`.
2. You're redirected to **Keycloak** to log in; on success you return to the workspace.
3. The left **navigation** shows only the sections your role allows.

---

# Part 1 — Clinician workflows

### The dashboard (Home)
After login you land on the **Home Dashboard**: key counts (patients, appointments, meds, …), a
**Predictive Watchlist** (patients ranked by AI readmission risk), upcoming appointments, and recent
CDS alerts.

### Finding & managing patients
- **Patients** (nav → Clinical → Patients) lists patients; open one to see its **chart**.
- Use **Create** to add a patient, **Edit** to update, and **History** to see past versions.

### The patient chart
The chart summarizes one patient: demographic header, metric cards (allergies, conditions, meds),
recent clinical items, **Patient Documents**, and the **AI & CDS Briefing** panel. Quick-link buttons
let you jump straight to "New Medication Request", "Add Condition", or "AI Insights".

### Recording clinical data
From the relevant nav item (Conditions, Observations, Procedures, Allergies, Encounters) use
**Create** to add a record; each supports edit, details, and history. Records are linked to the
patient and immediately feed the dashboards and AI models.

### Orders & scheduling
- **Medication Requests** — prescribe a drug; on save, Nabd looks up medicine knowledge and may raise
  a CDS card (see below).
- **Service Requests** — referrals / orders. **Appointments** — scheduling. **Locations** — facilities.

### Documents
On the patient chart, the **Patient Documents** panel uploads clinical files (PDF/images, up to 20 MB),
lists them, and lets you download or delete.

---

# Part 2 — The AI risk cards

On each patient chart, the **AI & CDS Briefing** shows three risk cards:

| Card | Meaning |
|---|---|
| **Readmission Risk** | Probability of a 30-day hospital readmission. |
| **Diabetes Risk** | Type-2 diabetes risk. |
| **Cardiovascular Risk** | Heart-disease risk. |

How to read a card:
- A **percentage + tier** (Low / Medium / High) from the model.
- The **feature values** the model used (e.g. glucose, BMI, # medications).
- If some inputs were missing, the card shows an **"estimated / imputed"** note — treat that score as
  less certain.
- "Model not available yet" means the model file hasn't been trained/placed — see the
  [AI training guide](ai-training/README.md).

### AI Insights page
Nav → Intelligence → **AI Insights** gives a population view: a **risk watchlist** across patients,
a **"How Scoring Works"** explainer, and **condition prevalence** across the workspace.

### CDS in action
When you prescribe a medication (or other triggering action), the CDS engine evaluates the active
rules using clinical data, medicine knowledge, and AI risk, and may show a **card** (e.g. a dosing
warning, a duplicate-ingredient alert, or a readmission-risk flag). Fired cards also appear in the
dashboard's recent-alerts list.

---

# Part 3 — Administration

### Organizations (System Admin)
Nav → Administration → **Organizations** to create and manage organizations and assign org admins.

### Onboarding staff & patients
Nabd links a person's login (Keycloak) to their clinical record via a one-time **invite → claim** flow:

1. **Create the invite** (admin): from the staff or patient onboarding screen, fill in the
   organization and the person's name/details. Nabd creates the FHIR record and returns an **invite
   code / link**.
2. **Claim the invite** (the new user): the invited person signs in and submits the invite code,
   which links their Keycloak account to the record and grants the correct role (patient, doctor, or
   org admin).

### Authoring CDS rules (CDS Admin)
Nav → Administration → **CDS Admin** is the rule workbench. Typical flow:

1. **Start from a template** (e.g. "Readmission Risk", "Polypharmacy", "Dosing") or create a rule
   from scratch. Templates list the parameters they need.
2. **Edit** the rule's trigger expression (JSON-Logic over CDS variables — see the
   `variables` list in the editor) and its **card** (the message shown to clinicians).
3. **Validate** and **Preview** the rule against sample context to see whether it fires.
4. **Publish** to create an immutable version, then **Activate** so it runs live. Use
   **Deactivate**/**Archive** to retire it. Each publish is versioned and viewable in history.

> CDS variables include the AI risk values (e.g. readmission probability/tier), clinical counts, and
> medicine-knowledge facts, so rules can combine model output with clinical signals.

---

# Part 4 — Loading demo data (development)

To populate a fresh environment for a demo, use the in-app dev seeding tools (Development only), which
import sample Synthea FHIR bundles (patients, conditions, observations, medications, encounters,
procedures). This gives the dashboards, CDS, and AI cards realistic data to display. See the
`/dev/seed*` endpoints in the [API reference](api-reference.md#6-development--seeding--dev-development-only)
or the seeding controls in the UI.
