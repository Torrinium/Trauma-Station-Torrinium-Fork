// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Trauma.Shared.Abnormalities;

namespace Content.Trauma.Server.Abnormalities;

public sealed partial class EnkaphalinExtractorSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbnormalityLinkerComponent, BeforeRangedInteractEvent>(OnLinkerInteract);
        SubscribeLocalEvent<EnkaphalinExtractorComponent, InteractUsingEvent>(OnExtractorInteractUsing);
    }

    // Step 1 of linking: use the linker on an abnormality, and it remembers it.
    private void OnLinkerInteract(Entity<AbnormalityLinkerComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<AbnormalityComponent>(target))
            return;

        args.Handled = true;

        if (!_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("abnormality-linker-not-alive"), target, args.User);
            return;
        }

        ent.Comp.Target = target;
        _popup.PopupEntity(Loc.GetString("abnormality-linker-scanned"), target, args.User);
    }

    // Step 2: use the linker on an extractor to bind them (or to cut the link if the linker is empty).
    private void OnExtractorInteractUsing(Entity<EnkaphalinExtractorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<AbnormalityLinkerComponent>(args.Used, out var linker))
            return;

        args.Handled = true;

        if (linker.Target is not { } target)
        {
            if (ent.Comp.Linked != null)
            {
                ent.Comp.Linked = null;
                _popup.PopupEntity(Loc.GetString("abnormality-extractor-unlinked"), ent, args.User);
            }

            return;
        }

        if (ent.Comp.Linked != null)
        {
            _popup.PopupEntity(Loc.GetString("abnormality-extractor-busy"), ent, args.User);
            return;
        }

        if (TerminatingOrDeleted(target) || IsLinked(target))
        {
            linker.Target = null;
            _popup.PopupEntity(Loc.GetString("abnormality-extractor-invalid"), ent, args.User);
            return;
        }

        ent.Comp.Linked = target;
        linker.Target = null;
        _popup.PopupEntity(Loc.GetString("abnormality-extractor-linked"), ent, args.User);
    }

    // One abnormality can only feed one extractor.
    private bool IsLinked(EntityUid abnormality)
    {
        var query = EntityQueryEnumerator<EnkaphalinExtractorComponent>();
        while (query.MoveNext(out _, out var extractor))
        {
            if (extractor.Linked == abnormality)
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EnkaphalinExtractorComponent, PowerSupplierComponent>();
        while (query.MoveNext(out _, out var extractor, out var supplier))
        {
            // A deleted abnormality frees the machine.
            if (extractor.Linked is { } linked && TerminatingOrDeleted(linked))
                extractor.Linked = null;

            var output = extractor.Linked is { } abnormality && _mobState.IsAlive(abnormality)
                ? extractor.PowerOutput
                : 0f;

            if (supplier.MaxSupply != output)
                supplier.MaxSupply = output;
        }
    }
}
