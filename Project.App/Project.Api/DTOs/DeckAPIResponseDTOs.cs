using System.Text.Json.Serialization;

namespace Project.Api.DTOs;

public class CreateDeckResponseDTO
{
    [JsonPropertyName("deck_id")]
    public string DeckId { get; set; } = string.Empty;
}

public class DrawCardsResponseDTO
{
    public List<CardDTO> Cards { get; set; } = [];
}

public class ListPilesResponseDTO
{
    public Dictionary<string, PileDTO> Piles { get; set; } = [];
}

public class PileDTO
{
    public List<CardDTO> Cards { get; set; } = new();
}
