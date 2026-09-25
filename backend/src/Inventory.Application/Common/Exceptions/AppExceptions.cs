using Inventory.Domain.Constants;

namespace Inventory.Application.Common.Exceptions;

public class NotFoundException(string resource, object key)
    : Exception(string.Format(ConstMessage.NotFound, resource, key));

public class ForbiddenException(string message = ConstMessage.Forbidden) : Exception(message);

public class UnauthorizedException(string message = ConstMessage.Unauthorized) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors) : base("Dữ liệu không hợp lệ.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }

    public IDictionary<string, string[]> Errors { get; }
}
