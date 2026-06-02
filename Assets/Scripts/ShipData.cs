public struct ShipStats
{
    public float speed;
    public float hp;
    public float torpedoDamage;
    public float fireDelay;
}

public static class ShipData
{
    public static readonly ShipStats Blue      = new ShipStats { speed = 20f, hp = 7f, torpedoDamage = 1f, fireDelay = 2f };
    public static readonly ShipStats Yellow    = new ShipStats { speed = 40f, hp = 7f, torpedoDamage = 1f, fireDelay = 2f };
    public static readonly ShipStats YellowRed = new ShipStats { speed = 24f, hp = 8f, torpedoDamage = 1f, fireDelay = 2f };
    public static readonly ShipStats Pirate    = new ShipStats { speed = 20f, hp = 7f, torpedoDamage = 1.2f, fireDelay = 1.5f };
}
