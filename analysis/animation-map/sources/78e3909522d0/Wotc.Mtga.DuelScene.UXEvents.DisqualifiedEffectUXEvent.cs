using AssetLookupTree;
using AssetLookupTree.Payloads.Card;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DisqualifiedEffectUXEvent : UXEvent
{
	private readonly MtgCardInstance _cardInstance;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameEffectController _gameEffectController;

	private readonly IVfxProvider _vfxProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly AssetLookupSystem _assetLookupSystem;

	public DisqualifiedEffectUXEvent(MtgCardInstance disqualifierCard, ICardViewProvider cardViewProvider, IGameEffectController gameEffectController, IVfxProvider vfxProvider, ICardHolderProvider cardHolderProvider, AssetLookupSystem assetLookupSystem)
	{
		_cardInstance = disqualifierCard;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_gameEffectController = gameEffectController ?? NullGameEffectController.Default;
		_vfxProvider = vfxProvider;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (TryGetCardViewForVFX(_cardInstance, out var cardView, out var transformOverride))
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
			_assetLookupSystem.Blackboard.CardHolderType = cardView.CurrentCardHolder.CardHolderType;
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<EffectDisqualified_VFX> loadedTree))
			{
				EffectDisqualified_VFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					foreach (VfxData vfxData in payload.VfxDatas)
					{
						_vfxProvider.PlayVFX(vfxData, cardView.Model, null, transformOverride);
					}
				}
			}
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<EffectDisqualified_SFX> loadedTree2))
			{
				EffectDisqualified_SFX payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
				if (payload2 != null)
				{
					AudioManager.PlayAudio(payload2.SfxData.AudioEvents, _stack.Get().gameObject);
				}
			}
		}
		Complete();
	}

	private bool TryGetCardViewForVFX(MtgCardInstance sourceInstance, out DuelScene_CDC cardView, out Transform transformOverride)
	{
		if (_cardViewProvider.TryGetCardView(sourceInstance.InstanceId, out var cardView2))
		{
			cardView = cardView2;
			transformOverride = null;
			return true;
		}
		foreach (DuelScene_CDC allGameEffect in _gameEffectController.GetAllGameEffects())
		{
			if (allGameEffect.Model.ObjectSourceGrpId == _cardInstance.GrpId)
			{
				cardView = allGameEffect;
				transformOverride = cardView.EffectsRoot;
				return true;
			}
		}
		cardView = null;
		transformOverride = null;
		return false;
	}

	protected override void Cleanup()
	{
		_stack.ClearCache();
		base.Cleanup();
	}
}
