using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using AssetLookupTree.Payloads.UI.DuelScene;
using GreClient.Rules;
using UnityEngine;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoicePromptButtonsWorkflow : WorkflowBase<SelectNRequest>
{
	private class HighlightGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly Dictionary<uint, HashSet<uint>> _affectedIds;

		private uint? _highlightId;

		public HighlightGenerator(Dictionary<uint, HashSet<uint>> affectedIds)
		{
			_affectedIds = affectedIds ?? new Dictionary<uint, HashSet<uint>>();
		}

		public void SetHighlightId(uint id)
		{
			_highlightId = id;
		}

		public void Clear()
		{
			_highlightId = null;
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			if (_highlightId.HasValue && _affectedIds.TryGetValue(_highlightId.Value, out var value))
			{
				foreach (uint item in value)
				{
					_highlights.IdToHighlightType_Workflow[item] = HighlightType.Selected;
				}
			}
			return _highlights;
		}
	}

	private readonly HighlightGenerator _highlights;

	private readonly ChoiceOptions _choiceOptions;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly AssetLoader.AssetTracker<Sprite> _assetTracker = new AssetLoader.AssetTracker<Sprite>("PromptButtonIcon");

	public ChoicePromptButtonsWorkflow(SelectNRequest request, ChoiceOptions choiceOptions, ICardDatabaseAdapter cardDatabaseAdapter, IResolutionEffectProvider resolutionEffectProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_choiceOptions = choiceOptions;
		_assetLookupSystem = assetLookupSystem;
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_resolutionEffectProvider = resolutionEffectProvider;
		_highlightsGenerator = (_highlights = new HighlightGenerator(request.AffectedObjects));
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
	}

	protected override void SetButtons()
	{
		foreach (string sortedChoice in _choiceOptions.SortedChoices)
		{
			uint id = _choiceOptions.ChoiceToIdMap[sortedChoice];
			int index = _choiceOptions.SortedChoices.IndexOf(sortedChoice);
			Buttons.WorkflowButtons.Insert(0, new PromptButtonData
			{
				ButtonText = new UnlocalizedMTGAString(sortedChoice),
				Style = GetStyleForButton(id, index),
				Tag = ButtonTag.Secondary,
				ButtonIcon = GetButtonIconSprite(id, index),
				ButtonCallback = () =>
				{
					SubmitRequest(id);
				},
				PointerEnter = () =>
				{
					SetButtonHighlights(id);
				},
				PointerExit = ClearHighlights
			});
		}
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = GetCancelLocKey(),
				Style = ButtonStyle.StyleType.Secondary,
				Tag = ButtonTag.Secondary,
				ButtonCallback = _request.Cancel
			};
		}
		base.SetButtons();
		_assetTracker.Cleanup();
	}

	private ButtonStyle.StyleType GetStyleForButton(uint id, int index)
	{
		ResolutionEffectModel resolutionEffectModel = _resolutionEffectProvider.ResolutionEffect;
		if (resolutionEffectModel != null && _request.ReqPrompt.Parameters.Count > index)
		{
			_assetLookupSystem.Blackboard.SetCardDataExtensive(resolutionEffectModel.Model);
			_assetLookupSystem.Blackboard.PromptParameterId = (uint)_request.ReqPrompt.Parameters[index].PromptId;
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonStylePayload> loadedTree))
		{
			SecondaryButtonStylePayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null && payload != null)
			{
				return payload.Style;
			}
		}
		if (_request.HotIds.Count <= 0 || _request.HotIds.Contains(id))
		{
			return ButtonStyle.StyleType.Secondary;
		}
		return ButtonStyle.StyleType.Tepid;
	}

	private Sprite GetButtonIconSprite(uint id, int index)
	{
		if (_request.StaticList == StaticList.CardTypes)
		{
			return null;
		}
		if (_request.IdType == IdType.PromptParameterIndex && _request.ReqPrompt.Parameters.Count < index)
		{
			return null;
		}
		_assetLookupSystem.Blackboard.Clear();
		if (_request.ReqPrompt.Parameters.Count > index)
		{
			_assetLookupSystem.Blackboard.PromptParameterId = (uint)_request.ReqPrompt.Parameters[index].PromptId;
		}
		_assetLookupSystem.Blackboard.Ability = _cardDatabaseAdapter.AbilityDataProvider.GetAbilityPrintingById(id);
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PromptButtonSpritePayload> loadedTree))
		{
			PromptButtonSpritePayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null && payload != null)
			{
				return _assetTracker.Acquire(payload.Reference.RelativePath);
			}
		}
		return null;
	}

	private void SetButtonHighlights(uint id)
	{
		_highlights.SetHighlightId(id);
		SetHighlights();
	}

	private void ClearHighlights()
	{
		_highlights.Clear();
		SetHighlights();
	}

	private string GetCancelLocKey()
	{
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree))
		{
			SecondaryButtonTextPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				return payload.LocKey.Key;
			}
		}
		return "DuelScene/ClientPrompt/ClientPrompt_Button_Decline";
	}

	private void SubmitRequest(uint id)
	{
		_request.SubmitSelection(id);
	}
}
