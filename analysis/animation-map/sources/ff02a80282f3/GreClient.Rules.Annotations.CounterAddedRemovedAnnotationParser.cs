using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public abstract class CounterAddedRemovedAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public const string DETAIL_COUNTER_TYPE = "counter_type";

	public const string DETAIL_TRANSACTION_AMOUNT = "transaction_amount";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	protected abstract int GetCount(AnnotationInfo ai);

	public virtual IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected IDs.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details.", "ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		int count = GetCount(ai);
		CounterType counterType = (CounterType)ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("counter_type")).ValueInt32[0];
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgEntity oldInstance = oldState.GetEntityById(affectedId);
			if (oldInstance != null)
			{
				MtgEntity newInstance = newState.GetEntityById(affectedId);
				MtgEntity previousIncrementalEntity = GreInterface.GetPreviousIncrementalEntity(affectedId, changes);
				MtgEntity mtgEntity = GreInterface.CreateNewIncrementalEntity(oldInstance, previousIncrementalEntity, newInstance);
				if (!mtgEntity.Counters.ContainsKey(counterType))
				{
					mtgEntity.Counters[counterType] = 0;
				}
				mtgEntity.Counters[counterType] += count;
				if (mtgEntity.Counters[counterType] == 0)
				{
					mtgEntity.Counters.Remove(counterType);
				}
				yield return new CountersChangedEvent(oldInstance, mtgEntity, ai.AffectorId, count, counterType);
				if (counterType == CounterType.Loyalty && oldInstance is MtgCardInstance)
				{
					MtgCardInstance oldInstance2 = (MtgCardInstance)oldInstance;
					MtgCardInstance newInstance2 = (MtgCardInstance)newInstance;
					PropertyType property = ((count < 0) ? PropertyType.LoyaltyDown : PropertyType.LoyaltyUp);
					yield return new CardChangedEvent(oldInstance2, newInstance2, ai.AffectorId, property);
				}
				else if (counterType == CounterType.Defense)
				{
					MtgCardInstance oldInstance3 = (MtgCardInstance)oldInstance;
					MtgCardInstance newInstance3 = (MtgCardInstance)newInstance;
					PropertyType property2 = ((count < 0) ? PropertyType.DefenseDown : PropertyType.DefenseUp);
					yield return new CardChangedEvent(oldInstance3, newInstance3, ai.AffectorId, property2);
				}
			}
		}
	}
}
