// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

namespace Content.Trauma.Shared.Abnormalities;

/// <summary>
/// A one-use injector that triggers the Cogito transformation on whoever it is used on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CogitoInjectorComponent : Component
{
    // How long the injection takes.
    [DataField]
    public TimeSpan InjectDelay = TimeSpan.FromSeconds(5);

    // Sound played when injection completes.
    [DataField]
    public SoundSpecifier? InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    // Whether the injector has been used up.
    [DataField, AutoNetworkedField]
    public bool UsedUp;

    /// <summary>
    /// The pool a random abnormality is picked from when this injector is used.
    /// </summary>
    [DataField]
    public ProtoId<EntityTablePrototype> AbnormalityTable = "CogitoAbnormalities";

}
