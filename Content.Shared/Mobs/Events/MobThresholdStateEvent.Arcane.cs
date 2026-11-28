// SPDX-License-Identifier: AGPL-3.0-or-later

// Arcane-Start
namespace Content.Shared.Mobs.Events;

/// <summary>
/// Raised before a damage threshold changes a mob's state.
/// </summary>
[ByRefEvent]
public record struct MobThresholdStateEvent(MobState State);
// Arcane-End
