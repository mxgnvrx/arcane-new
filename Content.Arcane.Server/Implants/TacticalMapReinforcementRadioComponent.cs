// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Arcane.Server.Implants;

/// <summary>
/// Links the purchased reinforcement radio to its buyer's operative mission.
/// </summary>
[RegisterComponent]
public sealed partial class TacticalMapReinforcementRadioComponent : Component
{
    public EntityUid? Rule;
}
