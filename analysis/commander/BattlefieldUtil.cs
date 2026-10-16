using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Nodes;
using AssetLookupTree.Payloads.Battlefield;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wotc.Mtga.Extensions;

public static class BattlefieldUtil
{
	public static string DesktopFallbackBattlefieldPath = "Assets/Core/DuelScene/Battlefield/Fallback/Battlefield_Fallback.unity";

	public static string MobileFallbackBattlefieldPath = "Assets/Core/DuelScene/Battlefield/Mobile/Battlefield_FDN_Mobile.unity";

	public static string BattlefieldId { get; private set; } = "DAR";

	public static string BattlefieldPath { get; private set; } = string.Empty;

	public static string BattlefieldSceneName { get; private set; } = string.Empty;

	public static string BattlefieldAudioBankName { get; private set; } = string.Empty;

	public static bool BattlefieldLoaded => SceneManager.GetSceneByPath(BattlefieldPath).isLoaded;

	public static void SetBattlefieldById(AssetLookupSystem altSystem, string battlefieldId)
	{
		altSystem.Blackboard.Clear();
		altSystem.Blackboard.BattlefieldId = battlefieldId;
		if (altSystem.TreeLoader.TryLoadTree(out AssetLookupTree<BattlefieldScenePayload> loadedTree))
		{
			BattlefieldScenePayload payload = loadedTree.GetPayload(altSystem.Blackboard);
			if (payload != null)
			{
				SetBattlefield(payload);
			}
		}
	}

	public static bool SetBattlefield(BattlefieldScenePayload battlefieldPayload)
	{
		if (battlefieldPayload == null)
		{
			return false;
		}
		if (BattlefieldLoaded)
		{
			Debug.LogWarningFormat("Attempting to change Battlefield to \"{0}\" while a battlefield is already loaded: {1}", battlefieldPayload.BattlefieldID, BattlefieldPath);
			return false;
		}
		BattlefieldId = battlefieldPayload.BattlefieldID;
		BattlefieldPath = battlefieldPayload.ScenePath;
		BattlefieldSceneName = battlefieldPayload.SceneName;
		BattlefieldAudioBankName = battlefieldPayload.AudioBankName;
		return true;
	}

	public static string GetRandomBattlefieldId(AssetLookupSystem assetLookupSystem)
	{
		return (from ValueNode<BattlefieldScenePayload> x in from x in assetLookupSystem.TreeLoader.LoadTree<BattlefieldScenePayload>().EnumerateNodes()
				where x is ValueNode<BattlefieldScenePayload> valueNode && valueNode.Payload.InRandomPool
				select x
			select x.Payload.BattlefieldID).Distinct().SelectRandom();
	}
}
