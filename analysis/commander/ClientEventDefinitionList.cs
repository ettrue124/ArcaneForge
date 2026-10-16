using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Avatar;
using AssetLookupTree.Payloads.Booster;
using AssetLookupTree.Payloads.Event;
using EventPage.Components.NetworkModels;
using UnityEngine;
using Wizards.MDN;
using Wotc.Mtga.Wrapper;

public static class ClientEventDefinitionList
{
	public static string GetBillboardImagePath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		BillboardPayload payload = assetLookupSystem.TreeLoader.LoadTree<BillboardPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}

	public static string GetBladeImagePath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		BladePayload payload = assetLookupSystem.TreeLoader.LoadTree<BladePayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		if (payload == null)
		{
			return GetBillboardImagePath(assetLookupSystem, eventContext);
		}
		return payload.Reference.RelativePath;
	}

	public static string GetBladeLogoPath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		BoosterPacksDisplayData boosterPacksDisplayData = eventContext.PlayerEvent.EventUXInfo.EventComponentData?.BoosterPacksDisplay;
		string result = string.Empty;
		if (boosterPacksDisplayData != null && boosterPacksDisplayData.CollationIds.Count > 0)
		{
			uint boosterCollationMapping = boosterPacksDisplayData.CollationIds.Max();
			assetLookupSystem.Blackboard.Clear();
			assetLookupSystem.Blackboard.BoosterCollationMapping = (CollationMapping)boosterCollationMapping;
			Logo logo = null;
			if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Logo> loadedTree))
			{
				logo = loadedTree.GetPayload(assetLookupSystem.Blackboard);
			}
			result = ((logo != null) ? logo.TextureRef.RelativePath : string.Empty);
		}
		return result;
	}

	public static string GetBladeImagePath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		BladePayload payload = assetLookupSystem.TreeLoader.LoadTree<BladePayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}

	public static string GetPoptartBackgroundPath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		PoptartBackgroundPayload payload = assetLookupSystem.TreeLoader.LoadTree<PoptartBackgroundPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}

	public static string GetBackgroundImagePath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		BackgroundPayload payload = assetLookupSystem.TreeLoader.LoadTree<BackgroundPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}

	public static string GetBackgroundImagePath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		BackgroundPayload payload = assetLookupSystem.TreeLoader.LoadTree<BackgroundPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}

	public static string GetDissolveNoiseTexturePath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		DissolveNoiseTexturePayload payload = assetLookupSystem.TreeLoader.LoadTree<DissolveNoiseTexturePayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.TextureRef.RelativePath;
	}

	public static string GetDissolveNoiseTexturePath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		DissolveNoiseTexturePayload payload = assetLookupSystem.TreeLoader.LoadTree<DissolveNoiseTexturePayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.TextureRef.RelativePath;
	}

	public static string GetManaIconPath_Title(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		AssetLookupTree<ManaIconPayload> assetLookupTree = assetLookupSystem.TreeLoader.LoadTree<ManaIconPayload>();
		HashSet<ManaIconPayload> hashSet = new HashSet<ManaIconPayload>();
		assetLookupTree.GetPayloadLayered(assetLookupSystem.Blackboard, hashSet);
		assetLookupSystem.Blackboard.Clear();
		return hashSet.FirstOrDefault((ManaIconPayload p) => p.Layers.Contains("title"))?.Reference.RelativePath ?? null;
	}

	public static string GetManaIconPath_Title(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		AssetLookupTree<ManaIconPayload> assetLookupTree = assetLookupSystem.TreeLoader.LoadTree<ManaIconPayload>();
		HashSet<ManaIconPayload> hashSet = new HashSet<ManaIconPayload>();
		assetLookupTree.GetPayloadLayered(assetLookupSystem.Blackboard, hashSet);
		assetLookupSystem.Blackboard.Clear();
		return hashSet.FirstOrDefault((ManaIconPayload p) => p.Layers.Contains("title"))?.Reference.RelativePath ?? null;
	}

	public static string[] GetManaIconPaths_Banner(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		AssetLookupTree<ManaIconPayload> assetLookupTree = assetLookupSystem.TreeLoader.LoadTree<ManaIconPayload>();
		HashSet<ManaIconPayload> hashSet = new HashSet<ManaIconPayload>();
		assetLookupTree.GetPayloadLayered(assetLookupSystem.Blackboard, hashSet);
		assetLookupSystem.Blackboard.Clear();
		return new string[2]
		{
			hashSet.FirstOrDefault((ManaIconPayload p) => p.Layers.Contains("banner_active"))?.Reference.RelativePath,
			hashSet.FirstOrDefault((ManaIconPayload p) => p.Layers.Contains("banner_inactive"))?.Reference.RelativePath
		};
	}

	public static AudioEvent GetAvatarVO(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		VoiceSFX payload = assetLookupSystem.TreeLoader.LoadTree<VoiceSFX>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		if (payload != null && payload.SfxData != null && payload.SfxData.AudioEvents.Count > 0)
		{
			int count = payload.SfxData.AudioEvents.Count;
			int index = Random.Range(0, count);
			return payload.SfxData.AudioEvents[index];
		}
		return null;
	}

	public static string GetLossHintPrefabPath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		LossHintDeluxeTooltipPayload payload = assetLookupSystem.TreeLoader.LoadTree<LossHintDeluxeTooltipPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.PrefabRef.RelativePath;
	}

	public static string GetLossHintPrefabPath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		LossHintDeluxeTooltipPayload payload = assetLookupSystem.TreeLoader.LoadTree<LossHintDeluxeTooltipPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.PrefabRef.RelativePath;
	}

	public static string GetColorChallengeMatchPath(AssetLookupSystem assetLookupSystem, EventContext eventContext)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Event = eventContext;
		ColorChallengeMatchPayload payload = assetLookupSystem.TreeLoader.LoadTree<ColorChallengeMatchPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.DataRef.RelativePath;
	}

	public static string GetColorChallengeMatchPath(AssetLookupSystem assetLookupSystem, string nodeName)
	{
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.CampaignGraphNodeName = nodeName;
		ColorChallengeMatchPayload payload = assetLookupSystem.TreeLoader.LoadTree<ColorChallengeMatchPayload>().GetPayload(assetLookupSystem.Blackboard);
		assetLookupSystem.Blackboard.Clear();
		return payload?.DataRef.RelativePath;
	}
}
