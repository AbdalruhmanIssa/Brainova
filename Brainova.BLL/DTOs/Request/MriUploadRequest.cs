using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.DTOs.Request
{
    public class MriUploadRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
