using System.Net.Http.Headers;
using System.Text.Json;
using Brainova.BLL.DTOs.Response;
using Brainova.BLL.Services.Interface;
using Microsoft.AspNetCore.Hosting;

namespace Brainova.BLL.Services.Classes
{
    public class AiTumorService : IAiTumorService
    {
        // Used to access environment info (like project root path)
        private readonly IWebHostEnvironment _env;

        // Used to create HttpClient instances (best practice instead of new HttpClient())
        private readonly IHttpClientFactory _httpClientFactory;

        // Constructor (Dependency Injection)
        public AiTumorService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        // This class matches EXACTLY what Python returns
        private sealed class PythonGradcamResponse
        {
            public string label { get; set; } = default!; // predicted class (glioma, etc.)
            public float[] probabilities { get; set; } = Array.Empty<float>(); // confidence values
            public string gradcam_image_base64 { get; set; } = default!; // image encoded as base64 string
        }

        // Main method: sends image to Python and gets Grad-CAM + prediction
        public async Task<GradcamResultDto> GetGradcamAsync(
            Stream fileStream,       // MRI image stream
            string fileName,         // original file name
            string contentType,      // image type (jpeg/png)
            CancellationToken ct = default)
        {
            // Create HTTP client configured for Python API
            var client = _httpClientFactory.CreateClient("GradCamClient");

            // Create multipart form data (because Python expects file upload)
            using var form = new MultipartFormDataContent();

            // Wrap the file stream into HTTP content
            using var fileContent = new StreamContent(fileStream);

            // Set the content type (default to jpeg if missing)
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    string.IsNullOrWhiteSpace(contentType)
                        ? "image/jpeg"
                        : contentType
                );

            // Add file to form with key "file" (must match Python endpoint parameter name)
            form.Add(fileContent, "file", fileName);

            // Send POST request to Python API (/predict endpoint)
            using var res = await client.PostAsync("predict", form, ct);

            // Read response body as string
            var json = await res.Content.ReadAsStringAsync(ct);

            // If Python failed → throw error with details
            if (!res.IsSuccessStatusCode)
                throw new Exception($"GradCAM python error: {res.StatusCode} -> {json}");

            // Convert JSON response into C# object
            var py = JsonSerializer.Deserialize<PythonGradcamResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;

            // Convert base64 string → actual image bytes
            var bytes = Convert.FromBase64String(py.gradcam_image_base64);

            // Define folder path where Grad-CAM images will be saved
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "gradcam");

            // Ensure folder exists (creates if missing)
            Directory.CreateDirectory(folder);

            // Generate unique file name (GUID to avoid collisions)
            var storedName = $"{Guid.NewGuid():N}.jpg";

            // Full path to save image
            var fullPath = Path.Combine(folder, storedName);

            // Save image bytes to disk
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            // Return result DTO (used by AiResultService)
            return new GradcamResultDto
            {
                Label = py.label,                 // predicted class
                Probabilities = py.probabilities, // confidence scores
                FileName = storedName             // saved file name (not exposed directly)
            };
        }
    }
}