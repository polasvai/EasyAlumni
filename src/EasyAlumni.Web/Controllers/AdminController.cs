using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin,Accounts")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IQrCodeService _qrCodeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IJanataPayService _janataPayService;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            ISmsService smsService,
            IQrCodeService qrCodeService,
            UserManager<ApplicationUser> userManager,
            IJanataPayService janataPayService,
            IFileStorageService fileStorage,
            ILogger<AdminController> logger)
        {
            _context = context;
            _smsService = smsService;
            _qrCodeService = qrCodeService;
            _userManager = userManager;
            _janataPayService = janataPayService;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var totalReg = await _context.EventRegistrations.CountAsync();
            var approvedReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Approved);
            var pendingReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Pending);
            var rejectedReg = await _context.EventRegistrations.CountAsync(r => r.Status == RegistrationStatus.Rejected);

            var totalCollected = await _context.RegistrationPayments
                .Where(p => p.Status == PaymentStatus.Approved)
                .SumAsync(p => p.Amount);

            var totalPendingAmount = await _context.RegistrationPayments
                .Where(p => p.Status == PaymentStatus.Pending)
                .SumAsync(p => p.Amount);

            var spouses = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.SpouseCount);
            var children = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.ChildCount);
            var guests = await _context.EventRegistrations.Where(r => r.Status == RegistrationStatus.Approved).SumAsync(r => r.GuestCount);
            var totalHeads = approvedReg + spouses + children + guests;

            // Batch distribution
            var batchData = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Where(r => r.AlumniProfile != null)
                .GroupBy(r => r.AlumniProfile!.PassingYear)
                .Select(g => new { Batch = g.Key, Count = g.Count() })
                .OrderBy(x => x.Batch)
                .ToListAsync();

            // T-Shirt size demand
            var tShirtData = await _context.EventRegistrations
                .GroupBy(r => r.TShirtSize)
                .Select(g => new { Size = g.Key, Count = g.Count() })
                .ToListAsync();

            // Recent 10 registrations
            var recentRegistrations = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .OrderByDescending(r => r.RegisteredAt)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalReg = totalReg;
            ViewBag.ApprovedReg = approvedReg;
            ViewBag.PendingReg = pendingReg;
            ViewBag.RejectedReg = rejectedReg;
            ViewBag.TotalCollected = totalCollected;
            ViewBag.TotalPendingAmount = totalPendingAmount;
            ViewBag.TotalHeads = totalHeads;
            ViewBag.Spouses = spouses;
            ViewBag.Children = children;
            ViewBag.Guests = guests;

            ViewBag.BatchLabels = batchData.Select(b => $"Batch {b.Batch}").ToList();
            ViewBag.BatchCounts = batchData.Select(b => b.Count).ToList();

            ViewBag.TShirtLabels = tShirtData.Select(t => t.Size).ToList();
            ViewBag.TShirtCounts = tShirtData.Select(t => t.Count).ToList();

            return View(recentRegistrations);
        }

        public async Task<IActionResult> Registrations(int? batch, RegistrationStatus? status)
        {
            var query = _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Include(r => r.ReunionEvent)
                .AsQueryable();

            if (batch.HasValue)
            {
                query = query.Where(r => r.AlumniProfile!.PassingYear == batch.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            var list = await query.OrderByDescending(r => r.RegisteredAt).ToListAsync();

            var distinctBatches = await _context.AlumniProfiles
                .Select(a => a.PassingYear)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();

            ViewBag.Batches = distinctBatches;
            ViewBag.SelectedBatch = batch;
            ViewBag.SelectedStatus = status;

            return View(list);
        }

        public async Task<IActionResult> PaymentQueue(PaymentStatus? status = PaymentStatus.Pending)
        {
            var query = _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .Include(p => p.ApprovedByUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            var payments = await query.OrderByDescending(p => p.SubmittedAt).ToListAsync();
            ViewBag.SelectedStatus = status;

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var user = await _userManager.GetUserAsync(User);

            payment.Status = PaymentStatus.Approved;
            payment.ApprovedByUserId = user?.Id;
            payment.ApprovedAt = DateTime.UtcNow;

            if (payment.EventRegistration != null)
            {
                payment.EventRegistration.Status = RegistrationStatus.Approved;

                // Ensure signed QR code exists
                if (string.IsNullOrEmpty(payment.EventRegistration.QrCodeBase64))
                {
                    var token = _qrCodeService.GenerateSignedToken(
                        payment.EventRegistration.RegistrationNo,
                        payment.EventRegistration.Id,
                        payment.EventRegistration.AlumniProfile?.UserCode ?? "ALUMNI");

                    payment.EventRegistration.QrCodeToken = token;
                    payment.EventRegistration.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(token);
                }

                // 1. Auto-Reconcile into Accounting Cash Book
                var incomeHead = await _context.AccountHeads
                    .FirstOrDefaultAsync(h => h.HeadName.Contains("Registration") && h.Type == AccountHeadType.Income)
                    ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

                if (incomeHead != null)
                {
                    var countVouchers = await _context.CashEntries.CountAsync() + 1;
                    _context.CashEntries.Add(new CashEntry
                    {
                        VoucherNumber = $"V-INC-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                        EntryDate = DateTime.UtcNow,
                        AccountHeadId = incomeHead.Id,
                        Amount = payment.Amount,
                        PaymentMode = payment.PaymentMode.ToString(),
                        Description = $"Auto-Reconciled Reunion Fee for {payment.EventRegistration.RegistrationNo} ({payment.EventRegistration.AlumniProfile?.NameEnglish}) TrxID: {payment.TransactionId}",
                        RelatedRegistrationId = payment.EventRegistration.Id,
                        CreatedByUserId = user?.Id
                    });
                }

                // 2. Dispatch Approval SMS
                if (payment.EventRegistration.AlumniProfile != null)
                {
                    var phone = payment.EventRegistration.AlumniProfile.ContactNumber;
                    var trxCode = !string.IsNullOrEmpty(payment.GatewayFtNumber) ? payment.GatewayFtNumber : (payment.TransactionId ?? "");
                    var passUrl = $"{Request.Scheme}://{Request.Host}/Pass/ViewPass/{payment.EventRegistration.RegistrationNo}?trx={trxCode}";

                    var placeholders = new Dictionary<string, string>
                    {
                        ["Name"] = payment.EventRegistration.AlumniProfile.NameEnglish,
                        ["TicketNo"] = payment.EventRegistration.RegistrationNo,
                        ["Amount"] = payment.Amount.ToString("N0"),
                        ["EventName"] = payment.EventRegistration.ReunionEvent?.EventTitle ?? "Reunion",
                        ["PassUrl"] = passUrl
                    };

                    await _smsService.SendTemplateSmsAsync("PaymentSMS", phone, placeholders);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Payment for {payment.EventRegistration?.RegistrationNo} approved successfully!";
            return RedirectToAction(nameof(PaymentQueue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPayment(int paymentId, string? remarks)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var user = await _userManager.GetUserAsync(User);

            payment.Status = PaymentStatus.Rejected;
            payment.AdminRemarks = remarks ?? "Transaction ID could not be verified.";
            payment.ApprovedByUserId = user?.Id;
            payment.ApprovedAt = DateTime.UtcNow;

            if (payment.EventRegistration != null)
            {
                payment.EventRegistration.Status = RegistrationStatus.Rejected;
            }

            await _context.SaveChangesAsync();
            TempData["Warning"] = $"Payment for {payment.EventRegistration?.RegistrationNo} was rejected.";
            return RedirectToAction(nameof(PaymentQueue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReverifyJanataPayment(int paymentId)
        {
            var payment = await _context.RegistrationPayments
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.AlumniProfile)
                .Include(p => p.EventRegistration)
                    .ThenInclude(r => r!.ReunionEvent)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            if (string.IsNullOrEmpty(payment.GatewayReferenceId))
            {
                TempData["Error"] = "This payment has no gateway reference ID recorded.";
                return RedirectToAction(nameof(PaymentQueue));
            }

            var verifyResult = await _janataPayService.VerifyPaymentAsync(payment.GatewayReferenceId, payment.GatewayTransactionToken ?? "");

            payment.GatewayStatus = verifyResult.TransactionStatus ?? verifyResult.TransactionStatusCode;
            payment.GatewayFtNumber = verifyResult.FtNumber;

            if (verifyResult.Success && verifyResult.Amount >= payment.Amount)
            {
                var user = await _userManager.GetUserAsync(User);
                payment.Status = PaymentStatus.Approved;
                payment.ApprovedByUserId = user?.Id;
                payment.ApprovedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(verifyResult.FtNumber))
                {
                    payment.TransactionId = verifyResult.FtNumber;
                }

                if (payment.EventRegistration != null)
                {
                    var reg = payment.EventRegistration;
                    reg.Status = RegistrationStatus.Approved;
                    reg.PaidAmount = verifyResult.Amount;

                    if (string.IsNullOrEmpty(reg.QrCodeBase64))
                    {
                        var signedToken = _qrCodeService.GenerateSignedToken(
                            reg.RegistrationNo,
                            reg.Id,
                            reg.AlumniProfile?.UserCode ?? "ALUMNI");

                        reg.QrCodeToken = signedToken;
                        reg.QrCodeBase64 = _qrCodeService.GenerateQrCodeBase64(signedToken);
                    }

                    var incomeHead = await _context.AccountHeads
                        .FirstOrDefaultAsync(h => h.HeadName.Contains("Registration") && h.Type == AccountHeadType.Income)
                        ?? await _context.AccountHeads.FirstOrDefaultAsync(h => h.Type == AccountHeadType.Income);

                    if (incomeHead != null)
                    {
                        var countVouchers = await _context.CashEntries.CountAsync() + 1;
                        _context.CashEntries.Add(new CashEntry
                        {
                            VoucherNumber = $"V-JP-{DateTime.UtcNow.Year}-{countVouchers:D5}",
                            EntryDate = DateTime.UtcNow,
                            AccountHeadId = incomeHead.Id,
                            Amount = payment.Amount,
                            PaymentMode = "JanataPay",
                            Description = $"Reconciled JanataPay Online for {reg.RegistrationNo} ({reg.AlumniProfile?.NameEnglish}) FT: {verifyResult.FtNumber}",
                            RelatedRegistrationId = reg.Id,
                            CreatedByUserId = user?.Id
                        });
                    }

                    if (reg.AlumniProfile != null && !string.IsNullOrWhiteSpace(reg.AlumniProfile.ContactNumber))
                    {
                        var trxCode = !string.IsNullOrEmpty(verifyResult.FtNumber) ? verifyResult.FtNumber : (payment.TransactionId ?? "");
                        var passUrl = $"{Request.Scheme}://{Request.Host}/Pass/ViewPass/{reg.RegistrationNo}?trx={trxCode}";
                        var placeholders = new Dictionary<string, string>
                        {
                            ["Name"] = reg.AlumniProfile.NameEnglish,
                            ["TicketNo"] = reg.RegistrationNo,
                            ["Amount"] = payment.Amount.ToString("N0"),
                            ["EventName"] = reg.ReunionEvent?.EventTitle ?? "Reunion",
                            ["PassUrl"] = passUrl
                        };

                        await _smsService.SendTemplateSmsAsync("PaymentSMS", reg.AlumniProfile.ContactNumber, placeholders);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Payment for {payment.EventRegistration?.RegistrationNo} was successfully verified and approved with JanataPay! (FT: {verifyResult.FtNumber})";
            }
            else
            {
                await _context.SaveChangesAsync();
                TempData["Warning"] = $"Gateway returned status '{verifyResult.TransactionStatus ?? "Incomplete"}' (Code: {verifyResult.TransactionStatusCode}). Not approved.";
            }

            return RedirectToAction(nameof(PaymentQueue));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAllRegistrations()
        {
            try
            {
                // Delete in cascade / foreign-key order
                _context.RegistrationQuestionResponses.RemoveRange(_context.RegistrationQuestionResponses);
                _context.RegistrationGiftChoices.RemoveRange(_context.RegistrationGiftChoices);
                _context.RegistrationGuests.RemoveRange(_context.RegistrationGuests);
                _context.GiftDistributions.RemoveRange(_context.GiftDistributions);
                _context.CashEntries.RemoveRange(_context.CashEntries.Where(c => c.RelatedRegistrationId != null));
                _context.RegistrationPayments.RemoveRange(_context.RegistrationPayments);
                _context.EventRegistrations.RemoveRange(_context.EventRegistrations);
                _context.AlumniProfiles.RemoveRange(_context.AlumniProfiles);

                var giftItems = await _context.GiftItems.ToListAsync();
                foreach (var g in giftItems)
                {
                    g.AllocatedQuantity = 0;
                    g.DistributedQuantity = 0;
                }

                var sizeStocks = await _context.GiftItemSizeStocks.ToListAsync();
                foreach (var s in sizeStocks)
                {
                    s.AllocatedStock = 0;
                    s.DistributedStock = 0;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "All registration and attendee data have been successfully deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear registration data.");
                TempData["Error"] = $"Failed to clear registrations: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRegistration(int id)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Include(r => r.Guests)
                .Include(r => r.GiftChoices)
                .Include(r => r.QuestionResponses)
                .Include(r => r.GiftDistributions)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
            {
                TempData["Error"] = "Registration not found.";
                return RedirectToAction(nameof(Registrations));
            }

            try
            {
                var regNo = reg.RegistrationNo;
                var attendeeName = reg.AlumniProfile?.NameEnglish ?? "Alumnus";

                // Revert stock allocations if any
                var sizeStock = await _context.GiftItemSizeStocks
                    .FirstOrDefaultAsync(s => s.SizeName == reg.TShirtSize.ToString());
                if (sizeStock != null && sizeStock.AllocatedStock > 0)
                {
                    sizeStock.AllocatedStock = Math.Max(0, sizeStock.AllocatedStock - 1);
                }

                // Delete related records in cascade order
                _context.RegistrationQuestionResponses.RemoveRange(reg.QuestionResponses);
                _context.RegistrationGiftChoices.RemoveRange(reg.GiftChoices);
                _context.RegistrationGuests.RemoveRange(reg.Guests);
                _context.GiftDistributions.RemoveRange(reg.GiftDistributions);

                var cashEntries = await _context.CashEntries.Where(c => c.RelatedRegistrationId == reg.Id).ToListAsync();
                _context.CashEntries.RemoveRange(cashEntries);

                _context.RegistrationPayments.RemoveRange(reg.Payments);

                var profile = reg.AlumniProfile;
                _context.EventRegistrations.Remove(reg);

                // If profile has no other registrations, remove the profile as well to free up phone/email
                if (profile != null)
                {
                    var otherRegs = await _context.EventRegistrations
                        .AnyAsync(r => r.AlumniProfileId == profile.Id && r.Id != reg.Id);
                    if (!otherRegs)
                    {
                        _context.AlumniProfiles.Remove(profile);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Registration {regNo} for {attendeeName} was successfully deleted. The alumnus may now register again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete registration {Id}", id);
                TempData["Error"] = $"Failed to delete registration: {ex.Message}";
            }

            return RedirectToAction(nameof(Registrations));
        }

        // GET: /Admin/EditRegistration/5
        [HttpGet]
        public async Task<IActionResult> EditRegistration(int id)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .Include(r => r.ReunionEvent)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
            {
                TempData["Error"] = "Registration record not found.";
                return RedirectToAction(nameof(Registrations));
            }

            var profile = reg.AlumniProfile;
            var payment = reg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault();

            var availablePackages = await _context.RegistrationPackages
                .Where(p => p.IsActive && p.ReunionEventId == reg.ReunionEventId)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            var vm = new AdminEditRegistrationViewModel
            {
                // Read-only Identifiers
                RegistrationId = reg.Id,
                RegistrationNo = reg.RegistrationNo,
                UserCode = profile?.UserCode ?? string.Empty,
                ProfileId = profile?.Id ?? 0,
                TransactionId = payment?.TransactionId,
                GatewayFtNumber = payment?.GatewayFtNumber,
                GatewayStatus = payment?.GatewayStatus,
                RegisteredAt = reg.RegisteredAt,

                // Personal Details
                NameBangla = profile?.NameBangla ?? string.Empty,
                NameEnglish = profile?.NameEnglish ?? string.Empty,
                NickName = profile?.NickName,
                PassingYear = profile?.PassingYear ?? 2010,
                BloodGroup = profile?.BloodGroup,
                CurrentRecentPhotoPath = profile?.RecentPhotoPath,

                // Contact & Addresses
                ContactNumber = profile?.ContactNumber ?? string.Empty,
                AlternativeNumber = profile?.AlternativeNumber,
                Email = profile?.Email ?? string.Empty,
                PresentAddress = profile?.PresentAddress,
                PermanentAddress = profile?.PermanentAddress,

                // Education & Career
                LastInstitute = profile?.LastInstitute,
                LastDegree = profile?.LastDegree,
                LastDegreeSubject = profile?.LastDegreeSubject,
                CompanyName = profile?.CompanyName,
                Designation = profile?.Designation,
                CurrentWorkingAddress = profile?.CurrentWorkingAddress,

                // Package, Merchandise & Attendance
                RegistrationPackageId = reg.RegistrationPackageId,
                AvailablePackages = availablePackages,
                TShirtSize = reg.TShirtSize ?? "L",
                SpouseCount = reg.SpouseCount,
                ChildCount = reg.ChildCount,
                GuestCount = reg.GuestCount,
                IsKitDistributed = reg.IsKitDistributed,
                KitDistributedAt = reg.KitDistributedAt,
                RegistrationStatus = reg.Status,

                // Payment & Financial
                TotalAmount = reg.TotalAmount,
                PaidAmount = reg.PaidAmount,
                PaymentMode = payment?.PaymentMode ?? PaymentMode.JanataPay,
                PaymentStatus = payment?.Status ?? (reg.Status == RegistrationStatus.Approved ? PaymentStatus.Approved : PaymentStatus.Pending),
                SenderNumber = payment?.SenderNumber ?? profile?.ContactNumber,
                AdminRemarks = payment?.AdminRemarks
            };

            return View(vm);
        }

        // POST: /Admin/EditRegistration/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRegistration(AdminEditRegistrationViewModel model)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == model.RegistrationId);

            if (reg == null)
            {
                TempData["Error"] = "Registration record not found.";
                return RedirectToAction(nameof(Registrations));
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePackages = await _context.RegistrationPackages
                    .Where(p => p.IsActive && p.ReunionEventId == reg.ReunionEventId)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
                return View(model);
            }

            // Check if updated contact number or email collides with another alumnus for the same event
            var cleanPhone = model.ContactNumber.Trim();
            var cleanEmail = model.Email.Trim().ToLowerInvariant();

            var duplicateExists = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .AnyAsync(r => r.Id != reg.Id &&
                               r.ReunionEventId == reg.ReunionEventId &&
                               r.AlumniProfile != null &&
                               (r.AlumniProfile.ContactNumber == cleanPhone ||
                                (!string.IsNullOrEmpty(cleanEmail) && r.AlumniProfile.Email == cleanEmail)));

            if (duplicateExists)
            {
                ModelState.AddModelError("ContactNumber", "Another registered member already uses this mobile number or email address.");
                model.AvailablePackages = await _context.RegistrationPackages
                    .Where(p => p.IsActive && p.ReunionEventId == reg.ReunionEventId)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
                return View(model);
            }

            // Handle optional photo replacement
            if (model.NewPhotoFile != null && model.NewPhotoFile.Length > 0)
            {
                try
                {
                    var allowedImageExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    using var stream = model.NewPhotoFile.OpenReadStream();
                    var photoPath = await _fileStorage.SaveFileAsync(stream, model.NewPhotoFile.FileName, "RecentPhotos", allowedImageExt, 5242880);
                    if (reg.AlumniProfile != null)
                    {
                        reg.AlumniProfile.RecentPhotoPath = photoPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upload replacement photo for attendee {RegistrationNo}", reg.RegistrationNo);
                }
            }

            // 1. Update Profile Fields (Ticket No & UserCode remain strictly untouched)
            if (reg.AlumniProfile != null)
            {
                reg.AlumniProfile.NameBangla = model.NameBangla.Trim();
                reg.AlumniProfile.NameEnglish = model.NameEnglish.Trim().ToUpperInvariant();
                reg.AlumniProfile.NickName = model.NickName?.Trim() ?? string.Empty;
                reg.AlumniProfile.PassingYear = model.PassingYear;
                reg.AlumniProfile.BloodGroup = model.BloodGroup;
                reg.AlumniProfile.ContactNumber = cleanPhone;
                reg.AlumniProfile.AlternativeNumber = model.AlternativeNumber?.Trim();
                reg.AlumniProfile.Email = cleanEmail;
                reg.AlumniProfile.PresentAddress = model.PresentAddress?.Trim();
                reg.AlumniProfile.PermanentAddress = model.PermanentAddress?.Trim();
                reg.AlumniProfile.LastInstitute = model.LastInstitute?.Trim();
                reg.AlumniProfile.LastDegree = model.LastDegree?.Trim();
                reg.AlumniProfile.LastDegreeSubject = model.LastDegreeSubject?.Trim();
                reg.AlumniProfile.CompanyName = model.CompanyName?.Trim();
                reg.AlumniProfile.Designation = model.Designation?.Trim();
                reg.AlumniProfile.CurrentWorkingAddress = model.CurrentWorkingAddress?.Trim();
            }

            // 2. Update Registration Fields (RegistrationNo, Id, ProfileId, EventId remain strictly untouched)
            var oldTShirt = reg.TShirtSize;
            reg.RegistrationPackageId = model.RegistrationPackageId;
            reg.TShirtSize = model.TShirtSize;
            reg.SpouseCount = model.SpouseCount;
            reg.ChildCount = model.ChildCount;
            reg.GuestCount = model.GuestCount;
            reg.TotalAmount = model.TotalAmount;
            reg.PaidAmount = model.PaidAmount;
            reg.Status = model.RegistrationStatus;

            if (model.IsKitDistributed && !reg.IsKitDistributed)
            {
                reg.IsKitDistributed = true;
                reg.KitDistributedAt = DateTime.UtcNow;
            }
            else if (!model.IsKitDistributed && reg.IsKitDistributed)
            {
                reg.IsKitDistributed = false;
                reg.KitDistributedAt = null;
            }

            // Adjust stock allocations if T-Shirt size changed
            if (oldTShirt != model.TShirtSize)
            {
                var oldStock = await _context.GiftItemSizeStocks.FirstOrDefaultAsync(s => s.SizeName == oldTShirt);
                if (oldStock != null && oldStock.AllocatedStock > 0)
                {
                    oldStock.AllocatedStock--;
                }
                var newStock = await _context.GiftItemSizeStocks.FirstOrDefaultAsync(s => s.SizeName == model.TShirtSize);
                if (newStock != null)
                {
                    newStock.AllocatedStock++;
                }
            }

            // 3. Update Payment (TransactionId, GatewayFtNumber remain strictly untouched)
            var payment = reg.Payments.OrderByDescending(p => p.SubmittedAt).FirstOrDefault();
            if (payment != null)
            {
                payment.PaymentMode = model.PaymentMode;
                payment.Status = model.PaymentStatus;
                payment.Amount = model.PaidAmount;
                payment.SenderNumber = model.SenderNumber?.Trim() ?? cleanPhone;
                payment.AdminRemarks = model.AdminRemarks?.Trim();

                if (model.PaymentStatus == PaymentStatus.Approved && payment.ApprovedAt == null)
                {
                    payment.ApprovedAt = DateTime.UtcNow;
                    var currentUser = await _userManager.GetUserAsync(User);
                    payment.ApprovedByUserId = currentUser?.Id;
                }
            }
            else
            {
                // If payment record was missing, insert a payment record
                var currentUser = await _userManager.GetUserAsync(User);
                _context.RegistrationPayments.Add(new RegistrationPayment
                {
                    EventRegistrationId = reg.Id,
                    PaymentMode = model.PaymentMode,
                    Status = model.PaymentStatus,
                    Amount = model.PaidAmount,
                    SenderNumber = model.SenderNumber?.Trim() ?? cleanPhone,
                    AdminRemarks = model.AdminRemarks?.Trim(),
                    TransactionId = string.Empty,
                    SubmittedAt = DateTime.UtcNow,
                    ApprovedAt = model.PaymentStatus == PaymentStatus.Approved ? DateTime.UtcNow : null,
                    ApprovedByUserId = model.PaymentStatus == PaymentStatus.Approved ? currentUser?.Id : null
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Attendee information for {reg.AlumniProfile?.NameEnglish} ({reg.RegistrationNo}) was updated successfully.";
            return RedirectToAction(nameof(Registrations));
        }
    }
}
