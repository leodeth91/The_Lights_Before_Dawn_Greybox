using System.Collections.Generic;

public interface IRoomSelectionStrategy
{
    RoomConnectionCandidate Select(IReadOnlyList<RoomConnectionCandidate> candidates,
        IReadOnlyCollection<string> recentRoomIds);
}
