using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DamageDealtEventTranslator : IEventTranslator
{
	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IEntityViewProvider _viewProvider;

	private readonly CombatAnimationPlayer _combatAnimationPlayer;

	public DamageDealtEventTranslator(IContext context, CombatAnimationPlayer combatAnimationPlayer)
		: this(context.Get<IResolutionEffectProvider>(), context.Get<IEntityViewProvider>(), combatAnimationPlayer)
	{
	}

	public DamageDealtEventTranslator(IResolutionEffectProvider resolutionEffectProvider, IEntityViewProvider viewProvider, CombatAnimationPlayer combatAnimationPlayer)
	{
		_resolutionEffectProvider = resolutionEffectProvider;
		_viewProvider = viewProvider;
		_combatAnimationPlayer = combatAnimationPlayer;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		DamageDealtEvent damageDealtEvent = allChanges[changeIndex] as DamageDealtEvent;
		if (damageDealtEvent == null)
		{
			return;
		}
		MtgCardInstance mtgCardInstance = damageDealtEvent.Damager;
		if (damageDealtEvent.Type == DamageType.Fight)
		{
			for (int num = events.Count - 1; num >= 0; num--)
			{
				if (events[num] is UXEventDamageDealt uXEventDamageDealt && damageDealtEvent.Victim.InstanceId == uXEventDamageDealt.Source.InstanceId && damageDealtEvent.Damager.InstanceId == uXEventDamageDealt.Target.InstanceId)
				{
					ResolutionEvent resolutionEvent = null;
					for (int i = changeIndex; i < allChanges.Count; i++)
					{
						if (allChanges[i] is ResolutionEvent { IsStart: false } resolutionEvent2)
						{
							resolutionEvent = resolutionEvent2;
							break;
						}
					}
					if (resolutionEvent != null && oldState.GetEntityById(resolutionEvent.InstanceID) is MtgCardInstance mtgCardInstance2 && mtgCardInstance2.Controller.InstanceId == damageDealtEvent.Damager.Controller.InstanceId)
					{
						events.RemoveAt(num);
						break;
					}
					return;
				}
			}
		}
		if (mtgCardInstance.Zone.Type == ZoneType.Limbo)
		{
			MtgCardInstance cardById = oldState.GetCardById(damageDealtEvent.Damager.InstanceId);
			if (cardById.Zone.Type != ZoneType.Stack && cardById.Zone.Type != ZoneType.Battlefield)
			{
				mtgCardInstance = oldState.Stack.VisibleCards.Find((MtgCardInstance x) => x.Parent != null && x.Parent.InstanceId == damageDealtEvent.Damager.InstanceId);
				if (mtgCardInstance == null && oldState.Stack.VisibleCards.Count > 0)
				{
					mtgCardInstance = oldState.Stack.VisibleCards[0];
				}
			}
		}
		if (mtgCardInstance != null && damageDealtEvent.Victim != null)
		{
			UXEventDamageDealt item = new UXEventDamageDealt(mtgCardInstance, damageDealtEvent.Victim, (int)damageDealtEvent.Amount, damageDealtEvent.Type, damageDealtEvent.Type == DamageType.Combat && mtgCardInstance.Controller.ClientPlayerEnum != newState.ActivePlayer.ClientPlayerEnum, _resolutionEffectProvider, _viewProvider, _combatAnimationPlayer);
			events.Add(item);
		}
		else
		{
			Debug.LogError("DamageEvent could not be converted to UXEvent because the appropriate damager and victim instances could not be located.");
		}
	}
}
