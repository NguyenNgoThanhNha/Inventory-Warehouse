using FluentValidation;
using MediatR;

namespace Inventory.Application.Common.Behaviors;

/// <summary>Chạy toàn bộ FluentValidation validator của request trước khi vào handler.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var errors = new Dictionary<string, List<string>>();
        foreach (var validator in validators) // tuần tự: validator có thể dùng chung DbContext (không thread-safe)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            foreach (var failure in result.Errors)
            {
                var key = ToCamelCase(failure.PropertyName);
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
                if (!list.Contains(failure.ErrorMessage)) list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0) throw new ValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));

        return await next(cancellationToken);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
