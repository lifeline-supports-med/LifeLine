using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LifeLine.Application.Helpers
{
    public static class SlugHelper
    {
        public static string Generate(string patientName, string condition)
        {
            var raw = $"{patientName}-{condition}".ToLower();
            var slug = Regex.Replace(raw, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');
            var unique = $"{slug}-{Guid.NewGuid().ToString()[..8]}";
            return unique;
        }
    }
}
