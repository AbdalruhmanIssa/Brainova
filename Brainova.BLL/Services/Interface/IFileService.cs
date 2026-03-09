using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Interface
{
    public interface IFileService
    {
        Task<string> SaveAsync(IFormFile file, string folderName, CancellationToken ct = default);
        string BuildPath(string folderName, string storedFileName);
    }
}
