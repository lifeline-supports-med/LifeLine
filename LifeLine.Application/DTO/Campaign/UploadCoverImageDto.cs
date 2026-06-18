using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.DTO.Campaign
{
    public class UploadCoverImageDto
    {
        public IFormFile File { get; set; } = null!;
    }

    public class UploadDocumentDto
    {
        public IFormFile File { get; set; } = null!;
        public string FileType { get; set; } = string.Empty;
    }
}
