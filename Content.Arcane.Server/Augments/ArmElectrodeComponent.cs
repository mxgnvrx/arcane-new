namespace Content.Arcane.Server.Augments;

[RegisterComponent]
public sealed partial class ArmElectrodeComponent : Component
{
    [DataField]
    public float EmpEnergy = 1000f;

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(4);
}
