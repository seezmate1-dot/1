namespace LastHearth.Core
{
    /// <summary>
    /// The fortress at the first minute of play (MODES.md, section 10): the hearth and three rooms
    /// stand on dug-out cells; rubble covers the rest of the first section around them (GDD 6).
    /// </summary>
    public static class NewGame
    {
        public static GameSession Create(Balance b, long now)
        {
            var state = new GameState { Time = now };
            state.SurgeDay = (now + state.UtcOffsetSeconds) / 86400 - 1; // today's Surge is still ahead
            var s = new GameSession(b, state);
            int floor = b.Grid.HearthFloor;
            var bedrooms = AddRoom(s, RoomType.Bedrooms, floor, 1);
            var kitchen = AddRoom(s, RoomType.Kitchen, floor, 4);
            var mine = AddRoom(s, RoomType.CoalMine, floor - 1, 2);

            var mara = s.AddDweller("Мара", new[] { 9, 8, 6, 12, 10 }, Sex.Female);
            mara.HeroClass = HeroClass.Guardian;
            mara.HeroLevel = 1;
            var tim = s.AddDweller("Тим", new[] { 7, 5, 4, 9, 5 }, Sex.Male);
            var ina = s.AddDweller("Ина", new[] { 4, 9, 6, 5, 6 }, Sex.Female);
            s.Assign(tim.Id, mine.Id);
            s.Assign(ina.Id, kitchen.Id);
            _ = bedrooms;

            state.Wallet[ResourceType.Coal] = b.Starter.Coal;
            state.Wallet[ResourceType.Food] = b.Starter.Food;
            // Tutorial and first-day quests (RESEARCH.md): claimed into the warehouse when needed.
            state.Rewards.Add(ResourceBag.Materials(b.Starter.Materials, b.SilverShare));
            state.Rewards[ResourceType.Essence] = b.Starter.Essence;
            s.ClaimRewards();
            return s;
        }

        static Room AddRoom(GameSession s, RoomType type, int floor, int cell)
        {
            var room = new Room { Id = s.State.NextRoomId++, Type = type, Level = 1, Floor = floor, Cell = cell };
            s.State.Rooms.Add(room);
            return room;
        }
    }
}
