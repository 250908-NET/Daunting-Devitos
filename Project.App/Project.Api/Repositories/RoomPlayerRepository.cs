using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Utilities;
using Project.Api.Utilities.Enums;

namespace Project.Api.Repositories;

public class RoomPlayerRepository(AppDbContext context) : IRoomPlayerRepository
{
    private readonly AppDbContext _context = context;

    public async Task<RoomPlayer?> GetByIdAsync(Guid id)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .FirstOrDefaultAsync(rp => rp.Id == id);
    }

    public async Task<IEnumerable<RoomPlayer>> GetAllAsync()
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoomPlayer>> GetByRoomIdAsync(Guid roomId)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .Where(rp => rp.RoomId == roomId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoomPlayer>> GetByUserIdAsync(Guid userId)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .Where(rp => rp.UserId == userId)
            .ToListAsync();
    }

    public async Task<RoomPlayer?> GetByRoomIdAndUserIdAsync(Guid roomId, Guid userId)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .FirstOrDefaultAsync(rp => rp.RoomId == roomId && rp.UserId == userId);
    }

    public async Task<IEnumerable<RoomPlayer>> GetActivePlayersInRoomAsync(Guid roomId)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .Where(rp => rp.RoomId == roomId && rp.Status == Status.Active)
            .ToListAsync();
    }

    public async Task<RoomPlayer> CreateAsync(RoomPlayer roomPlayer)
    {
        _context.RoomPlayers.Add(roomPlayer);
        await _context.SaveChangesAsync();
        return roomPlayer;
    }

    public async Task<RoomPlayer> UpdateAsync(RoomPlayer roomPlayer)
    {
        _context.Entry(roomPlayer).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return roomPlayer;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var roomPlayer = await _context.RoomPlayers.FindAsync(id);
        if (roomPlayer == null)
            return false;
        _context.RoomPlayers.Remove(roomPlayer);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.RoomPlayers.AnyAsync(rp => rp.Id == id);
    }

    public async Task<bool> IsPlayerInRoomAsync(Guid roomId, Guid userId)
    {
        return await _context.RoomPlayers.AnyAsync(rp =>
            rp.RoomId == roomId && rp.UserId == userId
        );
    }

    public async Task<int> GetPlayerCountInRoomAsync(Guid roomId)
    {
        return await _context.RoomPlayers.CountAsync(rp => rp.RoomId == roomId);
    }

    public async Task<RoomPlayer?> GetRoomHostAsync(Guid roomId)
    {
        return await _context
            .RoomPlayers.Include(rp => rp.Room)
            .Include(rp => rp.User)
            .Include(rp => rp.Hands)
            .FirstOrDefaultAsync(rp => rp.RoomId == roomId && rp.Role == Role.Admin);
    }

    public async Task UpdatePlayerStatusAsync(Guid id, Status status)
    {
        RoomPlayer roomPlayer =
            await _context.RoomPlayers.FindAsync(id)
            ?? throw new NotFoundException("Player not found");

        roomPlayer.Status = status;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Adds the specified amount to the player's balance (can be negative).
    /// </summary>
    public async Task UpdatePlayerBalanceAsync(Guid id, long change)
    {
        RoomPlayer roomPlayer =
            await _context.RoomPlayers.FindAsync(id)
            ?? throw new NotFoundException("Player not found");

        roomPlayer.Balance += change;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Batch updates all players in a room with a single database operation.
    /// </summary>
    public async Task UpdatePlayersInRoomAsync(Guid roomId, Action<RoomPlayer> updateAction)
    {
        var players = await _context.RoomPlayers.Where(rp => rp.RoomId == roomId).ToListAsync();

        foreach (var player in players)
        {
            updateAction(player);
            _context.Entry(player).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync();
    }
}
