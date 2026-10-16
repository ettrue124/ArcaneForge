using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Pooling;
using ReferenceMap;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class OptionalActionMessageWorkflow : WorkflowBase<OptionalActionMessageRequest>, ISecondaryLayoutIdListProvider
{
	private class OptionalActionMessageHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _highlightIds;

		public OptionalActionMessageHighlightsGenerator(IReadOnlyCollection<uint> highlightIds)
		{
			_highlightIds = highlightIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint highlightId in _highlightIds)
			{
				highlights.IdToHighlightType_Workflow[highlightId] = HighlightType.Hot;
			}
			return highlights;
		}
	}

	private readonly IObjectPool _pool;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserProvider _browserProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public OptionalActionMessageWorkflow(OptionalActionMessageRequest request, IObjectPool objectPool, IGreLocProvider greLocProvider, IClientLocProvider clientLocProvider, IGameStateProvider gameStateProvider, IBrowserProvider browserProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_pool = objectPool ?? NullObjectPool.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_greLocProvider = greLocProvider ?? NullGreLocManager.Default;
		_clientLocProvider = clientLocProvider ?? NullLocProvider.Default;
		_browserProvider = browserProvider ?? NullBrowserProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_highlightsGenerator = new OptionalActionMessageHighlightsGenerator(_request.HighlightIds);
	}

	protected override void SetArrows()
	{
		Arrows.ClearLines();
		Arrows.ClearCtMLines();
		BrowserBase currentBrowser = _browserProvider.CurrentBrowser;
		if (currentBrowser == null || !currentBrowser.IsVisible)
		{
			MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
			MtgCardInstance cardById = mtgGameState.GetCardById(_request.SourceId);
			if (cardById != null)
			{
				if (cardById.Zone.Type == ZoneType.Stack)
				{
					Arrows.Exclusive = true;
					foreach (uint recipientId in _request.RecipientIds)
					{
						Arrows.AddLine(new Arrows.LineData(cardById.InstanceId, recipientId));
					}
				}
				else
				{
					HashSet<ReferenceMap.Reference> results = _pool.PopObject<HashSet<ReferenceMap.Reference>>();
					if (mtgGameState.ReferenceMap.GetReferences(cardById.InstanceId, ReferenceMap.ReferenceType.Targeting, 0u, ref results))
					{
						foreach (ReferenceMap.Reference item in results)
						{
							Arrows.AddLine(new Arrows.LineData(cardById.InstanceId, item.B));
						}
					}
					results.Clear();
					_pool.PushObject(results, tryClear: false);
				}
			}
		}
		OnUpdateArrows(Arrows);
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
	}

	protected override void SetButtons()
	{
		Buttons = new Buttons();
		MTGALocalizedString buttonText = "DuelScene/ClientPrompt/Take_Action";
		ButtonStyle.StyleType styleType = ButtonStyle.StyleType.Main;
		MTGALocalizedString buttonText2 = "DuelScene/ClientPrompt/Decline_Action";
		ButtonStyle.StyleType styleType2 = ButtonStyle.StyleType.Secondary;
		ButtonTag tag = ButtonTag.Secondary;
		FillBlackboardForWorkflow(_assetLookupSystem.Blackboard);
		_assetLookupSystem.Blackboard.ButtonStyle = styleType;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ButtonTextPayload> loadedTree))
		{
			ButtonTextPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				buttonText = payload.LocKey.GetText(_clientLocProvider, _greLocProvider);
			}
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ButtonStylePayload> loadedTree2))
		{
			ButtonStylePayload payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
			if (payload2 != null)
			{
				styleType = payload2.Style;
				if (styleType == ButtonStyle.StyleType.Escalated)
				{
					tag = ButtonTag.Primary;
				}
			}
		}
		_assetLookupSystem.Blackboard.ButtonStyle = styleType2;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree3))
		{
			SecondaryButtonTextPayload payload3 = loadedTree3.GetPayload(_assetLookupSystem.Blackboard);
			if (payload3 != null)
			{
				buttonText2 = payload3.LocKey.GetText(_clientLocProvider, _greLocProvider);
			}
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonStylePayload> loadedTree4))
		{
			SecondaryButtonStylePayload payload4 = loadedTree4.GetPayload(_assetLookupSystem.Blackboard);
			if (payload4 != null)
			{
				styleType2 = payload4.Style;
			}
		}
		_assetLookupSystem.Blackboard.Clear();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = buttonText,
			Style = styleType,
			Tag = tag,
			ButtonCallback = () =>
			{
				_request.SubmitResponse(OptionResponse.AllowYes);
			}
		});
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = buttonText2,
			Tag = ButtonTag.Secondary,
			Style = styleType2,
			ButtonCallback = () =>
			{
				_request.SubmitResponse(OptionResponse.CancelNo);
			}
		});
		if (_request.AllowUndo)
		{
			Buttons.UndoData = new PromptButtonData
			{
				ButtonCallback = () =>
				{
					_request.Undo();
				}
			};
		}
		OnUpdateButtons(Buttons);
	}

	public IEnumerable<uint> GetSecondaryLayoutIds()
	{
		return _request.RecipientIds;
	}

	private void FillBlackboardForWorkflow(IBlackboard bb)
	{
		bb.Clear();
		bb.Prompt = _prompt;
		bb.Request = _request;
		bb.Interaction = this;
		switch (_request.HighlightType)
		{
		default:
			bb.HighlightType = HighlightType.None;
			break;
		case Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold:
			bb.HighlightType = HighlightType.Cold;
			break;
		case Wotc.Mtgo.Gre.External.Messaging.HighlightType.Tepid:
			bb.HighlightType = HighlightType.Tepid;
			break;
		case Wotc.Mtgo.Gre.External.Messaging.HighlightType.Hot:
			bb.HighlightType = HighlightType.Hot;
			break;
		}
	}
}
