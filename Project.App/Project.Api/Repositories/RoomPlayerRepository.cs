using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Enums;
using Project.Api.Models;
using Project.Api.Repositories.Interface;

namespace Project.Api.Repositories;

public class RoomPlayerRepository : IRoomPlayerRepository
{
    private readonly AppDbContext _context;

    public RoomPlayerRepository(AppDbContext context)
    {
        _context = context;
    }

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

    public async Task<RoomPlayer?> GetByRoomAndUserAsync(Guid roomId, Guid userId)
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
        var roomPlayer = await _context.RoomPlayers.FindAsync(id);
        if (roomPlayer != null)
        {
            roomPlayer.Status = status;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdatePlayerBalanceAsync(Guid id, long balance)
    {
        var roomPlayer = await _context.RoomPlayers.FindAsync(id);
        if (roomPlayer != null)
        {
            roomPlayer.Balance = balance;
            await _context.SaveChangesAsync();
        }
    }
}
