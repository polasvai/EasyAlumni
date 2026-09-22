namespace EasyAlumni.Core.Models
{
    public enum JanataPayAccountType
    {
        Registration = 0,
        Donation = 1
    }

    public class JanataPayOptions
    {
        public const string SectionName = "JanataPay";

        public string BaseUrl { get; set; } = "https://sandbox-pg.janatapay.com";
        public string MerchantUid { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public string? Socks5Proxy { get; set; } = "socks5://127.0.0.1:1080";
        public bool UseProxy { get; set; } = true;
        public string CallbackBaseUrl { get; set; } = "https://alumni.snhghs.edu.bd";
    }

    public class JanataPayTokenizeResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? TransactionToken { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? ReferenceId { get; set; }
        public int StatusCode { get; set; }
    }

    public class JanataPayVerifyResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; }
        public string? TransactionStatusCode { get; set; }
        public string? TransactionStatus { get; set; }
        public string? ReferenceId { get; set; }
        public string? FtNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionDate { get; set; }
        public string? RawResponseJson { get; set; }
    }
}
