using System.Collections.Concurrent;
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

        // Cached token state per account type
        private class AccountTokenCache
        {
            public string? AccessToken { get; set; }
            public DateTime TokenExpiresAt { get; set; } = DateTime.MinValue;
            public byte[]? SessionAesKey { get; set; }
            public SemaphoreSlim Lock { get; } = new(1, 1);
        }

        private static readonly ConcurrentDictionary<JanataPayAccountType, AccountTokenCache> _tokenCaches = new();

        private static AccountTokenCache GetCache(JanataPayAccountType accountType)
        {
            return _tokenCaches.GetOrAdd(accountType, _ => new AccountTokenCache());
        }

        public JanataPayService(
            ApplicationDbContext context,
            IOptions<JanataPayOptions> defaultOptions,
            ILogger<JanataPayService> logger)
        {
            _context = context;
            _defaultOptions = defaultOptions.Value;
            _logger = logger;
        }

        private async Task<JanataPayOptions> GetEffectiveOptionsAsync(JanataPayAccountType accountType = JanataPayAccountType.Registration)
        {
            try
            {
                var settings = await _context.SystemSettings
                    .Where(s => s.SettingKey.StartsWith("JanataPay") || s.SettingKey.StartsWith("Donation_JanataPay"))
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

                // Default registration options
                var regBaseUrl = settings.TryGetValue("JanataPayBaseUrl", out var bUrl) && !string.IsNullOrWhiteSpace(bUrl) ? bUrl : _defaultOptions.BaseUrl;
                var regMerchantUid = settings.TryGetValue("JanataPayMerchantUid", out var mUid) && !string.IsNullOrWhiteSpace(mUid) ? mUid : _defaultOptions.MerchantUid;
                var regUsername = settings.TryGetValue("JanataPayUsername", out var uName) && !string.IsNullOrWhiteSpace(uName) ? uName : _defaultOptions.Username;
                var regPassword = settings.TryGetValue("JanataPayPassword", out var pwd) && !string.IsNullOrWhiteSpace(pwd) ? pwd : _defaultOptions.Password;
                var regPublicKey = settings.TryGetValue("JanataPayPublicKey", out var pKey) && !string.IsNullOrWhiteSpace(pKey) ? pKey : _defaultOptions.PublicKey;
                var regSocks5Proxy = settings.TryGetValue("JanataPaySocks5Proxy", out var sProxy) ? sProxy : _defaultOptions.Socks5Proxy;
                var regUseProxy = settings.TryGetValue("JanataPayUseProxy", out var uProxy) ? uProxy == "1" : _defaultOptions.UseProxy;
                var regCallbackBaseUrl = settings.TryGetValue("JanataPayCallbackBaseUrl", out var cUrl) && !string.IsNullOrWhiteSpace(cUrl) ? cUrl : _defaultOptions.CallbackBaseUrl;

                if (accountType == JanataPayAccountType.Donation)
                {
                    bool useDedicated = settings.TryGetValue("Donation_JanataPayUseDedicated", out var dedicatedVal) && dedicatedVal == "1";
                    if (useDedicated)
                    {
                        var donMerchantUid = settings.TryGetValue("Donation_JanataPayMerchantUid", out var dm) && !string.IsNullOrWhiteSpace(dm) ? dm : regMerchantUid;
                        var donUsername = settings.TryGetValue("Donation_JanataPayUsername", out var du) && !string.IsNullOrWhiteSpace(du) ? du : regUsername;
                        var donPassword = settings.TryGetValue("Donation_JanataPayPassword", out var dp) && !string.IsNullOrWhiteSpace(dp) ? dp : regPassword;
                        var donPublicKey = settings.TryGetValue("Donation_JanataPayPublicKey", out var dpk) && !string.IsNullOrWhiteSpace(dpk) ? dpk : regPublicKey;
                        var donBaseUrl = settings.TryGetValue("Donation_JanataPayBaseUrl", out var dbu) && !string.IsNullOrWhiteSpace(dbu) ? dbu : regBaseUrl;

                        return new JanataPayOptions
                        {
                            BaseUrl = donBaseUrl,
                            MerchantUid = donMerchantUid,
                            Username = donUsername,
                            Password = donPassword,
                            PublicKey = donPublicKey,
                            Socks5Proxy = regSocks5Proxy,
                            UseProxy = regUseProxy,
                            CallbackBaseUrl = regCallbackBaseUrl
                        };
                    }
                }

                return new JanataPayOptions
                {
                    BaseUrl = regBaseUrl,
                    MerchantUid = regMerchantUid,
                    Username = regUsername,
                    Password = regPassword,
                    PublicKey = regPublicKey,
                    Socks5Proxy = regSocks5Proxy,
                    UseProxy = regUseProxy,
                    CallbackBaseUrl = regCallbackBaseUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load JanataPay options from database, using fallback.");
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
            string? customerEmail,
            JanataPayAccountType accountType = JanataPayAccountType.Registration)
        {
            var options = await GetEffectiveOptionsAsync(accountType);
            using var httpClient = CreateHttpClient(options);
            var cache = GetCache(accountType);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var token = await GetAccessTokenAsync(options, httpClient, accountType, forceRefresh: attempt > 0);
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

                    // Amount MUST be integer according to JanataPay Integration Guide
                    long amountInt = (long)Math.Round(amount, MidpointRounding.AwayFromZero);

                    var description = accountType == JanataPayAccountType.Donation
                        ? $"Alumni Fund Voluntary Donation {registrationNo}"
                        : $"Alumni Reunion Registration Fee {registrationNo}";

                    var tokenizePayload = new
                    {
                        merchantUid = options.MerchantUid,
                        accessToken = token,
                        referenceId = referenceId,
                        amount = amountInt.ToString(),
                        currency = "BDT",
                        customerName = string.IsNullOrWhiteSpace(customerName) ? "Alumni Attendee" : customerName.Trim(),
                        customerPhone = string.IsNullOrWhiteSpace(customerPhone) ? "01700000000" : customerPhone.Trim(),
                        customerEmail = string.IsNullOrWhiteSpace(customerEmail) ? "info@alumni.snhghs.edu.bd" : customerEmail.Trim(),
                        description = description,
                        successUrl = successUrl,
                        failUrl = failUrl,
                        cancelUrl = cancelUrl
                    };

                    var payloadJson = JsonSerializer.Serialize(tokenizePayload);
                    var encryptedData = EncryptPayload(payloadJson, cache);

                    var requestObj = new
                    {
                        merchantUid = options.MerchantUid,
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
                        _logger.LogError("JanataPay Tokenize HTTP Error ({AccountType}): {StatusCode}, Body: {Body}", accountType, response.StatusCode, responseBody);

                        // If token expired or unauthorized, clear cache and retry once
                        if (attempt == 0 && (response.StatusCode == HttpStatusCode.Unauthorized || responseBody.Contains("JWT expired") || responseBody.Contains("expired")))
                        {
                            _logger.LogWarning("JanataPay token expired during tokenize for {AccountType}. Invalidating cache and retrying...", accountType);
                            InvalidateToken(accountType);
                            continue;
                        }

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
                        var decryptedJson = DecryptPayload(encData, cache);
                        _logger.LogInformation("JanataPay Tokenize decrypted response ({AccountType}): {Json}", accountType, decryptedJson);
                        var dataObj = JsonNode.Parse(decryptedJson);

                        var trxToken = dataObj?["transactionToken"]?.GetValue<string>();
                        var checkoutUrl = dataObj?["paymentUrl"]?.GetValue<string>() ?? dataObj?["url"]?.GetValue<string>();

                        // If paymentUrl is relative or missing, build it as per guide: https://sandbox-pg.janatapay.com/jbagg/aggregator?token=<transactionToken>
                        if (string.IsNullOrEmpty(checkoutUrl) && !string.IsNullOrEmpty(trxToken))
                        {
                            var baseHost = (options.BaseUrl ?? "https://sandbox-pg.janatapay.com").TrimEnd('/');
                            checkoutUrl = $"{baseHost}/jbagg/aggregator?token={trxToken}";
                        }

                        return new JanataPayTokenizeResult
                        {
                            Success = true,
                            StatusCode = code,
                            ReferenceId = referenceId,
                            TransactionToken = trxToken,
                            CheckoutUrl = checkoutUrl
                        };
                    }

                    // Check for JWT expired inside 200/500 JSON payload
                    var statusMsg = root?["statusMessage"]?.GetValue<string>() ?? root?["message"]?.GetValue<string>() ?? "";
                    if (attempt == 0 && (statusMsg.Contains("JWT expired") || statusMsg.Contains("expired")))
                    {
                        _logger.LogWarning("JanataPay returned token expired inside payload ({AccountType}): {StatusMsg}. Retrying...", accountType, statusMsg);
                        InvalidateToken(accountType);
                        continue;
                    }

                    return new JanataPayTokenizeResult
                    {
                        Success = false,
                        StatusCode = code,
                        ErrorMessage = !string.IsNullOrWhiteSpace(statusMsg) ? statusMsg : "Failed to initialize payment session."
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during JanataPay InitiatePaymentAsync ({AccountType}) for {RegNo}", accountType, registrationNo);
                    return new JanataPayTokenizeResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }

            return new JanataPayTokenizeResult
            {
                Success = false,
                ErrorMessage = "Payment initialization failed after retry."
            };
        }

        public async Task<JanataPayVerifyResult> VerifyPaymentAsync(
            string referenceId,
            string transactionToken,
            JanataPayAccountType accountType = JanataPayAccountType.Registration)
        {
            var options = await GetEffectiveOptionsAsync(accountType);
            using var httpClient = CreateHttpClient(options);
            var cache = GetCache(accountType);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var token = await GetAccessTokenAsync(options, httpClient, accountType, forceRefresh: attempt > 0);
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
                        merchantUid = options.MerchantUid,
                        accessToken = token,
                        referenceId = referenceId,
                        transactionToken = transactionToken
                    };

                    var payloadJson = JsonSerializer.Serialize(verifyPayload);
                    var encryptedData = EncryptPayload(payloadJson, cache);

                    var requestObj = new
                    {
                        merchantUid = options.MerchantUid,
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
                        _logger.LogError("JanataPay Verify HTTP Error ({AccountType}): {StatusCode}, Body: {Body}", accountType, response.StatusCode, responseBody);

                        if (attempt == 0 && (response.StatusCode == HttpStatusCode.Unauthorized || responseBody.Contains("JWT expired") || responseBody.Contains("expired")))
                        {
                            _logger.LogWarning("JanataPay token expired during verify ({AccountType}). Retrying with fresh token...", accountType);
                            InvalidateToken(accountType);
                            continue;
                        }

                        return new JanataPayVerifyResult
                        {
                            Success = false,
                            StatusCode = (int)response.StatusCode,
                            ErrorMessage = $"JanataPay verify failed: HTTP {(int)response.StatusCode}"
                        };
                    }

                    _logger.LogInformation("JanataPay Verify raw response ({AccountType}): StatusCode={StatusCode}, Body={Body}", accountType, response.StatusCode, responseBody);

                    var root = JsonNode.Parse(responseBody);
                    var code = root?["statusCode"]?.GetValue<int>() ?? 0;
                    var encData = root?["data"]?.GetValue<string>();

                    if (code == 200 && !string.IsNullOrEmpty(encData))
                    {
                        var decryptedJson = DecryptPayload(encData, cache);
                        _logger.LogInformation("JanataPay Verify decrypted response ({AccountType}) for {RefId}: {Json}", accountType, referenceId, decryptedJson);
                        var dataObj = JsonNode.Parse(decryptedJson);

                        // If response wraps data under a nested 'data' node: { "statusCode": 200, "data": { ... } }
                        var payloadObj = dataObj?["data"] ?? dataObj;

                        var trxStatus = payloadObj?["transactionStatus"]?.GetValue<string>();
                        var trxStatusCode = payloadObj?["transactionStatusCode"]?.GetValue<string>();
                        var ftNumber = payloadObj?["ftNumber"]?.GetValue<string>();
                        var refId = payloadObj?["referenceId"]?.GetValue<string>() ?? referenceId;
                        var amountStr = payloadObj?["amount"]?.ToString();
                        decimal.TryParse(amountStr, out var amt);
                        var currency = payloadObj?["currency"]?.GetValue<string>();
                        var paymentMethod = payloadObj?["paymentGateway"]?.GetValue<string>() ?? payloadObj?["paymentMethod"]?.GetValue<string>();
                        var dateStr = payloadObj?["transactionDate"]?.GetValue<string>();

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

                    var statusMsg = root?["statusMessage"]?.GetValue<string>() ?? root?["message"]?.GetValue<string>() ?? "";
                    if (attempt == 0 && (statusMsg.Contains("JWT expired") || statusMsg.Contains("expired")))
                    {
                        _logger.LogWarning("JanataPay returned token expired inside verify payload ({AccountType}): {StatusMsg}. Retrying...", accountType, statusMsg);
                        InvalidateToken(accountType);
                        continue;
                    }

                    return new JanataPayVerifyResult
                    {
                        Success = false,
                        StatusCode = code,
                        ErrorMessage = !string.IsNullOrWhiteSpace(statusMsg) ? statusMsg : "Verification returned invalid payload."
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during JanataPay VerifyPaymentAsync ({AccountType}) for {RefId}", accountType, referenceId);
                    return new JanataPayVerifyResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }

            return new JanataPayVerifyResult
            {
                Success = false,
                ErrorMessage = "Payment verification failed after retry."
            };
        }

        public async Task<(bool Success, string Message, string? AccessToken)> TestConnectionAsync(JanataPayAccountType accountType = JanataPayAccountType.Registration)
        {
            var options = await GetEffectiveOptionsAsync(accountType);
            using var httpClient = CreateHttpClient(options);
            var cache = GetCache(accountType);

            try
            {
                // Force fresh authentication
                InvalidateToken(accountType);
                var (token, expiresAt) = await AuthenticateGatewayAsync(options, httpClient, cache);
                if (!string.IsNullOrEmpty(token))
                {
                    cache.AccessToken = token;
                    cache.TokenExpiresAt = expiresAt;

                    var remainingSec = Math.Max(0, (int)(expiresAt - DateTime.UtcNow).TotalSeconds);
                    return (true, $"Authentication successful ({accountType})! Merchant UID: {options.MerchantUid}. Received Bearer JWT Token (expires in {remainingSec}s at {expiresAt:HH:mm:ss} UTC).", token);
                }

                return (false, $"Authentication failed for {accountType} account with Merchant UID: {options.MerchantUid}. Please check Merchant UID, Username, Password, and RSA Public Key.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during JanataPay TestConnectionAsync for {AccountType}", accountType);
                return (false, $"Connection error: {ex.Message}", null);
            }
        }

        private static void InvalidateToken(JanataPayAccountType accountType)
        {
            var cache = GetCache(accountType);
            cache.AccessToken = null;
            cache.TokenExpiresAt = DateTime.MinValue;
        }

        private async Task<string?> GetAccessTokenAsync(JanataPayOptions options, HttpClient httpClient, JanataPayAccountType accountType, bool forceRefresh = false)
        {
            var cache = GetCache(accountType);

            if (!forceRefresh && !string.IsNullOrEmpty(cache.AccessToken) && DateTime.UtcNow < cache.TokenExpiresAt)
            {
                return cache.AccessToken;
            }

            await cache.Lock.WaitAsync();
            try
            {
                if (!forceRefresh && !string.IsNullOrEmpty(cache.AccessToken) && DateTime.UtcNow < cache.TokenExpiresAt)
                {
                    return cache.AccessToken;
                }

                var (token, expiresAt) = await AuthenticateGatewayAsync(options, httpClient, cache);
                if (!string.IsNullOrEmpty(token))
                {
                    cache.AccessToken = token;
                    cache.TokenExpiresAt = expiresAt;
                    return token;
                }

                return null;
            }
            finally
            {
                cache.Lock.Release();
            }
        }

        private async Task<(string? Token, DateTime ExpiresAt)> AuthenticateGatewayAsync(JanataPayOptions options, HttpClient httpClient, AccountTokenCache cache)
        {
            var aesKey = new byte[32];
            RandomNumberGenerator.Fill(aesKey);
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
            cache.SessionAesKey = aesKey; // update active session key for this account
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
                return (null, DateTime.MinValue);
            }

            var root = JsonNode.Parse(responseBody);
            var code = root?["statusCode"]?.GetValue<int>() ?? 0;
            var encData = root?["data"]?.GetValue<string>();

            if (code == 200 && !string.IsNullOrEmpty(encData))
            {
                var decryptedJson = DecryptPayloadWithKey(encData, aesKey);
                var dataObj = JsonNode.Parse(decryptedJson);

                var token = dataObj?["accessToken"]?.GetValue<string>();
                var expiresIn = dataObj?["expiresIn"]?.GetValue<int>() ?? 300;

                // Calculate expiry from JWT claim if available, with safety buffer
                var expiresAt = ParseJwtExpiry(token, expiresIn);

                _logger.LogInformation("JanataPay Auth successful for Merchant UID {MerchantUid}. Token expires at {ExpiresAt} UTC.", options.MerchantUid, expiresAt);
                return (token, expiresAt);
            }

            _logger.LogError("JanataPay Auth returned non-200 code: {Body}", responseBody);
            return (null, DateTime.MinValue);
        }

        private DateTime ParseJwtExpiry(string? jwt, int defaultExpiresInSec)
        {
            if (!string.IsNullOrWhiteSpace(jwt))
            {
                try
                {
                    var parts = jwt.Split('.');
                    if (parts.Length >= 2)
                    {
                        var b64 = parts[1].Replace('-', '+').Replace('_', '/');
                        switch (b64.Length % 4)
                        {
                            case 2: b64 += "=="; break;
                            case 3: b64 += "="; break;
                        }
                        var jsonBytes = Convert.FromBase64String(b64);
                        var claims = JsonNode.Parse(jsonBytes);
                        if (claims?["exp"] != null)
                        {
                            var expUnix = claims["exp"]!.GetValue<long>();
                            var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                            // Deduct 15 seconds buffer to prevent edge-of-expiry rejections
                            var buffered = expUtc.AddSeconds(-15);
                            if (buffered > DateTime.UtcNow)
                            {
                                return buffered;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JWT exp claim, falling back to expiresIn");
                }
            }

            // Fallback to expiresIn with 30s buffer
            var safetySeconds = Math.Max(30, defaultExpiresInSec - 30);
            return DateTime.UtcNow.AddSeconds(safetySeconds);
        }

        #region Cryptography Helpers

        private string EncryptPayload(string plaintext, AccountTokenCache cache)
        {
            if (cache.SessionAesKey == null)
            {
                cache.SessionAesKey = new byte[32];
                RandomNumberGenerator.Fill(cache.SessionAesKey);
            }

            return EncryptWithAesGcm(plaintext, cache.SessionAesKey);
        }

        private string DecryptPayload(string ciphertextBase64, AccountTokenCache cache)
        {
            if (cache.SessionAesKey == null)
            {
                throw new InvalidOperationException("No AES session key initialized for decryption.");
            }

            return DecryptWithAesGcm(ciphertextBase64, cache.SessionAesKey);
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
