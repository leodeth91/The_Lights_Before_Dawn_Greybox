using System.Collections.Generic;
using System;
using System.Linq;

public sealed class RandomRoomSelectionStrategy : IRoomSelectionStrategy
{
    private readonly Random random;

    public RandomRoomSelectionStrategy(int seed)
    {
        random = new Random(seed);
    }

    public RoomConnectionCandidate Select(IReadOnlyList<RoomConnectionCandidate> candidates,
        IReadOnlyCollection<string> recentRoomIds)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return default;
        }

        List<RoomModule> rooms = candidates.Select(item => item.Room).Distinct().ToList();
        // Todas las habitaciones seguras conservan una oportunidad. Las menos recientes
        // tienen dos oportunidades y las recientes una: evita excluirlas por completo.
        int totalWeight = 0;
        foreach (RoomModule room in rooms) totalWeight += recentRoomIds.Contains(room.RoomId) ? 1 : 2;
        int selection = random.Next(totalWeight);
        RoomModule selectedRoom = rooms[rooms.Count - 1];
        foreach (RoomModule room in rooms)
        {
            selection -= recentRoomIds.Contains(room.RoomId) ? 1 : 2;
            if (selection < 0) { selectedRoom = room; break; }
        }
        List<RoomConnectionCandidate> roomSockets = candidates
            .Where(item => item.Room == selectedRoom)
            .ToList();
        return roomSockets[random.Next(0, roomSockets.Count)];
    }
}
