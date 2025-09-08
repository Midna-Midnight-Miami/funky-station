// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Radio.EntitySystems;
using System.Linq;
using Content.Shared.Radio.Components;

namespace Content.Server.Radio.Components;

/// <summary>
/// Added to entities that are wearing headsets.
/// Allows speaking into radio channels.
/// Modified to support multiple headsets simultaneously.
/// </summary>
[RegisterComponent]
public sealed partial class WearingHeadsetComponent : Component
{
    /// <summary>
    /// All headsets currently being worn by this entity.
    /// Changed from single EntityUid to HashSet to support multiple headsets.
    /// </summary>
    [DataField("headsets")]
    public HashSet<EntityUid> Headsets = new();
    
    /// <summary>
    /// Legacy property for backwards compatibility.
    /// Returns the first headset or null if none equipped.
    /// </summary>
    public EntityUid? Headset
    {
        get => Headsets.FirstOrDefault();
        set
        {
            if (value.HasValue)
                Headsets.Add(value.Value);
        }
    }
}