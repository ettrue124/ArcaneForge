using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class GroupWorkflow_BattlefieldPermanents : WorkflowBase<GroupRequest>, ICardStackWorkflow, IAutoRespondWorkflow, IClickableWorkflow, IAttachmentWorkflow
{
	public static class Utils
	{
		public static Dictionary<uint, uint> InitializeDistributions(IReadOnlyList<uint> targetIds, uint minPer)
		{
			Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
			foreach (uint targetId in targetIds)
			{
				dictionary[targetId] = minPer;
			}
			return dictionary;
		}

		public static List<SpinnerData> CreateSpinnerData(IReadOnlyList<uint> targetIds, int min, int max)
		{
			List<SpinnerData> list = new List<SpinnerData>();
			foreach (uint targetId in targetIds)
			{
				list.Add(new SpinnerData(targetId, min, min, max));
			}
			return list;
		}
	}

	private const int MAX_PILES = 2;

	private const int MIN_PILES = 1;

	private readonly Dictionary<uint, uint> _piles;

	private const string PILE_PARAMETER_STRING = "pileNumber";

	private readonly SpinnerController _spinnerController;

	private readonly IObjectPool _genericPool;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserManager _browserManager;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardTitleProvider _cardTitleProvider;

	private readonly IHighlightController _highlightController;

	private readonly ICardHoverController _cardHoverController;

	private readonly IWorkflowProvider _workflowProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBattlefieldCardHolder _battlefieldCardHolder;

	private readonly StackCardHolder _stackCardHolder;

	private readonly HandCardHolder _handCardHolder;

	private IReadOnlyList<uint> _targetIds => _request.InstanceIds;

	public GroupWorkflow_BattlefieldPermanents(GroupRequest request, ICardHolderProvider cardHolderProvider, IClientLocProvider clientLocProvider, IBrowserManager browserManager, IObjectPool genericPool, ICardViewProvider cardViewProvider, ICardTitleProvider cardTitleProvider, IHighlightController highlightController, ICardHoverController cardHoverController, IWorkflowProvider workflowProvider, IGameStateProvider gameStateProvider, SpinnerController spinnerController)
		: base(request)
	{
		_genericPool = genericPool ?? NullObjectPool.Default;
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_cardTitleProvider = cardTitleProvider ?? NullCardTitleProvider.Default;
		_highlightController = highlightController ?? NullHighlightController.Default;
		_cardHoverController = cardHoverController ?? NullCardHoverController.Default;
		_workflowProvider = workflowProvider ?? NullWorkflowProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_battlefieldCardHolder = cardHolderProvider.GetCardHolder<IBattlefieldCardHolder>(GREPlayerNum.Invalid, CardHolderType.Battlefield);
		_stackCardHolder = cardHolderProvider.GetCardHolder<StackCardHolder>(GREPlayerNum.Invalid, CardHolderType.Stack);
		_handCardHolder = cardHolderProvider.GetCardHolder<HandCardHolder>(GREPlayerNum.LocalPlayer, CardHolderType.Hand);
		_spinnerController = spinnerController;
		_piles = Utils.InitializeDistributions(_targetIds, 1u);
	}

	protected override void ApplyInteractionInternal()
	{
		List<SpinnerData> data = Utils.CreateSpinnerData(_targetIds, 1, 2);
		_spinnerController.Open(data);
		_spinnerController.ValueChanged += OnValueChanged;
		_handCardHolder.SetHandCollapse(collapsed: true);
		_stackCardHolder.TryAutoDock(_targetIds);
		_battlefieldCardHolder.LayoutNow();
		SetButtons();
	}

	private void Submit()
	{
		List<Group> groups = new List<Group>();
		Dictionary<uint, Group> dictionary = _genericPool.PopObject<Dictionary<uint, Group>>();
		List<uint> list = _genericPool.PopObject<List<uint>>();
		for (uint num = 1u; num <= 2; num++)
		{
			dictionary.Add(num, new Group());
		}
		foreach (KeyValuePair<uint, uint> pile in _piles)
		{
			dictionary[pile.Value].Ids.Add(pile.Key);
		}
		foreach (KeyValuePair<uint, Group> item in dictionary)
		{
			if (item.Value.Ids.Count == 0)
			{
				list.Add(item.Key);
			}
			groups.Add(item.Value);
		}
		if (_targetIds.Count > 2 && list.Count > 0)
		{
			YesNoProvider browserTypeProvider = new YesNoProvider(_clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Pile_Is_Empty", ("pileNumber", list[0].ToString())), YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
			{
				_request.SubmitGroups(groups);
			}));
			_browserManager.OpenBrowser(browserTypeProvider);
		}
		else
		{
			_request.SubmitGroups(groups);
		}
		dictionary.Clear();
		list.Clear();
		_genericPool.PushObject(dictionary);
		_genericPool.PushObject(list);
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit",
			Style = ButtonStyle.StyleType.Main,
			ButtonCallback = () =>
			{
				Submit();
			}
		});
		OnUpdateButtons(Buttons);
	}

	private void OnValueChanged(uint id, uint value)
	{
		_piles[id] = value;
		SetButtons();
		_battlefieldCardHolder.LayoutNow();
	}

	public override void CleanUp()
	{
		if (_spinnerController.Active)
		{
			_spinnerController.Close();
		}
		if ((bool)_stackCardHolder)
		{
			_stackCardHolder.ResetAutoDock();
			_stackCardHolder.TargetingSourceId = 0u;
		}
		if (_spinnerController != null)
		{
			_spinnerController.ValueChanged -= OnValueChanged;
		}
		base.CleanUp();
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		bool flag = _piles.TryGetValue(lhs.InstanceId, out var value);
		bool flag2 = _piles.TryGetValue(rhs.InstanceId, out var value2);
		if (flag & flag2)
		{
			return value == value2;
		}
		return flag == flag2;
	}

	public bool TryAutoRespond()
	{
		return false;
	}

	private bool IsSelectable(uint id)
	{
		return _request.InstanceIds.Contains(id);
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (clickType != SimpleInteractionType.Primary)
		{
			return false;
		}
		if (!_browserManager.IsAnyBrowserOpen && entity is DuelScene_CDC duelScene_CDC)
		{
			IBattlefieldStack stackForCard = _battlefieldCardHolder.GetStackForCard(duelScene_CDC);
			if (stackForCard != null && stackForCard.HasAttachmentOrExile && stackForCard.StackParent != duelScene_CDC && stackForCard.StackParentModel.Instance.AttachedWithIds.Count > 1)
			{
				foreach (uint attachedWithId in stackForCard.StackParentModel.Instance.AttachedWithIds)
				{
					if (IsSelectable(attachedWithId))
					{
						return true;
					}
				}
			}
		}
		return IsSelectable(entity.InstanceId);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_browserManager.IsAnyBrowserOpen || !(entity is DuelScene_CDC card))
		{
			return;
		}
		IBattlefieldStack stackForCard = _battlefieldCardHolder.GetStackForCard(card);
		if (stackForCard != null && stackForCard.HasAttachmentOrExile)
		{
			AttachmentAndExileStackBrowserProvider attachmentAndExileStackBrowserProvider = new AttachmentAndExileStackBrowserProvider(stackForCard.StackParentModel, _cardViewProvider, _cardTitleProvider, _highlightController, _cardHoverController, _workflowProvider, _gameStateProvider, _spinnerController, (DuelScene_CDC x) =>
			{
				OnClick(x, SimpleInteractionType.Primary);
			});
			attachmentAndExileStackBrowserProvider.SetOpenedBrowser(_browserManager.OpenBrowser(attachmentAndExileStackBrowserProvider));
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
