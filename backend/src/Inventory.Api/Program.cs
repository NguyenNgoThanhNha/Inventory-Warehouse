using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Inventory.Api.Authorization;
using Inventory.Api.Controllers.V1;
using Inventory.Api.Infrastructure;
using Inventory.Application;
using Inventory.Application.Common.Interfaces;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Logging;
using Inventory.Infrastructure.Security;
using Inventory.Infrastructure.Seed;
using Inventory.Persistence;
using Inventory.Persistence.Interceptors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(Serilogger.Configure);

// ---------- Services ----------
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration,
    enableBackgroundJobs: builder.Configuration.GetValue("BackgroundJobs:Enabled", true));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuditUser, HttpAuditUser>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase; // key trong errors (kể cả lỗi binding tự động) luôn camelCase
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<InventoryDbContext>();

// Auth: JWT chỉ mang định danh; quyền kiểm tra qua [HasPermission] + IPermissionService.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key phải dài tối thiểu 32 ký tự (đặt qua user-secrets / biến môi trường).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // giữ nguyên "sub", "name", "email"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };
    });
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 20),
            Window = TimeSpan.FromMinutes(1)
        }));
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5174"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán access token (không cần chữ 'Bearer')"
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// ---------- Pipeline ----------
// Sau reverse proxy (nginx / Caddy): lấy IP + scheme thật từ X-Forwarded-* — trước ApiLogging để log đúng IP.
if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    forwarded.KnownIPNetworks.Clear(); // tin proxy trong mạng docker nội bộ; API không mở cổng ra ngoài
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

app.UseApiLogging();            // ngoài cùng → ghi được cả response lỗi do exception handler sinh ra
app.UseExceptionHandler();
app.UseStatusCodePages();       // 401/403/404 rỗng → ProblemDetails
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseUserLogContext();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var config = app.Configuration;
    if (!config.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment())) return;

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync(config.GetValue("Database:SeedDemoData", app.Environment.IsDevelopment()));
    await seeder.SeedBulkProductsAsync(config.GetValue("Database:SeedBulkProducts", 0));
}

public partial class Program;
