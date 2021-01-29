namespace LoUAM
{
    public enum MarkerFile
    {
        None,
        Common,
        Personal
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

    public struct Marker
    {
        public MarkerFile File { get; set; }
        public MarkerType Type { get; set; }
        public string Id { get; set; }
        public MarkerIcon Icon { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Marker(MarkerFile file, MarkerType type, string id, MarkerIcon Icon, string label, double x, double y, double z)
        {
            this.File = file;
            this.Type = type;
            this.Id = id;
            this.Icon = Icon;
            this.Label = label;
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }
}
