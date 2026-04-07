using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Hosting;

namespace Brainova.BLL.Services.Classes
{
    public class AiTumorService : IAiTumorService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public AiTumorService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        private sealed class PythonGradcamResponse
        {
            public string label { get; set; } = default!;
            public float[] probabilities { get; set; } = Array.Empty<float>();
            public string gradcam_image_base64 { get; set; } = default!;
        }

        private sealed class PythonErrorResponse
        {
            public bool success { get; set; }
            public string? message { get; set; }
        }

        public async Task<GradcamResultDto> GetGradcamAsync(
            Stream fileStream,
            string fileName,
            string contentType,
            CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient("GradCamClient");

            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(contentType)
                        ? "image/jpeg"
                        : contentType
                );

            form.Add(fileContent, "file", fileName);

            using var res = await client.PostAsync("predict", form, ct);
            var json = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                string message = "GradCAM python error.";

                try
                {
                    var pyError = JsonSerializer.Deserialize<PythonErrorResponse>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (!string.IsNullOrWhiteSpace(pyError?.message))
                        message = pyError.message!;
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(json))
                        message = json;
                }

                if (res.StatusCode == HttpStatusCode.BadRequest)
                    throw new BadRequestException(message);

                throw new Exception($"GradCAM python error: {(int)res.StatusCode} -> {message}");
            }

            var py = JsonSerializer.Deserialize<PythonGradcamResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;

            var bytes = Convert.FromBase64String(py.gradcam_image_base64);

            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "gradcam");
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}.jpg";
            var fullPath = Path.Combine(folder, storedName);

            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            return new GradcamResultDto
            {
                Label = py.label,
                Probabilities = py.probabilities,
                FileName = storedName
            };
        }
    }
}