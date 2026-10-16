using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Card;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ChoiceResultUXEvent_Permanent : UXEvent
{
	private readonly ChoiceResultEvent _choiceResult;

	private readonly IObjectPool _objectPool;

	private readonly ICardDataProvider _cardDataProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private DuelScene_CDC _affectorCdc;

	private readonly List<DuelScene_CDC> _optionCdcs;

	private readonly List<DuelScene_CDC> _valueCdcs;

	public override bool IsBlocking => true;

	public ChoiceResultUXEvent_Permanent(ChoiceResultEvent cre, IObjectPool objectPool, ICardDataProvider cardDataProvider, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_choiceResult = cre;
		_cardDataProvider = cardDataProvider;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
		_objectPool = objectPool;
		_optionCdcs = _objectPool.PopObject<List<DuelScene_CDC>>();
		_valueCdcs = _objectPool.PopObject<List<DuelScene_CDC>>();
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		_cardViewProvider.TryGetCardView(_choiceResult.AffectorId, out _affectorCdc);
		foreach (uint choiceOption in _choiceResult.ChoiceOptions)
		{
			if (_cardViewProvider.TryGetCardView(choiceOption, out var cardView))
			{
				_optionCdcs.Add(cardView);
			}
		}
		foreach (uint choiceValue in _choiceResult.ChoiceValues)
		{
			if (_cardViewProvider.TryGetCardView(choiceValue, out var cardView2))
			{
				_valueCdcs.Add(cardView2);
			}
		}
		if (_valueCdcs.Count > 0)
		{
			_assetLookupSystem.Blackboard.Clear();
			if ((bool)_affectorCdc)
			{
				List<DesignationData> designations = mtgGameState.Designations;
				if (designations != null && designations.Count > 0)
				{
					uint affectedId = mtgGameState.Designations.FirstOrDefault((DesignationData x) => x.Type == Designation.Ringbearer && _choiceResult.ChoiceValues.Contains(x.AffectedId)).AffectedId;
					if (mtgGameState.TryGetCard(affectedId, out var card))
					{
						_assetLookupSystem.Blackboard.LayeredEffects = card.LayeredEffects;
						if (!_cardDataProvider.TryGetCardPrintingById(card.GrpId, out var card2))
						{
							card2 = card.ObjectSourcePrinting;
						}
						_assetLookupSystem.Blackboard.SetCardDataExtensive(new CardData(card, card2));
					}
				}
				if (_assetLookupSystem.Blackboard.CardData == null && _valueCdcs.Count == 1)
				{
					_assetLookupSystem.Blackboard.SetCardDataExtensive(_valueCdcs[0].Model);
				}
				if (_assetLookupSystem.Blackboard.CardData == null)
				{
					_assetLookupSystem.Blackboard.SetCardDataExtensive(_affectorCdc.Model);
				}
				_assetLookupSystem.Blackboard.AffectorCardData = _affectorCdc.Model;
				_assetLookupSystem.Blackboard.CardHolderType = _affectorCdc.HolderType;
			}
			_assetLookupSystem.Blackboard.HighlightType = ConvertGreHighlightToClientHighlight(_choiceResult.ChoiceSentiment);
			ChoiceResult payload = _assetLookupSystem.TreeLoader.LoadTree<ChoiceResult>().GetPayload(_assetLookupSystem.Blackboard);
			_assetLookupSystem.Blackboard.Clear();
			if (payload != null)
			{
				foreach (DuelScene_CDC valueCdc in _valueCdcs)
				{
					_vfxProvider.PlayVFX(payload.VfxData, (_affectorCdc != null) ? _affectorCdc.Model : null, (valueCdc != null) ? valueCdc.Model.Instance : null);
					if ((bool)valueCdc && payload.SfxData.AudioEvents.Count > 0)
					{
						AudioManager.PlayAudio(payload.SfxData.AudioEvents, valueCdc.EffectsRoot.gameObject);
					}
				}
			}
		}
		Complete();
	}

	protected override void Cleanup()
	{
		_optionCdcs.Clear();
		_objectPool.PushObject(_optionCdcs, tryClear: false);
		_valueCdcs.Clear();
		_objectPool.PushObject(_valueCdcs, tryClear: false);
	}

	private static HighlightType ConvertGreHighlightToClientHighlight(Wotc.Mtgo.Gre.External.Messaging.HighlightType greHighlight)
	{
		return greHighlight switch
		{
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.None => HighlightType.None, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold => HighlightType.Cold, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.Tepid => HighlightType.Tepid, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.Hot => HighlightType.Hot, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.Counterspell => HighlightType.AutoPay, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.Random => HighlightType.Selected, 
			Wotc.Mtgo.Gre.External.Messaging.HighlightType.CopySpell => HighlightType.None, 
			_ => HighlightType.None, 
		};
	}
}
