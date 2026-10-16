using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ModifiedLifeAnnotationParser : IAnnotationEventParser, INeedMtgGameState_New, INeedList_Changes
{
	public const string LIFE_DETAIL = "life";

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.ModifiedLife))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ID.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details.", "ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		uint affectorId = ai.AffectorId;
		MtgPlayer playerById = newState.GetPlayerById(ai.AffectedIds.First());
		int num = ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key == "life").ValueInt32[0];
		if (num < 0)
		{
			foreach (UpdatePlayerLifeTotal item in GetRelatedLifeTotalUpdatesBeforeThisOne(playerById, changes))
			{
				num -= item.LifeChange;
			}
		}
		if (num != 0)
		{
			yield return new UpdatePlayerLifeTotal
			{
				Affector = affectorId,
				Player = playerById,
				LifeChange = num,
				GeneratedByAnnotation = true
			};
		}
	}

	private static IEnumerable<UpdatePlayerLifeTotal> GetRelatedLifeTotalUpdatesBeforeThisOne(MtgPlayer affected, IReadOnlyList<GameRulesEvent> changes)
	{
		int i = 0;
		while (i < changes.Count)
		{
			if (changes[i] is UpdatePlayerLifeTotal { GeneratedByAnnotation: false } updatePlayerLifeTotal && updatePlayerLifeTotal.Player.InstanceId == affected.InstanceId)
			{
				yield return updatePlayerLifeTotal;
			}
			int num = i + 1;
			i = num;
		}
	}
}
