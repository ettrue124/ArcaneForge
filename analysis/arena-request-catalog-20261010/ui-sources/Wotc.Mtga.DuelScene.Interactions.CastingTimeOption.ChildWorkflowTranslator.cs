using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wizards.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Logging;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class ChildWorkflowTranslator : IWorkflowTranslation<CastingTimeOptionRequest>
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IEntityViewProvider _entityCardViewProvider;

	private readonly IPromptEngine _promptEngine;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly ICastTimeOptionHeaderProvider _castTimeOptionHeaderProvider;

	private readonly IWorkflowTranslation<CastingTimeOption_NumericInputRequest> _castingTimeNumericInputTranslation;

	private readonly IWorkflowTranslation<CastingTimeOption_ModalRequest> _modalTranslation;

	private readonly IWorkflowTranslation<CastingTimeOptionRequest> _replicateTranslation;

	private readonly CostKeywordTranslation _costKeywordTranslation;

	private readonly IWorkflowTranslation<SelectNRequest> _selectNTranslation;

	public ChildWorkflowTranslator(IContext context, IWorkflowTranslation<SelectNRequest> selectNTranslation, AssetLookupSystem assetLookupSystem, IUnityObjectPool unityObjectPool, ICanvasRootProvider canvasRootProvider)
	{
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = context.Get<ICardBuilder<DuelScene_CDC>>() ?? NullCardBuilder<DuelScene_CDC>.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_fakeCardViewController = context.Get<IFakeCardViewController>() ?? NullFakeCardViewController.Default;
		_browserController = context.Get<IBrowserController>() ?? NullBrowserController.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
		_promptEngine = context.Get<IPromptEngine>() ?? NullPromptEngine.Default;
		_clientLocProvider = context.Get<IClientLocProvider>() ?? NullLocProvider.Default;
		_selectNTranslation = selectNTranslation;
		IChooseXInterfaceBuilder chooseXInterfaceBuilder = new ChooseXInterfaceBuilder(unityObjectPool, assetLookupSystem, canvasRootProvider);
		_castingTimeNumericInputTranslation = new ChooseXTranslation(chooseXInterfaceBuilder, new ChooseXTranslation.ClientLocKeyProvider(assetLookupSystem), new ConfirmZeroLogger(new EventTypeLoggerAdapter(BIEventType.ConfirmZeroEvent), _gameStateProvider));
		_replicateTranslation = new ReplicateTranslation(chooseXInterfaceBuilder, _clientLocProvider);
		_modalTranslation = new ModalTranslation(context, assetLookupSystem);
		_costKeywordTranslation = new CostKeywordTranslation(context);
		_castTimeOptionHeaderProvider = new CastTimeOptionHeaderProvider(context);
	}

	public WorkflowBase Translate(CastingTimeOptionRequest req)
	{
		BaseUserRequest baseUserRequest = PrioritizedRequest(req);
		if (!(baseUserRequest is CastingTimeOption_NumericInputRequest req2))
		{
			if (!(baseUserRequest is CastingTimeOption_KickerRequest))
			{
				if (!(baseUserRequest is CastingTimeOption_TimingPermissionRequest))
				{
					if (!(baseUserRequest is CastingTimeOption_SelectNRequest castingTimeOption_SelectNRequest))
					{
						if (!(baseUserRequest is CastingTimeOption_ModalRequest req3))
						{
							if (!(baseUserRequest is CastingTimeOption_ChooseOrCostRequest request))
							{
								if (!(baseUserRequest is CastingTimeOption_CostKeywordRequest childRequest))
								{
									if (!(baseUserRequest is CastingTimeOption_SpecializeRequest request2))
									{
										if (!(baseUserRequest is CastingTimeOption_AdditionalCostRequest))
										{
											if (baseUserRequest is CastingTimeOption_Replicate)
											{
												return _replicateTranslation.Translate(req);
											}
											return null;
										}
										return new AdditionalCostWorkflow(req, _cardDatabase, _fakeCardViewController, _gameStateProvider, _browserController, _headerTextProvider, _castTimeOptionHeaderProvider);
									}
									return new CastingTimeOption_SpecializeWorkflow(request2, _cardDatabase, _fakeCardViewController, _browserController, _headerTextProvider);
								}
								return _costKeywordTranslation.Translate(req, childRequest);
							}
							return new CastingTimeOption_ChooseOrCostWorkflow(request, _cardViewProvider, _promptEngine, _browserController, _clientLocProvider);
						}
						return _modalTranslation.Translate(req3);
					}
					return _selectNTranslation.Translate(castingTimeOption_SelectNRequest.SelectNRequest);
				}
				return new CastingTimeOption_FlashWorkflow(req, _cardDatabase, _cardBuilder, _cardViewProvider, _browserController, _headerTextProvider);
			}
			return new CastingTimeOption_KickerWorkflow(req, _cardDatabase, _cardBuilder, _cardViewProvider, _browserController, _headerTextProvider);
		}
		return _castingTimeNumericInputTranslation.Translate(req2);
	}

	private static BaseUserRequest PrioritizedRequest(CastingTimeOptionRequest req)
	{
		if (req == null)
		{
			return null;
		}
		return PrioritizedRequest(req.ChildRequests);
	}

	public static BaseUserRequest PrioritizedRequest(List<BaseUserRequest> requests)
	{
		if (requests == null || requests.Count == 0)
		{
			return null;
		}
		if (requests.Count == 1)
		{
			return requests[0];
		}
		int num = requests.FindIndex((BaseUserRequest x) => x is CastingTimeOption_NumericInputRequest);
		if (num != -1)
		{
			return requests[num];
		}
		num = requests.FindIndex((BaseUserRequest x) => x is CastingTimeOption_SelectNRequest castingTimeOption_SelectNRequest && castingTimeOption_SelectNRequest.IsRequired);
		if (num != -1)
		{
			return requests[num];
		}
		num = requests.FindIndex((BaseUserRequest x) => x is CastingTimeOption_KickerRequest);
		if (num != -1)
		{
			return requests[num];
		}
		num = requests.FindIndex((BaseUserRequest x) => x is CastingTimeOption_CostKeywordRequest);
		if (num != -1)
		{
			return requests[num];
		}
		num = requests.FindIndex((BaseUserRequest x) => x is CastingTimeOption_TimingPermissionRequest);
		if (num != -1)
		{
			return requests[num];
		}
		return requests[0];
	}
}
