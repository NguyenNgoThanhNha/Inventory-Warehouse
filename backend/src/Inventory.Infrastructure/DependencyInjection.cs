using Inventory.Application.Common.Interfaces;
using Inventory.Infrastructure.Caching;
using Inventory.Infrastructure.Commons;
using Inventory.Infrastructure.Logging;
using Inventory.Infrastructure.Security;
using Inventory.Infrastructure.Seed;
using Inventory.Infrastructure.Services;
using Inventory.Persistence;
using Inventory.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        bool enableBackgroundJobs = true)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // --- Data: DbContext + audit interceptor + UnitOfWork (open generic, Scoped — chuẩn BE §5) ---
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Default"),
                    sql => sql.MigrationsAssembly(typeof(InventoryDbContext).Assembly.FullName))
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

        // --- Cache phân tán: Redis nếu có ConnectionStrings:Redis, không thì bộ nhớ trong tiến trình (dev / test) ---
        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                var cfg = ConfigurationOptions.Parse(redis);
                cfg.AbortOnConnectFail = false; // Redis chưa lên thì app vẫn chạy, cache tự nối lại sau
                cfg.ConnectTimeout = 2000;
                cfg.AsyncTimeout = 500;         // cache chậm hơn DB thì vô nghĩa — timeout ngắn rồi đọc DB
                cfg.SyncTimeout = 500;
                o.ConfigurationOptions = cfg;
                o.InstanceName = "inventory:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        services.AddSingleton<ICacheService, DistributedCacheService>();

        // --- Security ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<PermissionCacheVersion>();
        services.AddScoped<IPermissionService, PermissionService>();

        // --- External services ---
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddTransient<LoggingDelegatingHandler>();

        // --- API logging (chuẩn BE §9.2) ---
        services.Configure<ApiLoggingOptions>(configuration.GetSection(ApiLoggingOptions.SectionName));
        services.AddSingleton<ApiLogQueue>();

        services.AddScoped<DataSeeder>();

        if (enableBackgroundJobs)
        {
            services.AddHostedService<ApiLogWriterService>();
            services.AddHostedService<ApiLogCleanupService>();
        }

        return services;
    }
}
