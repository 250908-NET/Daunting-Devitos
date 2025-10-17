using Project.Api.Models;
using Project.Api.Repositories.Interface;

namespace Project.Api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository repo, ILogger<UserService> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                _logger.LogInformation("Getting all users");
                return await _repo.GetAllAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error getting all users: {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation($"Getting user {userId}");
                return await _repo.GetByIdAsync(userId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error getting user {userId}: {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                _logger.LogInformation($"Getting user by email {email}");
                return await _repo.GetByEmailAsync(email);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error getting user by email {email}: {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public async Task<User> CreateUserAsync(User user)
        {
            _logger.LogInformation("Creating a new user");
            await _repo.AddAsync(user);
            return user;
        }

        public async Task<User> UpdateUserAsync(Guid userId, User user)
        {
            try
            {
                _logger.LogInformation($"Updating user {userId}");
                user.Id = userId;
                await _repo.UpdateAsync(user);
                return user;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error updating user {userId}: {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation($"Deleting user {userId}");
                await _repo.DeleteAsync(userId);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error deleting user {userId}: {e.Message}");
                throw new Exception(e.Message);
            }
        }

        public async Task<User> UpdateUserBalanceAsync(Guid userId, double newBalance)
        {
            try
            {
                _logger.LogInformation($"Updating balance for user {userId} to {newBalance}");
                var user = await GetUserByIdAsync(userId);

                if (user == null)
                {
                    throw new KeyNotFoundException($"User {userId} not found");
                }

                user.Balance = newBalance;
                await _repo.UpdateAsync(user);
                return user;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error updating balance for user {userId}: {e.Message}");
                throw new Exception(e.Message);
            }
        }
    }
}
