using Project.Api.Models;
using Project.Api.Utilities.Enums;

namespace Project.Api.Repositories.Interface
{
    public interface IRoomPlayerRepository
    {
        Task<RoomPlayer?> GetByIdAsync(Guid id);
        Task<IEnumerable<RoomPlayer>> GetAllAsync();
        Task<IEnumerable<RoomPlayer>> GetByRoomIdAsync(Guid roomId);
        Task<IEnumerable<RoomPlayer>> GetByUserIdAsync(Guid userId);
        Task<RoomPlayer?> GetByRoomIdAndUserIdAsync(Guid roomId, Guid userId);
        Task<IEnumerable<RoomPlayer>> GetActivePlayersInRoomAsync(Guid roomId);
        Task<RoomPlayer> CreateAsync(RoomPlayer roomPlayer);
        Task<RoomPlayer> UpdateAsync(RoomPlayer roomPlayer);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
        Task<bool> IsPlayerInRoomAsync(Guid roomId, Guid userId);
        Task<int> GetPlayerCountInRoomAsync(Guid roomId);
        Task<RoomPlayer?> GetRoomHostAsync(Guid roomId);
        Task UpdatePlayerStatusAsync(Guid id, Status status);
        Task UpdatePlayerBalanceAsync(Guid id, long balance);
        Task UpdatePlayersInRoomAsync(Guid roomId, Action<RoomPlayer> updateAction);
    }
}
