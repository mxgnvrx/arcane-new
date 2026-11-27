using Content.Medical.Common.Body;
using Content.Shared.Body;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Arcane.Shared.Augments;

public sealed partial class AntiDropSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AntiDropComponent, OrganEnabledEvent>(OnOrganEnabled);
        SubscribeLocalEvent<AntiDropComponent, OrganDisabledEvent>(OnOrganDisabled);
        SubscribeLocalEvent<AntiDropComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<AntiDropHeldItemComponent, ContainerGettingRemovedAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<AntiDropHeldItemComponent, EntGotRemovedFromContainerMessage>(OnRemovedFromContainer);
    }

    private void OnOrganEnabled(Entity<AntiDropComponent> ent, ref OrganEnabledEvent args)
        => SetBodyEnabled(ent, args.Body, true);

    private void OnOrganDisabled(Entity<AntiDropComponent> ent, ref OrganDisabledEvent args)
        => SetBodyEnabled(ent, args.Body, false);

    private void SetBodyEnabled(Entity<AntiDropComponent> ent, EntityUid body, bool enabled)
    {
        if (_timing.ApplyingState)
            return;

        if (enabled)
        {
            _actions.AddAction(body, ref ent.Comp.Action, ent.Comp.ToggleAction, ent);
            Dirty(ent);
        }
        else
        {
            Release(ent, body);
            _actions.RemoveAction(ent.Comp.Action);
        }
    }

    private void OnToggle(Entity<AntiDropComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled || !HasComp<EnabledOrganComponent>(ent) || _body.GetBody(ent) != args.Performer)
            return;

        args.Handled = true;
        if (ent.Comp.Active)
        {
            Release(ent, args.Performer);
            _popup.PopupClient(Loc.GetString("anti-drop-relaxed"), args.Performer, args.Performer);
            return;
        }

        foreach (var item in _hands.EnumerateHeld(args.Performer))
        {
            var held = EnsureComp<AntiDropHeldItemComponent>(item);
            held.Implant = ent;
            Dirty(item, held);
            ent.Comp.Active = true;
        }

        _actions.SetToggled(ent.Comp.Action, ent.Comp.Active);
        Dirty(ent);
        _popup.PopupClient(Loc.GetString(ent.Comp.Active ? "anti-drop-tightened" : "anti-drop-empty"),
            args.Performer,
            args.Performer);
    }

    private void Release(Entity<AntiDropComponent> ent, EntityUid body)
    {
        foreach (var item in _hands.EnumerateHeld(body))
        {
            if (TryComp<AntiDropHeldItemComponent>(item, out var held) && held.Implant == ent.Owner)
                RemComp<AntiDropHeldItemComponent>(item);
        }

        ent.Comp.Active = false;
        _actions.SetToggled(ent.Comp.Action, false);
        Dirty(ent);
    }

    private void OnRemoveAttempt(Entity<AntiDropHeldItemComponent> ent, ref ContainerGettingRemovedAttemptEvent args)
    {
        if (!_timing.ApplyingState
            && TryComp<AntiDropComponent>(ent.Comp.Implant, out var implant) && implant.Active
            && HasComp<EnabledOrganComponent>(ent.Comp.Implant)
            && _body.GetBody(ent.Comp.Implant) == args.Container.Owner && _hands.IsHolding(args.Container.Owner, ent.Owner))
            args.Cancel();
    }

    private void OnRemovedFromContainer(Entity<AntiDropHeldItemComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        RemComp<AntiDropHeldItemComponent>(ent);
        if (!TryComp<AntiDropComponent>(ent.Comp.Implant, out var implant))
            return;

        foreach (var item in _hands.EnumerateHeld(args.Container.Owner))
        {
            if (item != ent.Owner && TryComp<AntiDropHeldItemComponent>(item, out var held)
                && held.Implant == ent.Comp.Implant)
                return;
        }

        implant.Active = false;
        _actions.SetToggled(implant.Action, false);
        Dirty(ent.Comp.Implant, implant);
    }
}
