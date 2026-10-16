using AssetLookupTree;
using AssetLookupTree.Payloads.Card;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

internal class RegeneratedCardUXEvent : UXEvent
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly uint _instanceId;

	public RegeneratedCardUXEvent(ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookup, uint cardInstanceId)
	{
		_cardViewProvider = cardViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookup;
		_instanceId = cardInstanceId;
	}

	public override void Execute()
	{
		if (!_cardViewProvider.TryGetCardView(_instanceId, out var cardView))
		{
			Complete();
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
		_assetLookupSystem.Blackboard.CardHolder = cardView.CurrentCardHolder;
		RegenerateVFX regenerateVFX = null;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<RegenerateVFX> loadedTree))
		{
			regenerateVFX = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (regenerateVFX != null)
		{
			_vfxProvider.PlayVFX(regenerateVFX.VfxData, cardView.Model);
		}
		Complete();
	}
}
