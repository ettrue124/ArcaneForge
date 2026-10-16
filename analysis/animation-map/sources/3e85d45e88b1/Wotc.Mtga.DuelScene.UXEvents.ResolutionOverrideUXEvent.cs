using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Resolution;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ResolutionOverrideUXEvent : UXEvent
{
	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IVfxProvider _vfxProvider;

	private readonly ICardDataAdapter _cardModel;

	private readonly AbilityPrintingData _abilityPrinting;

	private readonly UXEvent _overriddenEvent;

	public ResolutionOverrideUXEvent(ICardDataAdapter cardModel, AbilityPrintingData ability, UXEvent overridenEvent, AssetLookupSystem assetLookupSystem, IVfxProvider vfxProvider)
	{
		_cardModel = cardModel;
		_abilityPrinting = ability;
		_overriddenEvent = overridenEvent;
		_assetLookupSystem = assetLookupSystem;
		_vfxProvider = vfxProvider;
	}

	public override void Execute()
	{
		PlayOverrideVFX();
		Complete();
	}

	private void PlayOverrideVFX()
	{
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.SetCardDataExtensive(_cardModel);
		blackboard.Ability = _abilityPrinting ?? blackboard.Ability;
		if (_overriddenEvent is UXEventDamageDealt uXEventDamageDealt)
		{
			if (uXEventDamageDealt.Target is MtgPlayer mtgPlayer)
			{
				blackboard.Player = mtgPlayer;
				blackboard.GREPlayerNum = mtgPlayer.ClientPlayerEnum;
			}
			else if (uXEventDamageDealt.Target is MtgCardInstance mtgCardInstance)
			{
				blackboard.Player = mtgCardInstance.Controller;
				blackboard.GREPlayerNum = mtgCardInstance.Controller.ClientPlayerEnum;
			}
		}
		if (!_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<EventOverrideVFX> loadedTree))
		{
			return;
		}
		EventOverrideVFX payload = loadedTree.GetPayload(blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (VfxData vfxData in payload.VfxDatas)
		{
			_vfxProvider.PlayVFX(vfxData, _cardModel);
		}
	}
}
