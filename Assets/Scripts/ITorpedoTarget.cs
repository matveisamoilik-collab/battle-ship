// Anything a torpedo can hit that is not a ship: Level 6 pillars, Level 7 kraken / sunfish /
// jellyfish / throne. Torpedo.OnCollisionEnter looks it up with GetComponentInParent before the
// ship-tag logic. Return true when the torpedo is used up (it explodes and is destroyed);
// false lets it fly on (e.g. it hit a part that ignores torpedoes).
public interface ITorpedoTarget
{
    bool OnTorpedoHit(Torpedo torpedo);
}
