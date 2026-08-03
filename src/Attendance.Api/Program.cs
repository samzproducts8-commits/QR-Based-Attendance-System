using System.Text;
using Attendance.Api.BackgroundServices;
using Attendance.Api.Hubs;
using Attendance.Api.Middleware;
using Attendance.Api.Services;
using Attendance.Application.Interfaces;
using Attendance.Application.Services;
using Attendance.Infrastructure.Data;
using Attendance.Infrastructure.Helpers;
using Attendance.Infrastructure.Models;
using Attendance.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog (Requirement 7.1 / task 22) ────────────────────────────────────
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("logs", "attendance-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

// ─── EF Core + Identity ─────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = false; // usernames are EMP-XXXX codes
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ─── JWT bearer authentication (Requirements 5.1, 5.6) ─────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
    jwtSection["SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR WebSockets cannot send an Authorization header — the kiosk
        // passes the JWT as ?access_token=… on the hub handshake (Requirement 6.1).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── CORS (Requirement 5.6) ─────────────────────────────────────────────────
// Cors:AllowedOrigins lists the allowed SPA origins. A "*" entry (the dev
// default) reflects any origin instead — needed on a home network where the
// router reassigns the PC's IP and the SPA is reached via localhost, the LAN
// IP, or a phone. Production should list exact origins and remove "*".
// (SetIsOriginAllowed is the credentials-compatible form of "any origin";
// AllowAnyOrigin() + AllowCredentials() is rejected by ASP.NET Core.)
const string CorsPolicy = "AngularClient";
string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["*"];

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    if (allowedOrigins.Contains("*"))
        policy.SetIsOriginAllowed(_ => true);
    else
        policy.WithOrigins(allowedOrigins);

    policy.AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();   // required for the SignalR negotiate handshake
}));

// ─── Application services + Infrastructure repositories (DI) ───────────────
builder.Services.AddScoped<IQrSessionRepository, QrSessionRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IStaffRepository, StaffRepository>();
builder.Services.AddScoped<ISlotConfigRepository, SlotConfigRepository>();

builder.Services.AddScoped<IFileStorageHelper, LocalFileStorageHelper>();
builder.Services.AddSingleton<IReportExporter, ReportExportHelper>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IAttendanceHubContext, AttendanceHubContext>();

// The QR deep link must carry a host phones can reach — never "localhost".
// "auto" (the default) detects this machine's current LAN IP at startup, so
// DHCP reassignments and switching networks (venue Wi-Fi, phone hotspot)
// require zero config edits; an explicit URL in config still wins.
string? scanBaseUrl = builder.Configuration["Kiosk:ScanBaseUrl"];
if (string.IsNullOrWhiteSpace(scanBaseUrl)
    || scanBaseUrl.Equals("auto", StringComparison.OrdinalIgnoreCase))
{
    string scanHost = NetworkHelper.DetectLanIpv4() ?? "localhost";
    int scanPort = builder.Configuration.GetValue("Kiosk:ScanPort", 4200);
    scanBaseUrl = $"http://{scanHost}:{scanPort}/scan";
}

string employeeIdBaseUrl = Attendance.Api.Controllers.StaffController.BuildEmployeeIdBaseUrl(builder.Configuration);

builder.Services.AddScoped<IQrSessionService>(sp => new QrSessionService(
    sp.GetRequiredService<IQrSessionRepository>(),
    sp.GetRequiredService<IAttendanceHubContext>(),
    TimeSpan.FromSeconds(builder.Configuration.GetValue("QrToken:ExpirySeconds", 15)),
    scanBaseUrl));
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<ISlotConfigService, SlotConfigService>();

// Background sweep for stale tokens (Requirement 3.7 / task 6.3).
builder.Services.AddHostedService<QrTokenExpiryBackgroundService>();

// ─── MVC, SignalR, Swagger ──────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QR Attendance API",
        Version = "v1",
        Description = "QR-Based Employee Attendance Management System"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT from POST /api/auth/login."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>()
    });

    // Surface controller/action XML doc comments in the Swagger UI (task 23.3).
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ─── Pipeline ───────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();   // outermost: consistent ProblemDetails
app.UseSerilogRequestLogging();                     // method, path, status, duration (task 22)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();                               // serves wwwroot/staff-photos/…
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AttendanceHub>("/hubs/attendance");      // Requirement 6.1

// ─── Migrate + seed (slots, roles, admin user) ──────────────────────────────
await SeedData.SeedAsync(app.Services);

app.Logger.LogInformation(
    "Kiosk QR deep-link base: {ScanBaseUrl} — phones must be able to reach this host.",
    scanBaseUrl);
app.Logger.LogInformation(
    "Public Employee ID QR base URL: {EmployeeIdBaseUrl} — generated QR codes will resolve to this address.",
    employeeIdBaseUrl);

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests (task 21).</summary>
public partial class Program;
