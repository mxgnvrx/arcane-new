using Content.Medical.Common.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;

namespace Content.Arcane.Shared.Augments;

public sealed partial class BreathingTubeSystem : EntitySystem
{
    [Dependency] private SharedInternalsSystem _internals = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BreathingTubeComponent, OrganEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<BreathingTubeComponent, OrganDisabledEvent>(OnDisabled);
    }

    private void OnEnabled(Entity<BreathingTubeComponent> ent, ref OrganEnabledEvent args)
    {
        if (TryComp<InternalsComponent>(args.Body, out var internals))
            _internals.ConnectBreathTool((args.Body, internals), ent);
    }

    private void OnDisabled(Entity<BreathingTubeComponent> ent, ref OrganDisabledEvent args)
    {
        if (TryComp<InternalsComponent>(args.Body, out var internals))
            _internals.DisconnectBreathTool((args.Body, internals), ent, forced: true);
    }
}
