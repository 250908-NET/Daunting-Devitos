using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories.Interface;

namespace Project.Api.Repositories
{
    public class RoomRepository : IRoomRepository
    {
        private readonly AppDbContext _context;

        public RoomRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Room?> GetByIdAsync(Guid id)
        {
            return await _context
                .Rooms.Include(r => r.Host)
                .Include(r => r.RoomPlayers)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<IEnumerable<Room>> GetAllAsync()
        {
            return await _context
                .Rooms.Include(r => r.Host)
                .Include(r => r.RoomPlayers)
                .ToListAsync();
        }

        public async Task<IEnumerable<Room>> GetActiveRoomsAsync()
        {
            return await _context
                .Rooms.Include(r => r.Host)
                .Include(r => r.RoomPlayers)
                .Where(r => r.isActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<Room>> GetPublicRoomsAsync()
        {
            return await _context
                .Rooms.Include(r => r.Host)
                .Include(r => r.RoomPlayers)
                .Where(r => r.isPublic && r.isActive)
                .ToListAsync();
        }

        public async Task<Room?> GetByHostIdAsync(Guid hostId)
        {
            return await _context
                .Rooms.Include(r => r.Host)
                .Include(r => r.RoomPlayers)
                .FirstOrDefaultAsync(r => r.HostId == hostId);
        }

        public async Task<Room> CreateAsync(Room room)
        {
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();
            return room;
        }

        public async Task<Room?> UpdateAsync(Room room)
        {
            var existingRoom = await _context.Rooms.FindAsync(room.Id);
            if (existingRoom == null)
            {
                return null;
            }

            _context.Entry(existingRoom).CurrentValues.SetValues(room);
            await _context.SaveChangesAsync();
            return existingRoom;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return false;
            }

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.Rooms.AnyAsync(r => r.Id == id);
        }
    }
}
