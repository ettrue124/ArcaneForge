using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Card;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ReplacementEffectAppliedUXEvent : UXEvent
{
	private readonly uint _affectorId;

	private readonly uint _abilityGrpId;

	private readonly IAbilityDataProvider _abilityProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ReplacementEffectAppliedUXEvent(uint affectorId, uint abilityGrpId, IAbilityDataProvider abilityProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_affectorId = affectorId;
		_abilityGrpId = abilityGrpId;
		_abilityProvider = abilityProvider ?? NullAbilityDataProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (_cardViewProvider.TryGetCardView(_affectorId, out var cardView))
		{
			IBlackboard blackboard = _assetLookupSystem.Blackboard;
			blackboard.Clear();
			blackboard.SetCardDataExtensive(cardView.Model);
			blackboard.CardHolderType = cardView.CurrentCardHolder.CardHolderType;
			AbilityPrintingData abilityPrintingById = _abilityProvider.GetAbilityPrintingById(_abilityGrpId);
			if (abilityPrintingById != null)
			{
				blackboard.Ability = abilityPrintingById;
			}
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ProtectionApplied_VFX> loadedTree))
			{
				ProtectionApplied_VFX payload = loadedTree.GetPayload(blackboard);
				if (payload != null)
				{
					foreach (VfxData vfxData in payload.VfxDatas)
					{
						_vfxProvider.PlayVFX(vfxData, cardView.Model);
					}
				}
			}
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ProtectionApplied_SFX> loadedTree2))
			{
				ProtectionApplied_SFX payload2 = loadedTree2.GetPayload(blackboard);
				if (payload2 != null)
				{
					AudioManager.PlayAudio(payload2.SfxData.AudioEvents, cardView.gameObject);
				}
			}
		}
		Complete();
	}
}
