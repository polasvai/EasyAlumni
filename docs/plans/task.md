| Task | Description | Status | Evidence |
| :--- | :--- | :--- | :--- |
| Task 1 | Remove T-Shirt column and add Edit action button in `Views/Admin/Registrations.cshtml` | done | Removed `th` and `td` for TShirtSize, added Edit button with tooltip and icon, committed in `eafa943` |
| Task 2 | Create `AdminEditRegistrationViewModel.cs` with validation and read-only identifier attributes | done | Created `AdminEditRegistrationViewModel.cs` with all immutable, personal, contact, education, package, and payment fields, committed in `e0b0675` |
| Task 3 | Implement GET and POST `EditRegistration` actions in `AdminController.cs` | done | Implemented GET (entity mapping) and POST (safe update protecting ticket no, usercode, txn id, and handling photo replacement), committed in `c34caad` |
| Task 4 | Create dedicated Bootstrap 5 edit view `Views/Admin/EditRegistration.cshtml` | done | Built responsive multi-section view with read-only badge banner, photo replacement, and validation, committed in `fd074d2` |
| Task 5 | Build, publish to `/publish`, restart `easyalumni.service`, and verify | done | Published Release build, restarted `easyalumni.service` (running), automated verification confirmed T-Shirt column removed, Edit button present, Edit page renders with locked badges, and successful POST update committed and pushed to `main` |
