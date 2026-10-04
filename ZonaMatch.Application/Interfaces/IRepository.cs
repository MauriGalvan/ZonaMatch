namespace ZonaMatch.Application.Interfaces
{
    // Basic CRUD contract. Every entity of the zm schema has a text primary key.
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(string id);
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
    }
}
