using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using LifeLine.Application.Interfaces;
using LifeLine.Domain.Settings.Cloudinary;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLine.Persistence.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;

        private static readonly string[] AllowedImageTypes =
            ["image/jpeg", "image/png", "image/webp", "image/jpg"];

        private static readonly string[] AllowedDocumentTypes =
            ["application/pdf", "image/jpeg", "image/png", "image/jpg"];

        private const long MaxImageSize = 5 * 1024 * 1024;
        private const long MaxDocumentSize = 10 * 1024 * 1024;

        public CloudinaryService(
            IOptions<CloudinarySettings> settings,
            ILogger<CloudinaryService> logger)
        {
            var s = settings.Value;
            var account = new Account(s.CloudName, s.ApiKey, s.ApiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
            _logger = logger;
        }

        public async Task<CloudinaryUploadResult> UploadImageAsync(
    IFormFile file, string folder)
        {
            if (file is null || file.Length == 0)
                return Fail("No file provided.");
            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return Fail("Only JPEG, PNG, and WebP images are allowed.");
            if (file.Length > MaxImageSize)
                return Fail("Image must be smaller than 5MB.");

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var stream = file.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = $"lifeline/{folder}",
                        PublicId = $"{folder}-{Guid.NewGuid()}", 
                        Transformation = new Transformation()
        .Width(800).Height(600).Crop("limit").Quality("auto").FetchFormat("auto"),
                        UseFilename = false,
                        UniqueFilename = false,
                        Overwrite = true
                    };

                    var result = await _cloudinary.UploadAsync(uploadParams);
                    if (result.Error is not null)
                    {
                        _logger.LogError(
                            "Cloudinary image upload error (attempt {Attempt}/{Max}): {Error}",
                            attempt, maxAttempts, result.Error.Message);

                        if (attempt == maxAttempts)
                            return Fail(result.Error.Message);

                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                        continue;
                    }
                    _logger.LogInformation(
                        "Image uploaded to Cloudinary: {Url}", result.SecureUrl);
                    return new CloudinaryUploadResult
                    {
                        IsSuccess = true,
                        Url = result.SecureUrl.ToString(),
                        PublicId = result.PublicId
                    };
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    _logger.LogWarning(
                        "Cloudinary image upload socket error (attempt {Attempt}/{Max}): {Error}",
                        attempt, maxAttempts, ex.Message);

                    if (attempt == maxAttempts)
                        return Fail("Image upload failed after multiple attempts. Please check your connection and try again.");

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt)); // 2s, 4s, then fail
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "Cloudinary image upload failed (attempt {Attempt}/{Max}): {Error}",
                        attempt, maxAttempts, ex.Message);

                    if (attempt == maxAttempts)
                        return Fail("Image upload failed. Please try again.");

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }

            return Fail("Image upload failed after multiple attempts.");
        }


        public async Task<CloudinaryUploadResult> UploadDocumentAsync(
            IFormFile file, string folder)
        {
            if (file is null || file.Length == 0)
                return Fail("No file provided.");
            if (!AllowedDocumentTypes.Contains(file.ContentType.ToLower()))
                return Fail("Only PDF and image files are allowed for documents.");
            if (file.Length > MaxDocumentSize)
                return Fail("Document must be smaller than 10MB.");

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var stream = file.OpenReadStream();
                    if (file.ContentType.ToLower() == "application/pdf")
                    {
                        var rawParams = new RawUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = $"lifeline/{folder}",
                            UseFilename = false,
                            UniqueFilename = true
                        };
                        var rawResult = await _cloudinary.UploadAsync(rawParams);
                        if (rawResult.Error is not null)
                        {
                            _logger.LogError(
                                "Cloudinary document upload error (attempt {Attempt}/{Max}): {Error}",
                                attempt, maxAttempts, rawResult.Error.Message);

                            if (attempt == maxAttempts)
                                return Fail(rawResult.Error.Message);

                            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                            continue;
                        }
                        return new CloudinaryUploadResult
                        {
                            IsSuccess = true,
                            Url = rawResult.SecureUrl.ToString(),
                            PublicId = rawResult.PublicId
                        };
                    }
                    else
                    {
                        var imgParams = new ImageUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = $"lifeline/{folder}",
                            UseFilename = false,
                            UniqueFilename = true
                        };
                        var imgResult = await _cloudinary.UploadAsync(imgParams);
                        if (imgResult.Error is not null)
                        {
                            _logger.LogError(
                                "Cloudinary document image upload error (attempt {Attempt}/{Max}): {Error}",
                                attempt, maxAttempts, imgResult.Error.Message);

                            if (attempt == maxAttempts)
                                return Fail(imgResult.Error.Message);

                            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                            continue;
                        }
                        return new CloudinaryUploadResult
                        {
                            IsSuccess = true,
                            Url = imgResult.SecureUrl.ToString(),
                            PublicId = imgResult.PublicId
                        };
                    }
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    _logger.LogWarning(
                        "Cloudinary document upload socket error (attempt {Attempt}/{Max}): {Error}",
                        attempt, maxAttempts, ex.Message);

                    if (attempt == maxAttempts)
                        return Fail("Document upload failed after multiple attempts. Please check your connection and try again.");

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "Cloudinary document upload failed (attempt {Attempt}/{Max}): {Error}",
                        attempt, maxAttempts, ex.Message);

                    if (attempt == maxAttempts)
                        return Fail("Document upload failed. Please try again.");

                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }

            return Fail("Document upload failed after multiple attempts.");
        }

        public async Task<bool> DeleteFileAsync(string publicId)
        {
            try
            {
                var result = await _cloudinary.DestroyAsync(
                    new DeletionParams(publicId));

                return result.Result == "ok";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Cloudinary delete failed for {PublicId}: {Error}",
                    publicId, ex.Message);
                return false;
            }
        }

        private static CloudinaryUploadResult Fail(string error) =>
            new() { IsSuccess = false, Error = error };
    }
}
