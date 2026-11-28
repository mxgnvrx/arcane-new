// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Trauma.Common.Wizard;
using Content.Server.GameTicking;
using Content.Shared.Antag;
using Content.Shared.Ghost;
using Content.Shared.GameTicking.Rules.Components;
using Content.Shared.Implants;
using Robust.Shared.Prototypes;
using Content.Server.StationEvents.Components;
using Content.Arcane.Shared.Implants;
using Content.Shared.GameTicking.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NukeOps;
using Content.Shared.Pinpointer;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Arcane.Server.Implants;

public sealed partial class TacticalMapSystem : EntitySystem
{
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    [Dependency] private SharedSubdermalImplantSystem _implants = default!;

    private static readonly EntProtoId MapImplant = "TacticalMapImplant";
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<GhostRoleSpawnerUsedEvent>(OnReinforcementSpawned);
        SubscribeLocalEvent<StationEventComponent, GameRuleEndedEvent>(OnStationEventEnded);
        SubscribeLocalEvent<TacticalMapReinforcementRadioComponent, ItemPurchasedEvent>(OnRadioPurchased);
        SubscribeLocalEvent<TacticalMapOperationComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnStationEventEnded(Entity<StationEventComponent> ent, ref GameRuleEndedEvent args)
    {
        // The event timer expires independently of the assigned operative's mission.
        if (ent.Comp.EndTime <= _timing.CurTime && HasComp<NukeopsRuleComponent>(ent))
            EnsureComp<TacticalMapRetainedOperationComponent>(ent);
    }

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (!HasComp<NukeopsRuleComponent>(args.GameRule.Owner) || !HasComp<NukeOperativeComponent>(args.EntityUid))
            return;

        var operation = EnsureComp<TacticalMapOperationComponent>(args.EntityUid);
        operation.Rule = args.GameRule.Owner;
        operation.Mind = _mind.TryGetMind(args.EntityUid, out var mind, out _) ? mind : null;
        InstallMap(args.EntityUid, operation);
    }

    private void OnRadioPurchased(Entity<TacticalMapReinforcementRadioComponent> ent, ref ItemPurchasedEvent args)
    {
        ent.Comp.Rule = GetOperation(args.Buyer);
    }

    private void OnReinforcementSpawned(ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<TacticalMapReinforcementRadioComponent>(args.Spawner, out var radio)
            || radio.Rule is not { } rule || !HasComp<NukeOperativeComponent>(args.Spawned))
            return;

        var operation = EnsureComp<TacticalMapOperationComponent>(args.Spawned);
        operation.Rule = rule;
        InstallMap(args.Spawned, operation);
    }

    private void OnMindAdded(Entity<TacticalMapOperationComponent> ent, ref MindAddedMessage args)
    {
        if (HasComp<NukeOperativeComponent>(ent))
            ent.Comp.Mind ??= args.Mind.Owner;
    }

    private void InstallMap(EntityUid body, TacticalMapOperationComponent member)
    {
        if (!Exists(member.Implant))
            member.Implant = _implants.AddImplant(body, MapImplant);
    }

    private EntityUid? GetOperation(EntityUid body)
    {
        if (!TryComp<TacticalMapOperationComponent>(body, out var member)
            || member.Rule is not { } rule || member.Mind is not { } assignedMind
            || !HasComp<NukeOperativeComponent>(body)
            || !TryComp<MobStateComponent>(body, out var state) || state.CurrentState == MobState.Dead
            || !_mind.TryGetMind(body, out var currentMind, out _) || currentMind != assignedMind)
            return null;

        return rule;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.5);

        var implants = EntityQueryEnumerator<TacticalMapImplantComponent, SubdermalImplantComponent>();
        while (implants.MoveNext(out var implant, out _, out var subdermal))
        {
            foreach (var actor in _ui.GetActors(implant, StationMapUiKey.Key))
            {
                var message = actor == subdermal.ImplantedEntity
                    ? GetMapUpdate(actor)
                    : new TacticalMapUpdateMessage(null, []);
                // Never replicate ally coordinates to other clients through implant state.
                _ui.ServerSendUiMessage(implant, StationMapUiKey.Key, message, actor);
            }
        }
    }

    private TacticalMapUpdateMessage GetMapUpdate(EntityUid viewer)
    {
        if (GetOperation(viewer) is not { } rule
            || (!_ticker.IsGameRuleActive((rule, null)) && !HasComp<TacticalMapRetainedOperationComponent>(rule))
            || !TryComp<NukeopsRuleComponent>(rule, out var nukeops)
            || nukeops.TargetStation is not { } station
            || _station.GetLargestGrid((station, null)) is not { } grid)
            return new TacticalMapUpdateMessage(null, []);

        var positions = new List<Vector2>();
        var gridInverse = _transform.GetInvWorldMatrix(grid);
        var operatives = EntityQueryEnumerator<TacticalMapOperationComponent, TransformComponent>();
        while (operatives.MoveNext(out var operative, out var member, out var transform))
        {
            if (operative != viewer && member.Rule == rule && transform.GridUid == grid
                && GetOperation(operative) == rule)
                positions.Add(Vector2.Transform(_transform.GetWorldPosition(transform), gridInverse));
        }

        return new TacticalMapUpdateMessage(GetNetEntity(grid), positions.ToArray());
    }
}
