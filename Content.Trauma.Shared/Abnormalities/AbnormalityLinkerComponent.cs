// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Trauma.Shared.Abnormalities;

/// <summary>
/// A handheld tool that remembers an abnormality so it can be bound to an extractor.
/// </summary>
[RegisterComponent]
public sealed partial class AbnormalityLinkerComponent : Component
{
    /// <summary>
    /// The abnormality this linker is currently attuned to.
    /// </summary>
    [DataField]
    public EntityUid? Target;
}
