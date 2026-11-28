// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Arcane.Server.Implants;
using Content.Arcane.Shared.Implants;
using Content.IntegrationTests.Tests.Interaction;
using Content.Goobstation.Shared.Autosurgeon;
using Content.Medical.Shared.Body;
using Content.Shared.Antag;
using Content.Shared.Antag.Components;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Rules.Components;
using Content.Shared.Ghost;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.NukeOps;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Systems;
using Content.Trauma.Common.Wizard;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane;

// Arcane-Start
public sealed class CyberneticsPartTwoTest : InteractionTest
{
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<OrganCategoryPrototype> ArmLeft = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ArmRight = "ArmRight";
    private static readonly ProtoId<OrganCategoryPrototype> ArmAugment = "ArmAugment";
    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId TacticalDeniability = "TacticalDeniabilityImplant";
    private static readonly EntProtoId MacroBomb = "MacroBombImplant";
    private static readonly EntProtoId Reinforcement = "ReinforcementRadioSyndicateNukeops";
    private static readonly ProtoId<AntagSpecifierPrototype> Commander = "NukeopsCommander";

    [TestCase(false)]
    [TestCase(true)]
    public async Task TacticalDeniabilityAbsorbsMacroBombInEitherOrder(bool macroFirst)
    {
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnAttachedTo(Human, SEntMan.GetCoordinates(PlayerCoords));
            var implants = SEntMan.System<SharedSubdermalImplantSystem>();
            var trigger = SEntMan.System<TriggerSystem>();
            var first = implants.AddImplant(patient, macroFirst ? MacroBomb : TacticalDeniability);
            Assert.That(first, Is.Not.Null);
            if (macroFirst)
                Assert.That(trigger.ActivateTimerTrigger(first!.Value, patient), Is.True);
            var second = implants.AddImplant(patient, macroFirst ? TacticalDeniability : MacroBomb);
            Assert.That(second, Is.Not.Null);
            var deniability = (macroFirst ? second : first)!.Value;
            var macro = (macroFirst ? first : second)!.Value;

            var settings = SEntMan.GetComponent<ExplosionOnTriggerComponent>(deniability);
            Assert.That(settings.TotalIntensity, Is.EqualTo(4120));
            Assert.That(settings.MaxTileIntensity, Is.EqualTo(50));
            Assert.That(settings.CanCreateVacuum, Is.True);
            Assert.That(SEntMan.GetComponent<TimerTriggerComponent>(deniability).Delay,
                Is.EqualTo(TimeSpan.FromSeconds(17)));

            Assert.That(SEntMan.IsQueuedForDeletion(macro), Is.True);
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(macro), Is.False);
            Assert.That(trigger.Trigger(macro, patient, "timer"), Is.False,
                "The queued macro-bomb must not detonate separately before deletion.");

            var softCrit = new MobThresholdStateEvent(MobState.SoftCrit);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref softCrit);
            Assert.That(softCrit.State, Is.EqualTo(MobState.Alive));
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(deniability), Is.True);

            var lethal = new MobThresholdStateEvent(MobState.Dead);
            SEntMan.EventBus.RaiseLocalEvent(patient, ref lethal);
            Assert.That(lethal.State, Is.EqualTo(MobState.Dead),
                "Lethal hits still transition to death normally.");
        });
    }

    [Test]
    public async Task ActualDamageCrossingCriticalThresholdStartsDeniabilityTimer()
    {
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnAttachedTo(Human, SEntMan.GetCoordinates(PlayerCoords));
            var body = SEntMan.System<BodySystem>();
            var torso = body.GetOrgan(patient, Torso);
            Assert.That(torso, Is.Not.Null);
            var implants = SEntMan.System<SharedSubdermalImplantSystem>();
            var implant = implants.AddImplant(patient, TacticalDeniability);
            Assert.That(implant, Is.Not.Null);
            var thresholds = SEntMan.System<MobThresholdSystem>();
            thresholds.SetMobStateThreshold(patient, 2, MobState.SoftCrit);
            var damage = SEntMan.System<DamageableSystem>();
            Assert.That(damage.TryChangeDamage(torso!.Value,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } },
                ignoreResistances: true, canMiss: false), Is.True);

            thresholds.VerifyThresholds(patient);
            Assert.That(SEntMan.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(implant!.Value), Is.True);
        });
    }

    [Test]
    public async Task ArmAutosurgeonMakesSpaceOnlyInTheSelectedArm()
    {
        EntityUid patient = default;
        EntityUid autosurgeon = default;
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.GetCoordinates(PlayerCoords);
            patient = SEntMan.SpawnAttachedTo(Human, area);
            autosurgeon = SEntMan.SpawnAttachedTo(new EntProtoId("AutosurgeonLeftLaserArm"), area);
            var comp = SEntMan.GetComponent<AutoSurgeonComponent>(autosurgeon);
            comp.DoAfterTime = TimeSpan.FromMilliseconds(100);
            var systems = SEntMan.System<BodySystem>();
            var left = systems.GetOrgan(patient, ArmLeft)!.Value;
            var parts = SEntMan.System<BodyPartSystem>();
            Assert.That(parts.HasOrganSlot(left, ArmAugment), Is.False);
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(patient, null, autosurgeon), Is.True);
        });

        await RunTicks(20);

        await Server.WaitAssertion(() =>
        {
            var bodies = SEntMan.System<BodySystem>();
            var parts = SEntMan.System<BodyPartSystem>();
            var left = bodies.GetOrgan(patient, ArmLeft)!.Value;
            var right = bodies.GetOrgan(patient, ArmRight)!.Value;
            var implanted = parts.GetOrgan(left, ArmAugment);
            Assert.That(implanted, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(implanted!.Value).EntityPrototype?.ID,
                Is.EqualTo("LaserArmAugment"));
            Assert.That(parts.GetOrgan(right, ArmAugment), Is.Null);
            Assert.That(SEntMan.GetComponent<AutoSurgeonComponent>(autosurgeon).Used, Is.True);
        });
    }

    [Test]
    public async Task ReinforcementUsesOnlyTheBuyersAssignedOperation()
    {
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.GetCoordinates(PlayerCoords);
            var rule = SEntMan.SpawnAttachedTo(null, area);
            SEntMan.EnsureComponent<NukeopsRuleComponent>(rule);
            var antagComp = SEntMan.EnsureComponent<AntagSelectionComponent>(rule);
            var commander = Server.ResolveDependency<IPrototypeManager>().Index(Commander);
            var minds = SEntMan.System<SharedMindSystem>();
            var buyer = SEntMan.SpawnAttachedTo(Human, area);
            SEntMan.EnsureComponent<NukeOperativeComponent>(buyer);
            var buyerMind = minds.CreateMind(null, "Tactical map buyer");
            minds.TransferTo(buyerMind, buyer);
            var selection = new AfterAntagEntitySelectedEvent(null, buyer, (rule, antagComp), commander);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref selection);

            var buyerMap = SEntMan.GetComponent<TacticalMapOperationComponent>(buyer);
            Assert.That(buyerMap.Rule, Is.EqualTo(rule));
            Assert.That(buyerMap.Mind, Is.EqualTo(buyerMind.Owner));
            Assert.That(buyerMap.Implant, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<SubdermalImplantComponent>(buyerMap.Implant!.Value)
                .ImplantedEntity, Is.EqualTo(buyer));

            var radio = SEntMan.SpawnAttachedTo(Reinforcement, area);
            SEntMan.EventBus.RaiseLocalEvent(radio, new ItemPurchasedEvent(buyer));
            Assert.That(SEntMan.GetComponent<TacticalMapReinforcementRadioComponent>(radio).Rule, Is.EqualTo(rule));

            var reinforcement = SEntMan.SpawnAttachedTo(Human, area);
            SEntMan.EnsureComponent<NukeOperativeComponent>(reinforcement);
            var spawn = new GhostRoleSpawnerUsedEvent(radio, reinforcement);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref spawn);

            var reinforcementMap = SEntMan.GetComponent<TacticalMapOperationComponent>(reinforcement);
            Assert.That(reinforcementMap.Rule, Is.EqualTo(rule));
            Assert.That(reinforcementMap.Mind, Is.Null);
            Assert.That(reinforcementMap.Implant, Is.Not.Null);
            var reinforcementMind = minds.CreateMind(null, "Tactical map reinforcement");
            minds.TransferTo(reinforcementMind, reinforcement);
            Assert.That(reinforcementMap.Mind, Is.EqualTo(reinforcementMind.Owner));

            var outsider = SEntMan.SpawnAttachedTo(Human, area);
            var foreignRadio = SEntMan.SpawnAttachedTo(Reinforcement, area);
            SEntMan.EventBus.RaiseLocalEvent(foreignRadio, new ItemPurchasedEvent(outsider));
            Assert.That(SEntMan.GetComponent<TacticalMapReinforcementRadioComponent>(foreignRadio).Rule, Is.Null);

            minds.WipeMind(reinforcementMind);
            minds.WipeMind(buyerMind);
            SEntMan.DeleteEntity(rule);
        });
    }
}
// Arcane-End
