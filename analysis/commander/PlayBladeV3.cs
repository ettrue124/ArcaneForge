using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetLookupTree;
using Core.Shared.Code;
using Pooling;
using UnityEngine;
using Wizards.Mtga.PlayBlade.Extensions;
using Wizards.Unification.Models.PlayBlade;
using Wotc.Mtga;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;

namespace Wizards.Mtga.PlayBlade;

public class PlayBladeV3 : MonoBehaviour
{
	[SerializeField]
	private Localize _headerText;

	[SerializeField]
	private CustomButton _dismissButton;

	[SerializeField]
	private CustomButton _hideCollumnButton;

	[SerializeField]
	private List<BladeContext> _bladeContexts;

	private BladeContext _selectedContext;

	private IUnityObjectPool _objectPool;

	private ICardBuilder<Meta_CDC> _cardBuilder;

	private IPlayBladeSelectionProvider _selectionProvider;

	private Task<IBladeModel> _dataTask;

	private IBladeModel _data;

	private Dictionary<BladeType, BladeContext> _contextByType = new Dictionary<BladeType, BladeContext>();

	private BladeSelectionInfo _currentSelectionInfo;

	private Action<string, Guid> JoinMatchMaking;

	private Action<Guid, string, string, bool> EditDeck;

	private Action<string> GoToEventPage;

	private Action<BladeEventInfo> SelectQueue;

	private Action<BladeEventFilter> SelectFilter;

	private EnterMatchMakingSignal _enterMatchMakingSignal;

	private EditDeckSignal _editDeckSignal;

	private GoToEventPageSignal _goToEventPageSignal;

	private QueueSelectedSignal _queueSelectedSignal;

	private SelectTabSignal _selectTabSignal;

	private ClosePlayBladeSignal _closePlayBladeSignal;

	private FilterSelectedSignal _filterSelectedSignal;

	private AssetLookupSystem _assetLookupSystem;

	public event Action OnVisibilityChanged;

	private void Awake()
	{
		_dismissButton.OnClick.AddListener(OnDismissClicked);
		_hideCollumnButton.OnClick.AddListener(OnHideColumnClicked);
		foreach (BladeContext bladePair in _bladeContexts)
		{
			bladePair.bladeTabView.SetClicked(() =>
			{
				SelectTab(bladePair.bladeView.Type);
			});
			_contextByType.Add(bladePair.bladeView.Type, bladePair);
		}
	}

	private void OnDestroy()
	{
		_dismissButton.OnClick.RemoveListener(OnDismissClicked);
		_hideCollumnButton.OnClick.RemoveListener(OnHideColumnClicked);
		_contextByType.Clear();
		_contextByType = null;
		_data = null;
		JoinMatchMaking = null;
		EditDeck = null;
		GoToEventPage = null;
		SelectQueue = null;
		_enterMatchMakingSignal.Listeners -= EnterMatchMakingSignalDispatched;
		_enterMatchMakingSignal = null;
		_editDeckSignal.Listeners -= EditDeckSignalDispatched;
		_editDeckSignal = null;
		_goToEventPageSignal.Listeners -= GoToEventPageSignalDispatched;
		_goToEventPageSignal = null;
		_queueSelectedSignal.Listeners -= QueueSelectedSignalDispatched;
		_queueSelectedSignal = null;
		_selectTabSignal.Listeners -= SelectTabSignalDispatched;
		_selectTabSignal = null;
		_closePlayBladeSignal.Listeners -= ClosePlayBladeSignalDispatched;
		_closePlayBladeSignal = null;
	}

	private void OnDisable()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_main_play_shelf_close, AudioManager.Default);
		}
		PlayBladeController.PreviousPlayBladeVisualState = PlayBladeController.PlayBladeVisualStates.Events;
	}

	private void Update()
	{
		if (_selectedContext != null)
		{
			_selectedContext.bladeContentView.TickUpdate();
		}
	}

	public void Initialize(ICardBuilder<Meta_CDC> cardViewBuilder, IPlayBladeSelectionProvider selectionProvider, Action<string, Guid> joinMatch, Action<Guid, string, string, bool> editDeck, Action<string> goToEventPage, Action<BladeEventInfo> selectQueue, Action<BladeEventFilter> selectFilter, IUnityObjectPool objectPool, AssetLookupSystem assetLookupSystem)
	{
		_objectPool = objectPool ?? NullUnityObjectPool.Default;
		_cardBuilder = cardViewBuilder;
		_selectionProvider = selectionProvider;
		_assetLookupSystem = assetLookupSystem;
		JoinMatchMaking = joinMatch;
		EditDeck = editDeck;
		GoToEventPage = goToEventPage;
		SelectQueue = selectQueue;
		SelectFilter = selectFilter;
		_enterMatchMakingSignal = new EnterMatchMakingSignal();
		_enterMatchMakingSignal.Listeners += EnterMatchMakingSignalDispatched;
		_editDeckSignal = new EditDeckSignal();
		_editDeckSignal.Listeners += EditDeckSignalDispatched;
		_goToEventPageSignal = new GoToEventPageSignal();
		_goToEventPageSignal.Listeners += GoToEventPageSignalDispatched;
		_queueSelectedSignal = new QueueSelectedSignal();
		_queueSelectedSignal.Listeners += QueueSelectedSignalDispatched;
		_selectTabSignal = new SelectTabSignal();
		_selectTabSignal.Listeners += SelectTabSignalDispatched;
		_closePlayBladeSignal = new ClosePlayBladeSignal();
		_closePlayBladeSignal.Listeners += ClosePlayBladeSignalDispatched;
		_filterSelectedSignal = new FilterSelectedSignal();
		_filterSelectedSignal.Listeners += FilterSelectedSignalDispatched;
		PlayBladeSignals signals = new PlayBladeSignals(_enterMatchMakingSignal, _editDeckSignal, _goToEventPageSignal, _queueSelectedSignal, _selectTabSignal, _closePlayBladeSignal, _filterSelectedSignal);
		foreach (BladeContext bladeContext in _bladeContexts)
		{
			bladeContext.bladeView.Inject(_objectPool, signals, _cardBuilder, _assetLookupSystem);
			bladeContext.bladeContentView.Inject(_objectPool, signals, _cardBuilder, _assetLookupSystem);
		}
	}

	private void OnSelectionInfoUpdated(BladeSelectionInfo selectionInfo)
	{
		_currentSelectionInfo = selectionInfo;
		_selectedContext.UpdateSelectionInfo(_currentSelectionInfo);
	}

	public void SetData(IBladeModel data)
	{
		_data = data;
	}

	public void SetDataAsync<T>(Task<T> dataTask) where T : IBladeModel
	{
		_dataTask = dataTask.ContinueWith((Task<T> data) => _data = data.Result);
	}

	public void Show()
	{
		Pantry.Get<GlobalCoroutineExecutor>().StartCoroutine(ShowCoroutine());
	}

	public IEnumerator ShowCoroutine()
	{
		yield return new WaitUntil(() => _data != null);
		try
		{
			gameObject.UpdateActive(active: true);
			AudioManager.PlayAudio("Music_SetState_Scenes_Click_Play", AudioManager.Default);
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_main_play_shelf_open, AudioManager.Default);
			BladeSelectionData selection = _selectionProvider.GetSelection();
			_currentSelectionInfo = ToMemoryFormat(selection, _data);
			if (_currentSelectionInfo.FindMatchInfo?.SelectedBladeQueueInfo == null)
			{
				_currentSelectionInfo = ToMemoryFormat(_selectionProvider.GetDefaultSelectionData(), _data);
			}
			if (_data.RecentlyPlayed.Count == 0 && _currentSelectionInfo.BladeType == BladeType.LastPlayed)
			{
				_currentSelectionInfo.BladeType = BladeType.FindMatch;
			}
			_contextByType[BladeType.Event].SetNotificationPip(_data.UnviewedEventsExist());
			SelectTab(_currentSelectionInfo.BladeType);
			OnVisibilityChanged?.Invoke();
		}
		catch (Exception arg)
		{
			SimpleLog.LogError($"PlayBladeV3.ShowCoroutine failed to display the play blade: {arg}");
		}
	}

	public void Hide(bool doMainNavAudio)
	{
		BladeSelectionData selection = ToSerializableFormat(_currentSelectionInfo);
		_selectionProvider.SetSelection(selection);
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_main_play_shelf_close, AudioManager.Default);
		if (doMainNavAudio)
		{
			AudioManager.PlayAudio("Music_SetState_Scenes_Menu_MainNav", AudioManager.Default);
		}
		gameObject.UpdateActive(active: false);
		OnVisibilityChanged?.Invoke();
	}

	private BladeSelectionData ToSerializableFormat(BladeSelectionInfo bladeSelectionInfo)
	{
		FindMatchSelectionInfo findMatchSelectionInfo = bladeSelectionInfo?.FindMatchInfo;
		if (findMatchSelectionInfo?.SelectedBladeQueueInfo != null && findMatchSelectionInfo?.SelectedBladeQueueInfoForQueueType != null)
		{
			Dictionary<PlayBladeQueueType, string> dictionary = new Dictionary<PlayBladeQueueType, string>();
			foreach (KeyValuePair<PlayBladeQueueType, BladeQueueInfo> item in findMatchSelectionInfo.SelectedBladeQueueInfoForQueueType)
			{
				if (item.Value != null)
				{
					dictionary.Add(item.Key, item.Value.QueueId);
				}
			}
			return new BladeSelectionData
			{
				findMatch = new FindMatchData
				{
					QueueType = findMatchSelectionInfo.SelectedQueueType,
					QueueId = findMatchSelectionInfo.SelectedBladeQueueInfo.QueueId,
					QueueIdForQueueType = dictionary,
					UseBO3 = findMatchSelectionInfo.UseBO3,
					DeckId = findMatchSelectionInfo.SelectedDeckId
				},
				bladeType = (bladeSelectionInfo?.BladeType ?? BladeType.LastPlayed)
			};
		}
		return _selectionProvider.GetDefaultSelectionData();
	}

	private static BladeSelectionInfo ToMemoryFormat(BladeSelectionData serializableFormat, IBladeModel data)
	{
		Dictionary<PlayBladeQueueType, BladeQueueInfo> selectedBladeQueueInfoForQueueType = ((serializableFormat.findMatch.QueueIdForQueueType == null) ? new Dictionary<PlayBladeQueueType, BladeQueueInfo> { 
		{
			serializableFormat.findMatch.QueueType,
			GetQueueForId(serializableFormat.findMatch.QueueType, serializableFormat.findMatch.QueueId)
		} } : serializableFormat.findMatch.QueueIdForQueueType.ToDictionary((KeyValuePair<PlayBladeQueueType, string> kvp) => kvp.Key, (KeyValuePair<PlayBladeQueueType, string> kvp) => GetQueueForId(kvp.Key, kvp.Value)));
		return new BladeSelectionInfo
		{
			FindMatchInfo = new FindMatchSelectionInfo
			{
				SelectedQueueType = serializableFormat.findMatch.QueueType,
				SelectedBladeQueueInfoForQueueType = selectedBladeQueueInfoForQueueType,
				UseBO3 = serializableFormat.findMatch.UseBO3,
				SelectedDeckId = serializableFormat.findMatch.DeckId
			},
			BladeType = serializableFormat.bladeType,
			EventInfo = new EventSelectionInfo
			{
				SelectedFilter = null
			}
		};
		BladeQueueInfo GetQueueForId(PlayBladeQueueType queueType, string id)
		{
			if (data?.Queues == null || !data.Queues.TryGetValue(queueType, out var value) || value == null)
			{
				SimpleLog.LogError($"Could not find BladeQueueInfo for QueueType: {queueType}");
				return null;
			}
			return value.FirstOrDefault((BladeQueueInfo bqi) => bqi?.QueueId == id);
		}
	}

	private void SelectTab(BladeType view)
	{
		_selectedContext?.Hide();
		_headerText.SetText(GetLocKeyForBladeType(view));
		BladeContext selectedContext = _contextByType[view];
		_selectedContext = selectedContext;
		_selectedContext.Show(OnSelectionInfoUpdated);
		_selectedContext.SetData(_data, _currentSelectionInfo, _assetLookupSystem);
		_currentSelectionInfo.BladeType = view;
	}

	public IEnumerator ShowEventsTabAndFilter(string filterId)
	{
		BladeEventFilter eventFilter = _data.GetFilterForID(filterId);
		if (eventFilter == null)
		{
			SimpleLog.LogError("Could not find dynamic event filter by filter criteria name: " + filterId);
			yield break;
		}
		yield return ShowCoroutine();
		BladeContext selectedContext = _selectedContext;
		if (selectedContext == null || selectedContext.bladeView.Type != BladeType.Event)
		{
			SelectTab(BladeType.Event);
		}
		if (_selectedContext?.bladeView is EventBladeView eventBladeView)
		{
			eventBladeView.SelectFilter(eventFilter);
		}
	}

	private static string GetLocKeyForBladeType(BladeType type)
	{
		string result = "MainNav/General/Empty_String";
		switch (type)
		{
		case BladeType.Event:
			result = "PlayBlade/Tabs/Event_Title";
			break;
		case BladeType.FindMatch:
			result = "PlayBlade/Tabs/FindMatch_Title";
			break;
		case BladeType.LastPlayed:
			result = "PlayBlade/Tabs/LastPlayed_Title";
			break;
		}
		return result;
	}

	private void EnterMatchMakingSignalDispatched(EnterMatchMakingSignalArgs args)
	{
		JoinMatchMaking?.Invoke(args.InternalEventName, args.DeckId);
	}

	private void EditDeckSignalDispatched(EditDeckSignalArgs args)
	{
		EditDeck?.Invoke(args.DeckId, args.EventId, args.EventFormat, args.IsInvalidForFormat);
	}

	private void GoToEventPageSignalDispatched(GoToEventPageSignalArgs args)
	{
		GoToEventPage?.Invoke(args.InternalEventName);
	}

	private void QueueSelectedSignalDispatched(QueueSelectedSignalArgs args)
	{
		SelectQueue?.Invoke(args.BladeEventInfo);
	}

	private void SelectTabSignalDispatched(SelectTabSignalArgs args)
	{
		if (args.BladeType != BladeType.Invalid)
		{
			SelectTab(args.BladeType);
		}
	}

	private void ClosePlayBladeSignalDispatched(ClosePlayBladeSignalArgs args)
	{
		Hide(doMainNavAudio: false);
	}

	private void FilterSelectedSignalDispatched(FilterSelectedSignalArgs args)
	{
		SelectFilter?.Invoke(args.BladeEventFilter);
	}

	private void OnDismissClicked()
	{
		Hide(doMainNavAudio: true);
	}

	private void OnHideColumnClicked()
	{
	}

	private void AnimationEvent_OnPlayBladeShown()
	{
	}

	private void AnimationEvent_OnPlayBladeStartedMove()
	{
	}

	private void AnimationEvent_OnPlayBladeHidden()
	{
	}

	private void AnimationEvent_OnPlayBladeFinishedMove()
	{
	}
}
