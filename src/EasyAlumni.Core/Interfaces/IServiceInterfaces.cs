using EasyAlumni.Core.Enums;

namespace EasyAlumni.Core.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string originalFileName, string subFolder, string[] allowedExtensions, long maxSizeBytes = 2097152); // Default 2MB
        void DeleteFile(string? relativePath);
    }

    public interface IQrCodeService
    {
        string GenerateQrCodeBase64(string payload);
        string GenerateSignedToken(string registrationNo, int registrationId, string userCode);
        bool ValidateToken(string token, out string registrationNo, out int registrationId);
    }

    public interface ISmsService
    {
        Task<(bool Success, string Response)> SendSmsAsync(string mobileNumber, string message);
        Task<string> CheckBalanceAsync();
        Task SendTemplateSmsAsync(string templateKey, string mobileNumber, Dictionary<string, string> placeholders);
    }

    public interface IWhatsAppService
    {
        Task<(bool Success, string Response)> SendWhatsAppAsync(string mobileNumber, string message);
    }

    public interface IPaymentService
    {
        Task<(bool Success, string Message)> ProcessManualPaymentAsync(
            int registrationId,
            PaymentMode mode,
            string trxId,
            string senderPhone,
            decimal amount,
            string? slipPath);
    }

    public interface IJanataPayService
    {
        Task<EasyAlumni.Core.Models.JanataPayTokenizeResult> InitiatePaymentAsync(
            int registrationId,
            string registrationNo,
            decimal amount,
            string customerName,
            string customerPhone,
            string? customerEmail,
            EasyAlumni.Core.Models.JanataPayAccountType accountType = EasyAlumni.Core.Models.JanataPayAccountType.Registration);

        Task<EasyAlumni.Core.Models.JanataPayVerifyResult> VerifyPaymentAsync(
            string referenceId,
            string transactionToken,
            EasyAlumni.Core.Models.JanataPayAccountType accountType = EasyAlumni.Core.Models.JanataPayAccountType.Registration);

        Task<(bool Success, string Message, string? AccessToken)> TestConnectionAsync(
            EasyAlumni.Core.Models.JanataPayAccountType accountType = EasyAlumni.Core.Models.JanataPayAccountType.Registration);
    }
}

