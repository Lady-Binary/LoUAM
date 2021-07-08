using System;

namespace LoUAM
{
    public enum PlaceFileEnum
    {
        None,
        Common,
        Personal
    }

    public enum PlaceServerEnum
    {
        Unknown = 0,
        HOPE,
        LoA,
        LoU,
        WhiteWolf
    }

    public enum PlaceRegionEnum
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
        TwoTowers,
        britanniamain,
        loudungeons
    }

    public enum PlaceType
    {
        CurrentPlayer = 0,
        OtherPlayer = 1,
        MOB = 2,
        Place = 3
    }

    public enum PlaceIcon
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

    public class Place
    {
        public PlaceFileEnum File { get; set; }
        public PlaceServerEnum Server { get; set; }
        public PlaceRegionEnum Region { get; set; }
        public PlaceType Type { get; set; }
        public string Id { get; set; }
        public PlaceIcon Icon { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Place(PlaceFileEnum file, PlaceServerEnum server, PlaceRegionEnum region, PlaceType type, string id, PlaceIcon Icon, string label, double x, double y, double z)
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

        public static PlaceServerEnum URLToServer(string url)
        {
            if (url == "cluster1.shardsonline.com:5148")
            {
                return PlaceServerEnum.HOPE;
            }
            else if (url == "84.16.234.196:5001" // Crimson Sea
                || url == "23.105.169.78:5001") // Ethereal Moon
            { 
                return PlaceServerEnum.LoA;
            }
            else if (url.Contains("uo4.life"))
            {
                return PlaceServerEnum.LoU;
            }
            else if (url == "135.181.132.140:5001")
            {
                return PlaceServerEnum.WhiteWolf;
            }

            return PlaceServerEnum.Unknown;
        }

        public static PlaceRegionEnum StringToRegion(string region)
        {
            if (region == "")
                return PlaceRegionEnum.Unknown;

            if (Enum.TryParse(region, true, out PlaceRegionEnum regionEnum))
                return regionEnum;
            else
                return PlaceRegionEnum.Unknown;
        }
    }
}
