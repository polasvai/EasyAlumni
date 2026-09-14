using System.Security.Cryptography;
using System.Text;
using EasyAlumni.Core.Interfaces;
using QRCoder;

namespace EasyAlumni.Infrastructure.Services
{
    public class QrCodeService : IQrCodeService
    {
        private const string SecretSalt = "EasyAlumni#Secret#Signature#2026";

        public string GenerateQrCodeBase64(string payload)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(20);
            return $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
        }

        public string GenerateSignedToken(string registrationNo, int registrationId, string userCode)
        {
            var rawData = $"{registrationNo}|{registrationId}|{userCode}";
            var signature = ComputeHmac(rawData);
            var fullToken = $"{rawData}|{signature}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(fullToken));
        }

        public bool ValidateToken(string token, out string registrationNo, out int registrationId)
        {
            registrationNo = string.Empty;
            registrationId = 0;

            if (string.IsNullOrWhiteSpace(token))
                return false;

            try
            {
                // Might be raw registrationNo as fallback or base64 token
                if (!token.Contains('|') && token.Length > 20)
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                    var parts = decoded.Split('|');
                    if (parts.Length == 4)
                    {
                        var expectedSig = ComputeHmac($"{parts[0]}|{parts[1]}|{parts[2]}");
                        if (parts[3] == expectedSig && int.TryParse(parts[1], out int id))
                        {
                            registrationNo = parts[0];
                            registrationId = id;
                            return true;
                        }
                    }
                }
                else if (token.StartsWith("RE-"))
                {
                    // Direct ticket number scan
                    registrationNo = token.Trim();
                    return true;
                }
            }
            catch
            {
                // If decoding fails, check if plain ticket number
                if (token.StartsWith("RE-"))
                {
                    registrationNo = token.Trim();
                    return true;
                }
            }

            return false;
        }

        private static string ComputeHmac(string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretSalt));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash)[..12]; // Short 12-char signature
        }
    }
}
