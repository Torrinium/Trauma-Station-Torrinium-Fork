// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Medical.Common.Targeting;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Jittering;
using Content.Server.NPC.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Trauma.Shared.Abnormalities;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.EntityTable;

namespace Content.Trauma.Server.Abnormalities;

public sealed partial class CogitoTransformationSystem : EntitySystem
{
    [Dependency] private JitteringSystem _jitter = default!;
    [Dependency] private StutteringSystem _stutter = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityQuery<VocalComponent> _vocalQuery = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;

    private static readonly EntProtoId DebugAbnormality = "MobAbnormalityDebug";
    private static readonly TimeSpan PromptTime = TimeSpan.FromSeconds(30);

    private readonly List<CogitoPromptEui> _prompts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CogitoInjectorComponent, CogitoInjectedEvent>(OnCogitoInjected);
    }

    private void OnCogitoInjected(Entity<CogitoInjectorComponent> ent, ref CogitoInjectedEvent args)
    {
        var target = args.Target;

        var transforming = EnsureComp<CogitoTransformingComponent>(target);
        transforming.EndTime = _timing.CurTime + transforming.Delay;

        var table = ProtoMan.Index(ent.Comp.AbnormalityTable);
        transforming.Abnormality = _entityTable.GetFirstOrNull(table);

        _jitter.DoJitter(target, transforming.Delay, true, 10f, 10f, true);
        _stutter.DoStutter(target, transforming.Delay, refresh: true);

        if (_vocalQuery.TryComp(target, out var vocal))
            _chat.TryEmoteWithChat(target, vocal.ScreamId, forceEmote: true);
    }

    private void FinishTransformation(EntityUid uid, CogitoTransformingComponent comp)
    {
        // Remember where they're standing before anything happens to the body.
        var coordinates = Transform(uid).Coordinates;

        // 1. Capture the mind BEFORE the head comes off.
        _mind.TryGetMind(uid, out var mindId, out var mind);
        Log.Info($"Cogito: {ToPrettyString(uid)} has a mind before decapitation: {mind != null}");

        // 2. Destroy the head.
        _popup.PopupEntity(Loc.GetString("cogito-head-burst"), uid, PopupType.LargeCaution);
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", 300);
        _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, targetPart: TargetBodyPart.Head);
        var decapitated = _body.TryDecapitate(uid, uid);
        Log.Info($"Cogito: TryDecapitate returned {decapitated}");

        // 3. Find out where the mind ended up.
        if (mind != null)
            Log.Info($"Cogito: mind {ToPrettyString(mindId)} is now in {ToPrettyString(mind.OwnedEntity)}");

        // 4. The abnormality arrives.
        var abnormality = Spawn(comp.Abnormality ?? DebugAbnormality, coordinates);

        // 5. Ask the player whether they want to take control.
        if (mind != null && _player.TryGetSessionById(mind.UserId, out var session))
        {
            // Keep the abnormality still while the player decides.
            _npc.SleepNPC(abnormality);

            var prompt = new CogitoPromptEui(this, mindId, abnormality, _timing.CurTime + PromptTime);
            _prompts.Add(prompt);
            _eui.OpenEui(prompt, session);
        }
        else
        {
            // No player to ask, so offer it to ghosts straight away.
            OfferToGhosts(abnormality);
        }
    }

    public void ResolvePrompt(CogitoPromptEui prompt, bool accepted)
    {
        if (prompt.Resolved)
            return;

        prompt.Resolved = true;
        _prompts.Remove(prompt);

        if (accepted)
            AcceptPossession(prompt.MindId, prompt.Abnormality);
        else
            DeclinePossession(prompt.Abnormality);
    }

    private void DeclinePossession(EntityUid abnormality)
    {
        if (TerminatingOrDeleted(abnormality))
            return;

        // Nobody is driving it, so it goes back to its own AI...
        _npc.WakeNPC(abnormality);

        // ...and any ghost can take it over.
        OfferToGhosts(abnormality);
    }

    public void AcceptPossession(EntityUid mindId, EntityUid abnormality)
    {
        // The window can sit open for a while, so check both still exist.
        if (TerminatingOrDeleted(abnormality) || TerminatingOrDeleted(mindId))
            return;

        _mind.MakeSentient(abnormality);
        _mind.TransferTo(mindId, abnormality);
    }
    private void OfferToGhosts(EntityUid abnormality)
    {
        if (TerminatingOrDeleted(abnormality))
            return;

        var role = EnsureComp<GhostRoleComponent>(abnormality);
        EnsureComp<GhostTakeoverAvailableComponent>(abnormality);

        role.RoleName = Loc.GetString("cogito-ghost-role-name", ("name", Name(abnormality)));
        role.RoleDescription = Loc.GetString("cogito-ghost-role-description");
        role.AllowMovement = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<CogitoTransformingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EndTime > now)
                continue;

            FinishTransformation(uid, comp);
            RemCompDeferred(uid, comp);
        }

        // Expire prompts the player never answered, or whose subject is gone.
        for (var i = _prompts.Count - 1; i >= 0; i--)
        {
            var prompt = _prompts[i];

            if (prompt.Deadline > now
                && !TerminatingOrDeleted(prompt.Abnormality)
                && !TerminatingOrDeleted(prompt.MindId))
                continue;

            ResolvePrompt(prompt, false);
            prompt.Close();
        }
    }
}
