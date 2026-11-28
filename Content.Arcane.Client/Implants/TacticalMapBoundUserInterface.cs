// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Arcane.Shared.Implants;
using Content.Client.Pinpointer.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Arcane.Client.Implants;

[UsedImplicitly]
public sealed class TacticalMapBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private StationMapWindow? _window;
    private TacticalMapUpdateMessage? _latest;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<StationMapWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        if (_latest is { } update)
            Apply(update);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not TacticalMapUpdateMessage update)
            return;

        _latest = update;
        Apply(update);
    }

    private void Apply(TacticalMapUpdateMessage update)
    {
        EntMan.TryGetEntity(update.Grid, out var grid);
        var stationName = EntMan.TryGetComponent<MetaDataComponent>(grid, out var metadata)
            ? metadata.EntityName
            : string.Empty;
        _window?.SetTacticalMarkers(stationName, grid, Owner, update.Allies);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _window?.Dispose();
            _window = null;
            _latest = null;
        }

        base.Dispose(disposing);
    }
}
