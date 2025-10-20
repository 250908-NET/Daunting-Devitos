namespace Project.Api.DTOs
{
    public class HandUpdateDTO
    {
        public int Order { get; set; }
        public string CardsJson { get; set; } = string.Empty;
        public int Bet { get; set; }
    }
}
