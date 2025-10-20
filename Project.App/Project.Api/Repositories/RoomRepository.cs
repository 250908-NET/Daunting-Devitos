using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Utilities;

namespace Project.Api.Repositories;

public class RoomRepository(AppDbContext context) : IRoomRepository
{
    private readonly AppDbContext _context = context;

    public async Task<Room?> GetByIdAsync(Guid id)
    {
        return await _context
            .Rooms.Include(r => r.Host)
            .Include(r => r.RoomPlayers)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Room>> GetAllAsync()
    {
        return await _context.Rooms.Include(r => r.Host).Include(r => r.RoomPlayers).ToListAsync();
    }

    public async Task<IEnumerable<Room>> GetActiveRoomsAsync()
    {
        return await _context
            .Rooms.Include(r => r.Host)
            .Include(r => r.RoomPlayers)
            .Where(r => r.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<Room>> GetPublicRoomsAsync()
    {
        return await _context
            .Rooms.Include(r => r.Host)
            .Include(r => r.RoomPlayers)
            .Where(r => r.IsPublic && r.IsActive)
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The room you are trying to update has been modified by another user. Please refresh and try again."
            );
        }

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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The room you are trying to update has been modified by another user. Please refresh and try again."
            );
        }

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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The room you are trying to update has been modified by another user. Please refresh and try again."
            );
        }

        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Rooms.AnyAsync(r => r.Id == id);
    }

    public async Task<string> GetGameStateAsync(Guid id)
    {
        // check if room exists
        Room room =
            await _context.Rooms.FindAsync(id) ?? throw new NotFoundException("Room not found.");

        return room.GameState;
    }

    public async Task<bool> UpdateGameStateAsync(Guid id, string gamestate)
    {
        // check if room exists
        Room room =
            await _context.Rooms.FindAsync(id) ?? throw new NotFoundException("Room not found.");

        room.GameState = gamestate;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The room you are trying to update has been modified by another user. Please refresh and try again."
            );
        }

        return true;
    }

    public async Task<string> GetGameConfigAsync(Guid id)
    {
        // check if room exists
        Room room =
            await _context.Rooms.FindAsync(id) ?? throw new NotFoundException("Room not found.");

        return room.GameConfig;
    }

    public async Task<bool> UpdateGameConfigAsync(Guid id, string gameConfig)
    {
        // check if room exists
        Room room =
            await _context.Rooms.FindAsync(id) ?? throw new NotFoundException("Room not found.");

        room.GameConfig = gameConfig;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The room you are trying to update has been modified by another user. Please refresh and try again."
            );
        }

        return true;
    }
}
