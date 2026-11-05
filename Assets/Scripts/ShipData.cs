public struct ShipStats
{
    public float speed;
    public float hp;
    public float torpedoDamage;
    public float fireDelay;
    public float size;
}

public static class ShipData
{
    public static readonly ShipStats Blue      = new ShipStats { speed = 20f, hp = 7f,  torpedoDamage = 1f,   fireDelay = 2f,   size = 1f    };
    public static readonly ShipStats Yellow    = new ShipStats { speed = 40f, hp = 7f,  torpedoDamage = 1f,   fireDelay = 2f,   size = 1f    };
    public static readonly ShipStats YellowRed = new ShipStats { speed = 24f, hp = 8f,  torpedoDamage = 1f,   fireDelay = 2f,   size = 1f    };
    public static readonly ShipStats Pirate    = new ShipStats { speed = 20f, hp = 7f,    torpedoDamage = 1.2f, fireDelay = 1.5f, size = 1f    };
    public static readonly ShipStats White     = new ShipStats { speed = 20f, hp = 1000f, torpedoDamage = 1f,   fireDelay = 2f,   size = 1f    };
    public static readonly ShipStats Shark     = new ShipStats { speed = 32f, hp = 6f,    torpedoDamage = 1.2f, fireDelay = 1.6f, size = 1f    };
    public static readonly ShipStats Black     = new ShipStats { speed = 15f, hp = 2f,    torpedoDamage = 1f,   fireDelay = 2.5f, size = 0.66f };
    public static readonly ShipStats Red       = new ShipStats { speed = 20f, hp = 7f,    torpedoDamage = 1f,   fireDelay = 1.7f, size = 1f    };
    public static readonly ShipStats Purple    = new ShipStats { speed = 13f, hp = 1f,    torpedoDamage = 1f,   fireDelay = 2.3f, size = 0.5f  };
}
