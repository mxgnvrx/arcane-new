// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion.Components;
using Content.Shared.Implants;
using Content.Shared.Mobs.Events;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Arcane.Shared.Implants;

public sealed partial class TacticalDeniabilitySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TacticalDeniabilityImplantComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<TacticalDeniabilityComponent, EntInsertedIntoContainerMessage>(OnImplantInserted);
        SubscribeLocalEvent<TacticalDeniabilityComponent, MobThresholdStateEvent>(OnUpdateMobState);
        SubscribeLocalEvent<TacticalDeniabilityAbsorbedComponent, AttemptTriggerEvent>(OnImplantTriggerAttempt);
    }

    private void OnImplanted(Entity<TacticalDeniabilityImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (!_container.TryGetContainer(args.Implanted, ImplanterComponent.ImplantSlotId, out var implants))
            return;

        foreach (var implant in implants.ContainedEntities)
        {
            if (implant != ent.Owner)
                Absorb(ent, implant);
        }
    }

    private void OnImplantInserted(Entity<TacticalDeniabilityComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ImplanterComponent.ImplantSlotId
            || GetImplant(ent) is not { } implant
            || args.Entity == implant.Owner)
            return;

        Absorb(implant, args.Entity);
    }

    private void OnUpdateMobState(Entity<TacticalDeniabilityComponent> ent, ref MobThresholdStateEvent args)
    {
        if (args.State is not (MobState.SoftCrit or MobState.Critical)
            || GetImplant(ent) is not { } implant)
            return;

        args.State = MobState.Alive;
        _trigger.Trigger(implant, ent, TriggerSystem.DefaultTriggerKey);
    }

    private void Absorb(Entity<TacticalDeniabilityImplantComponent> ent, EntityUid other)
    {
        if (_net.IsClient
            || EntityManager.IsQueuedForDeletion(other)
            || HasComp<TacticalDeniabilityImplantComponent>(other)
            || !TryComp<ExplosionOnTriggerComponent>(ent, out var explosion))
            return;

        if (TryComp<ExplosionOnTriggerComponent>(other, out var onTrigger))
        {
            explosion.TotalIntensity += onTrigger.TotalIntensity;
            explosion.MaxTileIntensity = MathF.Max(explosion.MaxTileIntensity, onTrigger.MaxTileIntensity);
            explosion.CanCreateVacuum |= onTrigger.CanCreateVacuum;
        }
        else if (TryComp<ExplosiveComponent>(other, out var explosive))
        {
            explosion.TotalIntensity += explosive.TotalIntensity;
            explosion.MaxTileIntensity = MathF.Max(explosion.MaxTileIntensity, explosive.MaxIntensity);
            explosion.CanCreateVacuum |= explosive.CanCreateVacuum;
        }
        else
        {
            return;
        }

        Dirty(ent, explosion);

        if (TryComp<TimerTriggerComponent>(ent, out var timer))
        {
            var delay = timer.Delay + (CompOrNull<TimerTriggerComponent>(other)?.Delay ?? TimeSpan.Zero);
            timer.Delay = delay < ent.Comp.MaxDelay ? delay : ent.Comp.MaxDelay;
            Dirty(ent, timer);
        }

        EnsureComp<TacticalDeniabilityAbsorbedComponent>(other);
        if (TryComp<TimerTriggerComponent>(other, out var otherTimer))
            _trigger.StopTimerTrigger((other, otherTimer));
        QueueDel(other);
    }

    private void OnImplantTriggerAttempt(Entity<TacticalDeniabilityAbsorbedComponent> ent, ref AttemptTriggerEvent args)
    {
        // Implant relays and timer updates can still run before queued deletions are drained.
        args.Cancelled = true;
    }

    private Entity<TacticalDeniabilityImplantComponent>? GetImplant(EntityUid mob)
    {
        if (!_container.TryGetContainer(mob, ImplanterComponent.ImplantSlotId, out var implants))
            return null;

        foreach (var implant in implants.ContainedEntities)
        {
            if (TryComp<TacticalDeniabilityImplantComponent>(implant, out var comp))
                return (implant, comp);
        }

        return null;
    }
}
