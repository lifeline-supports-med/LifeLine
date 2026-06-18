using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces
{
    public interface ICloudinaryService
    {
        Task<CloudinaryUploadResult> UploadImageAsync(IFormFile file, string folder);
        Task<CloudinaryUploadResult> UploadDocumentAsync(IFormFile file, string folder);
        Task<bool> DeleteFileAsync(string publicId);
    }

    public class CloudinaryUploadResult
    {
        public bool IsSuccess { get; set; }
        public string? Url { get; set; }
        public string? PublicId { get; set; }
        public string? Error { get; set; }
    }
}
