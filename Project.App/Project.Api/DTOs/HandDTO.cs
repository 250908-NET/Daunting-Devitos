namespace Project.Api.DTOs
{
    public class HandDTO
    {
        public required Guid Id { get; set; }

        public required Guid RoomPlayerId { get; set; }

        public required int Order { get; set; }
        public int HandNumber { get; set; } = 0;

        public required long Bet { get; set; }
    }
}
