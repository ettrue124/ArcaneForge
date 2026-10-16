using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ReplacementEffectStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_SPAWNER_TYPE = "replacementEffectSpawnerType";

	public const string DETAIL_GRPID = "grpid";

	public const string DETAIL_SOURCE = "replacedEffectSource";

	public const string DETAIL_RECIPIENT = "replacedEffectRecipient";

	public const string DETAIL_DURABILITY = "durability";

	public const string DETAIL_SOURCE_ZCID = "ReplacementSourceZcid";

	public const string DETAIL_DESTINATION = "damageRedirectionDestination";

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
		if (!ai.Type.Contains(AnnotationType.ReplacementEffect))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint affectorId = ai.AffectorId;
		uint num = ai.AffectedIds[0];
		uint abilityId = GetAbilityId(ai);
		IReadOnlyList<uint> sourceIds = GetSourceIds(ai);
		IReadOnlyList<uint> recipientIds = GetRecipientIds(ai);
		uint? durability = GetDurability(ai);
		uint? replacementSourceZcid = GetReplacementSourceZcid(ai);
		uint? redirectionDestination = GetRedirectionDestination(ai);
		MtgCardInstance affectorCard = newState.GetCardById(affectorId) ?? oldState.GetCardById(affectorId);
		ReplacementEffectData replacementEffectData = new ReplacementEffectData
		{
			Id = ai.Id,
			SpawnerType = new List<ReplacementEffectSpawnerType>(ai.GetEnumList<ReplacementEffectSpawnerType>("replacementEffectSpawnerType")),
			AffectorId = affectorId,
			AffectedId = num,
			SourceIds = sourceIds,
			SourceZcId = replacementSourceZcid,
			RecipientIds = recipientIds,
			AbilityId = abilityId,
			Durability = durability,
			DamageRedirectionId = redirectionDestination,
			Key = $"Id:{ai.Id}_Tgt:{num}_Src:{affectorId}_Abl:{abilityId}_Durability:{durability}_DmgId:{redirectionDestination}"
		};
		newState.TrackHistoricEntity(affectorId, oldState);
		newState.TrackHistoricEntity(num, oldState);
		AddReplacementEffectToEntity(newState, ref replacementEffectData, replacementSourceZcid, affectorCard);
		AddReplacementEffectToState(replacementEffectData, newState);
		AddDamageRedirectionLink(ai, affectorId, num, redirectionDestination, newState, affectorCard);
	}

	private static void AddReplacementEffectToEntity(MtgGameState newState, ref ReplacementEffectData replacementEffectData, uint? replacementSourceZcid, MtgCardInstance affectorCard)
	{
		bool flag = false;
		MtgPlayer playerById = newState.GetPlayerById(replacementEffectData.AffectedId);
		if (playerById != null)
		{
			playerById.ReplacementEffects.Add(replacementEffectData);
			flag = true;
		}
		foreach (uint sourceId in replacementEffectData.SourceIds)
		{
			MtgEntity entityById = newState.GetEntityById(sourceId);
			if (entityById != null)
			{
				entityById.ReplacementEffects.Add(replacementEffectData);
				flag = true;
			}
		}
		foreach (uint recipientId in replacementEffectData.RecipientIds)
		{
			if (!replacementEffectData.SourceIds.Contains(recipientId))
			{
				MtgEntity entityById2 = newState.GetEntityById(recipientId);
				if (entityById2 != null)
				{
					entityById2.ReplacementEffects.Add(replacementEffectData);
					flag = true;
				}
			}
		}
		if (!flag && replacementSourceZcid.HasValue)
		{
			uint value = replacementSourceZcid.Value;
			if (newState.TryGetEntity(value, out var mtgEntity) || (newState.VisibleCards.TryGetValue(value, out var value2) && (mtgEntity = value2) != null))
			{
				replacementEffectData.SourceIds = new List<uint> { value };
				replacementEffectData.RecipientIds = null;
				mtgEntity.ReplacementEffects.Add(replacementEffectData);
				flag = true;
			}
		}
		if (!flag && affectorCard != null)
		{
			MtgPlayer playerById2 = newState.GetPlayerById(affectorCard.Controller.InstanceId);
			if (playerById2 != null)
			{
				playerById2.ReplacementEffects.Add(replacementEffectData);
				flag = true;
			}
		}
	}

	private static void AddReplacementEffectToState(ReplacementEffectData replacementEffectData, MtgGameState newState)
	{
		if (!newState.ReplacementEffects.TryGetValue(replacementEffectData.AffectorId, out var value))
		{
			value = (newState.ReplacementEffects[replacementEffectData.AffectorId] = new List<ReplacementEffectData>(1));
		}
		value.Add(replacementEffectData);
	}

	private static void AddDamageRedirectionLink(AnnotationInfo ai, uint affectorId, uint affectedId, uint? damageRedirectionId, MtgGameState newState, MtgCardInstance affectorCard)
	{
		if (damageRedirectionId.HasValue && affectorCard != null && newState.TryGetCard(damageRedirectionId.Value, out var card))
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			AnnotationDataUtils.ExtractDetails(ai, dictionary);
			LinkInfoData item = new LinkInfoData(ai.Id, LinkType.Choose, affectorId, affectedId, 0u, dictionary);
			affectorCard.AffectorOfLinkInfos.Add(item);
			card.AffectedByLinkInfos.Add(item);
		}
	}

	public static ReplacementEffectSpawnerType GetSpawnerType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("replacementEffectSpawnerType") && detail.ValueInt32.Count > 0)
			{
				return (ReplacementEffectSpawnerType)detail.ValueInt32[0];
			}
		}
		return ReplacementEffectSpawnerType.None;
	}

	public static uint GetAbilityId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	public static IReadOnlyList<uint> GetSourceIds(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("replacedEffectSource") && detail.ValueInt32.Count > 0)
			{
				return (from x in detail.ValueInt32
					orderby x
					select (uint)x).ToArray();
			}
		}
		return Array.Empty<uint>();
	}

	public static IReadOnlyList<uint> GetRecipientIds(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("replacedEffectRecipient") && detail.ValueInt32.Count > 0)
			{
				return (from x in detail.ValueInt32
					orderby x
					select (uint)x).ToArray();
			}
		}
		return Array.Empty<uint>();
	}

	public static uint? GetDurability(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("durability") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return null;
	}

	public static uint? GetReplacementSourceZcid(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ReplacementSourceZcid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return null;
	}

	public static uint? GetRedirectionDestination(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("damageRedirectionDestination") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return null;
	}
}
