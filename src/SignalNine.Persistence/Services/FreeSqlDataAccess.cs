using SignalNine.Persistence.Interfaces;

namespace SignalNine.Persistence.Services;

public class FreeSqlDataAccess<TEntity> : IDataAccess<TEntity> where TEntity : class
{
    private readonly IFreeSql _freeSql;

    public FreeSqlDataAccess(IFreeSql freeSql)
    {
        ArgumentNullException.ThrowIfNull(freeSql);

        _freeSql = freeSql;
    }

    public TEntity? GetByKey(object key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _freeSql.Select<TEntity>().WhereDynamic(key).First();
    }

    public IReadOnlyList<TEntity> List()
        => _freeSql.Select<TEntity>().ToList();

    public TEntity Insert(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        _freeSql.Insert(entity).ExecuteAffrows();

        return entity;
    }

    public int Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return _freeSql.Update<TEntity>().SetSource(entity).ExecuteAffrows();
    }

    public int Delete(object key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _freeSql.Delete<TEntity>().WhereDynamic(key).ExecuteAffrows();
    }
}
