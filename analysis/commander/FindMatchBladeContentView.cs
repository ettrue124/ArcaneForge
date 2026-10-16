using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Prefab;
using Core.Code.Decks;
using UnityEngine;
using UnityEngine.UI;
using Wizards.MDN;
using Wizards.Mtga.Decks;
using Wizards.Unification.Models.PlayBlade;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;

namespace Wizards.Mtga.PlayBlade;

public class FindMatchBladeContentView : BladeContentView
{
	[SerializeField]
	private Transform _deckViewSelectorParent;

	[SerializeField]
	private Image _backgroundImage;

	[SerializeField]
	private Image _RankImage;

	[SerializeField]
	private Localize _titleText;

	[SerializeField]
	private Localize _descriptionText;

	[SerializeField]
	private Localize _bestOfText;

	[SerializeField]
	private Localize _sideBoardSizeText;

	[SerializeField]
	private Localize _deckSizeText;

	[SerializeField]
	private CustomButton _editDeckButton;

	private AssetLoader.AssetTracker<Sprite> _backgroundImageSpriteTracker = new AssetLoader.AssetTracker<Sprite>("FindMatchBackgroundSprite");

	private AssetLoader.AssetTracker<Sprite> _rankImageSpriteTracker = new AssetLoader.AssetTracker<Sprite>("FindMatchRankSprite");

	private DeckViewSelector _deckViewSelector;

	private FindMatchBladeData _data;

	private BladeSelectionInfo _selectionInfo;

	private IReadOnlyList<DeckViewInfo> _decks = new List<DeckViewInfo>();

	private const string BO1_LOC_KEY = "PlayBlade/FindMatch/PlayBlade_FindMatch_BestOf_One";

	private const string BO3_LOC_KEY = "PlayBlade/FindMatch/PlayBlade_FindMatch_BestOf_Three";

	public override BladeType Type => BladeType.FindMatch;

	private FindMatchSelectionInfo CurrentSelection => _selectionInfo?.FindMatchInfo;

	protected override void OnAwakeCalled()
	{
		_editDeckButton.OnClick.AddListener(OnEditClicked);
		AssetLookupSystem.Blackboard.Clear();
		DeckViewSelectorPrefab payload = AssetLookupSystem.TreeLoader.LoadTree<DeckViewSelectorPrefab>().GetPayload(AssetLookupSystem.Blackboard);
		_deckViewSelector = AssetLoader.Instantiate(payload.Prefab, _deckViewSelectorParent);
		bool? flag = ((!MDNPlayerPrefs.HasSeenPlayBlade) ? new bool?(true) : ((bool?)null));
		if (flag ?? false)
		{
			MDNPlayerPrefs.HasSeenPlayBlade = true;
		}
		_deckViewSelector.Initialize(OnDeckSelected, OnDeckDoubleClicked, null, simpleSelect: false, flag);
		_deckViewSelector.SetSort(DeckViewSortType.PlayBlade);
		ScrollRect componentInChildren = _deckViewSelector.GetComponentInChildren<ScrollRect>();
		ScrollFade[] componentsInChildren = transform.GetComponentsInChildren<ScrollFade>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].ScrollView = componentInChildren;
		}
	}

	protected override void OnDestroyCalled()
	{
		_editDeckButton.OnClick.RemoveListener(OnEditClicked);
		AssetLoaderUtils.CleanupImage(_backgroundImage, _backgroundImageSpriteTracker);
		AssetLoaderUtils.CleanupImage(_RankImage, _rankImageSpriteTracker);
		_decks = null;
		if (_deckViewSelector != null)
		{
			_deckViewSelector.ClearDecks();
			_deckViewSelector = null;
		}
		_data = null;
		_selectionInfo = null;
	}

	private void OnDeckSelected(DeckViewInfo selectedDeck)
	{
		if (selectedDeck == null)
		{
			CurrentSelection.SelectedDeckId = Guid.Empty;
			CurrentSelection.SelectedDeckIsInvalidForFormat = false;
		}
		else
		{
			CurrentSelection.SelectedDeckId = selectedDeck.deckId;
			DeckDisplayInfo validationForFormat = selectedDeck.GetValidationForFormat(CurrentSelection.SelectedEvent.DeckFormat);
			CurrentSelection.SelectedDeckIsInvalidForFormat = !validationForFormat.IsValid && selectedDeck.DeckFormat != CurrentSelection.SelectedEvent.DeckFormat;
		}
		SendUpdatedSelections();
	}

	private void OnDeckDoubleClicked(DeckViewInfo selectedDeck)
	{
		if (!(this == null) && CurrentSelection != null)
		{
			EditDeck(CurrentSelection.SelectedDeckId);
		}
	}

	private void OnEditClicked()
	{
		if (CurrentSelection.SelectedDeckId != Guid.Empty)
		{
			EditDeck(CurrentSelection.SelectedDeckId);
		}
	}

	private void EditDeck(Guid deckId)
	{
		if (IsDeckEditable(deckId, _decks))
		{
			Signals.EditDeckSignal.Dispatch(new EditDeckSignalArgs(this, deckId, null, CurrentSelection.SelectedEvent.DeckFormat, CurrentSelection.SelectedDeckIsInvalidForFormat));
		}
	}

	private void SendUpdatedSelections()
	{
		UpdateSelection(_selectionInfo);
	}

	public override void OnSelectionInfoUpdated(BladeSelectionInfo selectionInfo)
	{
		if (!(CurrentSelection.SelectedDeckId != selectionInfo.FindMatchInfo.SelectedDeckId))
		{
			_selectionInfo = selectionInfo;
			UpdateDeckSelector(CurrentSelection);
			UpdateUx(CurrentSelection);
		}
	}

	public override void SetModel(IBladeModel model, BladeSelectionInfo selectionInfo, AssetLookupSystem assetLookupSystem)
	{
		_data = new FindMatchBladeData(model);
		_selectionInfo = selectionInfo;
		_decks = _data.Decks;
		if (CurrentSelection.SelectedBladeQueueInfo != null)
		{
			UpdateUx(CurrentSelection);
			HydrateDeckSelector(_decks, CurrentSelection);
		}
	}

	private static bool IsDeckEditable(Guid deckId, IReadOnlyList<DeckViewInfo> deckViewInfos)
	{
		if (deckId == Guid.Empty)
		{
			return false;
		}
		DeckViewInfo deckViewInfo = deckViewInfos.FirstOrDefault((DeckViewInfo x) => x.deckId == deckId);
		if (deckViewInfo == null)
		{
			return false;
		}
		return !deckViewInfo.IsNetDeck;
	}

	private void UpdateUx(FindMatchSelectionInfo currentSelection)
	{
		BladeQueueInfo selectedBladeQueueInfo = currentSelection.SelectedBladeQueueInfo;
		bool useBO = currentSelection.UseBO3;
		PlayBladeQueueType selectedQueueType = currentSelection.SelectedQueueType;
		BladeEventInfo selectedEvent = currentSelection.SelectedEvent;
		_titleText.SetText(selectedEvent?.LocTitle);
		_descriptionText.SetText(selectedEvent?.LocDescription);
		_bestOfText.SetText(useBO ? "PlayBlade/FindMatch/PlayBlade_FindMatch_BestOf_Three" : "PlayBlade/FindMatch/PlayBlade_FindMatch_BestOf_One");
		DeckConstraintInfo deckConstraintInfo = (useBO ? selectedBladeQueueInfo.DeckConstraintInfo_B03 : selectedBladeQueueInfo.DeckConstraintInfo_B01);
		if (deckConstraintInfo != null)
		{
			_deckSizeText.SetText((MTGALocalizedString)deckConstraintInfo.MainDeckKey);
			_sideBoardSizeText.SetText((MTGALocalizedString)deckConstraintInfo.SideBoardKey);
		}
		string text = ((selectedQueueType != PlayBladeQueueType.Ranked) ? string.Empty : selectedEvent?.RankImagePath);
		_RankImage.gameObject.UpdateActive(!string.IsNullOrEmpty(text));
		AssetLoaderUtils.TrySetSprite(_RankImage, _rankImageSpriteTracker, text);
		string text2 = selectedEvent?.BackgroundImagePath ?? string.Empty;
		_backgroundImage.gameObject.UpdateActive(!string.IsNullOrEmpty(text2));
		AssetLoaderUtils.TrySetSprite(_backgroundImage, _backgroundImageSpriteTracker, text2);
		_editDeckButton.gameObject.UpdateActive(IsDeckEditable(CurrentSelection.SelectedDeckId, _decks));
	}

	private void HydrateDeckSelector(IReadOnlyList<DeckViewInfo> decks, FindMatchSelectionInfo currentSelection)
	{
		string format = (currentSelection?.SelectedEvent)?.DeckFormat ?? string.Empty;
		bool valueOrDefault = WrapperController.Instance.EventManager.EventContexts.FirstOrDefault((EventContext evt) => evt.PlayerEvent.EventInfo.InternalEventName == CurrentSelection.SelectedEventName)?.PlayerEvent?.EventInfo?.AllowUncollectedCards == true;
		_deckViewSelector.SetDecks(decks, valueOrDefault);
		_deckViewSelector.SetFormat(format, valueOrDefault);
		_deckViewSelector.ShowCreateDeckButton(CreateDeckAction);
		if (currentSelection.SelectedDeckId != Guid.Empty)
		{
			_deckViewSelector.SelectDeck(decks.FirstOrDefault((DeckViewInfo d) => d.deckId == currentSelection.SelectedDeckId));
		}
	}

	private void CreateDeckAction()
	{
		if (!WrapperController.Instance.DecksManager.ShowDeckLimitError())
		{
			EventContext evt = WrapperController.Instance.EventManager.EventContexts.FirstOrDefault((EventContext eventContext) => eventContext.PlayerEvent.EventInfo.InternalEventName == CurrentSelection.SelectedEventName);
			DeckBuilderContext context = new DeckBuilderContext(DeckServiceWrapperHelpers.ToAzureModel(WrapperController.Instance.FormatManager.GetSafeFormat(CurrentSelection.SelectedEvent.Format.FormatName).NewDeck(WrapperController.Instance.DecksManager)), evt, sideboarding: false, firstEdit: true, DeckBuilderMode.DeckBuilding, ambiguousFormat: false, default, null, null, null, cachingEnabled: false, isPlayblade: true);
			SceneLoader.GetSceneLoader().GoToDeckBuilder(context);
		}
	}

	private void UpdateDeckSelector(FindMatchSelectionInfo currentSelection)
	{
		string format = (currentSelection?.SelectedEvent)?.DeckFormat ?? string.Empty;
		bool valueOrDefault = WrapperController.Instance.EventManager.EventContexts.FirstOrDefault((EventContext evt) => evt.PlayerEvent.EventInfo.InternalEventName == CurrentSelection.SelectedEventName)?.PlayerEvent?.EventInfo?.AllowUncollectedCards == true;
		_deckViewSelector.SetFormat(format, valueOrDefault);
	}
}
