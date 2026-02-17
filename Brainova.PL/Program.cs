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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IEmailSender, EmailSetting>();

builder.Services.AddScoped<ISeedData, SeedData>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

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

        // you’re using confirm email flow
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

        var secretBase64 = jwt["SecretKey"]!;
        var keyBytes = Convert.FromBase64String(secretBase64);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            // If you don’t want issuer/audience, set ValidateIssuer/Audience = false
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwt["Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            // IMPORTANT: because you add roles as new Claim("Role", role)
            RoleClaimType = "Role"
        };
    });

builder.Services.AddAuthorization();


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
