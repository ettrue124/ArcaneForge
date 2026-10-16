using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PowerToughnessModCreatedEventParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	private const string DETAIL_POWER = "power";

	private const string DETAIL_TOUGHNESS = "toughness";

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
		if (ai.AffectedIds.Count == 0)
		{
			yield break;
		}
		bool flag = ai.TryGetInt32Detail("power", out var value);
		bool toughnessModExists = ai.TryGetInt32Detail("toughness", out var toughnessMod);
		if (!flag && !toughnessModExists)
		{
			yield break;
		}
		uint affectorId = ai.AffectorId;
		uint num = ai.AffectedIds[0];
		if (!oldState.TryGetCard(num, out var oldInstance))
		{
			yield break;
		}
		MtgEntity cardById = newState.GetCardById(num);
		MtgCardInstance incrementalInstance = GreInterface.GetPreviousIncrementalEntity(num, changes) as MtgCardInstance;
		MtgCardInstance modifiedInstance = GreInterface.CreateNewIncrementalEntity(oldInstance, incrementalInstance, cardById) as MtgCardInstance;
		if (flag)
		{
			PropertyType property = ((value > 0) ? PropertyType.PowerUp : PropertyType.PowerDown);
			modifiedInstance.Power = new StringBackedInt(modifiedInstance.Power.Value + value);
			yield return new CardChangedEvent(incrementalInstance ?? oldInstance, modifiedInstance, affectorId, property);
			if (toughnessModExists)
			{
				incrementalInstance = modifiedInstance;
				modifiedInstance = modifiedInstance.GetCopy();
			}
		}
		if (toughnessModExists)
		{
			PropertyType property2 = ((toughnessMod > 0) ? PropertyType.ToughnessUp : PropertyType.ToughnessDown);
			modifiedInstance.Toughness = new StringBackedInt(modifiedInstance.Toughness.Value + toughnessMod);
			yield return new CardChangedEvent(incrementalInstance ?? oldInstance, modifiedInstance, ai.AffectorId, property2);
		}
	}
}
