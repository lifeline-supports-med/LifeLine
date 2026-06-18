using FluentValidation;
using LifeLine.Application.DTO.Auth;
using LifeLine.Application.DTO.Campaign;
using LifeLine.Application.DTO.Donation;
using LifeLine.Application.Helpers;
using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Application.Validators.Authentications;
using LifeLine.Application.Validators.Campaign;
using LifeLine.Application.Validators.Donation;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Settings.Cloudinary;
using LifeLine.Domain.Settings.MailKit;
using LifeLine.Domain.Settings.Paystack;
using LifeLine.Domain.SignalR;
using LifeLine.Persistence.Context;
using LifeLine.Persistence.Repositories;
using LifeLine.Persistence.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── CORS ─────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ─── Swagger ──────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LifeLine API",
        Version = "v1",
        Description = "Medical Emergency Fundraising Platform"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token. Example: Bearer eyJhbGci...",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Controllers (ONCE only) ──────────────────────────
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ProducesAttribute("application/json"));
});
builder.Services.AddSignalR();

// ─── File Upload Limits ───────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 20 * 1024 * 1024;
});

// ─── Database ─────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Identity ─────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@.-_+";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ─── JWT ──────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("JwtSettings");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
                                   Encoding.UTF8.GetBytes(jwt["SecretKey"]!)),
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// ─── Caching ──────────────────────────────────────────
builder.Services.AddMemoryCache(options =>
{
    options.CompactionPercentage = 0.25;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
});

// ─── Rate Limiting ────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = $"{retryAfter.TotalSeconds}";

            var problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();

            var problemDetails = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                "Too Many Requests",
                detail: $"Try again after {retryAfter.TotalSeconds} seconds."
            );

            await context.HttpContext.Response.WriteAsJsonAsync(
                problemDetails, cancellationToken: token);
        }
    };

    options.AddFixedWindowLimiter("fixed", cfg =>
    {
        cfg.PermitLimit = 100;
        cfg.Window = TimeSpan.FromMinutes(1);
    });

    options.AddPolicy("per-user", httpContext =>
    {
        string? userId = httpContext.User.FindFirstValue("userId");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return RateLimitPartition.GetTokenBucketLimiter(
                userId,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 5,
                    TokensPerPeriod = 2,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1)
                });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// ─── Paystack ─────────────────────────────────────────
builder.Services.Configure<PaystackSettings>(
    builder.Configuration.GetSection("Paystack"));
builder.Services.AddHttpClient("Paystack");

// ─── Cloudinary ───────────────────────────────────────
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// ─── Email ────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// ─── Services ─────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<JwtTokenHelper>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IDonationService, DonationService>();
builder.Services.AddScoped<IPayoutService, PayoutService>();

// ─── Repositories ─────────────────────────────────────
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IDonationRepository, DonationRepository>();
builder.Services.AddScoped<IPayoutRepository, PayoutRepository>();

// ─── Validators ───────────────────────────────────────
builder.Services.AddTransient<IValidator<LoginDto>, LoginDtoValidator>();
builder.Services.AddTransient<IValidator<RegisterDto>, RegisterDtoValidator>();
builder.Services.AddTransient<IValidator<ForgotPasswordDto>, ForgotPasswordDtoValidator>();
builder.Services.AddTransient<IValidator<ResetPasswordDto>, ResetPasswordDtoValidator>();
builder.Services.AddTransient<IValidator<ChangePasswordDto>, ChangePasswordDtoValidator>();
builder.Services.AddTransient<IValidator<RefreshTokenDto>, RefreshTokenDtoValidator>();
builder.Services.AddTransient<IValidator<CreateCampaignDto>, CreateCampaignDtoValidator>();
builder.Services.AddTransient<IValidator<UpdateCampaignDto>, UpdateCampaignDtoValidator>();
builder.Services.AddTransient<IValidator<InitiateDonationDto>, InitiateDonationDtoValidator>();

// ─── Logging ──────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// ─── Seed Roles + SuperAdmin ──────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    string[] roles =
    [
        "SuperAdmin", "VerificationAdmin",
        "CampaignCreator", "Donor", "Organization"
    ];

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ── SuperAdmin 1 ───────────────────────────────────
    var admin1Email = configuration["AdminSettings:SuperAdmin1Email"]!;
    if (await userManager.FindByEmailAsync(admin1Email) is null)
    {
        var admin1 = new ApplicationUser
        {
            FirstName = configuration["AdminSettings:SuperAdmin1FirstName"]!,
            LastName = configuration["AdminSettings:SuperAdmin1LastName"]!,
            Email = admin1Email,
            NormalizedEmail = admin1Email.ToUpper(),
            UserName = admin1Email,
            NormalizedUserName = admin1Email.ToUpper(),
            Role = "SuperAdmin",
            IsActive = true,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(
            admin1, configuration["AdminSettings:SuperAdmin1Password"]!);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin1, "SuperAdmin");
    }

    // ── SuperAdmin 2 ───────────────────────────────────
    var admin2Email = configuration["AdminSettings:SuperAdmin2Email"]!;
    if (await userManager.FindByEmailAsync(admin2Email) is null)
    {
        var admin2 = new ApplicationUser
        {
            FirstName = configuration["AdminSettings:SuperAdmin2FirstName"]!,
            LastName = configuration["AdminSettings:SuperAdmin2LastName"]!,
            Email = admin2Email,
            NormalizedEmail = admin2Email.ToUpper(),
            UserName = admin2Email,
            NormalizedUserName = admin2Email.ToUpper(),
            Role = "SuperAdmin",
            IsActive = true,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(
            admin2, configuration["AdminSettings:SuperAdmin2Password"]!);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin2, "SuperAdmin");
    }
}

// ─── Middleware Pipeline ──────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LifeLine v1.0");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<ChatBotSig>("/ChatBotSig");

app.Run();