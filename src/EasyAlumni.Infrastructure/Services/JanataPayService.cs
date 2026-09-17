using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyAlumni.Infrastructure.Services
{
    public class JanataPayService : IJanataPayService
    {
        private readonly JanataPayOptions _options;
        private readonly ILogger<JanataPayService> _logger;
        private readonly HttpClient _httpClient;

        // Cached token state
        private static string? _cachedAccessToken;
        private static DateTime _tokenExpiresAt = DateTime.MinValue;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);

        public JanataPayService(
            IOptions<JanataPayOptions> options,
            ILogger<JanataPayService> logger)
        {
            _options = options.Value;
            _logger = logger;

            SocketsHttpHandler handler = new();
            if (_options.UseProxy && !string.IsNullOrWhiteSpace(_options.Socks5Proxy))
            {
                handler.Proxy = new WebProxy(_options.Socks5Proxy);
                handler.UseProxy = true;
            }

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/"),
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
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return new JanataPayTokenizeResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to obtain gateway authorization token."
                    };
                }

                // Reference ID format: EA-REGNO-TIMESTAMP (Must be <= 30 chars)
                // Example: EA-2026-0001-174123
                var cleanReg = registrationNo.Replace("-", "");
                var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000;
                var referenceId = $"EA{cleanReg}{ts}";
                if (referenceId.Length > 25)
                {
                    referenceId = referenceId[..25];
                }

                var callbackBase = _options.CallbackBaseUrl.TrimEnd('/');
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

                var response = await _httpClient.SendAsync(requestMessage);
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
            try
            {
                var token = await GetAccessTokenAsync();
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

                var response = await _httpClient.SendAsync(requestMessage);
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

        private async Task<string?> GetAccessTokenAsync()
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

                var (token, expiresIn) = await AuthenticateGatewayAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _cachedAccessToken = token;
                    // Expire 5 minutes early for clock skew safety
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

        private async Task<(string? Token, int ExpiresIn)> AuthenticateGatewayAsync()
        {
            var authPayload = new
            {
                username = _options.Username,
                password = _options.Password,
                merchantUid = _options.MerchantUid
            };

            var payloadJson = JsonSerializer.Serialize(authPayload);
            var (aesKey, encryptedData) = EncryptPayloadWithNewKey(payloadJson);
            var rsaEncryptedKey = EncryptAesKeyWithRsa(aesKey);

            var merchantUidBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(_options.MerchantUid));
            var rsaKeyBase64 = Convert.ToBase64String(rsaEncryptedKey);

            var requestObj = new
            {
                merchant = merchantUidBase64,
                key = rsaKeyBase64,
                data = encryptedData
            };

            var requestJson = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("jbagg/api/auth", content);
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

        // Active AES session key for tokenized calls
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
            _sessionAesKey = key; // update session key
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
            var iv = new byte[12]; // 12-byte standard GCM nonce
            RandomNumberGenerator.Fill(iv);

            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[16]; // 16-byte authentication tag

            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Encrypt(iv, plainBytes, cipherBytes, tag);

            // Structure: IV (12) + CipherText (N) + Tag (16)
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

        private byte[] EncryptAesKeyWithRsa(byte[] aesKey)
        {
            var aesKeyBase64 = Convert.ToBase64String(aesKey);
            var aesKeyBytes = Encoding.UTF8.GetBytes(aesKeyBase64);

            using var rsa = RSA.Create();
            var cleanKey = _options.PublicKey
                .Replace("-----BEGIN PUBLIC KEY-----", "")
                .Replace("-----END PUBLIC KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();

            var keyBytes = Convert.FromBase64String(cleanKey);
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);

            // RSA-OAEP with SHA-256
            return rsa.Encrypt(aesKeyBytes, RSAEncryptionPadding.OaepSHA256);
        }

        #endregion
    }
}
