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
            public string label { get; set; } = default!;// predicted class (glioma, etc.)
            public float[] probabilities { get; set; } = Array.Empty<float>();// confidence values
            public string gradcam_image_base64 { get; set; } = default!;// image encoded as base64 string
        }

        private sealed class PythonErrorResponse
        {
            public bool success { get; set; }
            public string? message { get; set; }
        }

        public async Task<GradcamResultDto> GetGradcamAsync(
            Stream fileStream,// MRI image stream
            string fileName,// original file name
            string contentType,// image type (jpeg/png)
            CancellationToken ct = default)
        {  // Create HTTP client configured for Python API
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
            //send POST request to Python API and get response
            using var res = await client.PostAsync("predict", form, ct);
            //convert response content to string (JSON)
            var json = await res.Content.ReadAsStringAsync(ct);
            // If response indicates failure, try to extract error message from JSON and throw exception
            if (!res.IsSuccessStatusCode)
            {
                string message = "GradCAM python error.";

                try
                {
                    
                    var pyError = JsonSerializer.Deserialize<PythonErrorResponse>(
                        json,
                        
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    // If Python returned a structured error message, use it

                    if (!string.IsNullOrWhiteSpace(pyError?.message))
                        message = pyError.message!;
                }
                catch
                {
                    // If JSON parsing fails, fallback to raw response content as error message
                    if (!string.IsNullOrWhiteSpace(json))
                        message = json;
                }
                // If it's a bad request, throw a specific exception type for better error handling in the application
                if (res.StatusCode == HttpStatusCode.BadRequest)
                    throw new BadRequestException(message);

                throw new Exception($"GradCAM python error: {(int)res.StatusCode} -> {message}");
            }
            // If response is successful, parse the JSON into our PythonGradcamResponse object
            var py = JsonSerializer.Deserialize<PythonGradcamResponse>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;
            // Decode the base64 image string into bytes
            var bytes = Convert.FromBase64String(py.gradcam_image_base64);
            // Create a directory to store the generated GradCAM images if it doesn't exist
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "gradcam");
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}.jpg";
            var fullPath = Path.Combine(folder, storedName);
            // Save the decoded image bytes to a file on disk
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