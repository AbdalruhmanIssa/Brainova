using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Brainova.BLL.Services.Classes
{
    public sealed class AiTumorService : IAiTumorService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public AiTumorService(IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _httpClientFactory = httpClientFactory;
            _env = env;
        }

        private sealed class PythonGradcamResponse
        {
            public string label { get; set; } = default!;
            public float[] probabilities { get; set; } = Array.Empty<float>();
            public string gradcam_image_base64 { get; set; } = default!;
        }

        public async Task<GradcamResultDto> GetGradcamAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient("GradCamClient");

            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);

            form.Add(fileContent, "file", fileName);

            using var res = await client.PostAsync("predict", form, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                throw new Exception($"GradCAM python error: {res.StatusCode} -> {json}");

            var py = JsonSerializer.Deserialize<PythonGradcamResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var bytes = Convert.FromBase64String(py.gradcam_image_base64);

            // Save gradcam file (same as your code)
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "gradcam");
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}.jpg";
            var fullPath = Path.Combine(folder, storedName);

            await System.IO.File.WriteAllBytesAsync(fullPath, bytes, ct);

            return new GradcamResultDto
            {
                Label = py.label,
                Probabilities = py.probabilities,
                FileName = storedName
            };
        }
    }
}