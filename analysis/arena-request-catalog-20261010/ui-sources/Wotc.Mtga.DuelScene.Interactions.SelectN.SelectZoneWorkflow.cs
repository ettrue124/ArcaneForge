using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectZoneWorkflow : WorkflowBase<SelectNRequest>, ISecondaryLayoutIdListProvider
{
	private readonly IEntityViewProvider _entityViewProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private const uint SecondaryLayout_PromptId = 15749u;

	public SelectZoneWorkflow(SelectNRequest req, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, IEntityViewProvider entityViewProvider, AssetLookupSystem assetLookupSystem)
		: base(req)
	{
		_gameStateProvider = gameStateProvider;
		_cardHolderProvider = cardHolderProvider;
		_entityViewProvider = entityViewProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	protected override void ApplyInteractionInternal()
	{
		SetButtons();
		LayoutAssociatedCardHolders(_gameStateProvider.LatestGameState);
	}

	private void SelectZone(uint zoneId)
	{
		_request.SubmitSelection(zoneId);
	}

	protected override void SetButtons()
	{
		AssetLookupTree<SelectZoneWorkflowButtonInfoPayload> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<SelectZoneWorkflowButtonInfoPayload>();
		IBlackboard blackboard = _assetLookupSystem.Blackboard;
		blackboard.Clear();
		blackboard.SetCardDataExtensive(_entityViewProvider.GetCardView(_request.SourceId)?.Model);
		Buttons.Cleanup();
		foreach (uint id in _request.Ids)
		{
			MtgZone zone = (blackboard.ZoneSelection = _gameStateProvider.LatestGameState.Value.GetZoneById(id));
			SelectZoneWorkflowButtonInfoPayload payload = assetLookupTree.GetPayload(blackboard);
			PromptButtonData promptButtonData = new PromptButtonData
			{
				ButtonText = GetButtonText(zone, id),
				Style = ButtonStyle.StyleType.Secondary,
				ButtonCallback = () =>
				{
					SelectZone(id);
				}
			};
			if (payload != null && payload.IsMainButton)
			{
				promptButtonData.Style = ButtonStyle.StyleType.Main;
			}
			if (payload != null && payload.IsTopSelection)
			{
				Buttons.WorkflowButtons.Add(promptButtonData);
			}
			else
			{
				Buttons.WorkflowButtons.Insert(0, promptButtonData);
			}
		}
		blackboard.Clear();
		base.SetButtons();
	}

	private string GetButtonText(MtgZone zone, uint zoneId)
	{
		if (zone != null)
		{
			return Utils.GetLocalizedZoneKey(zone.Type, zone.Owner);
		}
		return $"Zone: {zoneId}";
	}

	private void LayoutAssociatedCardHolders(MtgGameState gameState)
	{
		foreach (uint secondaryLayoutId in GetSecondaryLayoutIds())
		{
			if (gameState.TryGetCard(secondaryLayoutId, out var card))
			{
				MtgZone zone = card.Zone;
				if (zone != null && _cardHolderProvider.TryGetCardHolder<ICardHolder>(zone.Id, out var result))
				{
					result.LayoutNow();
				}
			}
		}
	}

	public IEnumerable<uint> GetSecondaryLayoutIds()
	{
		return GetSecondaryLayoutIds(_request.Prompt);
	}

	public static IEnumerable<uint> GetSecondaryLayoutIds(Prompt prompt)
	{
		if (prompt == null || prompt.PromptId != 15749)
		{
			yield break;
		}
		foreach (PromptParameter parameter in prompt.Parameters)
		{
			if (parameter.Type == ParameterType.Number && !(parameter.ParameterName != "CardId"))
			{
				yield return (uint)parameter.NumberValue;
			}
		}
	}
}
