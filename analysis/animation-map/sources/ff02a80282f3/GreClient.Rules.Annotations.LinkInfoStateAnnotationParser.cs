using System;
using System.Collections.Generic;
using ReferenceMap;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LinkInfoStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_LINKTYPE = "LinkType";

	public const string DETAIL_CHOOSELINKTYPE = "ChooseLinkType";

	public const string DETAIL_NOTEDVALUE = "value";

	public const string DETAIL_TYPE_CATEGORY = "TypeCategory";

	private const string DETAIL_SOURCE_ABILITY_ID = "sourceAbilityGRPID";

	public const uint TIBALT_GRPID = 75156u;

	private static readonly IReadOnlyDictionary<LinkType, Action<AnnotationInfo, MtgCardInstance, MtgGameState>> _linkTypeHandlers = new Dictionary<LinkType, Action<AnnotationInfo, MtgCardInstance, MtgGameState>>
	{
		[LinkType.Choose] = ProcessLinkTypeChoose,
		[LinkType.Exile] = ProcessLinkTypeExile,
		[LinkType.Sacrifice] = ProcessLinkTypeSacrifice,
		[LinkType.Tap] = ProcessLinkTypeTap,
		[LinkType.Note] = ProcessLinkTypeNote,
		[LinkType.PaidLife] = ProcessLinkType_PaidLife,
		[LinkType.LoseAbility] = ProcessLinkTypeLoseAbility
	};

	private static readonly IReadOnlyDictionary<string, Action<AnnotationInfo, MtgCardInstance, MtgGameState>> _chooseLinkTypeHandlers = new Dictionary<string, Action<AnnotationInfo, MtgCardInstance, MtgGameState>>
	{
		["CardName"] = ProcessLinkTypeChoose_CardName,
		["Color"] = ProcessLinkTypeChoose_Color,
		["Type"] = ProcessLinkTypeChoose_Type,
		["AttributedInstance"] = ProcessLinkTypeChoose_AttributedInstance,
		["Amount"] = ProcessLinkTypeChoose_Amount,
		["Parity"] = ProcessLinkTypeChoose_Parity,
		["GamePlayer"] = ProcessLinkTypeGamePlayer,
		["AnchorWord"] = ProcessLinkTypeAnchorWord
	};

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.LinkInfo))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		LinkType int32Detail = (LinkType)ai.GetInt32Detail("LinkType");
		if (_linkTypeHandlers.TryGetValue(int32Detail, out var value) && newState.TryGetCard(ai.AffectorId, out var card))
		{
			value(ai, card, newState);
		}
	}

	private static void ProcessLinkType_PaidLife(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		if (ai.AffectedIds.Count == 1)
		{
			affectorCard.ActiveAbilityWords.Add(new AbilityWordData
			{
				AbilityWord = "LinkInfoPaidLife",
				IsActive = true,
				AdditionalDetail = ai.AffectedIds[0].ToString()
			});
		}
	}

	private static void ProcessLinkTypeChoose(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		string stringDetail = ai.GetStringDetail("ChooseLinkType");
		if (_chooseLinkTypeHandlers.TryGetValue(stringDetail, out var value))
		{
			value(ai, affectorCard, newState);
			return;
		}
		throw new ArgumentException("Unhandled Choose Link Type: " + stringDetail);
	}

	private static void ProcessLinkTypeChoose_CardName(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (affectedId != 0 && !affectorCard.LinkedInfoTitleLocIds.Contains(affectedId))
			{
				affectorCard.LinkedInfoTitleLocIds.Add(affectedId);
			}
		}
	}

	private static void ProcessLinkTypeChoose_Color(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			AddToLinkInfoText(ai, TypeCategory.Color, (int)affectedId, affectorCard);
		}
	}

	private static void ProcessLinkTypeChoose_Type(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		if (ai.AffectedIds.Count < 2)
		{
			throw new ArgumentException("Annotation did not contain enough AffectorIds for ChooseLinkType 'Type'.", "ai");
		}
		if (!Enum.IsDefined(typeof(TypeCategory), (int)ai.AffectedIds[0]))
		{
			throw new ArgumentException("Annotation did not contain a valid TypeCategory in AffectorIds 0 for ChooseLinkType 'Type'.", "ai");
		}
		TypeCategory typeCategory = (TypeCategory)ai.AffectedIds[0];
		int enumValue = (int)ai.AffectedIds[1];
		AddToLinkInfoText(ai, typeCategory, enumValue, affectorCard);
	}

	private static void AddToLinkInfoText(AnnotationInfo ai, TypeCategory typeCategory, int enumValue, MtgCardInstance affectorCard)
	{
		LinkedInfoText linkedInfoText = new LinkedInfoText(typeCategory, enumValue, new LinkInfoData(ai.Id, (LinkType)ai.GetInt32Detail("LinkType"), ai.AffectorId, (uint)enumValue, (uint)ai.GetInt32Detail("sourceAbilityGRPID")));
		if (!affectorCard.LinkedInfoText.Exists(linkedInfoText, (LinkedInfoText current, LinkedInfoText newInfo) => current.Equals(newInfo)))
		{
			affectorCard.LinkedInfoText.Add(linkedInfoText);
		}
	}

	private static void ProcessLinkTypeChoose_AttributedInstance(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.LinkedTo, affectedId);
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				AnnotationDataUtils.ExtractDetails(ai, dictionary);
				LinkInfoData item = new LinkInfoData(ai.Id, LinkType.Choose, ai.AffectorId, affectedId, 0u, dictionary);
				affectorCard.AffectorOfLinkInfos.Add(item);
				cardById.AffectedByLinkInfos.Add(item);
			}
		}
	}

	private static void ProcessLinkTypeChoose_Amount(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		if (ai.AffectedIds.Count == 1)
		{
			affectorCard.ActiveAbilityWords.Add(new AbilityWordData
			{
				AbilityWord = "LinkInfoAmount",
				AbilityGrpId = (uint)ai.GetInt32Detail("sourceAbilityGRPID"),
				IsActive = true,
				AdditionalDetail = ai.AffectedIds[0].ToString()
			});
		}
	}

	private static void ProcessLinkTypeChoose_Parity(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		if (ai.AffectedIds.Count == 1)
		{
			affectorCard.ActiveAbilityWords.Add(new AbilityWordData
			{
				AbilityWord = "LinkInfoParity",
				IsActive = true,
				Choice = ai.AffectedIds[0].ToString()
			});
		}
	}

	private static void ProcessLinkTypeExile(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.LinkedTo, affectedId);
		}
		if (affectorCard.GrpId != 75156)
		{
			return;
		}
		MtgZone zone = newState.GetZone(ZoneType.Command, GREPlayerNum.Invalid);
		if (zone == null)
		{
			return;
		}
		foreach (MtgCardInstance visibleCard in zone.VisibleCards)
		{
			if (visibleCard.ObjectType != GameObjectType.Emblem || visibleCard.ObjectSourceGrpId != 75156 || visibleCard.Owner?.ClientPlayerEnum != affectorCard.Owner?.ClientPlayerEnum)
			{
				continue;
			}
			foreach (uint affectedId2 in ai.AffectedIds)
			{
				newState.ReferenceMap.Add(visibleCard.InstanceId, ReferenceMap.ReferenceType.LinkedTo, affectedId2);
			}
		}
	}

	private static void ProcessLinkTypeSacrifice(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			affectorCard.AdditionalCostIds.Add(affectedId);
		}
	}

	private static void ProcessLinkTypeTap(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			affectorCard.AdditionalCostIds.Add(affectedId);
		}
	}

	private static void ProcessLinkTypeNote(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		TypeCategory int32Detail = (TypeCategory)ai.GetInt32Detail("TypeCategory");
		if (int32Detail != TypeCategory.None)
		{
			foreach (int @int in ai.GetIntList("value"))
			{
				AddToLinkInfoText(ai, int32Detail, @int, affectorCard);
			}
			return;
		}
		foreach (int int2 in ai.GetIntList("value"))
		{
			affectorCard.AffectorOfLinkInfos.Add(new LinkInfoData(ai.Id, LinkType.Note, ai.AffectorId, 0u, 0u, new Dictionary<string, object> { { "Note", int2 } }));
		}
	}

	private static void ProcessLinkTypeLoseAbility(AnnotationInfo ai, MtgCardInstance affectorCard, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.LinkedTo, affectedId);
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				AnnotationDataUtils.ExtractDetails(ai, dictionary);
				LinkInfoData item = new LinkInfoData(ai.Id, LinkType.LoseAbility, ai.AffectorId, affectedId, 0u, dictionary);
				affectorCard.AffectorOfLinkInfos.Add(item);
				cardById.AffectedByLinkInfos.Add(item);
			}
		}
	}

	private static void ProcessLinkTypeGamePlayer(AnnotationInfo ai, MtgCardInstance affector, MtgGameState newState)
	{
	}

	private static void ProcessLinkTypeAnchorWord(AnnotationInfo ai, MtgEntity affector, MtgGameState newState)
	{
	}
}
