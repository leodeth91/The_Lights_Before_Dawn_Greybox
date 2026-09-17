using System.Collections.Generic;

/// <summary>Elige entre destinos que ya fueron comprobados como seguros. Strategy permite cambiar la elección sin cambiar el movimiento de habitaciones.</summary>
public sealed class AnomalyDirector
{
    private const int RecentRoomLimit = 2;
    private readonly IRoomSelectionStrategy selectionStrategy;
    private readonly Queue<string> recentRoomIds = new Queue<string>();
    // La cola recuerda solo los últimos destinos. Al superar el límite sale el más antiguo para evitar repeticiones inmediatas.

    public IReadOnlyCollection<string> RecentRoomIds => recentRoomIds;

    public AnomalyDirector(IRoomSelectionStrategy strategy)
    {
        selectionStrategy = strategy;
    }

    public RoomConnectionCandidate SelectDestination(
        IReadOnlyList<RoomConnectionCandidate> candidates)
    {
        RoomConnectionCandidate selection = selectionStrategy.Select(candidates, recentRoomIds);
        if (selection.Room == null)
        {
            return selection;
        }

        recentRoomIds.Enqueue(selection.Room.RoomId);
        while (recentRoomIds.Count > RecentRoomLimit)
        {
            recentRoomIds.Dequeue();
        }

        return selection;
    }
}
