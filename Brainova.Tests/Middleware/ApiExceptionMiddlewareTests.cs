using System.Text.Json;
using Brainova.BLL.Exceptions;
using Brainova.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Brainova.Tests.Middleware
{
    /// <summary>
    /// ApiExceptionMiddleware turns exceptions thrown anywhere in the app
    /// into a JSON error response. These tests pin down the contract the
    /// frontend relies on: which exception → which HTTP status code, and the
    /// { success, message } body shape.
    ///
    /// How a middleware is tested: build a fake HttpContext, give the
    /// middleware a "next" delegate that throws, call Invoke, then read the
    /// status code and body back out of the context. No web server needed.
    /// </summary>
    public class ApiExceptionMiddlewareTests
    {
        public static IEnumerable<object[]> ApiExceptions => new List<object[]>
        {
            new object[] { new BadRequestException("bad input"), 400 },
            new object[] { new UnauthorizedException("log in first"), 401 },
            new object[] { new ForbiddenException("not allowed"), 403 },
            new object[] { new NotFoundException("no such thing"), 404 }
        };

        // [MemberData] is like [InlineData], but the rows come from a property,
        // which allows values (like exception objects) that an attribute can't hold.
        [Theory]
        [MemberData(nameof(ApiExceptions))]
        public async Task Invoke_WhenAnApiExceptionIsThrown_ReturnsItsStatusCodeAndMessage(
            Exception thrown, int expectedStatus)
        {
            var (status, body) = await RunAsync(_ => throw thrown);

            status.Should().Be(expectedStatus);
            body.GetProperty("success").GetBoolean().Should().BeFalse();
            body.GetProperty("message").GetString().Should().Be(thrown.Message);
        }

        [Fact]
        public async Task Invoke_WhenAnUnexpectedExceptionIsThrown_Returns500WithAGenericMessage()
        {
            var (status, body) = await RunAsync(_ => throw new InvalidOperationException("db exploded"));

            status.Should().Be(500);
            body.GetProperty("success").GetBoolean().Should().BeFalse();
            body.GetProperty("message").GetString().Should().Be("Unexpected server error.");
        }

        [Fact]
        public async Task Invoke_WhenAnUnexpectedExceptionIsThrown_NeverSendsExceptionDetailsToTheClient()
        {
            var (_, body, json) = await RunWithJsonAsync(
                _ => throw new InvalidOperationException("Login failed for user 'sa' on db-prod-01"));

            // The old debug fields are gone, and the exception text appears nowhere in the body.
            body.TryGetProperty("error", out _).Should().BeFalse();
            body.TryGetProperty("exceptionType", out _).Should().BeFalse();
            body.TryGetProperty("stack", out _).Should().BeFalse();
            json.Should().NotContain("db-prod-01");
            json.Should().NotContain("InvalidOperationException");
        }

        [Fact]
        public async Task Invoke_WhenAnUnexpectedExceptionIsThrown_ReturnsATraceIdThatIsAlsoInTheLog()
        {
            var logger = new CapturingLogger<ApiExceptionMiddleware>();

            var (_, body, _) = await RunWithJsonAsync(_ => throw new InvalidOperationException("boom"), logger);

            var traceId = body.GetProperty("traceId").GetString();
            traceId.Should().NotBeNullOrEmpty();
            logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error)
                  .Which.Message.Should().Contain(traceId);
        }

        [Fact]
        public async Task Invoke_WhenAnUnexpectedExceptionIsThrown_LogsTheFullExceptionAsError()
        {
            var logger = new CapturingLogger<ApiExceptionMiddleware>();
            var thrown = new InvalidOperationException("db exploded");

            await RunWithJsonAsync(_ => throw thrown, logger);

            // The details the client no longer sees must land in the server log instead.
            logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error)
                  .Which.Exception.Should().BeSameAs(thrown);
        }

        [Fact]
        public async Task Invoke_WhenAnApiExceptionIsThrown_LogsInformation_NotError()
        {
            var logger = new CapturingLogger<ApiExceptionMiddleware>();

            await RunWithJsonAsync(_ => throw new NotFoundException("no such case"), logger);

            // A 404 is the client's mistake, not a bug: no Error, and no stack trace.
            logger.Entries.Should().ContainSingle();
            logger.Entries[0].Level.Should().Be(LogLevel.Information);
            logger.Entries[0].Exception.Should().BeNull();
            logger.Entries[0].Message.Should().Contain("404").And.Contain("no such case");
        }

        [Fact]
        public async Task Invoke_WritesJsonContentType()
        {
            var context = NewContext();
            var sut = new ApiExceptionMiddleware(_ => throw new NotFoundException("x"),
                NullLogger<ApiExceptionMiddleware>.Instance);

            await sut.Invoke(context);

            context.Response.ContentType.Should().StartWith("application/json");
        }

        [Fact]
        public async Task Invoke_WhenNothingThrows_LeavesTheResponseAlone()
        {
            var context = NewContext();
            var nextWasCalled = false;
            var sut = new ApiExceptionMiddleware(ctx =>
            {
                nextWasCalled = true;
                ctx.Response.StatusCode = 204;
                return Task.CompletedTask;
            }, NullLogger<ApiExceptionMiddleware>.Instance);

            await sut.Invoke(context);

            nextWasCalled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(204);
            context.Response.Body.Length.Should().Be(0);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static DefaultHttpContext NewContext()
        {
            var context = new DefaultHttpContext();
            // The default body is a "null stream" that discards everything.
            // A MemoryStream lets the test read what the middleware wrote.
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<(int Status, JsonElement Body)> RunAsync(RequestDelegate next)
        {
            var (status, body, _) = await RunWithJsonAsync(next);
            return (status, body);
        }

        /// <summary>Runs the middleware and also returns the raw JSON text it wrote.</summary>
        private static async Task<(int Status, JsonElement Body, string Json)> RunWithJsonAsync(
            RequestDelegate next, ILogger<ApiExceptionMiddleware>? logger = null)
        {
            var context = NewContext();
            var sut = new ApiExceptionMiddleware(next, logger ?? NullLogger<ApiExceptionMiddleware>.Instance);

            await sut.Invoke(context);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var json = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);

            // Clone() so the element stays usable after the document is disposed.
            return (context.Response.StatusCode, doc.RootElement.Clone(), json);
        }
    }
}
