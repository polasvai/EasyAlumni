using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Core.Models;
using EasyAlumni.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyAlumni.Infrastructure.Services
{
    public class JanataPayService : IJanataPayService
    {
        private readonly ApplicationDbContext _context;
        private readonly JanataPayOptions _defaultOptions;
        private readonly ILogger<JanataPayService> _logger;

        // Cached token state
        private static string? _cachedAccessToken;
        private static DateTime _tokenExpiresAt = DateTime.MinValue;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);

        public JanataPayService(
            ApplicationDbContext context,
            IOptions<JanataPayOptions> defaultOptions,
            ILogger<JanataPayService> logger)
        {
            _context = context;
            _defaultOptions = defaultOptions.Value;
            _logger = logger;
        }

        private async Task<JanataPayOptions> GetEffectiveOptionsAsync()
        {
            try
            {
                var settings = await _context.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("JanataPay"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                var options = new JanataPayOptions
                {
                    BaseUrl = settings.TryGetValue("JanataPayBaseUrl", out var bUrl) && !string.IsNullOrWhiteSpace(bUrl) ? bUrl : _defaultOptions.BaseUrl,
                    MerchantUid = settings.TryGetValue("JanataPayMerchantUid", out var mUid) && !string.IsNullOrWhiteSpace(mUid) ? mUid : _defaultOptions.MerchantUid,
                    Username = settings.TryGetValue("JanataPayUsername", out var uName) && !string.IsNullOrWhiteSpace(uName) ? uName : _defaultOptions.Username,
                    Password = settings.TryGetValue("JanataPayPassword", out var pwd) && !string.IsNullOrWhiteSpace(pwd) ? pwd : _defaultOptions.Password,
                    PublicKey = settings.TryGetValue("JanataPayPublicKey", out var pKey) && !string.IsNullOrWhiteSpace(pKey) ? pKey : _defaultOptions.PublicKey,
                    Socks5Proxy = settings.TryGetValue("JanataPaySocks5Proxy", out var sProxy) ? sProxy : _defaultOptions.Socks5Proxy,
                    UseProxy = settings.TryGetValue("JanataPayUseProxy", out var uProxy) ? uProxy == "1" : _defaultOptions.UseProxy,
                    CallbackBaseUrl = settings.TryGetValue("JanataPayCallbackBaseUrl", out var cUrl) && !string.IsNullOrWhiteSpace(cUrl) ? cUrl : _defaultOptions.CallbackBaseUrl
                };

                return options;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load JanataPay options from database, using appsettings fallback.");
                return _defaultOptions;
            }
        }

        private HttpClient CreateHttpClient(JanataPayOptions options)
        {
            SocketsHttpHandler handler = new();
            if (options.UseProxy && !string.IsNullOrWhiteSpace(options.Socks5Proxy))
            {
                handler.Proxy = new WebProxy(options.Socks5Proxy);
                handler.UseProxy = true;
            }

            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://sandbox-pg.janatapay.com/" : options.BaseUrl.TrimEnd('/') + "/";
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<JanataPayTokenizeResult> InitiatePaymentAsync(
            int registrationId,
            string registrationNo,
            decimal amount,
            string customerName,
            string customerPhone,
            string? customerEmail)
        {
            var options = await GetEffectiveOptionsAsync();
            using var httpClient = CreateHttpClient(options);

            try
            {
                var token = await GetAccessTokenAsync(options, httpClient);
                if (string.IsNullOrEmpty(token))
                {
                    return new JanataPayTokenizeResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to obtain gateway authorization token."
                    };
                }

                // Reference ID format: EA-REGNO-TIMESTAMP (Must be <= 25 chars)
                var cleanReg = registrationNo.Replace("-", "");
                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000;
                var referenceId = $"EA{cleanReg}{ts}";
                if (referenceId.Length > 25)
                {
                    referenceId = referenceId[..25];
                }

                var callbackBase = (options.CallbackBaseUrl ?? "https://alumni.snhghs.edu.bd").TrimEnd('/');
                var successUrl = $"{callbackBase}/Payment/JanataPaySuccess?refid={referenceId}";
                var failUrl = $"{callbackBase}/Payment/JanataPayFail?refid={referenceId}";
                var cancelUrl = $"{callbackBase}/Payment/JanataPayCancel?refid={referenceId}";

                var tokenizePayload = new
                {
                    referenceId = referenceId,
                    amount = amount.ToString("F2"),
                    currency = "BDT",
                    customerName = string.IsNullOrWhiteSpace(customerName) ? "Alumni Attendee" : customerName.Trim(),
                    customerPhone = string.IsNullOrWhiteSpace(customerPhone) ? "01700000000" : customerPhone.Trim(),
                    customerEmail = string.IsNullOrWhiteSpace(customerEmail) ? "info@alumni.snhghs.edu.bd" : customerEmail.Trim(),
                    description = $"Alumni Reunion Registration Fee {registrationNo}",
                    successUrl = successUrl,
                    failUrl = failUrl,
                    cancelUrl = cancelUrl
                };

                var payloadJson = JsonSerializer.Serialize(tokenizePayload);
                var encryptedData = EncryptPayload(payloadJson);

                var requestObj = new
                {
                    data = encryptedData,
                    accessToken = token
                };

                var requestJson = JsonSerializer.Serialize(requestObj);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "jbagg/api/transaction/tokenize")
                {
                    Content = requestContent
                };
                requestMessage.Headers.Add("Authorization", $"Bearer {token}");

                var response = await httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("JanataPay Tokenize HTTP Error: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);
                    return new JanataPayTokenizeResult
                    {
                        Success = false,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = $"Payment gateway returned error HTTP {(int)response.StatusCode}: {responseBody}"
                    };
                }

                var root = JsonNode.Parse(responseBody);
                var code = root?["statusCode"]?.GetValue<int>() ?? 0;
                var encData = root?["data"]?.GetValue<string>();

                if (code == 200 && !string.IsNullOrEmpty(encData))
                {
                    var decryptedJson = DecryptPayload(encData);
                    var dataObj = JsonNode.Parse(decryptedJson);

                    var trxToken = dataObj?["transactionToken"]?.GetValue<string>();
                    var checkoutUrl = dataObj?["url"]?.GetValue<string>();

                    return new JanataPayTokenizeResult
                    {
                        Success = true,
                        StatusCode = code,
                        ReferenceId = referenceId,
                        TransactionToken = trxToken,
                        CheckoutUrl = checkoutUrl
                    };
                }

                return new JanataPayTokenizeResult
                {
                    Success = false,
                    StatusCode = code,
                    ErrorMessage = root?["message"]?.GetValue<string>() ?? "Failed to initialize payment session."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during JanataPay InitiatePaymentAsync for {RegNo}", registrationNo);
                return new JanataPayTokenizeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<JanataPayVerifyResult> VerifyPaymentAsync(string referenceId, string transactionToken)
        {
            var options = await GetEffectiveOptionsAsync();
            using var httpClient = CreateHttpClient(options);

            try
            {
                var token = await GetAccessTokenAsync(options, httpClient);
                if (string.IsNullOrEmpty(token))
                {
                    return new JanataPayVerifyResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to obtain gateway authorization token for verification."
                    };
                }

                var verifyPayload = new
                {
                    referenceId = referenceId,
                    transactionToken = transactionToken
                };

                var payloadJson = JsonSerializer.Serialize(verifyPayload);
                var encryptedData = EncryptPayload(payloadJson);

                var requestObj = new
                {
                    data = encryptedData,
                    accessToken = token
                };

                var requestJson = JsonSerializer.Serialize(requestObj);
                using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "jbagg/api/transaction/verify")
                {
                    Content = requestContent
                };
                requestMessage.Headers.Add("Authorization", $"Bearer {token}");

                var response = await httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("JanataPay Verify HTTP Error: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);
                    return new JanataPayVerifyResult
                    {
                        Success = false,
                        StatusCode = (int)response.StatusCode,
                        ErrorMessage = $"JanataPay verify failed: HTTP {(int)response.StatusCode}"
                    };
                }

                var root = JsonNode.Parse(responseBody);
                var code = root?["statusCode"]?.GetValue<int>() ?? 0;
                var encData = root?["data"]?.GetValue<string>();

                if (code == 200 && !string.IsNullOrEmpty(encData))
                {
                    var decryptedJson = DecryptPayload(encData);
                    var dataObj = JsonNode.Parse(decryptedJson);

                    var trxStatus = dataObj?["transactionStatus"]?.GetValue<string>();
                    var trxStatusCode = dataObj?["transactionStatusCode"]?.GetValue<string>();
                    var ftNumber = dataObj?["ftNumber"]?.GetValue<string>();
                    var refId = dataObj?["referenceId"]?.GetValue<string>() ?? referenceId;
                    var amountStr = dataObj?["amount"]?.ToString();
                    decimal.TryParse(amountStr, out var amt);
                    var currency = dataObj?["currency"]?.GetValue<string>();
                    var paymentMethod = dataObj?["paymentMethod"]?.GetValue<string>();
                    var dateStr = dataObj?["transactionDate"]?.GetValue<string>();

                    // 1003 is JanataPay Success status code
                    bool isApproved = (trxStatusCode == "1003");

                    return new JanataPayVerifyResult
                    {
                        Success = isApproved,
                        StatusCode = code,
                        TransactionStatusCode = trxStatusCode,
                        TransactionStatus = trxStatus,
                        ReferenceId = refId,
                        FtNumber = ftNumber,
                        Amount = amt,
                        Currency = currency,
                        PaymentMethod = paymentMethod,
                        TransactionDate = dateStr,
                        RawResponseJson = decryptedJson
                    };
                }

                return new JanataPayVerifyResult
                {
                    Success = false,
                    StatusCode = code,
                    ErrorMessage = root?["message"]?.GetValue<string>() ?? "Verification returned invalid payload."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during JanataPay VerifyPaymentAsync for {RefId}", referenceId);
                return new JanataPayVerifyResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<(bool Success, string Message, string? AccessToken)> TestConnectionAsync()
        {
            var options = await GetEffectiveOptionsAsync();
            using var httpClient = CreateHttpClient(options);

            try
            {
                // Force fresh authentication
                var (token, expiresIn) = await AuthenticateGatewayAsync(options, httpClient);
                if (!string.IsNullOrEmpty(token))
                {
                    _cachedAccessToken = token;
                    var validSeconds = Math.Max(60, expiresIn - 300);
                    _tokenExpiresAt = DateTime.UtcNow.AddSeconds(validSeconds);

                    return (true, $"Authentication successful! Received Bearer JWT Token (expires in {expiresIn}s).", token);
                }

                return (false, "Authentication failed with the configured credentials. Please check Merchant UID, Username, Password, and RSA Public Key.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during JanataPay TestConnectionAsync");
                return (false, $"Connection error: {ex.Message}", null);
            }
        }

        private async Task<string?> GetAccessTokenAsync(JanataPayOptions options, HttpClient httpClient)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAt)
            {
                return _cachedAccessToken;
            }

            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAt)
                {
                    return _cachedAccessToken;
                }

                var (token, expiresIn) = await AuthenticateGatewayAsync(options, httpClient);
                if (!string.IsNullOrEmpty(token))
                {
                    _cachedAccessToken = token;
                    var validSeconds = Math.Max(60, expiresIn - 300);
                    _tokenExpiresAt = DateTime.UtcNow.AddSeconds(validSeconds);
                    return token;
                }

                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<(string? Token, int ExpiresIn)> AuthenticateGatewayAsync(JanataPayOptions options, HttpClient httpClient)
        {
            var aesKey = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(aesKey);
            var aesKeyBase64 = Convert.ToBase64String(aesKey);

            var authPayload = new
            {
                merchantUid = options.MerchantUid,
                merchantUsername = options.Username,
                merchantPassword = options.Password,
                aesKey = aesKeyBase64
            };

            var payloadJson = JsonSerializer.Serialize(authPayload);
            var encryptedData = EncryptWithAesGcm(payloadJson, aesKey);
            _sessionAesKey = aesKey; // update active session key
            var rsaEncryptedKey = EncryptAesKeyWithRsa(aesKey, options.PublicKey);

            var merchantUidBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.MerchantUid));
            var rsaKeyBase64 = Convert.ToBase64String(rsaEncryptedKey);

            var requestObj = new
            {
                merchant = merchantUidBase64,
                key = rsaKeyBase64,
                data = encryptedData
            };

            var requestJson = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("jbagg/api/auth", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("JanataPay Auth HTTP Error: {StatusCode}, Body: {Body}", response.StatusCode, responseBody);
                return (null, 0);
            }

            var root = JsonNode.Parse(responseBody);
            var code = root?["statusCode"]?.GetValue<int>() ?? 0;
            var encData = root?["data"]?.GetValue<string>();

            if (code == 200 && !string.IsNullOrEmpty(encData))
            {
                var decryptedJson = DecryptPayloadWithKey(encData, aesKey);
                var dataObj = JsonNode.Parse(decryptedJson);

                var token = dataObj?["accessToken"]?.GetValue<string>();
                var expiresIn = dataObj?["expiresIn"]?.GetValue<int>() ?? 3600;

                return (token, expiresIn);
            }

            _logger.LogError("JanataPay Auth returned non-200 code: {Body}", responseBody);
            return (null, 0);
        }

        #region Cryptography Helpers

        private static byte[]? _sessionAesKey;

        private string EncryptPayload(string plaintext)
        {
            if (_sessionAesKey == null)
            {
                _sessionAesKey = new byte[32];
                RandomNumberGenerator.Fill(_sessionAesKey);
            }

            return EncryptWithAesGcm(plaintext, _sessionAesKey);
        }

        private string DecryptPayload(string ciphertextBase64)
        {
            if (_sessionAesKey == null)
            {
                throw new InvalidOperationException("No AES session key initialized for decryption.");
            }

            return DecryptWithAesGcm(ciphertextBase64, _sessionAesKey);
        }

        private (byte[] Key, string Ciphertext) EncryptPayloadWithNewKey(string plaintext)
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            _sessionAesKey = key;
            var cipher = EncryptWithAesGcm(plaintext, key);
            return (key, cipher);
        }

        private string DecryptPayloadWithKey(string ciphertextBase64, byte[] key)
        {
            return DecryptWithAesGcm(ciphertextBase64, key);
        }

        private static string EncryptWithAesGcm(string plaintext, byte[] key)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var iv = new byte[12];
            RandomNumberGenerator.Fill(iv);

            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[16];

            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Encrypt(iv, plainBytes, cipherBytes, tag);

            var combined = new byte[iv.Length + cipherBytes.Length + tag.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);
            Buffer.BlockCopy(tag, 0, combined, iv.Length + cipherBytes.Length, tag.Length);

            return Convert.ToBase64String(combined);
        }

        private static string DecryptWithAesGcm(string ciphertextBase64, byte[] key)
        {
            var combined = Convert.FromBase64String(ciphertextBase64);
            if (combined.Length < 28)
            {
                throw new ArgumentException("Invalid ciphertext length for AES-GCM.");
            }

            var iv = new byte[12];
            var tag = new byte[16];
            var cipherLength = combined.Length - 12 - 16;
            var cipherBytes = new byte[cipherLength];

            Buffer.BlockCopy(combined, 0, iv, 0, 12);
            Buffer.BlockCopy(combined, 12, cipherBytes, 0, cipherLength);
            Buffer.BlockCopy(combined, 12 + cipherLength, tag, 0, 16);

            var plainBytes = new byte[cipherLength];
            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Decrypt(iv, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        private byte[] EncryptAesKeyWithRsa(byte[] aesKey, string publicKeyPemOrBase64)
        {
            var aesKeyBase64 = Convert.ToBase64String(aesKey);
            var aesKeyBytes = Encoding.UTF8.GetBytes(aesKeyBase64);

            using var rsa = RSA.Create();
            var cleanKey = (publicKeyPemOrBase64 ?? "")
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            var keyBytes = Convert.FromBase64String(cleanKey);
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

            return rsa.Encrypt(aesKeyBytes, RSAEncryptionPadding.OaepSHA256);
        }

        #endregion
    }
}
