using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Enums;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyAlumni.Infrastructure.Services
{
    public class ManualPaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<ManualPaymentService> _logger;

        public ManualPaymentService(
            ApplicationDbContext context,
            ISmsService smsService,
            ILogger<ManualPaymentService> logger)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
        }

        public async Task<(bool Success, string Message)> ProcessManualPaymentAsync(
            int registrationId,
            PaymentMode mode,
            string trxId,
            string senderPhone,
            decimal amount,
            string? slipPath)
        {
            var reg = await _context.EventRegistrations
                .Include(r => r.AlumniProfile)
                .Include(r => r.ReunionEvent)
                .FirstOrDefaultAsync(r => r.Id == registrationId);

            if (reg == null)
            {
                return (false, "Registration record not found.");
            }

            var payment = new RegistrationPayment
            {
                EventRegistrationId = registrationId,
                PaymentMode = mode,
                TransactionId = trxId.Trim(),
                SenderNumber = senderPhone.Trim(),
                Amount = amount,
                SlipAttachmentPath = slipPath,
                Status = PaymentStatus.Pending,
                SubmittedAt = DateTime.UtcNow
            };

            _context.RegistrationPayments.Add(payment);
            await _context.SaveChangesAsync();

            // Send notification to attendee
            if (reg.AlumniProfile != null && !string.IsNullOrWhiteSpace(reg.AlumniProfile.ContactNumber))
            {
                var placeholders = new Dictionary<string, string>
                {
                    ["Name"] = reg.AlumniProfile.NameEnglish,
                    ["TicketNo"] = reg.RegistrationNo,
                    ["Amount"] = amount.ToString("N0"),
                    ["EventName"] = reg.ReunionEvent?.EventTitle ?? "Reunion"
                };

                await _smsService.SendTemplateSmsAsync("PendingPaymentSMS", reg.AlumniProfile.ContactNumber, placeholders);
            }

            return (true, "Payment submitted successfully. Awaiting administrative approval.");
        }
    }
}
