using System.Collections;
using System.Data;
using Inventory.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure.Commons;

/// <summary>
/// UnitOfWork generic theo DbContext (chuẩn BE §5). Đăng ký open generic, Scoped:
/// services.AddScoped(typeof(IUnitOfWork&lt;&gt;), typeof(UnitOfWork&lt;&gt;)).
/// Helper gọi stored procedure giữ cách dùng của Backend_Api_Template.
/// </summary>
public sealed class UnitOfWork<TContext>(TContext context, IServiceProvider serviceProvider) : IUnitOfWork<TContext>
    where TContext : DbContext
{
    private const int CommandTimeoutSeconds = 300;

    public DbSet<T> Repository<T>() where T : class => context.Set<T>();

    public IRepository<TEntity, TContext> GetRepository<TEntity>() where TEntity : class =>
        serviceProvider.GetService<IRepository<TEntity, TContext>>() ?? new Repository<TEntity, TContext>(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public void ClearChangeTracker() => context.ChangeTracker.Clear();

    public Task<List<T>> RawSqlQueryAsync<T>(string querySql, params SqlParameter[] parameters) =>
        context.Database.SqlQueryRaw<T>(querySql, parameters.Cast<object>().ToArray()).ToListAsync();

    public DataSet ExecuteStoreProcedureGetMultiTables(string storeProcedure, Hashtable data)
    {
        var dataSet = new DataSet();
        var connection = (SqlConnection)context.Database.GetDbConnection();
        using var command = new SqlCommand(storeProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = CommandTimeoutSeconds,
            Transaction = context.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction
        };

        foreach (DictionaryEntry parameter in data)
            command.Parameters.Add(new SqlParameter(parameter.Key.ToString(), parameter.Value ?? DBNull.Value));

        using var adapter = new SqlDataAdapter(command);
        adapter.Fill(dataSet); // SqlDataAdapter tự mở/đóng connection nếu đang đóng
        return dataSet;
    }
}

public sealed class Repository<TEntity, TContext>(TContext context) : IRepository<TEntity, TContext>
    where TEntity : class
    where TContext : DbContext
{
    private DbSet<TEntity> Set => context.Set<TEntity>();

    public IQueryable<TEntity> Query(bool asNoTracking = false) => asNoTracking ? Set.AsNoTracking() : Set;

    public ValueTask<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default) =>
        Set.FindAsync([id], cancellationToken);

    public void Add(TEntity entity) => Set.Add(entity);

    public void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public void RemoveRange(IEnumerable<TEntity> entities) => Set.RemoveRange(entities);
}
