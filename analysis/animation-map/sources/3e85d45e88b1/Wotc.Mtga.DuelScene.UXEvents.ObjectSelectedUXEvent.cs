using AssetLookupTree;
using AssetLookupTree.Payloads.Card;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ObjectSelectedUXEvent : UXEvent
{
	private readonly ObjectSelectedEvent _objectSelectedEvent;

	private readonly string _loopingEffectKey;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ObjectSelectedUXEvent(ObjectSelectedEvent ose, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_objectSelectedEvent = ose;
		_loopingEffectKey = $"ObjectSelection_{ose.AffectorId}_{ose.AffectedId}";
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (_cardViewProvider.TryGetCardView(_objectSelectedEvent.AffectedId, out var cardView))
		{
			_cardViewProvider.TryGetCardView(_objectSelectedEvent.AffectorId, out var cardView2);
			_assetLookupSystem.Blackboard.Clear();
			if ((bool)cardView2)
			{
				_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
				_assetLookupSystem.Blackboard.CardHolderType = cardView2.HolderType;
			}
			ChoiceVFX payload = _assetLookupSystem.TreeLoader.LoadTree<ChoiceVFX>().GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				ICardDataAdapter cardDataAdapter = (cardView ? cardView.Model : null);
				MtgEntity spaceContext = cardDataAdapter?.Instance;
				payload.VfxData.LoopingKey = _loopingEffectKey;
				_vfxProvider.PlayVFX(payload.VfxData, cardDataAdapter, spaceContext, cardView.EffectsRoot);
			}
		}
		Complete();
	}
}
