using FluentValidation;
using Inventory.Application.Common.Behaviors;
using Inventory.Application.Common.Models;
using Inventory.Application.Features.V1.Auth.Services;
using Inventory.Application.Features.V1.Stock.Services;
using Inventory.Application.Features.V1.StockDocuments.Services;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ConflictRetryBehavior<,>)); // trong cùng: chỉ bọc handler, validate một lần
        });
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        TypeAdapterConfig.GlobalSettings.Scan(assembly);

        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        services.AddScoped<IAuthTokenIssuer, AuthTokenIssuer>();
        services.AddScoped<ICurrentUserDtoFactory, CurrentUserDtoFactory>();

        // Kho: mọi thay đổi tồn đi qua IStockLedger.
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IDocumentNumberGenerator, DocumentNumberGenerator>();
        services.AddScoped<IStockDocumentPoster, StockDocumentPoster>();
        services.AddScoped<IStockDocumentWriter, StockDocumentWriter>();
        services.AddScoped<IStockDocumentReader, StockDocumentReader>();

        return services;
    }
}
