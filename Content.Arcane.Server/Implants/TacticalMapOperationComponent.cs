// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Arcane.Server.Implants;

[RegisterComponent]
public sealed partial class TacticalMapOperationComponent : Component
{
    public EntityUid? Rule;
    public EntityUid? Mind;
    public EntityUid? Implant;
}
