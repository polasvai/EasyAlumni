# Member Edit Feature & Registrations Table Cleanup Implementation Plan

> **For Antigravity:** REQUIRED WORKFLOW: Use `.agent/workflows/execute-plan.md` to execute this plan in single-flow mode.

**Goal:** Remove the T-Shirt column from the Admin Registrations directory table and provide a comprehensive dedicated Member Edit feature (`/Admin/EditRegistration/{id}`) where all member details are editable while strictly preserving immutable ticket numbers, user codes, IDs, and transaction IDs.

**Architecture:** ASP.NET Core MVC with Entity Framework Core, structured with a dedicated ViewModel (`AdminEditRegistrationViewModel`), controller actions (`GET` & `POST` in `AdminController`), and a multi-section Bootstrap 5 Razor view (`EditRegistration.cshtml`).

**Tech Stack:** ASP.NET Core 8, EF Core 8, SQL Server 2012, Bootstrap 5, FontAwesome 6.

---

### Task 1: Update Registrations Table View (`Views/Admin/Registrations.cshtml`)

**Files:**
- Modify: `src/EasyAlumni.Web/Views/Admin/Registrations.cshtml`

**Step 1: Edit table headers and rows**
- Remove `<th>T-Shirt</th>` from `<thead class="table-light small text-uppercase">`.
- Remove `<td><span class="badge bg-info text-dark fs-6">@reg.TShirtSize</span></td>` from `<tbody>`.
- Add an "Edit" button `<a asp-action="EditRegistration" asp-route-id="@reg.Id" class="btn btn-sm btn-outline-warning rounded-pill px-2 me-1" title="Edit Member Information"><i class="fas fa-edit"></i></a>` to the action buttons column.

**Step 2: Commit**
```bash
git add src/EasyAlumni.Web/Views/Admin/Registrations.cshtml
git commit -m "feat(admin): remove t-shirt column and add edit action button to registrations table"
```

---

### Task 2: Create AdminEditRegistrationViewModel (`ViewModels/AdminEditRegistrationViewModel.cs`)

**Files:**
- Create: `src/EasyAlumni.Web/ViewModels/AdminEditRegistrationViewModel.cs`

**Step 1: Define ViewModel with validation attributes**
- Read-only identifiers (for display):
  - `RegistrationId`, `RegistrationNo`, `UserCode`, `TransactionId`, `GatewayFtNumber`
- Personal Information:
  - `NameBangla`, `NameEnglish`, `NickName`, `PassingYear`, `BloodGroup`, `CurrentRecentPhotoPath`, `NewPhotoFile`
- Contact & Address:
  - `ContactNumber`, `AlternativeNumber`, `Email`, `PresentAddress`, `PermanentAddress`
- Education & Job:
  - `LastInstitute`, `LastDegree`, `LastDegreeSubject`, `CompanyName`, `Designation`, `CurrentWorkingAddress`
- Event & Merchandise:
  - `RegistrationPackageId`, `AvailablePackages` (for dropdown), `TShirtSize`, `SpouseCount`, `ChildCount`, `GuestCount`, `IsKitDistributed`
- Payment & Status:
  - `TotalAmount`, `PaidAmount`, `RegistrationStatus`, `PaymentMode`, `PaymentStatus`, `SenderNumber`, `AdminRemarks`

**Step 2: Commit**
```bash
git add src/EasyAlumni.Web/ViewModels/AdminEditRegistrationViewModel.cs
git commit -m "feat(admin): add AdminEditRegistrationViewModel for editing member details"
```

---

### Task 3: Implement GET & POST Actions in AdminController (`Controllers/AdminController.cs`)

**Files:**
- Modify: `src/EasyAlumni.Web/Controllers/AdminController.cs`

**Step 1: Implement `GET EditRegistration(int id)`**
- Fetch registration including `AlumniProfile`, `Payments`, `ReunionEvent`, `Guests`, and active packages.
- Map entities to `AdminEditRegistrationViewModel`.
- Return `View(vm)`.

**Step 2: Implement `POST EditRegistration(AdminEditRegistrationViewModel model)`**
- Validate `ModelState`.
- Retrieve tracked `EventRegistration`, `AlumniProfile`, and `RegistrationPayment`.
- Handle optional photo replacement via `_fileStorage.SaveFileAsync`.
- Update all editable properties on `AlumniProfile`, `EventRegistration`, and `RegistrationPayment`.
- Guard: Never overwrite `RegistrationNo`, `UserCode`, or `TransactionId`/`GatewayFtNumber`.
- Save changes via `_context.SaveChangesAsync()`.
- Set `TempData["Success"]` and redirect to `Registrations`.

**Step 3: Commit**
```bash
git add src/EasyAlumni.Web/Controllers/AdminController.cs
git commit -m "feat(admin): add GET and POST EditRegistration actions"
```

---

### Task 4: Create Edit View (`Views/Admin/EditRegistration.cshtml`)

**Files:**
- Create: `src/EasyAlumni.Web/Views/Admin/EditRegistration.cshtml`

**Step 1: Write razor view with Bootstrap 5 layout**
- Read-only banner at the top displaying Ticket No, Alumnus ID, and Transaction ID with padlock icons.
- Section 1: Personal Details & Photo (with current avatar preview and file input).
- Section 2: Contact & Addresses.
- Section 3: Education & Career Details.
- Section 4: Package, Merchandise (T-Shirt size dropdown) & Guest headcounts.
- Section 5: Financials, Approval Status & Kit Handover.
- Action buttons: "Save Changes" (primary) and "Cancel / Back to Directory" (outline).

**Step 2: Commit**
```bash
git add src/EasyAlumni.Web/Views/Admin/EditRegistration.cshtml
git commit -m "feat(admin): create EditRegistration view"
```

---

### Task 5: Build, Publish, Restart Service & Verify

**Files:**
- Publish directory: `/home/scs/Projects/EasyAlumni/publish`

**Step 1: Build the project**
```bash
dotnet build src/EasyAlumni.Web/EasyAlumni.Web.csproj
```

**Step 2: Publish Release build**
```bash
OPENSSL_CONF=/home/scs/Projects/EasyAlumni/publish/openssl.cnf dotnet publish src/EasyAlumni.Web/EasyAlumni.Web.csproj -c Release -o /home/scs/Projects/EasyAlumni/publish
```

**Step 3: Restart systemd service**
```bash
sudo systemctl restart easyalumni.service
sudo systemctl status easyalumni.service --no-pager
```

**Step 4: Verify via HTTP test**
- Confirm `/Admin/Registrations` HTML has no T-Shirt header and has Edit button.
- Confirm `/Admin/EditRegistration/{id}` renders properly.

**Step 5: Commit and push**
```bash
git push origin main
```
