using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions.ModalBrowser;
using AssetLookupTree.Payloads.General;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.BrowserCardHeaders;

public class ActivatedAbilityHeaders : ModalBrowserCardHeaderProvider.ISubProvider
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ActivatedAbilityHeaders(IClientLocProvider clientLocProvider, IGreLocProvider greLocProvider, IGameStateProvider gameStateProvider, AssetLookupSystem assetLookupSystem)
	{
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_greLocProvider = greLocProvider ?? NullGreLocManager.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public bool TryGetHeaderData(ICardDataAdapter cardModel, AbilityPrintingData abilityData, Action action, out ModalBrowserCardHeaderProvider.HeaderData headerData)
	{
		if (abilityData == null || action == null || abilityData.Category != AbilityCategory.Activated || !action.IsActivateAction())
		{
			headerData = ModalBrowserCardHeaderProvider.HeaderData.Null;
			return false;
		}
		string abilityText;
		if (HasParentOnBattlefield(cardModel))
		{
			headerData = ModalBrowserCardHeaderProvider.HeaderData.Null;
		}
		else if (HasUniqueBaseId(abilityData, out abilityText))
		{
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(abilityText);
		}
		else if (HasUniqueAbilityWord(abilityData, out abilityText))
		{
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(abilityText);
		}
		else if (IsActionFromCardOnBattlefield(action, _gameStateProvider.CurrentGameState))
		{
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(string.Empty);
		}
		else
		{
			headerData = new ModalBrowserCardHeaderProvider.HeaderData(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/BrowserCardInfo_ActivateAbility"));
		}
		return true;
	}

	private static bool IsActionFromCardOnBattlefield(Action action, MtgGameState gameState)
	{
		if (gameState != null && gameState.TryGetCard(action.InstanceId, out var card))
		{
			return card.Zone.Type == ZoneType.Battlefield;
		}
		return false;
	}

	private bool HasParentOnBattlefield(ICardDataAdapter cardModel)
	{
		MtgCardInstance parent = cardModel.Parent;
		if (parent != null)
		{
			MtgZone zone = parent.Zone;
			if (zone == null)
			{
				return false;
			}
			return zone.Type == ZoneType.Battlefield;
		}
		return false;
	}

	private bool HasUniqueBaseId(AbilityPrintingData ability, out string abilityText)
	{
		return GetUniqueAbilityText<ActivatedAbilityBaseIdHeader>(ability, out abilityText);
	}

	private bool HasUniqueAbilityWord(AbilityPrintingData ability, out string abilityText)
	{
		return GetUniqueAbilityText<ActivatedAbilityWordHeader>(ability, out abilityText);
	}

	private bool GetUniqueAbilityText<T>(AbilityPrintingData ability, out string abilityText) where T : ClientOrGreLocKeyPayload
	{
		abilityText = null;
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.Ability = ability;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<T> loadedTree))
		{
			T payload = loadedTree.GetPayload(blackboard);
			if (payload != null)
			{
				abilityText = payload.LocKey.GetText(_clientLocProvider, _greLocProvider);
			}
		}
		return !string.IsNullOrEmpty(abilityText);
	}
}
