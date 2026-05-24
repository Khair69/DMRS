# Frontend Demo Testing Guide

This guide covers:

1. the first client shell/dashboard step
2. the medication workflow + clinician-facing CDS step

## Prerequisites

- `DMRS.Api` running
- `DMRS.Client` running
- valid login with a role that can access the clinical pages
- sample data available, ideally including:
  - at least a few patients
  - one patient with `birthDate` and `gender` for AI risk
  - the CDS test patient `cds-test-patient` if you want to trigger medication CDS exactly

## Step 1: Test the client shell and dashboard

### Home dashboard

- Open `/`
- Expected:
  - no `Hello, world`
  - hero section with DMRS workspace messaging
  - metric cards for patients, appointments, active meds, encounters, service requests, conditions
  - predictive watchlist panel
  - CDS status panel
  - appointments and recent medication activity panels

### Navigation

- Open the left nav
- Expected:
  - grouped sections such as `Workspace`, `Clinical`, `Orders and Scheduling`, `Intelligence`, `Administration`
  - direct link to `AI Insights`
  - direct link to `CDS Admin`

### AI insights page

- Open `/ai-insights`
- Expected:
  - watchlist view for high-utilization risk
  - explanatory cards describing model inputs and use
  - if sample patients with `birthDate` and `gender` exist, at least one watchlist item should appear

### Patient chart page

- Open `/patients/{id}` for a patient with related records
- Expected:
  - hero section instead of plain heading
  - summary metric cards
  - grouped clinical sections for conditions, allergies, observations, medication requests, encounters, appointments
  - AI risk briefing on the right
  - account/invite panel still present

## Step 2: Test medication workflow CDS preview

### Create page

- Open `/medication-requests/new`
- Fill:
  - `Patient Id`: `cds-test-patient`
  - `Medication`: `Acetaminophen (Tylenol)`
  - `Medication RxCUI`: `161`
  - `Dose (mg)`: `1500`
  - `Frequency Per Day`: `3`
  - `Status`: `active`
  - `Intent`: `order`
- Click `Preview CDS`

- Expected:
  - medicine knowledge resolves to Acetaminophen
  - max daily dose appears as `4000 mg` if the mock knowledge is loaded
  - if the relevant rule is published and active, one or more CDS cards appear
  - for the classic dose demo, the card should indicate requested dose exceeds max daily dose

### Save and details page

- From the same create form, click `Create Medication Request`
- Expected:
  - redirect to `/medication-requests/{id}`
  - details page shows hero section
  - right-side `Clinical Decision Snapshot` panel
  - medicine knowledge resolves again
  - CDS cards match the live hook result for the saved request

### Non-alert path

- Repeat using:
  - `Medication RxCUI`: `5640`
  - `Dose (mg)`: `400`
  - `Frequency Per Day`: `3`
- Expected:
  - knowledge should resolve to Ibuprofen
  - details page should still render the knowledge panel cleanly
  - CDS may return no cards depending on active published rules

## Common Failure Cases

- Dashboard loads but watchlist is empty:
  - patients likely do not have both `birthDate` and binary `gender`
  - or AI endpoint is not returning data

- Patient chart sections are empty:
  - related resources may not reference the patient using `Patient/{id}`
  - or sample data was not imported yet

- Medication preview shows no medicine knowledge:
  - RxCUI missing or not present in mock medicine source
  - or knowledge endpoints are failing

- Medication preview shows no CDS cards:
  - no relevant rule is published and active
  - request data does not match the rule expression
  - test patient/allergy setup is missing
