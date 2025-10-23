using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Models;
using Project.Api.Repositories.Interface;
using Project.Api.Utilities;

namespace Project.Api.Repositories;

/*
    Name: HandRepository.cs
    Description: Repository for Hand entity
    Children: IHandRepository.cs
*/
public class HandRepository : IHandRepository
{
    private readonly AppDbContext _context;

    // Constructor
    public HandRepository(AppDbContext context)
    {
        _context = context;
    }

    // Implement the methods
    // Get a hand by its ID
    public async Task<Hand?> GetHandByIdAsync(Guid handId)
    {
        // Validate handId
        if (handId == Guid.Empty)
        {
            throw new ArgumentException("Invalid handId");
        }
        // Retrieve the hand from the database
        Hand? hand = await _context.Hands.FirstOrDefaultAsync(h => h.Id == handId);
        // return hand or throw exception if not found
        return hand ?? throw new Exception("Hand not found");
    }

    public async Task<List<Hand>> GetHandsByRoomIdAsync(Guid roomId)
    {
        // Validate roomId
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Invalid roomId");
        }
        // Retrieve the hands from the database
        List<Hand> hands = await _context
            .Hands.Include(h => h.RoomPlayer)
            .Where(h => h.RoomPlayer != null && h.RoomPlayer.RoomId == roomId)
            .ToListAsync();
        // return hands or throw exception if not found
        return (hands == null || hands.Count == 0) ? throw new Exception("No hands found") : hands;
    }

    public async Task<List<Hand>> GetHandsByUserIdAsync(Guid roomId, Guid userId)
    {
        // Validate roomId and userId
        if (userId == Guid.Empty || roomId == Guid.Empty)
        {
            throw new ArgumentException(userId == Guid.Empty ? "Invalid userId" : "Invalid roomId");
        }
        // Retrieve the hands from the database
        List<Hand> hands = await _context
            .Hands.Include(h => h.RoomPlayer)
            .Where(h =>
                h.RoomPlayer != null
                && h.RoomPlayer.RoomId == roomId
                && h.RoomPlayer.UserId == userId
            )
            .ToListAsync();
        // return hands or throw exception if not found
        return (hands == null || hands.Count == 0) ? throw new Exception("No hands found") : hands;
    }

    public async Task<Hand> GetHandByRoomOrderAsync(Guid roomId, int playerOrder, int handOrder)
    {
        // Validate roomId
        if (roomId == Guid.Empty)
        {
            throw new ArgumentException("Invalid roomId");
        }

        // Retrieve the hand from the database
        Hand? hand = await _context
            .Hands.Include(h => h.RoomPlayer)
            .FirstOrDefaultAsync(h =>
                h.RoomPlayer != null
                && h.RoomPlayer.RoomId == roomId
                && h.Order == playerOrder
                && h.HandNumber == handOrder
            );
        // return hand or throw exception if not found
        return hand ?? throw new NotFoundException("Hand not found");
    }

    // Create a new hand
    public async Task<Hand> CreateHandAsync(Hand hand)
    {
        //check if hand does not exist
        if (hand == null)
        {
            throw new ArgumentNullException("Hand cannot be null");
        }

        // Asynchronously add the hand to the context and save changes
        await _context.Hands.AddAsync(hand);
        await SaveChangesAsync();
        // return hand
        return hand;
    }

    // Update an existing hand
    public async Task<Hand> UpdateHandAsync(Guid handId, Hand hand)
    {
        //check if hand does not exist add hand if it does
        var existingHand =
            await _context.Hands.FindAsync(handId)
            ?? throw new KeyNotFoundException("Hand not found");

        // Update properties
        existingHand.Order = hand.Order;
        existingHand.Bet = hand.Bet;

        // Update the hand in the context and save changes
        _context.Hands.Update(existingHand);
        await SaveChangesAsync();

        // return newly updated hand
        return existingHand;
    }

    // update specific properties of an existing hand
    public async Task<Hand> PatchHandAsync(Guid handId, int? Order = null, int? Bet = null)
    {
        // Check if hand exists and retrieve it
        var existingHand =
            await _context.Hands.FindAsync(handId)
            ?? throw new KeyNotFoundException("Hand not found");

        // Update properties if provided
        existingHand.Order = Order ?? existingHand.Order;
        existingHand.Bet += Bet ?? 0;

        // Update the hand in the context and save changes
        _context.Hands.Update(existingHand);
        await SaveChangesAsync();

        // Return the updated hand
        return existingHand;
    }

    // Delete an existing hand
    public async Task<Hand> DeleteHandAsync(Guid handId)
    {
        // Check if hand exists and retrieve it
        var existingHand =
            await _context.Hands.FindAsync(handId)
            ?? throw new KeyNotFoundException("Hand not found");

        // Remove the hand from the context and save changes
        _context.Hands.Remove(existingHand);
        await SaveChangesAsync();

        // Return the deleted hand
        return existingHand;
    }

    // Save changes to the database
    public async Task SaveChangesAsync()
    {
        // Save changes to the database
        await _context.SaveChangesAsync();
    }
}
