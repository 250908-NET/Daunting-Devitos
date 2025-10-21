export default function DealerArea({ dealer, cards = [] }) {
  return (
    <div className="text-center">
      <h2 className="text-yellow-400 font-bold mb-2">{dealer?.userName || "Dealer"}</h2>
      <div className="flex justify-center gap-2">
        {cards.length > 0 ? (
          cards.map((card, i) => (
            <img
              key={i}
              src={card.image}
              alt={card.code}
              className="w-16 rounded-lg shadow-md"
            />
          ))
        ) : (
          <p className="text-sm text-gray-300 italic">Waiting...</p>
        )}
      </div>
    </div>
  );
}
