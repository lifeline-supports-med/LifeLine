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

        private const long MaxImageSize = 5 * 1024 * 1024; // 5MB
        private const long MaxDocumentSize = 10 * 1024 * 1024; // 10MB

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
            // Validate
            if (file is null || file.Length == 0)
                return Fail("No file provided.");

            if (!AllowedImageTypes.Contains(file.ContentType.ToLower()))
                return Fail("Only JPEG, PNG, and WebP images are allowed.");

            if (file.Length > MaxImageSize)
                return Fail("Image must be smaller than 5MB.");

            try
            {
                await using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"lifeline/{folder}",
                    Transformation = new Transformation()
                        .Width(800).Height(600)
                        .Crop("limit")
                        .Quality("auto")
                        .FetchFormat("auto"),
                    UseFilename = false,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error is not null)
                {
                    _logger.LogError(
                        "Cloudinary image upload error: {Error}", result.Error.Message);
                    return Fail(result.Error.Message);
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
            catch (Exception ex)
            {
                _logger.LogError("Cloudinary upload failed: {Error}", ex.Message);
                return Fail("Image upload failed. Please try again.");
            }
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
                        return Fail(rawResult.Error.Message);

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
                        return Fail(imgResult.Error.Message);

                    return new CloudinaryUploadResult
                    {
                        IsSuccess = true,
                        Url = imgResult.SecureUrl.ToString(),
                        PublicId = imgResult.PublicId
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Cloudinary document upload failed: {Error}", ex.Message);
                return Fail("Document upload failed. Please try again.");
            }
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
