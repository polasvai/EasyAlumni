# Design Document: Member Edit Feature & Registrations Table Cleanup

- **Date:** 2026-10-18
- **Status:** Approved
- **Target URL:** `https://alumni.snhghs.edu.bd/Admin/Registrations` & `/Admin/EditRegistration/{id}`

---

## 1. Overview & Objectives

Admins managing reunion attendees require full editing capability over all registered member data while strictly preserving immutable ticket numbers, user codes, internal database IDs, and financial transaction identifiers (TrxID / Gateway reference). Additionally, the directory table on `/Admin/Registrations` needs the `T-Shirt` column removed for a cleaner view, and an `Edit` action button added to each attendee row.

---

## 2. Requirements & Constraints

### 2.1 Table Changes (`/Admin/Registrations`)
- Remove `<th>T-Shirt</th>` from the table header.
- Remove `<td><span class="badge ...">@reg.TShirtSize</span></td>` from the table body.
- Add an **Edit** action button (`<i class="fas fa-edit"></i>`) in the actions column linking to `/Admin/EditRegistration/{id}`.

### 2.2 Immutable / Read-Only Elements
The following values MUST NEVER be modified or submitted as editable fields:
- **Registration Ticket Number** (`EventRegistration.RegistrationNo`)
- **Alumnus User ID** (`AlumniProfile.UserCode`)
- **Database Primary Keys** (`EventRegistration.Id`, `AlumniProfile.Id`, `RegistrationPayment.Id`)
- **Transaction ID / Gateway FT Number** (`RegistrationPayment.TransactionId`, `GatewayFtNumber`, `GatewayTransactionToken`)

These fields will be displayed prominently in a locked/disabled visual badge banner for reference only.

### 2.3 Editable Fields

#### Section A: Personal Information
- `NameBangla` (Bengali Name)
- `NameEnglish` (English Name)
- `NickName` (Nick Name)
- `PassingYear` (SSC Batch / Passing Year)
- `BloodGroup` (Blood Group dropdown: A+, A-, B+, B-, AB+, AB-, O+, O-)
- `RecentPhotoFile` (Optional replacement image upload with image preview)

#### Section B: Contact & Addresses
- `ContactNumber` (Primary 11-digit mobile number)
- `AlternativeNumber` (Alternative mobile number)
- `Email` (Email address)
- `PresentAddress` (Current residence address)
- `PermanentAddress` (Permanent home address)

#### Section C: Education & Professional
- `LastInstitute` (Last college/university attended)
- `LastDegree` (Degree obtained, e.g. BSc, MBBS, MBA)
- `LastDegreeSubject` (Major/subject)
- `CompanyName` (Current employer / company)
- `Designation` (Job designation)
- `CurrentWorkingAddress` (Office address)

#### Section D: Package, Merchandise & Guests
- `RegistrationPackageId` (Dropdown of active packages for the event)
- `TShirtSize` (Dropdown: S, M, L, XL, XXL, XXXL)
- `SpouseCount` (Number of spouses attending)
- `ChildCount` (Number of children attending)
- `GuestCount` (Number of other guests attending)
- `IsKitDistributed` (Kit handover toggle/checkbox)

#### Section E: Financial & Approval Status
- `TotalAmount` (Editable decimal)
- `PaidAmount` (Claimed / confirmed paid amount)
- `RegistrationStatus` (Dropdown: `Pending`, `Approved`, `Rejected`)
- `PaymentMode` (Dropdown: JanataPay, bKashManual, NagadManual, RocketManual, BankTransfer, Cash)
- `PaymentStatus` (Dropdown: `Pending`, `Approved`, `Rejected`)
- `AdminRemarks` (Optional admin note)

---

## 3. Architecture & Data Flow

1. **Controller Actions**:
   - `GET /Admin/EditRegistration/{id}`:
     - Loads `EventRegistration` with `AlumniProfile`, `Payments`, `ReunionEvent`, `Guests`, and available packages.
     - Maps into a dedicated `AdminEditRegistrationViewModel`.
     - Returns `Views/Admin/EditRegistration.cshtml`.
   - `POST /Admin/EditRegistration`:
     - Validates anti-forgery token and inputs.
     - Loads tracked entities: `EventRegistration`, `AlumniProfile`, and primary `RegistrationPayment`.
     - Handles file upload if a new photo is provided (via `IFileStorageService`).
     - Updates fields on `AlumniProfile`, `EventRegistration`, and `RegistrationPayment`.
     - Commits changes to the database.
     - Redirects back to `/Admin/Registrations` with `TempData["Success"] = "Member details updated successfully!"`.

---

## 4. UI / UX Design
- Follows existing modern Bootstrap 5 styling with rounded cards, pills, and subtle shadows matching `_AdminLayout.cshtml`.
- Read-only identifiers presented in distinct gray/light badges.
- Responsive grid: 2 columns on desktop, 1 column on mobile.
- Save & Cancel buttons with confirmation guards.
