using System.Net;
using System.Text;
using Brainova.BLL.Exceptions;
using Brainova.BLL.Services.Classes;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Moq;
using Xunit;

namespace Brainova.Tests.Services
{
    /// <summary>
    /// AiTumorService talks to the Python Grad-CAM API over HTTP.
    ///
    /// NEW CONCEPT: you cannot mock HttpClient itself (its methods are not
    /// virtual). What you replace is the HttpMessageHandler INSIDE it — the
    /// object that actually sends the request. StubHandler below is a fake
    /// handler that returns whatever response the test tells it to, so no
    /// network call ever happens.
    /// </summary>
    public class AiTumorServiceTests : IDisposable
    {
        private readonly TempContentRoot _root = new();
        private readonly Mock<IHttpClientFactory> _httpFactory = new();
        private readonly StubHandler _handler = new();
        private readonly AiTumorService _sut;

        public AiTumorServiceTests()
        {
            // The service asks the factory for the named client "GradCamClient".
            // We hand it a client that uses our fake handler.
            var client = new HttpClient(_handler) { BaseAddress = new Uri("http://gradcam.test/") };
            _httpFactory.Setup(f => f.CreateClient("GradCamClient")).Returns(client);

            _sut = new AiTumorService(_root.Env.Object, _httpFactory.Object);
        }

        public void Dispose() => _root.Dispose();

        [Fact]
        public async Task GetGradcamAsync_OnSuccess_ReturnsLabelAndProbabilities_AndSavesTheImage()
        {
            var imageBytes = new byte[] { 10, 20, 30, 40 };
            _handler.Respond(HttpStatusCode.OK,
                "{\"label\":\"glioma\",\"probabilities\":[0.9,0.05,0.03,0.02]," +
                "\"gradcam_image_base64\":\"" + Convert.ToBase64String(imageBytes) + "\"}");

            var result = await CallAsync();

            result.Label.Should().Be("glioma");
            result.Probabilities.Should().Equal(0.9f, 0.05f, 0.03f, 0.02f);

            // The base64 image was decoded and written to App_Data/gradcam.
            result.FileName.Should().EndWith(".jpg");
            var savedPath = Path.Combine(_root.AppDataFolder("gradcam"), result.FileName);
            File.Exists(savedPath).Should().BeTrue();
            File.ReadAllBytes(savedPath).Should().Equal(imageBytes);
        }

        [Fact]
        public async Task GetGradcamAsync_PostsTheFileAsMultipartToThePredictEndpoint()
        {
            _handler.Respond(HttpStatusCode.OK,
                "{\"label\":\"notumor\",\"probabilities\":[0,0,1,0],\"gradcam_image_base64\":\"AQID\"}");

            await CallAsync();

            _handler.LastMethod.Should().Be(HttpMethod.Post);
            _handler.LastUri!.AbsolutePath.Should().Be("/predict");
            _handler.LastContentType.Should().Be("multipart/form-data");
        }

        // Python returns 400 when the upload is not a valid MRI. That must
        // surface as OUR BadRequestException (→ HTTP 400 for the frontend),
        // carrying Python's own message.
        [Fact]
        public async Task GetGradcamAsync_WhenPythonReturns400_ThrowsBadRequestWithPythonMessage()
        {
            _handler.Respond(HttpStatusCode.BadRequest,
                "{\"success\":false,\"message\":\"Image is not a brain MRI\"}");

            var act = () => CallAsync();

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("Image is not a brain MRI");
        }

        [Fact]
        public async Task GetGradcamAsync_WhenPythonReturns400WithEmptyBody_UsesDefaultMessage()
        {
            _handler.Respond(HttpStatusCode.BadRequest, "");

            var act = () => CallAsync();

            await act.Should().ThrowAsync<BadRequestException>()
                     .WithMessage("GradCAM python error.");
        }

        [Fact]
        public async Task GetGradcamAsync_WhenPythonReturns500WithJsonMessage_ThrowsWithStatusAndMessage()
        {
            _handler.Respond(HttpStatusCode.InternalServerError,
                "{\"success\":false,\"message\":\"model not loaded\"}");

            var act = () => CallAsync();

            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("GradCAM python error: 500 -> model not loaded");
        }

        // When the error body is not JSON (e.g. an HTML error page from the
        // host), the raw text is used as the message.
        [Fact]
        public async Task GetGradcamAsync_WhenErrorBodyIsNotJson_UsesRawBodyAsMessage()
        {
            _handler.Respond(HttpStatusCode.BadGateway, "upstream crashed");

            var act = () => CallAsync();

            await act.Should().ThrowAsync<Exception>()
                     .WithMessage("GradCAM python error: 502 -> upstream crashed");
        }

        [Fact]
        public async Task GetGradcamAsync_WhenPythonFails_DoesNotWriteAnyImage()
        {
            _handler.Respond(HttpStatusCode.InternalServerError, "boom");

            var act = () => CallAsync();

            await act.Should().ThrowAsync<Exception>();
            Directory.Exists(_root.AppDataFolder("gradcam")).Should().BeFalse();
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private Task<Brainova.BLL.DTOs.Response.GradcamResultDto> CallAsync()
        {
            var upload = new MemoryStream(new byte[] { 1, 2, 3 });
            return _sut.GetGradcamAsync(upload, "scan.jpg", "image/jpeg");
        }

        /// <summary>
        /// Fake HttpMessageHandler: remembers the request it was given and
        /// returns the canned response set with Respond(...).
        /// </summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private HttpStatusCode _status = HttpStatusCode.OK;
            private string _body = "{}";

            public HttpMethod? LastMethod { get; private set; }
            public Uri? LastUri { get; private set; }
            public string? LastContentType { get; private set; }

            public void Respond(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Capture here: the service disposes the request content after the call.
                LastMethod = request.Method;
                LastUri = request.RequestUri;
                LastContentType = request.Content?.Headers.ContentType?.MediaType;

                return Task.FromResult(new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
