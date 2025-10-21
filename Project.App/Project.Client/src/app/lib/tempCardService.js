// src/app/lib/tempCardService.js
const API_BASE = "https://deckofcardsapi.com/api/deck";
let deckId = null;

// Create or reuse a deck
export async function createDeck() {
  try {
    if (deckId) return deckId; // reuse if we already have one
    const res = await fetch(`${API_BASE}/new/shuffle/?deck_count=1`);
    if (!res.ok) throw new Error("Failed to create deck");
    const data = await res.json();
    deckId = data.deck_id;
    return deckId;
  } catch (error) {
    console.error("Error creating deck:", error);
    throw error;
  }
}

// Draw a number of cards
export async function drawCards(count = 1) {
  try {
    if (!deckId) await createDeck(); // ensure a deck exists
    const res = await fetch(`${API_BASE}/${deckId}/draw/?count=${count}`);
    if (!res.ok) throw new Error("Failed to draw cards");
    const data = await res.json();
    return data.cards;
  } catch (error) {
    console.error("Error drawing cards:", error);
    throw error;
  }
}

// Reshuffle the deck
export async function reshuffle() {
  try {
    if (!deckId) await createDeck();
    const res = await fetch(`${API_BASE}/${deckId}/shuffle/`);
    if (!res.ok) throw new Error("Failed to reshuffle");
  } catch (error) {
    console.error("Error reshuffling deck:", error);
    throw error;
  }
}
