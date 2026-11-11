using System.Text.Json;
using Brainova.BLL.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
        public async Task Invoke_WritesJsonContentType()
        {
            var context = NewContext();
            var sut = new ApiExceptionMiddleware(_ => throw new NotFoundException("x"));

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
            });

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
            var context = NewContext();
            var sut = new ApiExceptionMiddleware(next);

            await sut.Invoke(context);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var json = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(json);

            // Clone() so the element stays usable after the document is disposed.
            return (context.Response.StatusCode, doc.RootElement.Clone());
        }
    }
}
