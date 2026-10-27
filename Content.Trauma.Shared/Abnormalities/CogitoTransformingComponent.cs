// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;

namespace Content.Trauma.Shared.Abnormalities;

/// <summary>
/// Added to someone injected with Cogito. When the timer runs out, the transformation happens.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CogitoTransformingComponent : Component
{
    /// <summary>
    /// How long the shaking lasts before the abnormality appears.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// When the countdown ends. Set by the system when the injection happens.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;

    /// <summary>
    /// The abnormality this person will become. Picked when the injection happens.
    /// </summary>
    [DataField]
    public EntProtoId? Abnormality;
}
