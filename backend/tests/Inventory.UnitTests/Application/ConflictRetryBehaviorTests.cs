using Inventory.Application.Common.Behaviors;
using Inventory.Application.Common.Interfaces;
using Inventory.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Inventory.UnitTests.Application;

public class ConflictRetryBehaviorTests
{
    private sealed record RetryableCommand : IRequest<int>, IRetryOnConflict;

    private sealed record PlainCommand : IRequest<int>;

    private static ConflictRetryBehavior<TRequest, int> Behavior<TRequest>(IUnitOfWork<InventoryDbContext> unitOfWork)
        where TRequest : notnull =>
        new(unitOfWork, NullLogger<ConflictRetryBehavior<TRequest, int>>.Instance);

    [Fact]
    public async Task Retries_on_concurrency_conflict_and_clears_tracker_before_each_retry()
    {
        var unitOfWork = Substitute.For<IUnitOfWork<InventoryDbContext>>();
        var calls = 0;

        var result = await Behavior<RetryableCommand>(unitOfWork).Handle(new RetryableCommand(), _ =>
        {
            if (++calls < 3) throw new DbUpdateConcurrencyException("conflict");
            return Task.FromResult(42);
        }, default);

        Assert.Equal((42, 3), (result, calls));
        unitOfWork.Received(2).ClearChangeTracker();
    }

    [Fact]
    public async Task Gives_up_after_max_attempts()
    {
        var calls = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            Behavior<RetryableCommand>(Substitute.For<IUnitOfWork<InventoryDbContext>>()).Handle(new RetryableCommand(), _ =>
            {
                calls++;
                throw new DbUpdateConcurrencyException("conflict");
            }, default));

        Assert.Equal(ConflictRetryBehavior<RetryableCommand, int>.MaxAttempts, calls);
    }

    [Fact]
    public async Task Does_not_retry_requests_without_marker()
    {
        var calls = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            Behavior<PlainCommand>(Substitute.For<IUnitOfWork<InventoryDbContext>>()).Handle(new PlainCommand(), _ =>
            {
                calls++;
                throw new DbUpdateConcurrencyException("conflict");
            }, default));

        Assert.Equal(1, calls);
    }
}
