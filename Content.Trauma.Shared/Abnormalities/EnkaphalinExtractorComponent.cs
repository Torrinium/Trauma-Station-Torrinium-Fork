// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Trauma.Shared.Abnormalities;

/// <summary>
/// A machine that draws power from a linked abnormality for as long as it is alive.
/// </summary>
[RegisterComponent]
public sealed partial class EnkaphalinExtractorComponent : Component
{
    /// <summary>
    /// The abnormality this machine is linked to, if any.
    /// </summary>
    [DataField]
    public EntityUid? Linked;

    /// <summary>
    /// Power supplied, in watts, while the linked abnormality is alive.
    /// </summary>
    [DataField]
    public float PowerOutput = 3000f;
}
