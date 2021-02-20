namespace LoUAM
{
    public enum MarkerFileEnum
    {
        None,
        Common,
        Personal
    }

    public enum MarkerServerEnum
    {
        Unknown = 0,
        HOPE,
        LoA,
        LoU
    }

    public enum MarkerRegionEnum
    {
        Unknown = 0,
        Catacombs,
        Contempt,
        Corruption,
        Deception,
        Founders,
        Limbo,
        Monolith,
        NewCelador,
        Perilous,
        Ruin,
        TwoTowers
    }

    public enum MarkerType
    {
        CurrentPlayer = 0,
        OtherPlayer = 1,
        MOB = 2,
        Place = 3
    }

    public enum MarkerIcon
    {
        none = 0,
        UOAM_l,
        UOAM,
        UOAM_s,
        arms,
        bank,
        carpenter,
        dungeon,
        baker,
        gem,
        guild,
        healer,
        inn,
        jeweler,
        mage,
        moongate,
        provisioner,
        ship,
        shipwright,
        stable,
        tailor,
        tanner,
        tinker,
        loot,
        shrine,
        town,
        archers_guild,
        armaments_guild,
        armourers_guild,
        assasins_guild,
        barbershop,
        beekeeper,
        blacksmith,
        blacksmiths_guild,
        body_of_water,
        bowyer,
        brass,
        bridge,
        butcher,
        cavalry_guild,
        cooks_guild,
        customs,
        docks,
        exit,
        fighters_guild,
        fishermans_guild,
        fletcher,
        gate,
        graveyard,
        healersguild,
        illusionists_guild,
        point_of_interest,
        island,
        landmark,
        library,
        mages_guild,
        merchants_guild,
        miners_guild,
        painter,
        point,
        provisioners_guild,
        reagents,
        rogues_guild,
        ruins,
        sailors_guild,
        scenic,
        shipwrights_guild,
        sorcerers_guild,
        stairs,
        tavern,
        teleporter,
        terrain,
        theater,
        thieves_guild,
        tinkers_guild,
        traders_guild,
        warriors_guild,
        weapons_guild,
        bard,
        bardic_guild,
        market
    }

    public class Marker
    {
        public MarkerFileEnum File { get; set; }
        public MarkerServerEnum Server { get; set; }
        public MarkerRegionEnum Region { get; set; }
        public MarkerType Type { get; set; }
        public string Id { get; set; }
        public MarkerIcon Icon { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Marker(MarkerFileEnum file, MarkerServerEnum server, MarkerRegionEnum region, MarkerType type, string id, MarkerIcon Icon, string label, double x, double y, double z)
        {
            this.File = file;
            this.Server = server;
            this.Region = region;
            this.Type = type;
            this.Id = id;
            this.Icon = Icon;
            this.Label = label;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public static MarkerServerEnum URToMarkerServerEnum(string url)
        {
            switch (url)
            {
                case "cluster1.shardsonline.com:5148":
                    return MarkerServerEnum.HOPE;
                case "84.16.234.196:5001": // Crimson Sea
                case "23.105.169.78:5001": // Ethereal Moon
                    return MarkerServerEnum.LoA;
                case "cluster1.shardsonline.com:5150":
                    return MarkerServerEnum.LoU;
                default:
                    return MarkerServerEnum.Unknown;
            }
        }
    }
}
