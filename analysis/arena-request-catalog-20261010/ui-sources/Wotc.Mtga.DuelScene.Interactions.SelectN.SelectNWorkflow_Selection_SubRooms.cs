using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_SubRooms : BrowserWorkflowBase<SelectNRequest>, ISelectCardsBrowserProvider, IBasicBrowserProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IAutoRespondWorkflow, IClickableWorkflow
{
	private class SelectNSelectionSubRoomsHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _instanceIds;

		public SelectNSelectionSubRoomsHighlightsGenerator(IReadOnlyCollection<uint> instanceIds)
		{
			_instanceIds = instanceIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint instanceId in _instanceIds)
			{
				highlights.IdToHighlightType_Workflow[instanceId] = HighlightType.Hot;
			}
			return highlights;
		}
	}

	private const string FAKE_CARD_ID_FORMAT = "Selection_SubRooms_{0}";

	private readonly Dictionary<DuelScene_CDC, uint> _fakeCardToIdMap = new Dictionary<DuelScene_CDC, uint>();

	private readonly Dictionary<DuelScene_CDC, string> _fakeCardToSubHeaderMap = new Dictionary<DuelScene_CDC, string>();

	private readonly Dictionary<uint, List<uint>> _roomToDoorIdMap = new Dictionary<uint, List<uint>>();

	private readonly HashSet<uint> _selections = new HashSet<uint>();

	private readonly HashSet<uint> _selectables = new HashSet<uint>();

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _locProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private static readonly Dictionary<DuelScene_CDC, HighlightType> _emptyHighlights = new Dictionary<DuelScene_CDC, HighlightType>();

	public bool AllowKeyboardSelection => false;

	private bool CanSubmit
	{
		get
		{
			if (_request.MinSel <= _selections.Count)
			{
				return _selections.Count <= _request.MaxSel;
			}
			return false;
		}
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public SelectNWorkflow_Selection_SubRooms(SelectNRequest request, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabase, IFakeCardViewController fakeCardViewController, ICardHolderProvider cardHolderProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, IClientLocProvider locProvider)
		: base(request)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_fakeCardController = fakeCardViewController ?? NullFakeCardViewController.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_locProvider = locProvider ?? NullLocProvider.Default;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_highlightsGenerator = new SelectNSelectionSubRoomsHighlightsGenerator(_selectables);
	}

	public bool TryAutoRespond()
	{
		int count = _request.Ids.Count;
		if (_request.MinSel == count && _request.MaxSel == count)
		{
			_request.SubmitSelection(_request.Ids);
			return true;
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		foreach (uint id in _request.Ids)
		{
			if (!mtgGameState.TryGetCardInChildren(id, mtgGameState, out var result))
			{
				continue;
			}
			DuelScene_CDC duelScene_CDC = CreateFakeCard($"Selection_SubRooms_{id}", result);
			_fakeCardToIdMap[duelScene_CDC] = id;
			_cardsToDisplay.Add(duelScene_CDC);
			MtgCardInstance parent = result.Parent;
			if (parent != null)
			{
				bool flag = (result.ObjectType == GameObjectType.RoomLeft && parent.Designations.Exists((DesignationData x) => x.Type == Designation.LeftUnlocked)) || (result.ObjectType == GameObjectType.RoomRight && parent.Designations.Exists((DesignationData x) => x.Type == Designation.RightUnlocked));
				_fakeCardToSubHeaderMap[duelScene_CDC] = (flag ? "DuelScene/ClientPrompt/LockRoom" : "DuelScene/ClientPrompt/UnlockRoom");
				if (_roomToDoorIdMap.ContainsKey(parent.InstanceId))
				{
					_roomToDoorIdMap[parent.InstanceId].Add(id);
					continue;
				}
				_roomToDoorIdMap.Add(parent.InstanceId, new List<uint> { id });
			}
		}
		SetHeaderAndSubheader();
		if (_roomToDoorIdMap.Keys.Count == 1)
		{
			SetOpenedBrowser(_browserController.OpenBrowser(this));
			return;
		}
		_selectables.UnionWith(_roomToDoorIdMap.Keys);
		_stack.Get().TryAutoDock(new List<uint>(_roomToDoorIdMap.Keys));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.MinSel, _request.MaxSel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText();
		_headerTextProvider.ClearParams();
	}

	protected override void SetPrompt()
	{
		_workflowPrompt.Reset();
		if (_roomToDoorIdMap.Keys.Count > 1)
		{
			_workflowPrompt.GrePrompt = _request.Prompt;
		}
		OnUpdatePrompt(_workflowPrompt);
	}

	private void OpenSubRoomsBrowser(List<uint> ids)
	{
		_cardsToDisplay.Clear();
		foreach (DuelScene_CDC key in _fakeCardToIdMap.Keys)
		{
			if (ids.Contains(_fakeCardToIdMap[key]))
			{
				_cardsToDisplay.Add(key);
			}
		}
		_selectables.Clear();
		UpdateHighlightsAndDimming();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private DuelScene_CDC CreateFakeCard(string fakeCardKey, MtgCardInstance card)
	{
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(card.GrpId, card.SkinCode);
		MtgCardInstance copy = card.GetCopy();
		CardPrintingRecord record = cardPrintingById.Record;
		IReadOnlyList<uint> linkedFaceGrpIds = Array.Empty<uint>();
		ICardDataAdapter cardData = new CardData(copy, new CardPrintingData(cardPrintingById, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, linkedFaceGrpIds)));
		return _fakeCardController.CreateFakeCard(fakeCardKey, cardData, isVisible: true);
	}

	public override BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		string value;
		return new BrowserCardHeader.BrowserCardHeaderData(string.Empty, _fakeCardToSubHeaderMap.TryGetValue(cardView, out value) ? _locProvider.GetLocalizedText(value) : string.Empty);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_fakeCardToIdMap.TryGetValue(cardView, out var value))
		{
			if (!_selections.Add(value))
			{
				_selections.Remove(value);
			}
			else if (CanSubmit)
			{
				_request.SubmitSelection(_selections);
			}
		}
	}

	public override void CleanUp()
	{
		foreach (KeyValuePair<DuelScene_CDC, uint> item in _fakeCardToIdMap)
		{
			_fakeCardController.DeleteFakeCard($"Selection_SubRooms_{item.Value}");
		}
		_fakeCardToIdMap.Clear();
		_fakeCardToSubHeaderMap.Clear();
		_selections.Clear();
		_cardsToDisplay.Clear();
		_stack.ClearCache();
		_battlefield.ClearCache();
		base.CleanUp();
	}

	public IEnumerable<DuelScene_CDC> GetSelectableCdcs()
	{
		foreach (KeyValuePair<DuelScene_CDC, uint> item in _fakeCardToIdMap)
		{
			yield return item.Key;
		}
	}

	public IEnumerable<DuelScene_CDC> GetNonSelectableCdcs()
	{
		return Array.Empty<DuelScene_CDC>();
	}

	public Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		return _emptyHighlights;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary || _openedBrowser != null)
		{
			return false;
		}
		return _roomToDoorIdMap.ContainsKey(entity.InstanceId);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (WorkflowBase.TryRerouteClick(entity.InstanceId, _battlefield.Get(), isSelecting: true, out var reroutedEntityView))
		{
			entity = reroutedEntityView;
		}
		if (_roomToDoorIdMap.ContainsKey(entity.InstanceId))
		{
			if (_roomToDoorIdMap[entity.InstanceId].Count == 1)
			{
				_selections.Add(_roomToDoorIdMap[entity.InstanceId][0]);
				_request.SubmitSelection(_selections);
			}
			else
			{
				OpenSubRoomsBrowser(_roomToDoorIdMap[entity.InstanceId]);
			}
		}
	}

	public bool CanClickStack(CdcStackCounterView entity, SimpleInteractionType clickType)
	{
		return false;
	}

	public void OnClickStack(CdcStackCounterView entity)
	{
	}

	public void OnBattlefieldClick()
	{
	}
}
