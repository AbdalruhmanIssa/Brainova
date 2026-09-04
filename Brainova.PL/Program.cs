using Brainova.BLL.Exceptions;
using Brainova.BLL.Hubs;
using Brainova.BLL.Services.Classes;
using Brainova.BLL.Services.Interface;
using Brainova.DAL.Data;
using Brainova.DAL.Modles;
using Brainova.DAL.Repositories.Classes;
using Brainova.DAL.Repositories.Interface;
using Brainova.DAL.Utilites;
using Brainova.DAL.Utilities;
using Brainova.PL.uti;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ML.OnnxRuntime;
using Scalar.AspNetCore;
using Mapster;
using Brainova.PL.Middlewares;

using Brainova.BLL.Mapping;

var builder = WebApplication.CreateBuilder(args);
TypeAdapterConfig.GlobalSettings.Scan(typeof(MapsterConfig).Assembly);

// Configure for cloud deployment (OnRender, Azure App Service, Docker, etc.)
// Only apply this in non-Development so launchSettings.json (and Scalar) work locally.
if (!builder.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ==============================
// Forwarded headers — REQUIRED when running behind a TLS-terminating proxy
// (Render, Azure App Service, Nginx, Cloudflare, etc.).
// Without this, ASP.NET sees Request.Scheme = "http" even though the browser
// is on HTTPS. That breaks `Secure` cookies, HttpsRedirection, and is the
// #1 cause of iOS Safari refusing to store the auth cookie after login.
// ==============================
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    // We are deployed on a managed PaaS where the proxy IP isn't known/static.
    // Clearing these lets ASP.NET trust the forwarded headers regardless of source.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});



builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IEmailSender, EmailSetting>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IMriCaseService, MriCaseService>();
builder.Services.AddScoped<IAiResultService, AiResultService>();
builder.Services.AddScoped<IReportQuestionService, ReportQuestionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
builder.Services.AddScoped<ISeedData, SeedData>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// ==============================
// SignalR (real-time notifications)
// ==============================
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Maps the JWT "Id" claim -> SignalR user id, so Clients.User(userId) works.
builder.Services.AddSingleton<IUserIdProvider, JwtIdUserIdProvider>();
// Register ONNX session as Singleton (heavy object)
//builder.Services.AddSingleton(sp =>
//{
//    var env = sp.GetRequiredService<IWebHostEnvironment>();
//    var modelPath = Path.Combine(env.WebRootPath, "Models", "brainova_effnetb1.onnx");
//    return new InferenceSession(modelPath);
//});

// Register service as Scoped (your normal style)
// 1) ONNX session as singleton


// 2) HttpClient for Python (named client)
builder.Services.AddScoped<IAiTumorService, AiTumorService>();

builder.Services.AddHttpClient("GradCamClient", client =>
{
    client.BaseAddress = new Uri("https://brainova-ai-1031567223264.europe-west1.run.app/");
    client.Timeout = TimeSpan.FromMinutes(5);
});

// 3) CORS Configuration
// NOTE: SignalR WebSockets require AllowCredentials(), and AllowCredentials
// is not allowed together with AllowAnyOrigin(). Using SetIsOriginAllowed
// keeps the "allow any origin" behavior while still permitting credentials.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});
// Database configuration - environment variables first, then user-secrets / appsettings
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "No database connection string is configured. " +
        "Local development: dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\". " +
        "Production: set the DB_CONNECTION_STRING (or ConnectionStrings__Default) environment variable. " +
        "See the Configuration section of README.md.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// ==============================
// Identity
// ==============================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;

        options.User.RequireUniqueEmail = true;

        // you�re using confirm email flow
        options.SignIn.RequireConfirmedEmail = true;

        // lockout (still useful for brute-force even if you also have IsBlocked)
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ==============================
// JWT Auth (Base64 Secret + RoleClaimType = "Role")
// ==============================
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("jwtOptions");

        var secretBase64 = jwt["SecretKey"];
        if (string.IsNullOrWhiteSpace(secretBase64))
            throw new InvalidOperationException(
                "No JWT signing key is configured. " +
                "Local development: dotnet user-secrets set \"jwtOptions:SecretKey\" \"<base64 key>\". " +
                "Production: set the jwtOptions__SecretKey environment variable. " +
                "Generate one with: openssl rand -base64 32. " +
                "See the Configuration section of README.md.");

        var keyBytes = Convert.FromBase64String(secretBase64);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            // If you don�t want issuer/audience, set ValidateIssuer/Audience = false
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwt["Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            // IMPORTANT: because you add roles as new Claim("Role", role)
            RoleClaimType = "Role"
        };

        // Token resolution order:
        //   1) Authorization: Bearer ... header (default JwtBearer behavior)
        //   2) For SignalR hub paths only, the ?access_token=... query string
        //   3) HttpOnly "access_token" cookie set by /cookie-login
        // If none are present the default behavior applies (401).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;

                // SignalR: WebSocket upgrade can't set Authorization header
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                    return Task.CompletedTask;
                }

                // Cookie fallback: only used when no Authorization header was sent.
                if (string.IsNullOrEmpty(context.Token))
                {
                    var cookieToken = context.Request.Cookies["access_token"];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ==============================
// Antiforgery (CSRF) — only enforced for cookie-authenticated requests.
// Frontend reads the XSRF-TOKEN cookie and echoes it as the X-XSRF-TOKEN header.
// ==============================
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // must be readable by the SPA's JS
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.None
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();
// Add services to the container.

builder.Services.AddControllers();

// ==============================
// Model-validation errors (DataAnnotations like [Required], [EmailAddress], etc.)
// run BEFORE the controller/exception middleware, so by default ASP.NET returns
// its RFC ProblemDetails shape. Override it to match our standard error body:
//   { "success": false, "message": "..." }
// We surface the first validation message so the frontend can show it directly.
// ==============================
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? "Invalid request.";

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
        {
            success = false,
            message
        });
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// MUST be the first middleware so all subsequent middleware sees the correct
// scheme/host/IP forwarded by the upstream proxy (HTTPS, real client IP, etc.).
app.UseForwardedHeaders();

app.UseCors("AllowFrontend");

app.UseMiddleware<Brainova.BLL.Exceptions.ApiExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
using (var scope = app.Services.CreateScope())
{
    var seed = scope.ServiceProvider.GetRequiredService<ISeedData>();
    await seed.DataSeedingAsync();                // migrate
    await seed.IdentitySeedingAsync();   // roles + users
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();


// HTTPS redirection intentionally NOT enabled in production:
// the platform proxy (Render/Azure/etc.) terminates TLS and forwards plain HTTP
// to our container on $PORT. Calling UseHttpsRedirection here would issue a 307
// from inside the proxy, which iOS Safari treats inconsistently with
// SameSite=None cookies (the auth cookie can be dropped on the redirect hop).
// TLS enforcement should be done at the proxy level.

// ==============================
// CSRF middleware DISABLED for now. The double-submit cookie pattern doesn't work
// cleanly cross-origin (frontend can't read the XSRF-TOKEN cookie set by the API
// domain), and we've decided to postpone CSRF hardening. Re-enable later by
// uncommenting this block.
// ==============================
//var antiforgery = app.Services.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
//app.Use(async (context, next) =>
//{
//    var method = context.Request.Method;
//    var isStateChanging = HttpMethods.IsPost(method)
//                          || HttpMethods.IsPut(method)
//                          || HttpMethods.IsPatch(method)
//                          || HttpMethods.IsDelete(method);
//
//    var usedCookieAuth = context.Request.Cookies.ContainsKey("access_token")
//                         && !context.Request.Headers.ContainsKey("Authorization");
//
//    var path = context.Request.Path.Value ?? string.Empty;
//    var isAuthEndpoint = path.StartsWith("/api/Identity/Auths/cookie-login", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/login", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/logout", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/csrf-token", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/register-student", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/forgot-password", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/reset-password", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/set-password", StringComparison.OrdinalIgnoreCase)
//                         || path.StartsWith("/api/Identity/Auths/confirm-email", StringComparison.OrdinalIgnoreCase);
//
//    if (isStateChanging && usedCookieAuth && !isAuthEndpoint)
//    {
//        try
//        {
//            await antiforgery.ValidateRequestAsync(context);
//        }
//        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
//        {
//            context.Response.StatusCode = StatusCodes.Status400BadRequest;
//            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing CSRF token (X-XSRF-TOKEN)." });
//            return;
//        }
//    }
//
//    await next();
//});

app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
