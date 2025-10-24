using Moq;
using Project.Api.DTOs;
using Project.Api.Services.Interface;

namespace Project.Test.Helpers;

public static class MockDeckAPIHelper
{
    /// <summary>
    /// Create a mock deck service that returns predictable cards and tracks them.
    /// </summary>
    /// <returns></returns>
    public static Mock<IDeckApiService> CreateMockDeckService(
        Queue<CardDTO>? predefinedCards = null
    )
    {
        var mockDeckService = new Mock<IDeckApiService>();
        var handCards = new Dictionary<string, List<CardDTO>>();
        var playerHandIds = new Dictionary<Guid, string>(); // Track player hand IDs

        // Use predefined cards or create a default sequence
        var cardSequence =
            predefinedCards
            ?? new Queue<CardDTO>(
                [
                    new CardDTO
                    {
                        Value = "7",
                        Suit = "HEARTS",
                        Code = "7H",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "8",
                        Suit = "SPADES",
                        Code = "8S",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "10",
                        Suit = "DIAMONDS",
                        Code = "10D",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "6",
                        Suit = "CLUBS",
                        Code = "6C",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "4",
                        Suit = "HEARTS",
                        Code = "4H",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "5",
                        Suit = "SPADES",
                        Code = "5S",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "10",
                        Suit = "HEARTS",
                        Code = "10H",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "KING",
                        Suit = "DIAMONDS",
                        Code = "KD",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "5",
                        Suit = "DIAMONDS",
                        Code = "5D",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "4",
                        Suit = "HEARTS",
                        Code = "4H",
                        Image = "dummy-url",
                    },
                    new CardDTO
                    {
                        Value = "6",
                        Suit = "SPADES",
                        Code = "6S",
                        Image = "dummy-url",
                    },
                ]
            );

        // Mock deck creation
        mockDeckService
            .Setup(m => m.CreateDeck(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync("test-deck-id");

        // Mock creating empty hand - store the hand ID
        mockDeckService
            .Setup(m => m.CreateEmptyHand(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(
                (string _, string handId) =>
                {
                    handCards[handId] = [];
                    return true;
                }
            );

        // Mock creating empty hand with long ID (for player hands)
        mockDeckService
            .Setup(m => m.CreateEmptyHand(It.IsAny<string>(), It.IsAny<long>()))
            .ReturnsAsync(
                (string _, long handId) =>
                {
                    var handIdStr = handId.ToString();
                    handCards[handIdStr] = [];
                    return true;
                }
            );

        // Mock drawing cards - track cards in hands
        mockDeckService
            .Setup(m => m.DrawCards(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(
                (string _, string handId, int count) =>
                {
                    var cards = new List<CardDTO>();
                    for (int i = 0; i < count; i++)
                    {
                        if (cardSequence.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "No more cards in predefined sequence"
                            );
                        }
                        var card = cardSequence.Dequeue();
                        cards.Add(card);

                        if (!handCards.TryGetValue(handId, out List<CardDTO>? value))
                        {
                            value = [];
                            handCards[handId] = value;
                        }

                        value.Add(card);
                    }
                    return cards;
                }
            );

        // Mock adding cards to hand (and implicitly create hand if it doesn't exist)
        mockDeckService
            .Setup(m => m.AddToHand(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(
                (string _, string handId, string cardCodes) =>
                {
                    // Ensure hand exists (create if not)
                    if (!handCards.TryGetValue(handId, out List<CardDTO>? value))
                    {
                        value = [];
                        handCards[handId] = value;
                    }

                    // Add cards to hand
                    foreach (var code in cardCodes.Split(','))
                    {
                        value.Add(
                            new CardDTO
                            {
                                Value = "10",
                                Suit = "HEARTS",
                                Code = code,
                                Image = "dummy-url",
                            }
                        );
                    }
                    return true;
                }
            );

        // Mock listing hands - return tracked cards
        mockDeckService
            .Setup(m => m.ListHand(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(
                (string _, string handId) =>
                {
                    if (!handCards.TryGetValue(handId, out List<CardDTO>? value))
                    {
                        value = [];
                        handCards[handId] = value;
                    }
                    return value;
                }
            );

        // Mock returning cards to deck
        mockDeckService
            .Setup(m => m.ReturnAllCardsToDeck(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(
                (string _, bool _) =>
                {
                    handCards.Clear();
                    return true;
                }
            );

        return mockDeckService;
    }
}
