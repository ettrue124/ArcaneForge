using System;
using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.AssignDamage;

public class AssignDamageWorkflow : WorkflowBase<AssignDamageRequest>, IUpdateWorkflow
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IEntityNameProvider<uint> _nameProvider;

	private readonly IPlayerSpriteProvider _playerSpriteProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly AssignDamageHighlightsGenerator _assignDamageHighlights;

	private readonly Queue<DamageAssigner> _unhandledAssigners = new Queue<DamageAssigner>();

	private readonly List<DamageAssigner> _handledAssigners = new List<DamageAssigner>();

	private MtgDamageAssigner _damageAssigner;

	private readonly Dictionary<uint, MtgDamageAssignment> _idToDamageAssignments = new Dictionary<uint, MtgDamageAssignment>();

	private readonly List<uint> _damageAssignmentOrder = new List<uint>();

	private AssignDamageBrowser _assignDamageBrowser;

	private string _temporaryPromptText = string.Empty;

	private (string, string)[] _temporaryPromptParameters = Array.Empty<(string, string)>();

	public AssignDamageWorkflow(AssignDamageRequest request, IContext context)
		: base(request)
	{
		_gameStateProvider = context.Get<IGameStateProvider>();
		_browserController = context.Get<IBrowserController>();
		_clientLocProvider = context.Get<IClientLocProvider>();
		_nameProvider = context.Get<IEntityNameProvider<uint>>();
		_playerSpriteProvider = context.Get<IPlayerSpriteProvider>();
		_cardViewProvider = context.Get<ICardViewProvider>();
		_highlightsGenerator = (_assignDamageHighlights = new AssignDamageHighlightsGenerator(_cardViewProvider));
	}

	protected override void ApplyInteractionInternal()
	{
		foreach (DamageAssigner assigner in _request.Assigners)
		{
			_unhandledAssigners.Enqueue(assigner);
		}
		HandleNextUnhandledDamageAssigner();
	}

	private void HandleNextUnhandledDamageAssigner()
	{
		if (_unhandledAssigners.Count == 0)
		{
			_request.SubmitAssignment(_handledAssigners);
		}
		else
		{
			DequeueDamageAssigner();
		}
	}

	private void DequeueDamageAssigner()
	{
		CloseBrowser();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_temporaryPromptText = string.Empty;
		_temporaryPromptParameters = Array.Empty<(string, string)>();
		DamageAssigner damageAssigner = _unhandledAssigners.Dequeue();
		_damageAssigner = MtgDamageAssigner.Create(damageAssigner, _gameStateProvider.LatestGameState);
		_idToDamageAssignments.Clear();
		_damageAssignmentOrder.Clear();
		foreach (DamageAssignment assignment in damageAssigner.Assignments)
		{
			uint instanceId = assignment.InstanceId;
			bool flag = _damageAssigner.AttackQuarryId == instanceId;
			bool isBlockingAttacker = flag && _damageAssigner.AttackQuarryIsBlockingAttacker;
			_damageAssignmentOrder.Add(instanceId);
			_idToDamageAssignments[instanceId] = new MtgDamageAssignment(instanceId, (!damageAssigner.DamageAffectedByReplacements) ? assignment.AssignedDamage : 0u, assignment.MinDamage, (assignment.MaxDamage != 0) ? assignment.MaxDamage : damageAssigner.TotalDamage, flag, mtgGameState.TryGetPlayer(instanceId, out var _), isBlockingAttacker);
		}
		if (_damageAssigner.CanIgnoreBlockers)
		{
			OpenIgnoreBlockersButtons();
		}
		else
		{
			OpenAssignmentBrowser();
		}
	}

	private void OpenAssignmentBrowser()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (uint item in _damageAssignmentOrder)
		{
			if (_cardViewProvider.TryGetCardView(item, out var cardView) && (_damageAssigner.AttackQuarryId != item || _damageAssigner.AttackQuarryIsBlockingAttacker))
			{
				list.Add(cardView);
			}
		}
		AssignDamageProvider browserTypeProvider = new AssignDamageProvider(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/Assign_Damage_Title"), GetSubheaderForBrowser(), _clientLocProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_First"), _clientLocProvider.GetLocalizedText("DuelScene/Browsers/OrderTriggeredAbilities_Last"));
		_assignDamageBrowser = _browserController.OpenBrowser(browserTypeProvider) as AssignDamageBrowser;
		_assignDamageBrowser.SetAttackerCard(_cardViewProvider.GetCardView(_damageAssigner.InstanceId));
		_assignDamageBrowser.SetBlockerCards(list);
		MtgDamageAssigner damageAssigner = _damageAssigner;
		if (damageAssigner.CanAssignDamageToAttackQuarry && !damageAssigner.AttackQuarryIsBlockingAttacker)
		{
			uint attackQuarryId = _damageAssigner.AttackQuarryId;
			if (_damageAssigner.AttackQuarryIsPlayer)
			{
				int lifeTotal = (mtgGameState.TryGetPlayer(attackQuarryId, out var player) ? player.LifeTotal : 0);
				Sprite playerSprite = _playerSpriteProvider.GetPlayerSprite(attackQuarryId);
				_assignDamageBrowser.SetAttackQuarryPlayer(attackQuarryId, playerSprite, lifeTotal);
			}
			else
			{
				_assignDamageBrowser.SetAttackQuarryCard(_cardViewProvider.GetCardView(attackQuarryId));
			}
		}
		_assignDamageBrowser.SetAutoAssignLegalToggleActive(!_damageAssigner.DamageAffectedByReplacements);
		_assignDamageBrowser.LayoutCardHolder();
		_assignDamageBrowser.SetSpinnerColorsEnabled(!_damageAssigner.DamageAffectedByReplacements);
		_assignDamageBrowser.InitializeSpinners(_damageAssignmentOrder);
		_assignDamageBrowser.SetSpinnerButtonsActive(_damageAssigner.DamageAffectedByReplacements);
		_assignDamageBrowser.AssignmentIncreased += IncreaseDamageAssignment;
		_assignDamageBrowser.AssignmentDecreased += DecreaseDamageAssignment;
		_assignDamageBrowser.AutoAssignLethalToggled += SetAutoAssignLethal;
		_assignDamageBrowser.DoneAction += SubmitCurrentDamageAssigner;
		_assignDamageBrowser.UndoAction += TryUndo;
		_assignDamageBrowser.CardDragCompleted += ReorderDamageAssignments;
		_assignDamageBrowser.SetBrowserCardVFX();
		UpdateBrowser();
	}

	private string GetSubheaderForBrowser()
	{
		string key = (_damageAssigner.DamageAffectedByReplacements ? "DuelScene/Browsers/Assign_Damage_With_Arrows_Text" : "DuelScene/Browsers/Assign_Damage_Text");
		return _clientLocProvider.GetLocalizedText(key);
	}

	private void OpenIgnoreBlockersButtons()
	{
		SetButtons();
		_temporaryPromptText = "DuelScene/ClientPrompt/ClientPrompt_IgnoreBlockers";
		_temporaryPromptParameters = new (string, string)[1] { ("cardName", _nameProvider.GetName(_damageAssigner.InstanceId)) };
	}

	protected override void SetPrompt()
	{
		_workflowPrompt.Reset();
		if (!string.IsNullOrEmpty(_temporaryPromptText))
		{
			_workflowPrompt.LocKey = _temporaryPromptText;
			_workflowPrompt.LocParams = _temporaryPromptParameters;
		}
		else
		{
			_workflowPrompt.GrePrompt = Prompt;
		}
		OnUpdatePrompt(_workflowPrompt);
	}

	private void SubmitCurrentDamageAssigner()
	{
		_handledAssigners.Add(_damageAssigner.ToDamageAssigner(GetOrderedDamageAssignments()));
		HandleNextUnhandledDamageAssigner();
	}

	private void IncreaseDamageAssignment(uint instanceId)
	{
		List<MtgDamageAssignment> orderedDamageAssignments = GetOrderedDamageAssignments();
		if (_idToDamageAssignments.TryGetValue(instanceId, out var value))
		{
			switch (value.GetIncrementAction(_damageAssigner, orderedDamageAssignments))
			{
			case IncrementAction.Increment_UnassignedDamage:
				value.AssignedDamage++;
				break;
			case IncrementAction.Increment_Transfer:
			{
				for (int num = orderedDamageAssignments.Count - 1; num >= 0; num--)
				{
					MtgDamageAssignment mtgDamageAssignment = orderedDamageAssignments[num];
					if (mtgDamageAssignment != value && mtgDamageAssignment.CanDecrement(_damageAssigner, orderedDamageAssignments))
					{
						mtgDamageAssignment.AssignedDamage--;
						value.AssignedDamage++;
						break;
					}
				}
				break;
			}
			case IncrementAction.RedistributeAllFromBlockers:
				orderedDamageAssignments.ForEach((MtgDamageAssignment x) =>
				{
					x.AssignedDamage = (x.IsAttackQuarry ? x.MaxDamage : 0u);
				});
				break;
			case IncrementAction.RedistributeAllFromQuarry:
			{
				int index = orderedDamageAssignments.FindIndex((MtgDamageAssignment x) => x.IsAttackQuarry);
				orderedDamageAssignments[index].AssignedDamage = 0u;
				orderedDamageAssignments.RemoveAt(index);
				AssignPrioritizedDamage(_damageAssigner.TotalDamage, orderedDamageAssignments);
				break;
			}
			}
		}
		UpdateBrowser();
	}

	private void DecreaseDamageAssignment(uint instanceId)
	{
		if (_idToDamageAssignments.TryGetValue(instanceId, out var value))
		{
			value.AssignedDamage--;
		}
		UpdateBrowser();
	}

	private void UpdateBrowser()
	{
		if (_assignDamageBrowser != null)
		{
			List<MtgDamageAssignment> orderedDamageAssignments = GetOrderedDamageAssignments();
			_assignDamageBrowser.SetSpinnerValues(GenerateSpinnerData(_damageAssigner, orderedDamageAssignments));
			_assignDamageBrowser.SetButtons(AssignDamageProvider.CreateButtonMap(_damageAssigner.CanSubmit(orderedDamageAssignments)));
			_assignDamageHighlights.SetDamageAssignments(orderedDamageAssignments);
			_assignDamageHighlights.SetColdManaHighlightOverride(_damageAssigner.DamageAffectedByReplacements);
			SetHighlights();
		}
	}

	private static IEnumerable<AssignDamageBrowser.SpinnerData> GenerateSpinnerData(MtgDamageAssigner damageAssigner, IReadOnlyList<MtgDamageAssignment> damageAssignments)
	{
		foreach (MtgDamageAssignment damageAssignment in damageAssignments)
		{
			yield return new AssignDamageBrowser.SpinnerData(damageAssignment.InstanceId, damageAssignment.AssignedDamage, damageAssignment.HasLethalDamageAssigned, damageAssignment.GetIncrementAction(damageAssigner, damageAssignments) != IncrementAction.None, damageAssignment.CanDecrement(damageAssigner, damageAssignments));
		}
	}

	private void ReorderDamageAssignments(uint instanceId, int newIdx)
	{
		if (!_damageAssigner.DamageAffectedByReplacements && newIdx >= 0)
		{
			int num = _damageAssignmentOrder.IndexOf(instanceId);
			if (num != newIdx)
			{
				_damageAssignmentOrder.RemoveAt(num);
				_damageAssignmentOrder.Insert(newIdx, instanceId);
				AssignPrioritizedDamage(_damageAssigner.TotalDamage, _damageAssignmentOrder, _idToDamageAssignments);
				UpdateBrowser();
			}
		}
	}

	private static void AssignPrioritizedDamage(uint totalDamage, IReadOnlyList<uint> orderedAssignments, Dictionary<uint, MtgDamageAssignment> idToDamageAssignments)
	{
		List<MtgDamageAssignment> list = new List<MtgDamageAssignment>();
		foreach (uint orderedAssignment in orderedAssignments)
		{
			if (idToDamageAssignments.TryGetValue(orderedAssignment, out var value))
			{
				list.Add(value);
			}
		}
		AssignPrioritizedDamage(totalDamage, list);
	}

	private List<MtgDamageAssignment> GetOrderedDamageAssignments()
	{
		List<MtgDamageAssignment> list = new List<MtgDamageAssignment>();
		foreach (uint item in _damageAssignmentOrder)
		{
			if (_idToDamageAssignments.TryGetValue(item, out var value))
			{
				list.Add(value);
			}
		}
		return list;
	}

	private void AssignDamageAsThoughUnblocked()
	{
		foreach (KeyValuePair<uint, MtgDamageAssignment> idToDamageAssignment in _idToDamageAssignments)
		{
			MtgDamageAssignment value = idToDamageAssignment.Value;
			value.AssignedDamage = (value.IsAttackQuarry ? value.MaxDamage : 0u);
		}
		SubmitCurrentDamageAssigner();
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Yes",
			Style = ButtonStyle.StyleType.Main,
			ClearsInteractions = false,
			ButtonCallback = AssignDamageAsThoughUnblocked
		});
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_No",
			Style = ButtonStyle.StyleType.Secondary,
			ClearsInteractions = false,
			ButtonCallback = OpenAssignmentBrowser
		});
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = _request.Undo
			};
		}
		OnUpdateButtons(Buttons);
	}

	private void CloseBrowser()
	{
		if (_assignDamageBrowser != null)
		{
			_assignDamageBrowser.AssignmentIncreased -= IncreaseDamageAssignment;
			_assignDamageBrowser.AssignmentDecreased -= DecreaseDamageAssignment;
			_assignDamageBrowser.AutoAssignLethalToggled -= SetAutoAssignLethal;
			_assignDamageBrowser.CardDragCompleted -= ReorderDamageAssignments;
			_assignDamageBrowser.DoneAction -= SubmitCurrentDamageAssigner;
			_assignDamageBrowser.UndoAction -= TryUndo;
			_assignDamageBrowser.Close();
			_assignDamageBrowser = null;
		}
	}

	public override void CleanUp()
	{
		CloseBrowser();
		_assignDamageHighlights.Dispose();
		_unhandledAssigners.Clear();
		_handledAssigners.Clear();
		_idToDamageAssignments.Clear();
		_damageAssignmentOrder.Clear();
		_temporaryPromptText = string.Empty;
		_temporaryPromptParameters = Array.Empty<(string, string)>();
		base.CleanUp();
	}

	private static void AssignPrioritizedDamage(uint totalDamage, IReadOnlyList<MtgDamageAssignment> assignments)
	{
		uint num = totalDamage;
		for (int i = 0; i < assignments.Count; i++)
		{
			MtgDamageAssignment mtgDamageAssignment = assignments[i];
			if (num == 0)
			{
				mtgDamageAssignment.AssignedDamage = 0u;
				continue;
			}
			num -= (mtgDamageAssignment.AssignedDamage = ((num > mtgDamageAssignment.LethalDamage) ? mtgDamageAssignment.LethalDamage : num));
			if (i == assignments.Count - 1)
			{
				mtgDamageAssignment.AssignedDamage += num;
			}
		}
	}

	private void SetAutoAssignLethal(bool autoAssignLethal)
	{
		if (autoAssignLethal)
		{
			AssignPrioritizedDamage(_damageAssigner.TotalDamage, _damageAssignmentOrder, _idToDamageAssignments);
			UpdateBrowser();
		}
		_assignDamageBrowser.SetSpinnerButtonsActive(!autoAssignLethal);
	}

	public void Update()
	{
		_assignDamageBrowser?.LayoutMovingSpinners();
	}
}
