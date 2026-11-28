using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Arcane.Server.Augments;

[RegisterComponent]
public sealed partial class DeployableArmComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ItemPrototype;

    [DataField]
    public EntityUid? Item;

    [DataField]
    public string ContainerId = "laser_arm";

    [DataField(required: true)]
    public EntProtoId ToggleAction;

    [DataField]
    public EntityUid? Action;

    [DataField]
    public SoundSpecifier ExtendSound = new SoundPathSpecifier("/Audio/_Goobstation/Weapons/Effects/nanotrasenenergyon.ogg");

    [DataField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/_Goobstation/Weapons/Effects/nanotrasenenergyoff.ogg");

    [DataField]
    public float EmpBreakChance;

    [DataField]
    public DamageSpecifier EmpDamage = new();

    [DataField]
    public float EmpFireStacks = 15f;

    [DataField]
    public SoundSpecifier EmpSound = new SoundPathSpecifier("/Audio/Effects/flash_bang.ogg");

    [DataField]
    public bool Broken;
}
