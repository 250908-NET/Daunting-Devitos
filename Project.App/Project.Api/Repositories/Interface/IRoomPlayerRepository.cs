using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Project.Api.Models;
using Project.Api.Enum;

namespace Project.Api.Repositories.Interface
{
    public interface IRoomPlayerRepository
    {
        Task<RoomPlayer?> GetByIdAsync(long id);
        Task<IEnumerable<RoomPlayer>> GetAllAsync();
        Task<IEnumerable<RoomPlayer>> GetByRoomIdAsync(Guid roomId);
        Task<IEnumerable<RoomPlayer>> GetByUserIdAsync(int userId);
        Task<RoomPlayer?> GetByRoomAndUserAsync(Guid roomId, int userId);
        Task<IEnumerable<RoomPlayer>> GetActivePlayersInRoomAsync(Guid roomId);
        Task<RoomPlayer> CreateAsync(RoomPlayer roomPlayer);
        Task<RoomPlayer> UpdateAsync(RoomPlayer roomPlayer);
        Task<bool> DeleteAsync(long id);
        Task<bool> ExistsAsync(long id);
        Task<bool> IsPlayerInRoomAsync(Guid roomId, int userId);
        Task<int> GetPlayerCountInRoomAsync(Guid roomId);
        Task<RoomPlayer?> GetRoomHostAsync(Guid roomId);
        Task UpdatePlayerStatusAsync(long id, Status status);
        Task UpdatePlayerBalanceAsync(long id, long balance);
    }
}