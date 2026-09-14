using EasyAlumni.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace EasyAlumni.Infrastructure.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private readonly IWebHostEnvironment _env;

        public LocalFileStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveFileAsync(
            Stream fileStream,
            string originalFileName,
            string subFolder,
            string[] allowedExtensions,
            long maxSizeBytes = 2097152)
        {
            if (fileStream == null || fileStream.Length == 0)
            {
                throw new ArgumentException("Uploaded file stream is empty.", nameof(fileStream));
            }

            if (fileStream.Length > maxSizeBytes)
            {
                throw new InvalidOperationException($"File size ({fileStream.Length} bytes) exceeds the allowed limit of {maxSizeBytes} bytes.");
            }

            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File extension '{extension}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}");
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadDir = Path.Combine(webRoot, "Uploads", subFolder);

            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(uploadDir, uniqueFileName);

            using (var destinationStream = new FileStream(fullPath, FileMode.Create))
            {
                await fileStream.CopyToAsync(destinationStream);
            }

            return $"/Uploads/{subFolder}/{uniqueFileName}";
        }

        public void DeleteFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var trimmed = relativePath.TrimStart('/', '\\');
                var fullPath = Path.Combine(webRoot, trimmed);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Silently ignore or log delete errors
            }
        }
    }
}
