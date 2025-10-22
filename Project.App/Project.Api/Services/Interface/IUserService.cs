using Project.Api.Models;

namespace Project.Api.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();

        Task<User?> GetUserByIdAsync(Guid userId);

        Task<User?> GetUserByEmailAsync(string email);

        Task<User> CreateUserAsync(User user);

        Task<User> UpdateUserAsync(Guid userId, User user);

        Task<bool> DeleteUserAsync(Guid userId);

        Task<User> UpdateUserBalanceAsync(Guid userId, double newBalance);

        Task<User> UpsertGoogleUserByEmailAsync(string email, string? name, string? avatarUrl); //update + insert new google login to our db

        Task<User?> GetByEmailAsync(string email);
    }
}
