namespace Project.Api.DTOs
{
    public class HandDTO
    {
        required public Guid Id { get; set; }

        required public Guid RoomPlayerId { get; set; }

        required public int Order { get; set; }

        required public string CardsJson { get; set; }

        required public int Bet { get; set; }
    }
}
