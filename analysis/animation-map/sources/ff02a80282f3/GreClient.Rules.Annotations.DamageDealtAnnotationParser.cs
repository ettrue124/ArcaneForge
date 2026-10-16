using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DamageDealtAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public const string TYPE_DETAIL = "type";

	public const string DAMAGE_DETAIL = "damage";

	private const string DETAIL_MARK_DAMAGE = "markDamage";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (oldState == null)
		{
			throw new ArgumentNullException("old gamestate state not set");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("new gamestate state not set");
		}
		if (changes == null)
		{
			throw new ArgumentNullException("changelist not set");
		}
		MtgCardInstance cardById = oldState.GetCardById(ai.AffectorId);
		MtgCardInstance cardById2 = newState.GetCardById(ai.AffectorId);
		MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(ai.AffectorId, changes) as MtgCardInstance;
		DamageType type = (DamageType)ai.GetInt32Detail("type");
		int amount = ai.GetInt32Detail("damage");
		bool markDamage = ai.GetInt32Detail("markDamage") == 1;
		MtgCardInstance damager = mtgCardInstance ?? cardById ?? cardById2;
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgEntity oldVictim = oldState.GetEntityById(affectedId);
			MtgEntity newVictim = newState.GetEntityById(affectedId);
			MtgEntity previousIncrementalEntity = GreInterface.GetPreviousIncrementalEntity(affectedId, changes);
			MtgEntity victim = previousIncrementalEntity ?? oldVictim ?? newVictim;
			yield return new DamageDealtEvent(damager, victim, (uint)amount, type);
			if (markDamage && victim is MtgCardInstance mtgCardInstance2)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(oldVictim, mtgCardInstance2, newVictim) as MtgCardInstance;
				mtgCardInstance3.Damage += (uint)amount;
				mtgCardInstance3.Damaged = mtgCardInstance3.Damage != 0;
				yield return new CardChangedEvent(mtgCardInstance2 ?? (oldVictim as MtgCardInstance), mtgCardInstance3, ai.AffectorId, PropertyType.Damage);
			}
		}
	}
}
