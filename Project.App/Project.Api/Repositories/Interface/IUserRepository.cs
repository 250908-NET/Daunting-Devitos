using System.Reflection.Metadata;
using Project.Api.Models;

namespace Project.Api.Repositories.Interface
{
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task UpdateBalanceAsync(User user);
        Task DeleteAsync(Guid id);
        Task SaveChangesAsync();
    }
}