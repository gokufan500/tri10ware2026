using UnityEngine;

// Applies the run's movement upgrades to player_movement. The values set in the Inspector are the base values.
// UpgradeManager adds this to the player automatically if it's missing.
[RequireComponent(typeof(player_movement))]
public class PlayerUpgrades : MonoBehaviour
{
    player_movement movement;
    float baseGroundMoveForce, baseAirMoveForce, baseJumpImpulse, baseSwingForce;

    void Awake()
    {
        movement = GetComponent<player_movement>();
        baseGroundMoveForce = movement.groundMoveForce;
        baseAirMoveForce = movement.airMoveForce;
        baseJumpImpulse = movement.jumpImpulse;
        baseSwingForce = movement.swingForce;
    }

    void OnEnable()
    {
        UpgradeManager.UpgradesChanged += OnUpgradesChanged;
        Apply();
    }

    void OnDisable()
    {
        UpgradeManager.UpgradesChanged -= OnUpgradesChanged;
        Apply(useUpgrades: false);
    }

    void OnUpgradesChanged(UpgradeManager upgrades) => Apply();

    void Apply(bool useUpgrades = true)
    {
      movement.groundMoveForce = Stat(UpgradeStat.MoveAcceleration, baseGroundMoveForce, useUpgrades);
      movement.airMoveForce = Stat(UpgradeStat.MoveAcceleration, baseAirMoveForce, useUpgrades);
      movement.jumpImpulse = Stat(UpgradeStat.JumpStrength, baseJumpImpulse, useUpgrades);
      movement.swingForce = Stat(UpgradeStat.GrappleMomentum, baseSwingForce, useUpgrades);
    }

    static float Stat(UpgradeStat stat, float baseValue, bool useUpgrades) => useUpgrades ? UpgradeManager.Stat(stat, baseValue) : baseValue;
}
