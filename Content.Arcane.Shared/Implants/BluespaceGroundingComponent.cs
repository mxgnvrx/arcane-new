using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Arcane.Shared.Implants;

[RegisterComponent, NetworkedComponent]
public sealed partial class BluespaceGroundingComponent : Component
{
    [DataField]
    public TimeSpan ParalyzeDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public EntProtoId SparksEffect = "EffectSparks";
}
