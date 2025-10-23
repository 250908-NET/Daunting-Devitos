using System.Collections.Generic;
using System.Threading.Tasks;
using Project.Api.DTOs;

namespace Project.Api.Services.Interface
{
    public interface ISplitService
    {
       
        // Splits a specific card from the original hand into a new hand.
    
        Task<bool> SplitCard(string deckId, string originalHand, string newHand, string cardCode);

        
        // Automatically splits the first pair found in the original hand into a new hand.
        
        Task<bool> SplitFirstPair(string deckId, string originalHand, string newHand);

        
        // Lists all cards in a specific hand.
        
        Task<List<CardDTO>> ListHand(string deckId, string handName);
    }
}
