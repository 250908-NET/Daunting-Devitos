using Project.Api.Models;

namespace Project.Api.Repositories.Interface
{
    public interface IRoomRepository
    {
        Task<Room?> GetByIdAsync(Guid id);
        Task<IEnumerable<Room>> GetAllAsync();
        Task<IEnumerable<Room>> GetActiveRoomsAsync();
        Task<IEnumerable<Room>> GetPublicRoomsAsync();
        Task<Room?> GetByHostIdAsync(Guid hostId);
        Task<Room> CreateAsync(Room room);
        Task<Room?> UpdateAsync(Room room);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
    }
}
