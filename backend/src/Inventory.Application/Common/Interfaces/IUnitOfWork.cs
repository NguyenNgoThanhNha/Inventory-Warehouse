using System.Collections;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Common.Interfaces;

/// <summary>
/// Cửa truy cập dữ liệu DUY NHẤT cho handler (chuẩn BE §5). Handler inject IUnitOfWork&lt;InventoryDbContext&gt;,
/// không inject DbContext trực tiếp. Gọi <see cref="SaveChangesAsync"/> một lần ở cuối mỗi handler ghi.
/// </summary>
public interface IUnitOfWork<TContext> where TContext : DbContext
{
    /// <summary>DbSet để query LINQ / Add / Remove.</summary>
    DbSet<T> Repository<T>() where T : class;

    /// <summary>Generic repository cho CRUD cơ bản.</summary>
    IRepository<TEntity, TContext> GetRepository<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Bỏ mọi entity đang theo dõi (chưa lưu). Chỉ dùng khi thử lại cả use case sau xung đột — xem ConflictRetryBehavior.</summary>
    void ClearChangeTracker();

    /// <summary>Raw SQL trả về một bảng, map theo tên cột.</summary>
    Task<List<T>> RawSqlQueryAsync<T>(string querySql, params SqlParameter[] parameters);

    /// <summary>Gọi stored procedure trả nhiều bảng. Key Hashtable có tiền tố '@', tên SP dạng [dbo].[usp_X].</summary>
    DataSet ExecuteStoreProcedureGetMultiTables(string storeProcedure, Hashtable data);
}

public interface IRepository<TEntity, TContext>
    where TEntity : class
    where TContext : DbContext
{
    IQueryable<TEntity> Query(bool asNoTracking = false);
    ValueTask<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Update(TEntity entity);

    /// <summary>Với BaseEntity sẽ thành xóa mềm (AuditSaveChangesInterceptor).</summary>
    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);
}
