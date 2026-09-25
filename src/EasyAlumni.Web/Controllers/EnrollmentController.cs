using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _fileStorage;
        private readonly IQrCodeService _qrCodeService;
        private readonly ISmsService _smsService;
        private readonly ILogger<EnrollmentController> _logger;

        public EnrollmentController(
            ApplicationDbContext context,
            IFileStorageService fileStorage,
            IQrCodeService qrCodeService,
            ISmsService smsService,
            ILogger<EnrollmentController> logger)
        {
            _context = context;
            _fileStorage = fileStorage;
            _qrCodeService = qrCodeService;
            _smsService = smsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var reunionEvent = await _context.ReunionEvents
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.EventDate)
                .FirstOrDefaultAsync();

            if (reunionEvent == null)
            {
                TempData["Error"] = "No active reunion registration event is currently available.";
                return RedirectToAction("Index", "Home");
            }

            var paymentSettings = await _context.SystemSettings
                .Where(s => s.SettingKey.StartsWith("Manual") || s.SettingKey.StartsWith("Payment_"))
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            var enabledPaymentModes = new List<PaymentMode>();
            if (paymentSettings.GetValueOrDefault("Payment_Enable_JanataPay", "1") == "1") enabledPaymentModes.Add(PaymentMode.JanataPay);
            if (paymentSettings.GetValueOrDefault("Payment_Enable_bKashManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.bKashManual);
            if (paymentSettings.GetValueOrDefault("Payment_Enable_NagadManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.NagadManual);
            if (paymentSettings.GetValueOrDefault("Payment_Enable_RocketManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.RocketManual);
            if (paymentSettings.GetValueOrDefault("Payment_Enable_BankTransfer", "1") == "1") enabledPaymentModes.Add(PaymentMode.BankTransfer);
            if (paymentSettings.GetValueOrDefault("Payment_Enable_Cash", "1") == "1") enabledPaymentModes.Add(PaymentMode.Cash);

            if (!enabledPaymentModes.Any())
            {
                enabledPaymentModes.Add(PaymentMode.JanataPay);
            }

            var defaultMethodStr = paymentSettings.GetValueOrDefault("Payment_Default_Method", "JanataPay");
            PaymentMode selectedPaymentMode = PaymentMode.JanataPay;
            if (Enum.TryParse<PaymentMode>(defaultMethodStr, out var parsedMode) && enabledPaymentModes.Contains(parsedMode))
            {
                selectedPaymentMode = parsedMode;
            }
            else
            {
                selectedPaymentMode = enabledPaymentModes.First();
            }

            var packages = await _context.RegistrationPackages
                .Where(p => p.IsActive && p.ReunionEventId == reunionEvent.Id)
                .Include(p => p.PackageGiftItems)
                    .ThenInclude(pg => pg.GiftItem)
                        .ThenInclude(g => g!.SizeStocks)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            var guestCategories = await _context.GuestCategories
                .Where(g => g.IsActive && g.ReunionEventId == reunionEvent.Id)
                .OrderBy(g => g.DisplayOrder)
                .ToListAsync();

            var customQuestions = await _context.EventCustomQuestions
                .Where(q => q.IsActive && q.ReunionEventId == reunionEvent.Id)
                .OrderBy(q => q.DisplayOrder)
                .ToListAsync();

            var formFieldSettings = await _context.SystemSettings
                .Where(s => s.SettingKey.StartsWith("FormField_"))
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

            var vm = new AlumniRegistrationViewModel
            {
                ReunionEventId = reunionEvent.Id,
                ReunionEvent = reunionEvent,
                PaymentSettings = paymentSettings,
                FormFieldSettings = formFieldSettings,
                EnabledPaymentModes = enabledPaymentModes,
                DefaultPaymentMode = defaultMethodStr,
                PaymentMode = selectedPaymentMode,
                AvailablePackages = packages,
                AvailableGuestCategories = guestCategories,
                AvailableCustomQuestions = customQuestions,
                RegistrationPackageId = packages.FirstOrDefault(p => p.IsFeatured)?.Id ?? packages.FirstOrDefault()?.Id
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(AlumniRegistrationViewModel model)
        {
            var reunionEvent = await _context.ReunionEvents.FindAsync(model.ReunionEventId);
            if (reunionEvent == null)
            {
                ModelState.AddModelError("", "Selected event is not valid.");
            }

            // Check if phone or email already registered for this event
            var existingReg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Where(r => r.ReunionEventId == model.ReunionEventId &&
                            (r.AlumniProfile!.ContactNumber == model.ContactNumber.Trim() ||
                             (!string.IsNullOrWhiteSpace(model.Email) && r.AlumniProfile.Email == model.Email.Trim().ToLower())))
                .OrderByDescending(r => r.RegisteredAt)
                .FirstOrDefaultAsync();

            if (existingReg != null)
            {
                if (existingReg.Status == RegistrationStatus.Pending)
                {
                    TempData["PaymentNotice"] = $"You already registered as {existingReg.AlumniProfile?.NameEnglish} ({existingReg.RegistrationNo}), but payment has not been completed yet. Please complete your JanataPay payment below to finalize your registration.";
                    return RedirectToAction("JanataPayCheckout", "Payment", new { registrationNo = existingReg.RegistrationNo });
                }
                else if (existingReg.Status == RegistrationStatus.Approved)
                {
                    HttpContext.Session.SetString($"Pass_Authorized_{existingReg.RegistrationNo}", "1");
                    var successPay = existingReg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault(p => p.Status == PaymentStatus.Approved)
                                     ?? existingReg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault();
                    var trxCode = !string.IsNullOrEmpty(successPay?.GatewayFtNumber) ? successPay.GatewayFtNumber : successPay?.TransactionId;

                    TempData["PassLookupWarning"] = $"Alumnus {existingReg.AlumniProfile?.NameEnglish} ({existingReg.RegistrationNo}) is already registered and payment is approved. You can view and download your ID pass directly.";
                    return RedirectToAction("ViewPass", "Pass", new { id = existingReg.RegistrationNo, trx = trxCode });
                }
                else
                {
                    ModelState.AddModelError("ContactNumber", $"An alumnus with this mobile number or email is already registered ({existingReg.RegistrationNo}) with status: {existingReg.Status}.");
                }
            }

            // Load Dynamic Form Field Settings
            var formFieldSettings = await _context.SystemSettings
                .Where(s => s.SettingKey.StartsWith("FormField_"))
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
            model.FormFieldSettings = formFieldSettings;

            // Enforce dynamic field requirements
            if (formFieldSettings.GetValueOrDefault("FormField_Photo_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_Photo_Required", "1") == "1" &&
                (model.RecentPhotoFile == null || model.RecentPhotoFile.Length == 0))
            {
                ModelState.AddModelError("RecentPhotoFile", "Recent passport portrait picture is required.");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_NickName_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_NickName_Required", "1") == "1" &&
                string.IsNullOrWhiteSpace(model.NickName))
            {
                ModelState.AddModelError("NickName", "Nick Name is required.");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_BloodGroup_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_BloodGroup_Required", "0") == "1" &&
                string.IsNullOrWhiteSpace(model.BloodGroup))
            {
                ModelState.AddModelError("BloodGroup", "Blood Group is required.");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_AltNumber_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_AltNumber_Required", "0") == "1" &&
                string.IsNullOrWhiteSpace(model.AlternativeNumber))
            {
                ModelState.AddModelError("AlternativeNumber", "Alternative Mobile Number is required.");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_Testimonial_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_Testimonial_Required", "0") == "1" &&
                (model.TestimonialFile == null || model.TestimonialFile.Length == 0))
            {
                ModelState.AddModelError("TestimonialFile", "Testimonial or School ID certificate is required.");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_PresentAddress_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_PresentAddress_Required", "1") == "1" &&
                string.IsNullOrWhiteSpace(model.PresentAddress))
            {
                ModelState.AddModelError("PresentAddress", "Present Address is required (বর্তমান ঠিকানা আবশ্যক).");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_PermanentAddress_Enabled", "1") == "1" &&
                formFieldSettings.GetValueOrDefault("FormField_PermanentAddress_Required", "1") == "1" &&
                string.IsNullOrWhiteSpace(model.PermanentAddress))
            {
                ModelState.AddModelError("PermanentAddress", "Permanent Address is required (স্থায়ী ঠিকানা আবশ্যক).");
            }

            if (formFieldSettings.GetValueOrDefault("FormField_EducationCareer_Enabled", "1") == "1")
            {
                if (formFieldSettings.GetValueOrDefault("FormField_LastInstitute_Required", "0") == "1" && string.IsNullOrWhiteSpace(model.LastInstitute))
                {
                    ModelState.AddModelError("LastInstitute", "Last Educational Institute is required.");
                }
                if (formFieldSettings.GetValueOrDefault("FormField_LastDegree_Required", "0") == "1" && string.IsNullOrWhiteSpace(model.LastDegree))
                {
                    ModelState.AddModelError("LastDegree", "Last Degree Obtained is required.");
                }
                if (formFieldSettings.GetValueOrDefault("FormField_CompanyName_Required", "0") == "1" && string.IsNullOrWhiteSpace(model.CompanyName))
                {
                    ModelState.AddModelError("CompanyName", "Company or Organization Name is required.");
                }
                if (formFieldSettings.GetValueOrDefault("FormField_Designation_Required", "0") == "1" && string.IsNullOrWhiteSpace(model.Designation))
                {
                    ModelState.AddModelError("Designation", "Job Designation is required.");
                }
            }

            // Validate Passing Year Package Eligibility
            var selectedPackage = model.RegistrationPackageId.HasValue
                ? await _context.RegistrationPackages
                    .Include(p => p.PackageGiftItems)
                        .ThenInclude(pg => pg.GiftItem)
                    .FirstOrDefaultAsync(p => p.Id == model.RegistrationPackageId.Value && p.IsActive)
                : null;

            if (selectedPackage != null)
            {
                if (selectedPackage.MinPassingYear.HasValue && model.PassingYear < selectedPackage.MinPassingYear.Value)
                {
                    ModelState.AddModelError("RegistrationPackageId", $"The selected package '{selectedPackage.PackageName}' is only for passing years from {selectedPackage.MinPassingYear.Value} onwards.");
                }
                if (selectedPackage.MaxPassingYear.HasValue && model.PassingYear > selectedPackage.MaxPassingYear.Value)
                {
                    ModelState.AddModelError("RegistrationPackageId", $"The selected package '{selectedPackage.PackageName}' is only for passing years up to {selectedPackage.MaxPassingYear.Value}.");
                }
            }

            // Manual payment modes require TransactionId and SenderNumber
            if (model.PaymentMode != PaymentMode.JanataPay)
            {
                if (string.IsNullOrWhiteSpace(model.TransactionId))
                {
                    ModelState.AddModelError("TransactionId", "Transaction ID (TrxID) is required for manual mobile banking.");
                }
                if (string.IsNullOrWhiteSpace(model.SenderNumber))
                {
                    ModelState.AddModelError("SenderNumber", "Sender Mobile Number is required for manual mobile banking.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.ReunionEvent = reunionEvent;
                model.PaymentSettings = await _context.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("Manual") || s.SettingKey.StartsWith("Payment_"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
                model.FormFieldSettings = formFieldSettings;

                var enabledPaymentModes = new List<PaymentMode>();
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_JanataPay", "1") == "1") enabledPaymentModes.Add(PaymentMode.JanataPay);
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_bKashManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.bKashManual);
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_NagadManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.NagadManual);
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_RocketManual", "1") == "1") enabledPaymentModes.Add(PaymentMode.RocketManual);
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_BankTransfer", "1") == "1") enabledPaymentModes.Add(PaymentMode.BankTransfer);
                if (model.PaymentSettings.GetValueOrDefault("Payment_Enable_Cash", "1") == "1") enabledPaymentModes.Add(PaymentMode.Cash);
                if (!enabledPaymentModes.Any()) enabledPaymentModes.Add(PaymentMode.JanataPay);
                model.EnabledPaymentModes = enabledPaymentModes;

                model.AvailablePackages = await _context.RegistrationPackages
                    .Where(p => p.IsActive && p.ReunionEventId == model.ReunionEventId)
                    .Include(p => p.PackageGiftItems)
                        .ThenInclude(pg => pg.GiftItem)
                            .ThenInclude(g => g!.SizeStocks)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
                model.AvailableGuestCategories = await _context.GuestCategories
                    .Where(g => g.IsActive && g.ReunionEventId == model.ReunionEventId)
                    .OrderBy(g => g.DisplayOrder)
                    .ToListAsync();
                model.AvailableCustomQuestions = await _context.EventCustomQuestions
                    .Where(q => q.IsActive && q.ReunionEventId == model.ReunionEventId)
                    .OrderBy(q => q.DisplayOrder)
                    .ToListAsync();
                return View(model);
            }

            try
            {
                var allowedImageExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var allowedDocExt = new[] { ".pdf", ".jpg", ".jpeg", ".png" };

                // 1. Upload files
                string? oldPhotoPath = null;
                if (model.OldPhotoFile != null && model.OldPhotoFile.Length > 0)
                {
                    using var stream = model.OldPhotoFile.OpenReadStream();
                    oldPhotoPath = await _fileStorage.SaveFileAsync(stream, model.OldPhotoFile.FileName, "OldPhotos", allowedImageExt);
                }

                string? recentPhotoPath = null;
                if (model.RecentPhotoFile != null && model.RecentPhotoFile.Length > 0)
                {
                    using var stream = model.RecentPhotoFile.OpenReadStream();
                    recentPhotoPath = await _fileStorage.SaveFileAsync(stream, model.RecentPhotoFile.FileName, "RecentPhotos", allowedImageExt);
                }

                string? testimonialPath = null;
                if (model.TestimonialFile != null && model.TestimonialFile.Length > 0)
                {
                    using var stream = model.TestimonialFile.OpenReadStream();
                    testimonialPath = await _fileStorage.SaveFileAsync(stream, model.TestimonialFile.FileName, "Testimonials", allowedDocExt, 307200); // Max 300KB
                }

                string? slipPath = null;
                if (model.SlipAttachmentFile != null && model.SlipAttachmentFile.Length > 0)
                {
                    using var stream = model.SlipAttachmentFile.OpenReadStream();
                    slipPath = await _fileStorage.SaveFileAsync(stream, model.SlipAttachmentFile.FileName, "PaymentSlips", allowedDocExt, 5242880); // Max 5MB
                }

                // 2. Generate Unique User Code (10 characters)
                var userCode = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

                // 3. Create or find AlumniProfile
                var profile = new AlumniProfile
                {
                    UserCode = userCode,
                    NameBangla = model.NameBangla.Trim(),
                    NameEnglish = model.NameEnglish.Trim().ToUpperInvariant(),
                    NickName = model.NickName.Trim(),
                    PassingYear = model.PassingYear,
                    BloodGroup = model.BloodGroup,
                    ContactNumber = model.ContactNumber.Trim(),
                    AlternativeNumber = model.AlternativeNumber?.Trim(),
                    Email = model.Email.Trim().ToLowerInvariant(),
                    LastInstitute = model.LastInstitute?.Trim(),
                    LastDegree = model.LastDegree?.Trim(),
                    LastDegreeSubject = model.LastDegreeSubject?.Trim(),
                    CompanyName = model.CompanyName?.Trim(),
                    CurrentWorkingAddress = model.CurrentWorkingAddress?.Trim(),
                    Designation = model.Designation?.Trim(),
                    PresentAddress = model.PresentAddress?.Trim(),
                    PermanentAddress = model.PermanentAddress?.Trim(),
                    OldPhotoPath = oldPhotoPath,
                    RecentPhotoPath = recentPhotoPath,
                    TestimonialPath = testimonialPath,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AlumniProfiles.Add(profile);
                await _context.SaveChangesAsync();

                // 4. Calculate total fee based on selected package & dynamic guests
                var basePackageFee = selectedPackage?.Fee ?? reunionEvent!.BaseAlumniFee;

                decimal dynamicGuestTotal = 0;
                int spouseCount = 0;
                int childCount = 0;
                int otherGuestCount = 0;

                var activeCategories = await _context.GuestCategories
                    .Where(c => c.ReunionEventId == reunionEvent!.Id && c.IsActive)
                    .ToDictionaryAsync(c => c.Id);

                var validGuestsToSave = new List<RegistrationGuest>();

                if (model.Guests != null && model.Guests.Any())
                {
                    foreach (var g in model.Guests)
                    {
                        if (activeCategories.TryGetValue(g.GuestCategoryId, out var cat))
                        {
                            var fee = cat.Fee;
                            dynamicGuestTotal += fee;

                            if (cat.CategoryName.Contains("Spouse", StringComparison.OrdinalIgnoreCase))
                                spouseCount++;
                            else if (cat.CategoryName.Contains("Child", StringComparison.OrdinalIgnoreCase))
                                childCount++;
                            else
                                otherGuestCount++;

                            validGuestsToSave.Add(new RegistrationGuest
                            {
                                GuestCategoryId = cat.Id,
                                GuestName = g.GuestName?.Trim(),
                                Gender = g.Gender?.Trim(),
                                Age = g.Age,
                                FeeCharged = fee
                            });
                        }
                    }
                }
                else
                {
                    spouseCount = model.SpouseCount;
                    childCount = model.ChildCount;
                    otherGuestCount = model.GuestCount;
                    dynamicGuestTotal = (model.SpouseCount * reunionEvent!.SpouseFee)
                        + (model.ChildCount * reunionEvent.ChildFee)
                        + (model.GuestCount * reunionEvent.GuestFee);
                }

                var totalFee = basePackageFee + dynamicGuestTotal;

                // 5. Generate Ticket Registration Number
                var regCount = await _context.EventRegistrations.CountAsync(r => r.ReunionEventId == reunionEvent!.Id) + 1;
                var regNo = $"RE-{reunionEvent!.EventDate.Year}-{regCount:D5}";

                // 6. Create EventRegistration
                var registration = new EventRegistration
                {
                    RegistrationNo = regNo,
                    ReunionEventId = reunionEvent.Id,
                    AlumniProfileId = profile.Id,
                    RegistrationPackageId = selectedPackage?.Id,
                    TShirtSize = !string.IsNullOrWhiteSpace(model.TShirtSize) ? model.TShirtSize : "L",
                    SpouseCount = spouseCount,
                    ChildCount = childCount,
                    GuestCount = otherGuestCount,
                    TotalAmount = totalFee,
                    PaidAmount = totalFee, // claimed amount submitted
                    Status = RegistrationStatus.Pending,
                    RegisteredAt = DateTime.UtcNow
                };

                // Generate signed QR code
                var signedToken = _qrCodeService.GenerateSignedToken(regNo, 0, userCode);
                var qrBase64 = _qrCodeService.GenerateQrCodeBase64(signedToken);

                registration.QrCodeToken = signedToken;
                registration.QrCodeBase64 = qrBase64;

                _context.EventRegistrations.Add(registration);
                await _context.SaveChangesAsync();

                // 7. Save Dynamic Guests
                foreach (var guest in validGuestsToSave)
                {
                    guest.EventRegistrationId = registration.Id;
                    _context.RegistrationGuests.Add(guest);
                }

                // 8. Save Custom Registration Question Responses
                if (model.QuestionResponses != null && model.QuestionResponses.Any())
                {
                    foreach (var qr in model.QuestionResponses)
                    {
                        if (qr.QuestionId > 0 && !string.IsNullOrWhiteSpace(qr.Answer))
                        {
                            _context.RegistrationQuestionResponses.Add(new RegistrationQuestionResponse
                            {
                                EventRegistrationId = registration.Id,
                                EventCustomQuestionId = qr.QuestionId,
                                AnswerValue = qr.Answer.Trim(),
                                SubAnswerValue = qr.SubAnswer?.Trim()
                            });
                        }
                    }
                }

                // 9. Save Dynamic Gift Size Choices
                if (model.GiftSizeChoices != null && model.GiftSizeChoices.Any())
                {
                    foreach (var gc in model.GiftSizeChoices)
                    {
                        if (gc.GiftItemId > 0 && !string.IsNullOrWhiteSpace(gc.SelectedSize))
                        {
                            _context.RegistrationGiftChoices.Add(new RegistrationGiftChoice
                            {
                                EventRegistrationId = registration.Id,
                                GiftItemId = gc.GiftItemId,
                                SelectedSize = gc.SelectedSize.Trim()
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 7. Record Payment
                var payment = new RegistrationPayment
                {
                    EventRegistrationId = registration.Id,
                    PaymentMode = model.PaymentMode,
                    TransactionId = !string.IsNullOrWhiteSpace(model.TransactionId) ? model.TransactionId.Trim() : (model.PaymentMode == PaymentMode.JanataPay ? $"JP-{registration.RegistrationNo}" : string.Empty),
                    SenderNumber = !string.IsNullOrWhiteSpace(model.SenderNumber) ? model.SenderNumber.Trim() : profile.ContactNumber,
                    Amount = totalFee,
                    SlipAttachmentPath = slipPath,
                    Status = PaymentStatus.Pending,
                    SubmittedAt = DateTime.UtcNow
                };

                _context.RegistrationPayments.Add(payment);

                // 8. Update inventory allocation for T-Shirt
                var tShirtItem = await _context.GiftItems
                    .Include(g => g.SizeStocks)
                    .FirstOrDefaultAsync(g => g.IsSizeSpecific && g.ItemName.Contains("T-Shirt"));

                if (tShirtItem != null)
                {
                    tShirtItem.AllocatedQuantity += 1;
                    var sizeStock = tShirtItem.SizeStocks.FirstOrDefault(s => s.SizeName == model.TShirtSize);
                    if (sizeStock != null)
                    {
                        sizeStock.AllocatedStock += 1;
                    }
                }

                await _context.SaveChangesAsync();

                // If JanataPay chosen, immediately redirect to JanataPayCheckout
                if (model.PaymentMode == PaymentMode.JanataPay)
                {
                    return RedirectToAction("JanataPayCheckout", "Payment", new { registrationNo = registration.RegistrationNo });
                }

                // 9. Dispatch Confirmation SMS to Attendee (Manual Payment flow)
                var placeholders = new Dictionary<string, string>
                {
                    ["Name"] = profile.NameEnglish,
                    ["UserId"] = profile.UserCode,
                    ["TicketNo"] = registration.RegistrationNo,
                    ["Amount"] = totalFee.ToString("N0"),
                    ["EventName"] = reunionEvent.EventTitle
                };

                await _smsService.SendTemplateSmsAsync("PendingPaymentSMS", profile.ContactNumber, placeholders);

                TempData["Success"] = "Your registration has been submitted successfully!";
                return RedirectToAction("Confirmation", new { registrationNo = registration.RegistrationNo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for alumnus {Name}", model.NameEnglish);
                ModelState.AddModelError("", $"Registration failed: {ex.Message}");
                model.ReunionEvent = reunionEvent;
                model.PaymentSettings = await _context.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("Manual"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Confirmation(string registrationNo)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.RegistrationNo == registrationNo);

            if (reg == null)
            {
                return NotFound("Registration not found.");
            }

            return View(reg);
        }

        // GET: /Enrollment/PayPending
        [HttpGet]
        public IActionResult PayPending()
        {
            return View();
        }

        // POST: /Enrollment/PayPending
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayPending(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                TempData["ErrorMessage"] = "Please enter your 11-digit mobile number.";
                return View();
            }

            var cleanMobile = mobileNumber.Trim();
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .Include(r => r.Payments)
                .Where(r => r.AlumniProfile != null &&
                            (r.AlumniProfile.ContactNumber == cleanMobile ||
                             r.AlumniProfile.AlternativeNumber == cleanMobile))
                .OrderByDescending(r => r.RegisteredAt)
                .FirstOrDefaultAsync();

            if (reg == null)
            {
                TempData["ErrorMessage"] = $"No registration found with mobile number '{cleanMobile}'. Please register first.";
                return View();
            }

            if (reg.Status == RegistrationStatus.Approved)
            {
                HttpContext.Session.SetString($"Pass_Authorized_{reg.RegistrationNo}", "1");
                var successPay = reg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault(p => p.Status == PaymentStatus.Approved)
                                 ?? reg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault();
                var trxCode = !string.IsNullOrEmpty(successPay?.GatewayFtNumber) ? successPay.GatewayFtNumber : successPay?.TransactionId;

                TempData["SuccessMessage"] = $"Your registration ({reg.RegistrationNo}) is already APPROVED and paid! You can print or download your ID card directly.";
                return RedirectToAction("ViewPass", "Pass", new { id = reg.RegistrationNo, trx = trxCode });
            }

            // Registration is Pending: redirect directly to JanataPay checkout
            TempData["PaymentNotice"] = $"Welcome back, {reg.AlumniProfile?.NameEnglish}! Your registration ticket {reg.RegistrationNo} was found with status: Pending. Please proceed to payment below.";
            return RedirectToAction("JanataPayCheckout", "Payment", new { registrationNo = reg.RegistrationNo });
        }
    }
}
