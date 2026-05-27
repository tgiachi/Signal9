namespace SignalNine.Persistence.Interfaces;

/// <summary>
/// Provides generic persistence operations for an entity type.
/// </summary>
/// <typeparam name="TEntity">The entity type handled by this data access object.</typeparam>
public interface IDataAccess<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    /// <param name="key">The primary key value.</param>
    /// <returns>The entity when found; otherwise, null.</returns>
    TEntity? GetByKey(object key);

    /// <summary>
    /// Lists all entities for the entity type.
    /// </summary>
    /// <returns>The persisted entities.</returns>
    IReadOnlyList<TEntity> List();

    /// <summary>
    /// Inserts an entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>The inserted entity.</returns>
    TEntity Insert(TEntity entity);

    /// <summary>
    /// Updates an existing entity using its primary key.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The affected row count.</returns>
    int Update(TEntity entity);

    /// <summary>
    /// Deletes an entity by its primary key.
    /// </summary>
    /// <param name="key">The primary key value.</param>
    /// <returns>The affected row count.</returns>
    int Delete(object key);
}
