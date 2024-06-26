﻿using Content.Shared._CM14.Xenos.Plasma;
using Content.Shared._CM14.Xenos.Weeds;
using Content.Shared.Actions;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CM14.Xenos.Walker;

public sealed class XenoResinWalkerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoResinWalkerComponent, XenoResinWalkerActionEvent>(OnXenoResinWalkerAction);
        SubscribeLocalEvent<XenoResinWalkerComponent, RefreshMovementSpeedModifiersEvent>(OnXenoResinWalkerRefreshMovementSpeed);

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    private void OnXenoResinWalkerAction(Entity<XenoResinWalkerComponent> xeno, ref XenoResinWalkerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!xeno.Comp.Active &&
            !_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
        {
            return;
        }

        args.Handled = true;

        xeno.Comp.Active = !xeno.Comp.Active;
        xeno.Comp.NextPlasmaUse = _timing.CurTime + xeno.Comp.PlasmaUseDelay;
        Dirty(xeno);

        _movementSpeed.RefreshMovementSpeedModifiers(xeno);

        foreach (var (actionId, action) in _actions.GetActions(xeno))
        {
            if (action.BaseEvent is XenoResinWalkerActionEvent)
                _actions.SetToggled(actionId, xeno.Comp.Active);
        }
    }

    private void OnXenoResinWalkerRefreshMovementSpeed(Entity<XenoResinWalkerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active &&
            TryComp(ent, out AffectableByWeedsComponent? affected) &&
            affected.OnXenoWeeds)
        {
            args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenoResinWalkerComponent>();
        while (query.MoveNext(out var uid, out var walker))
        {
            if (!walker.Active || _timing.CurTime < walker.NextPlasmaUse)
                continue;

            walker.NextPlasmaUse = _timing.CurTime + walker.PlasmaUseDelay;

            if (!_xenoPlasma.TryRemovePlasma(uid, walker.PlasmaUpkeep))
            {
                walker.Active = false;
                Dirty(uid, walker);

                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
}
