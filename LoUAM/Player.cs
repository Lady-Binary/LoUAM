namespace LoUAM
{
    public class Player
    {
        public long LastUpdate { get; set; }
        public ulong ObjectId { get; set; }
        public string DisplayName { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Player(long lastUpdate, ulong objectId, string displayName, float x, float y, float z)
        {
            this.LastUpdate = lastUpdate;
            this.ObjectId = objectId;
            this.DisplayName = displayName;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }
}
