using System;
using System.Collections.Generic;
using System.Linq;
using Core.Code.Input;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectBasicLandSubtypeWorkflow : WorkflowBase<SelectNRequest>
{
	private static IReadOnlyList<ManaColor> ORDERED_COLORS = new List<ManaColor>
	{
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green
	};

	private ColorSelectionBrowser _colorSelectionBrowser;

	private readonly ManaColorSelector _colorSelector;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	public SelectBasicLandSubtypeWorkflow(SelectNRequest request, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, ICardViewProvider cardViewProvider, IClientLocProvider clientLocProvider, IBrowserController browserController, ManaColorSelector manaColorSelector)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_colorSelector = manaColorSelector;
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
	}

	public override bool CanApply(List<UXEvent> events)
	{
		return events.Count == 0;
	}

	protected override void ApplyInteractionInternal()
	{
		DuelScene_CDC sourceCard;
		DuelScene_CDC topCard2;
		if (_request.MaxSel > 2)
		{
			ColorSelectionBrowserProvider browserTypeProvider = new ColorSelectionBrowserProvider(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/ColorSelectHeader"), ORDERED_COLORS, _request.MaxSel, _request.CanCancel);
			_colorSelectionBrowser = _browserController.OpenBrowser(browserTypeProvider) as ColorSelectionBrowser;
			_colorSelectionBrowser.ManaSelectionsMadeEvent += ColorsSelected;
		}
		else if (TryGetSourceCard(_request.SourceId, _gameStateProvider.LatestGameState, out sourceCard))
		{
			DuelScene_CDC topCard;
			if (((sourceCard.CurrentCardHolder != null) ? sourceCard.CurrentCardHolder.CardHolderType : CardHolderType.None) == CardHolderType.Battlefield)
			{
				_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, sourceCard.Root, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), _stack.Get()), ColorsSelected);
				_stack.Get().TryAutoDock(new uint[1] { _request.SourceId });
			}
			else if (_stack.Get().TryGetTopCardOnStack(out topCard) && (sourceCard.InstanceId == topCard.Model.InstanceId || sourceCard.IsParentOf(topCard)))
			{
				_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, topCard, new ManaColorSelector.ManaColorSelectorConfig(sourceCard.Model, _request.CanCancel, PromptText(_request.Prompt), _stack.Get()), ColorsSelected);
			}
			else
			{
				_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, InputHelper.GetPointerPosition(), new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt)), ColorsSelected);
			}
		}
		else if (_stack.Get().TryGetTopCardOnStack(out topCard2) && topCard2.IsChildOf(_request.SourceId))
		{
			_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, topCard2, new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt)), ColorsSelected);
		}
		else
		{
			_colorSelector.OpenSelector(ORDERED_COLORS, _request.MaxSel, _request.ValidationType, InputHelper.GetPointerPosition(), new ManaColorSelector.ManaColorSelectorConfig(_request.CanCancel, PromptText(_request.Prompt)), ColorsSelected);
		}
	}

	private void ColorsSelected(IReadOnlyCollection<ManaColor> colors)
	{
		if ((colors == null || colors.Count == 0) && _request.CanCancel)
		{
			_request.Cancel();
			return;
		}
		_request.SubmitSelection(colors.Select((ManaColor x) => Convert.ToUInt32(x.ToLandSubtype())));
	}

	private string PromptText(Prompt prompt)
	{
		if (prompt == null)
		{
			return _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Land_Type");
		}
		return _promptTextProvider.GetPromptText(prompt);
	}

	private bool TryGetSourceCard(uint sourceId, MtgGameState gameState, out DuelScene_CDC sourceCard)
	{
		if (gameState.TryGetCard(sourceId, out var card) && _cardViewProvider.TryGetCardView(card.InstanceId, out sourceCard))
		{
			return sourceCard != null;
		}
		sourceCard = null;
		return false;
	}

	public override void CleanUp()
	{
		if (_colorSelectionBrowser != null)
		{
			_colorSelectionBrowser.ManaSelectionsMadeEvent -= ColorsSelected;
			_colorSelectionBrowser.Close();
		}
		_stack.ClearCache();
		base.CleanUp();
	}
}
