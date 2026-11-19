using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Brainova.BLL.Exceptions
{
    /// <summary>
    /// Turns exceptions into the API's standard error body: { success: false, message }.
    ///
    /// ApiException subclasses (400/401/403/404) are expected outcomes, the client sent
    /// something wrong, so their message goes to the client and the log gets one
    /// Information line. Anything else is a bug: the full exception is logged as Error,
    /// and the client gets a generic message plus a traceId it can quote when reporting it.
    /// Exception details (message, type, stack trace) never reach the client.
    /// </summary>
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ApiException ex)
            {
                _logger.LogInformation(
                    "{Method} {Path} returned {StatusCode}: {Message}",
                    context.Request.Method, context.Request.Path, ex.StatusCode, ex.Message);

                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                // The traceId is in both the log line and the response, so a traceId from a
                // user's screenshot leads straight to this stack trace in the logs.
                _logger.LogError(ex,
                    "Unhandled exception on {Method} {Path} (traceId {TraceId})",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);

                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Unexpected server error.",
                    traceId = context.TraceIdentifier
                });
            }
        }
    }
}
