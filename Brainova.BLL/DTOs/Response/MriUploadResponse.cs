using Brainova.DAL.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Brainova.BLL.DTOs.Response
{
    public class MriUploadResponse
    {
        public Guid CaseId { get; set; }
        public CaseStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        [JsonIgnore]
        public string StoredFileName { get; set; } = default!;
        public string ImageUrl { get; set; } = default!;
    }
}
