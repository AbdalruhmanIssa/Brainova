using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.BLL.Services.Classes
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _env;

        public FileService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveAsync(IFormFile file, string folderName, CancellationToken ct = default)
        {
            if (file is null || file.Length == 0)
                throw new Exception("File is required.");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var storedName = $"{Guid.NewGuid():N}{ext}";

            var folder = Path.Combine(_env.ContentRootPath, "App_Data", folderName);
            Directory.CreateDirectory(folder);

            var fullPath = Path.Combine(folder, storedName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream, ct);

            return storedName;
        }

        public string BuildPath(string folderName, string storedFileName)
            => Path.Combine(_env.ContentRootPath, "App_Data", folderName, storedFileName);
    }
}
