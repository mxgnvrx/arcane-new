using Robust.Shared.GameStates;

namespace Content.Arcane.Shared.Augments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AntiDropHeldItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Implant;
}
