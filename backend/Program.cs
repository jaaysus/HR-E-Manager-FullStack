using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
LoadLocalEnvironmentFile(builder.Configuration, Path.Combine(builder.Environment.ContentRootPath, ".env"));
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")));
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<HrETrackerDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<HrETrackerDbContext>();
var jwtSettings = builder.Configuration.GetSection("Authentication:Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
    throw new InvalidOperationException("Authentication:Jwt:SigningKey must be supplied through user secrets or an environment variable.");
if (Encoding.UTF8.GetByteCount(jwtSettings.SigningKey) < 32
    || string.IsNullOrWhiteSpace(jwtSettings.Issuer) || string.IsNullOrWhiteSpace(jwtSettings.Audience)
    || jwtSettings.AccessTokenMinutes is < 1 or > 60 || jwtSettings.RefreshTokenDays is < 1 or > 30)
    throw new InvalidOperationException("JWT configuration requires a signing key of at least 32 bytes, issuer, audience, access lifetime of 1-60 minutes and refresh lifetime of 1-30 days.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                if (context.Principal is null || !await auth.ValidateSessionAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.Fail("Session is no longer valid.");
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var grant in AccessPolicies.Permissions)
        options.AddPolicy(grant.Key, policy => policy.RequireAuthenticatedUser().RequireRole(grant.Value));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<StockWorkflow>();
builder.Services.AddScoped<ItemRuleService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<EmployeeImportService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IRequestCycleService, RequestCycleService>();
builder.Services.AddHostedService<RequestCycleWorker>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddHostedService<IdentityBootstrapService>();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddControllers(options => options.Filters.Add<HrETracker.Utils.WorkflowExceptionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
    options.OperationFilter<HrETracker.Utils.AnonymousSecurityOperationFilter>();
});
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.Run();

static void LoadLocalEnvironmentFile(ConfigurationManager configuration, string filePath)
{
    if (!File.Exists(filePath))
        return;

    foreach (var line in File.ReadLines(filePath))
    {
        var trimmedLine = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
            continue;

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
            continue;

        var key = trimmedLine[..separatorIndex].Trim().Replace("__", ":");
        var value = trimmedLine[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(configuration[key]))
            configuration[key] = value;
    }
}

public partial class Program;
