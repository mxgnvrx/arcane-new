using Content.Arcane.Server.Augments;
using Content.Arcane.Shared.Augments;
using Content.Goobstation.Common.BlockTeleport;
using Content.IntegrationTests.Tests.Interaction;
using Content.Medical.Common.Body;
using Content.Medical.Shared.Body;
using Content.Medical.Shared.Surgery.Conditions;
using Content.Shared.Body;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Implants;
using Content.Shared.Toggleable;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Arcane;

// Arcane-Start
public sealed class CyberneticsPortTest : InteractionTest
{
    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId Grounding = "BluespaceGroundingImplant";
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> LeftArm = "ArmLeft";
    private static readonly ProtoId<OrganCategoryPrototype> ArmAugment = "ArmAugment";

    [TestCase("BreathingTubeAugment", "ArcaneBreathingTube", "SurgeryOpenBreathingTubeCavity", "SurgeryInsertBreathingTube", "SurgeryRemoveBreathingTube")]
    [TestCase("AntiDropAugment", "ArcaneAntiDrop", "SurgeryOpenAntiDropCavity", "SurgeryInsertAntiDrop", "SurgeryRemoveAntiDrop")]
    [TestCase("CnsRebooterAugment", "ArcaneCnsRebooter", "SurgeryOpenCnsRebooterCavity", "SurgeryInsertCnsRebooter", "SurgeryRemoveCnsRebooter")]
    public async Task SurgicalPrototypesMatchTheInstalledOrgan(
        string augmentId, string categoryId, string cavityId, string insertionId, string removalId)
    {
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.GetCoordinates(PlayerCoords);
            var body = SEntMan.SpawnAttachedTo(Human, area);
            var anatomy = SEntMan.System<BodyPartSystem>();
            var bodies = SEntMan.System<BodySystem>();
            var head = bodies.GetOrgan(body, Head);
            Assert.That(head, Is.Not.Null);

            ProtoId<OrganCategoryPrototype> category = categoryId;
            var cavity = SEntMan.SpawnAttachedTo(new EntProtoId(cavityId), area);
            var insertion = SEntMan.SpawnAttachedTo(new EntProtoId(insertionId), area);
            var removal = SEntMan.SpawnAttachedTo(new EntProtoId(removalId), area);
            Assert.That(SEntMan.GetComponent<SurgeryOrganSlotConditionComponent>(cavity).OrganSlot, Is.EqualTo(category));
            Assert.That(SEntMan.GetComponent<SurgeryOrganSlotConditionComponent>(insertion).OrganSlot, Is.EqualTo(category));
            Assert.That(SEntMan.GetComponent<SurgeryOrganConditionComponent>(insertion).Organ, Is.EqualTo(category));
            Assert.That(SEntMan.GetComponent<SurgeryOrganConditionComponent>(removal).Organ, Is.EqualTo(category));

            var implant = SEntMan.SpawnAttachedTo(new EntProtoId(augmentId), area);
            Assert.That(anatomy.TryAddSlot(head!.Value, category), Is.True);
            Assert.That(anatomy.InsertOrgan(head.Value, implant), Is.True);
            Assert.That(bodies.GetBody(implant), Is.EqualTo(body));
            Assert.That(SEntMan.HasComponent<EnabledOrganComponent>(implant), Is.True);
            Assert.That(anatomy.RemoveOrgan(head.Value, implant), Is.True);
            Assert.That(bodies.GetBody(implant), Is.Null);
            Assert.That(SEntMan.HasComponent<EnabledOrganComponent>(implant), Is.False);
        });
    }

    [Test]
    public async Task GroundingCancelsTeleportAttempt()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnAttachedTo(Human, SEntMan.GetCoordinates(PlayerCoords));
            var implants = SEntMan.System<SharedSubdermalImplantSystem>();
            Assert.That(implants.AddImplant(human, Grounding), Is.Not.Null);
            var attempt = new TeleportAttemptEvent(false);
            SEntMan.EventBus.RaiseLocalEvent(human, ref attempt);
            Assert.That(attempt.Cancelled, Is.True);
        });
    }

    [TestCase("LaserArmAugment")]
    [TestCase("PhotonProjectorAugment")]
    [TestCase("ArmElectrificationAugment")]
    public async Task ArmDeploysAndRetractsItsOwnItem(string augmentId)
    {
        await Server.WaitAssertion(() =>
        {
            var area = SEntMan.GetCoordinates(PlayerCoords);
            var body = SEntMan.SpawnAttachedTo(Human, area);
            var implant = SEntMan.SpawnAttachedTo(new EntProtoId(augmentId), area);
            var bodies = SEntMan.System<BodySystem>();
            var anatomy = SEntMan.System<BodyPartSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var arm = bodies.GetOrgan(body, LeftArm);
            Assert.That(arm, Is.Not.Null);
            Assert.That(anatomy.TryAddSlot(arm!.Value, ArmAugment), Is.True);
            Assert.That(anatomy.InsertOrgan(arm.Value, implant), Is.True);
            var component = SEntMan.GetComponent<DeployableArmComponent>(implant);

            var toggle = new ToggleActionEvent { Performer = body };
            SEntMan.EventBus.RaiseLocalEvent(implant, toggle);
            Assert.That(toggle.Handled, Is.True);
            Assert.That(component.Item, Is.Not.Null);
            Assert.That(hands.IsHolding(body, component.Item!.Value), Is.True);

            toggle = new ToggleActionEvent { Performer = body };
            SEntMan.EventBus.RaiseLocalEvent(implant, toggle);
            Assert.That(toggle.Handled, Is.True);
            Assert.That(hands.IsHolding(body, component.Item.Value), Is.False);
        });
    }
}
// Arcane-End