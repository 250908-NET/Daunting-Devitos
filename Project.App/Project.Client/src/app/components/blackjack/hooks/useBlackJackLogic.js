import { useEffect, useState } from "react";
import { getRoom } from "../../../lib/mockRoomService";
import { createDeck, drawCards, reshuffle } from "../../../lib/tempCardService";

export function useBlackjackLogic(roomId, user) {
    // ======== STATE ========
    const [room, setRoom] = useState(null);
    const [loading, setLoading] = useState(true);
    const [deckId, setDeckId] = useState(null);
    const [dealerCards, setDealerCards] = useState([]);
    const [playerHands, setPlayerHands] = useState({});
    const [gamePhase, setGamePhase] = useState("waiting");
    const [playerBets, setPlayerBets] = useState({});
    const [bettingOpen, setBettingOpen] = useState(true);
    const [timer, setTimer] = useState(30);
    const [canDouble, setCanDouble] = useState(true);


    // ======== FETCH ROOM DATA ========
    useEffect(() => {
        getRoom(roomId).then((r) => {
            setRoom(r);
            setLoading(false);
        });
    }, [roomId]);

    // ======== INITIALIZE DECK (TEMPORARY API) ========
    useEffect(() => {
        async function initDeck() {
            try {
            const id = await createDeck(); // returns the deck_id string
            setDeckId(id);
            } catch (err) {
            console.error("Deck init error:", err);
            }
        }
        initDeck();
    }, []);
    // ======== CARD ACTIONS ========
    async function dealInitialCards() {
    console.log("🟦 DEAL INITIAL CARDS called. deckId:", deckId, "room:", room);

    if (!deckId || !room) {
        console.warn("⚠️ Missing deck or room", { deckId, room });
        return;
    }

    try {
        const dealerDraw = await drawCards(2);
        console.log("🂠 Dealer drew:", dealerDraw);

        const playerDraws = {};
        for (const player of room.roomPlayers.filter((p) => p.role === "player")) {
        console.log(`➡️ Drawing for player ${player.userName}`);
        playerDraws[player.userName] = await drawCards(2);

        setCanDouble(true);
        }

        setDealerCards(dealerDraw);
        setPlayerHands(playerDraws);
        setGamePhase("in-progress");

        console.log("🃏 Dealt initial cards SUCCESS:", { dealerDraw, playerDraws });
    } catch (err) {
        console.error("❌ Error dealing cards:", err);
    }
    }


    async function hit(playerName) {
    try {
            const newCard = await drawCards(1);
            setPlayerHands((prev) => ({
            ...prev,
            [playerName]: [...(prev[playerName] || []), ...newCard],
            }));

            // Once the player hits, they can’t double anymore
            setCanDouble(false);
        } catch (err) {
            console.error("Error hitting:", err);
        }
    }

    async function doubleDown(playerName) {
        if (!canDouble) return;
        try {
            console.log(`💰 ${playerName} is doubling down`);

            // Double the player’s bet
            setPlayerBets((prev) => ({
            ...prev,
            [playerName]: prev[playerName] * 2,
            }));

            // Draw exactly one card
            const newCard = await drawCards(1);
            setPlayerHands((prev) => ({
            ...prev,
            [playerName]: [...(prev[playerName] || []), ...newCard],
            }));

            // Disable further doubling this round
            setCanDouble(false);

            // Normally you’d also auto-end their turn here
            console.log(`🃏 ${playerName} drew:`, newCard);
        } catch (err) {
            console.error("Error doubling:", err);
        }
    }

    async function split(playerName) {
        try {
            const hand = playerHands[playerName];
            if (!hand || hand.length !== 2) {
            console.warn("❌ Split not allowed — must have exactly two cards");
            return;
            }

            // Check if the two cards have the same value
            const [card1, card2] = hand;
            const getValue = (c) => c.value === "ACE" ? 11 :
            ["KING", "QUEEN", "JACK"].includes(c.value) ? 10 : Number(c.value);

            if (getValue(card1) !== getValue(card2)) {
            console.warn("❌ Split not allowed — cards are not equal in value");
            return;
            }

            console.log(`🪓 ${playerName} splits their hand!`);

            // Create two separate hands
            const newCard1 = await drawCards(1);
            const newCard2 = await drawCards(1);

            setPlayerHands((prev) => ({
            ...prev,
            [`${playerName}_1`]: [card1, ...newCard1],
            [`${playerName}_2`]: [card2, ...newCard2],
            }));

            // Disable double after splitting
            setCanDouble(false);
        } catch (err) {
            console.error("Error splitting:", err);
        }
    }



    async function shuffleDeck() {
        if (!deckId) return;
        try {
            await reshuffle(deckId);
            setDealerCards([]);
            setPlayerHands({});
            setGamePhase("waiting");
        } catch (err) {
            console.error("Error reshuffling:", err);
        }
    }

    // ======== 🪙 BETTING LOGIC ========
    function placeBet(playerName, amount) {
        if (!bettingOpen) {
            console.warn("Betting is closed!");
            return;
        }

        // Prevent double betting
        if (playerBets[playerName]) {
            alert("You have already placed your bet!");
            return;
        }

        console.log(`✅ ${playerName} bet $${amount}`);
        setPlayerBets((prev) => ({
            ...prev,
            [playerName]: amount,
        }));

        // Wait for all players
        setTimeout(() => {
            setPlayerBets((updatedBets) => {
                const allBet = room.roomPlayers
                    .filter((p) => p.role === "player")
                    .every((p) => updatedBets[p.userName] > 0);

                if (allBet) {
                    console.log("🎲 All bets in — dealing cards...");
                    setBettingOpen(false);
                    setGamePhase("dealing");
                    dealInitialCards();
                } else {
                    console.log("Waiting for other players to bet...");
                }

                return updatedBets;
            });
        }, 100);
    }

    // ======== REMOVE PLAYER (async-safe) ========
    function removePlayer(playerName) {
        return new Promise((resolve) => {
            setRoom((prev) => {
                const updatedRoom = {
                    ...prev,
                    roomPlayers: prev.roomPlayers.filter((p) => p.userName !== playerName),
                };
                console.log(`🚫 ${playerName} removed from the room`);
                resolve(updatedRoom);
                return updatedRoom;
            });
        });
    }

    // ======== HANDLE TIMEOUT (reliable fix) ========
    async function handleBettingTimeout() {
        console.log("⏰ Timer expired — removing idle players...");

        // Remove anyone who hasn’t bet yet
        const nonBettingPlayers = room?.roomPlayers?.filter(
            (p) => p.role === "player" && !playerBets[p.userName]
        );

        // Wait for all removals
        if (nonBettingPlayers?.length) {
            await Promise.all(nonBettingPlayers.map((p) => removePlayer(p.userName)));
        }

        console.log("✅ All non-betting players removed, dealing cards...");

        setBettingOpen(false);
        setGamePhase("dealing");
        dealInitialCards();
    }

    // ======== ⏱️ TIMER LOGIC ========
    useEffect(() => {
        if (!bettingOpen) return;

        if (timer <= 0) {
            // instead of calling removePlayer directly (which may run async inside useEffect),
            // we use a controlled async handler for reliability
            handleBettingTimeout();
            return;
        }

        const countdown = setTimeout(() => setTimer((t) => t - 1), 1000);
        return () => clearTimeout(countdown);
    }, [timer, bettingOpen]);

    // ======== COMPUTE LAYOUT ========
    if (!room)
        return {
            loading,
            room: null,
            dealer: null,
            seatPositions: [],
        };

    const dealer = room.roomPlayers.find((p) => p.role === "dealer");
    const players = room.roomPlayers.filter((p) => p.role === "player");

    const N = players.length;
    const currentIndex = players.findIndex((p) => p.userName === user.name);
    const currentPosNumber = currentIndex === -1 ? null : currentIndex + 1;

    // Seat layout visually: 0-4 (center = 2)
    const seatLayout = [
        { x: 950, y: 250 }, // seat 0 - far right
        { x: 775, y: 500 }, // seat 1 - right
        { x: 450, y: 550 }, // seat 2 - center (current player)
        { x: 125, y: 500 }, // seat 3 - left
        { x: -50, y: 250 }, // seat 4 - far left
    ];

    const visualPositionsNumbers = Array(N).fill(null);

    // ✅ Center seat = current player
    if (currentPosNumber !== null) visualPositionsNumbers[2] = currentPosNumber;

    // All other players, in order, skipping the current player
    const remainingPlayers = players
        .map((p, i) => i + 1)
        .filter((pos) => pos !== currentPosNumber);

    // Fill remaining seats left-to-right, skipping index 2
    const seatOrder = [0, 1, 3, 4];
    remainingPlayers.forEach((pos, idx) => {
        if (idx < seatOrder.length) visualPositionsNumbers[seatOrder[idx]] = pos;
    });

    // Helper to get player by position
    const getPlayerByPos = (posNumber) =>
        players.find((_, idx) => idx + 1 === posNumber);

    // Build seat positions
    const seatPositions = visualPositionsNumbers.map((posNumber, i) => {
        const playerObj = getPlayerByPos(posNumber);
        return {
            player: playerObj,
            x: seatLayout[i]?.x ?? seatLayout.at(-1).x,
            y: seatLayout[i]?.y ?? seatLayout.at(-1).y,
            positionLabel: posNumber,
            hand: playerHands[playerObj?.userName] || [],
        };
    });

    // ======== RETURN ========
    return {
        loading,
        room,
        dealer,
        seatPositions,
        dealerCards,
        gamePhase,
        bettingOpen,
        timer,
        placeBet,
        removePlayer,
        dealInitialCards,
        hit,
        shuffleDeck,
        doubleDown,
        canDouble,
        split,
    };
}
