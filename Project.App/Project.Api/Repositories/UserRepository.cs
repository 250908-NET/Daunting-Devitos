using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;

namespace Project.Api.Repositories.Interface
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context; // rename AppDbContext later

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        // Get all users
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.Users.ToListAsync();
        }

        //  Get user by ID
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }

        //  Get user by email
        public async Task<User?> GetByEmailAsync(string email)
        {
            var normalized = email.Trim().ToLower(); //normalize emails to lowercase
            return await _context
                .Users.AsNoTracking() //we only need to read this
                .FirstOrDefaultAsync(u => u.Email == normalized);
        }

        // Add new user
        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        // Update existing user
        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        // Delete user by ID
        public async Task DeleteAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
            }
            await _context.SaveChangesAsync();
        }

        //  Save changes
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
