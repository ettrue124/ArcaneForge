using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class QualificationAnnotationParser : IAnnotationStateParser
{
	public const string DETAILKEY_QUALTYPE = "QualificationType";

	public const string DETAILKEY_QUALSUBTYPE = "QualificationSubtype";

	public const string DETAILKEY_GRPID = "grpid";

	public const string DETAILKEY_PARENTGRPID = "parent_grpid";

	public const string DETAILKEY_SOURCEPARENT = "SourceParent";

	private const string QUAL_DATA_KEY_FORMAT = "[Qualification] Tgt:{0}_Src:{1}_Type:{2}";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.Qualification))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		newState.TrackHistoricEntity(ai.AffectorId, oldState);
		QualificationType qualificationType = (QualificationType)ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("QualificationType")).ValueInt32[0];
		QualificationSubtype subType = QualificationSubtype.None;
		KeyValuePairInfo keyValuePairInfo = ai.Details.FirstOrDefault((KeyValuePairInfo kvpi) => kvpi.Key.Equals("QualificationSubtype"));
		if (keyValuePairInfo != null)
		{
			subType = (QualificationSubtype)keyValuePairInfo.ValueInt32[0];
		}
		uint abilityId = (uint)ai.Details.First((KeyValuePairInfo kvpi) => kvpi.Key.Equals("grpid")).ValueInt32[0];
		uint abilityParentGrpId = (uint)(ai.Details.FirstOrDefault((KeyValuePairInfo kvpi) => kvpi.Key.Equals("parent_grpid"))?.ValueInt32[0] ?? 0);
		uint sourceParent = (uint)(ai.Details.FirstOrDefault((KeyValuePairInfo kvpi) => kvpi.Key.Equals("SourceParent"))?.ValueInt32[0] ?? 0);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Type == KeyValuePairValueType.String)
			{
				dictionary[detail.Key] = detail.ValueString[0];
			}
			if (detail.Type == KeyValuePairValueType.Int32)
			{
				dictionary[detail.Key] = detail.ValueInt32[0].ToString();
			}
			if (detail.Type == KeyValuePairValueType.Uint32)
			{
				dictionary[detail.Key] = detail.ValueUint32[0].ToString();
			}
		}
		uint affectorId = ai.AffectorId;
		MtgEntity mtgEntity = newState.GetEntityById(affectorId);
		if (mtgEntity == null && newState.TrackedHistoricCards.ContainsKey(affectorId))
		{
			mtgEntity = newState.TrackedHistoricCards[affectorId];
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			QualificationData item = new QualificationData
			{
				Id = ai.Id,
				SourceParent = sourceParent,
				Type = qualificationType,
				SubType = subType,
				AffectorId = affectorId,
				AffectedId = affectedId,
				AbilityId = abilityId,
				AbilityParentGrpId = abilityParentGrpId,
				Details = dictionary,
				Key = $"[Qualification] Tgt:{affectedId}_Src:{affectorId}_Type:{(int)qualificationType}"
			};
			if (newState.TryGetEntity(affectedId, out var mtgEntity2))
			{
				if (qualificationType == QualificationType.CantBeCountered && mtgEntity2 is MtgCardInstance { Zone: var zone } && (zone == null || zone.Type != ZoneType.Stack))
				{
					continue;
				}
				if (mtgEntity != null)
				{
					mtgEntity.AffectorOfQualifications.Add(item);
					if (mtgEntity is MtgCardInstance { ObjectType: GameObjectType.Ability } mtgCardInstance2 && newState.TryGetCard(mtgCardInstance2.ParentId, out var card))
					{
						card.AffectorOfQualifications.Add(item);
					}
				}
				mtgEntity2.AffectedByQualifications.Add(item);
			}
			newState.Qualifications.Add(item);
		}
	}
}
