using UnityEngine;

/// <summary>
/// Magnet Beam style: press to start a beam in front of Mega Man, it grows while the button is held and stops
/// when released, at full length, against a wall, out of energy, or when Mega Man is hit.
/// Energy is used per shot or, with Tiles Per Energy, while the beam grows.
/// </summary>
public class HoldBeamWeapon : WeaponBase
{
    [Tooltip("The beam (a MagnetBeam component).")]
    public GameObject beamPrefab;
    [Tooltip("1 energy for every N tiles the beam grows. 0 = Energy Cost once per beam.")]
    [Min(0)] public int tilesPerEnergy = 2;

    private MagnetBeam beam;
    private bool ready;
    private int tilesThisBeam;
    private bool shooting;

    public override bool IsShooting => shooting;

    private bool DrainsPerTile => tilesPerEnergy > 0;

    public override void Tick(WeaponInput input)
    {
        bool held = input.held;

        // Released: stop growing
        if (!held && ready) StopBeam();

        // Press: start a beam
        if (held && ready && !shooting && User.CanFire && !User.IsInvincible)
        {
            StartBeam();
            ready = false;
        }

        // The beam follows the muzzle while it grows
        if (beam != null && shooting)
        {
            beam.UpdateBeamPosition(User.MuzzlePosition);
            beam.SetBeamDirection(User.FacingRight ? Vector2.right : Vector2.left);
            if (beam.hasReachedMaxLength) StopBeam();
        }

        if (!held && !ready) ready = true;
    }

    private void StartBeam()
    {
        bool canStart = DrainsPerTile ? (Data.CurrentEnergy >= 1 || Data.usesHealthAsEnergy) : CanAfford(Data.energyCost);
        if (!canStart || !HasRoomOnScreen) return;

        var shot = Fire(beamPrefab, 0, 0f);
        beam = shot != null ? shot.GetComponent<MagnetBeam>() : null;
        if (beam == null) return;

        tilesThisBeam = 0;
        if (DrainsPerTile) beam.canAddTile = TryPayForTile;   // energy goes down while it grows
        else Spend(Data.energyCost);

        beam.SetBeamDirection(User.FacingRight ? Vector2.right : Vector2.left);
        beam.StartExtending();
        PlaySound(Data.weaponClip);
        shooting = true;
    }

    // Asked by the beam before each new tile: 1 energy for the first tile of every group of tilesPerEnergy
    private bool TryPayForTile()
    {
        if (tilesThisBeam % tilesPerEnergy == 0)
        {
            if (!CanAfford(1)) return false;
            Spend(1);
        }
        tilesThisBeam++;
        return true;
    }

    private void StopBeam()
    {
        if (beam != null) beam.StopExtending();
        beam = null;
        shooting = false;
    }

    public override void Cancel() => StopBeam();

    // Hit: the beam stops where it is
    public override void OnUserHurt() => StopBeam();
}
