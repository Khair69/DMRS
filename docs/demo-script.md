# DMRS — Live Demo Script

A rehearsed ~12-minute walkthrough for the defense. It tells one story: *standards-based records →
decision support → AI risk → configurable rules.* Cues are written as **Say** (narration) and
**Do** (on screen).

---

## Pre-demo checklist (do this before you present)

Bring the stack up in order and confirm each is reachable:

1. **PostgreSQL** running; `DMRS` database migrated.
2. **Keycloak** (`docker/keycloak`) up; you can log in.
3. **DMRS.MedicineInfo.Api** (`:5041`) running.
4. **DMRS.Api** (`:7029`) running — confirm `/swagger` loads.
5. **DMRS.Client** (`:7099`) running.
6. The three model files exist in `DMRS.Api/Ai/`
   (`readmission_predictor.onnx`, `diabetes_predictor.onnx`, `cardiovascular_predictor.onnx`).
7. **Sample data seeded** (see Reset below).

**Pick your demo patients ahead of time** and write down their names/IDs:
- **Patient A** — has labs (glucose, BMI, BP, cholesterol) so all three AI cards show real numbers; ideally a **High/Medium** readmission tier.
- **Patient B** — a clean case you'll *prescribe* to, to trigger a CDS card (e.g. a high-dose or controlled drug from the seeded medicine list).

Have two browser profiles/accounts ready if you want to show roles: a **practitioner** and a **system admin** (or one admin account that sees everything).

> **Provisioning staff logins for the demo.** The seeded doctors and org admins are FHIR
> `Practitioner` records with no login account. To log in *as* one of them, open their **Staff
> Details** page (Organization → Manage Staff → a person) and click **Create login account** in the
> *Login Account* card. It creates a Keycloak user (username = their email), assigns the right role
> (`ROLE_PRACTITIONER` / `ROLE_ORG_ADMIN`), links it to the resource, and shows the password
> (`Demo123!` by default — override with `Keycloak:DemoUserPassword`). The button only appears when
> the person has no account yet; afterwards it shows the linked username. Do this for one doctor and
> one org admin *before* you present so the credentials are ready.
>
> *This is a demo shortcut* — the production-correct path is the invite + self-registration flow,
> which is unchanged. Mention this if asked: an admin minting passwords is for demonstration only.

### Reset / re-seed (repeatable)
To start from a clean, known state:
- Clear data: `DELETE /dev/seed` (or the UI seeding control).
- Re-seed: `POST /dev/seed` (or seed file-by-file from the UI).
- If medicine lookups look empty, re-seed the medicine DB: `dropdb DMRSMedicine` then restart `DMRS.MedicineInfo.Api`.

> Keep this section open in a terminal during the demo so you can reset quickly if needed.

---

## The script

### 0. Framing (30s)
**Say:** "DMRS is a FHIR-compliant medical records system. On top of standards-based clinical data it
adds clinical decision support and three AI risk models. I'll show the clinician experience, then the
admin side."

### 1. Login & roles (1 min)
**Do:** Open `:7099` → log in via Keycloak.
**Say:** "Authentication is OAuth2/OpenID Connect through Keycloak. What each user sees is driven by
their role — the navigation adapts to the practitioner vs. admin."

### 2. Dashboard (1 min)
**Do:** Land on Home. Point to the metric cards and the **Predictive Watchlist**.
**Say:** "The workspace overview — live counts, upcoming appointments, recent CDS alerts, and a
watchlist of patients ranked by AI-predicted 30-day readmission risk."

### 3. Patient chart & FHIR (2 min)
**Do:** Open **Patient A**. Walk the chart: demographics, conditions/observations/medications.
Open **History** on one record.
**Say:** "Every resource is stored as FHIR and fully versioned — here's the audit history. This is
standard FHIR R5, so it's interoperable with other systems."

### 4. AI risk cards (2 min)  ⭐
**Do:** Scroll to the **AI & CDS Briefing**. Point to **Readmission**, **Diabetes**, **Cardiovascular**.
**Say:** "Three trained models score this patient from their own FHIR data. Each card shows the
probability, the risk tier, and the exact inputs the model used — for example glucose and BMI for
diabetes. If a value is missing we impute and flag it, so a clinician always sees an honest estimate."
**Say (contribution):** "These were trained on public datasets in Colab and exported to ONNX; the
key design choice was training only on features we can actually pull from a FHIR record."

### 5. CDS alert on prescribing (2.5 min)  ⭐
**Do:** Open **Patient B** → **New Medication Request** → choose a drug that triggers a rule
(e.g. a high dose or a controlled substance) → save.
**Do:** Show the **CDS card** that fires; return to the dashboard to show it in **recent alerts**.
**Say:** "When a clinician prescribes, the decision-support engine evaluates active rules using the
medicine knowledge base, the patient's data, and the AI risk — and surfaces a card in the workflow."

### 6. AI Insights — population view (1.5 min)
**Do:** Nav → Intelligence → **AI Insights**. Show the **watchlist**, **How Scoring Works**, and
**condition prevalence**.
**Say:** "Beyond a single patient, this is the population view — who's highest risk, how the model
works, and what conditions are most common across the workspace."

### 7. CDS Admin — author a rule (2 min)  ⭐
**Do:** Nav → Administration → **CDS Admin** → create a rule **from a template** (e.g. Readmission
Risk) → **Validate** → **Preview** (show it firing on sample context) → **Publish** → **Activate**.
**Say:** "Rules aren't hard-coded — an admin authors them here over CDS variables, including the AI
risk values. Validate, preview, publish as an immutable version, then activate. That closes the loop:
the AI feeds the rules, and the rules drive the alerts clinicians saw earlier."

### 8. Wrap (30s)
**Say:** "So: a FHIR-native record, decision support, three AI risk models, and a configurable rule
engine that ties them together. Happy to dive into any layer."

---

## Fallback notes (if something misbehaves)

- **Play the backup recording.** If the live stack fails, switch to the pre-recorded walkthrough
  (it follows this exact script).
- **AI card says "not available":** the model file is missing — confirm the three `.onnx` files are
  in `DMRS.Api/Ai/`. The rest of the demo still works.
- **CDS card doesn't fire:** make sure at least one rule is **Published + Active**, and that you
  prescribed a drug/dose that meets its trigger. Have a known-good patient+drug combination noted.
- **Empty medicine lookup:** re-seed the medicine DB (see Reset).
- **Keycloak login slow/looping:** clear the browser session / use a fresh private window; verify the
  client redirect URIs point at `https://localhost:7099`.
- **Don't re-seed mid-demo** unless necessary — it takes time. Reset *before* you start.
