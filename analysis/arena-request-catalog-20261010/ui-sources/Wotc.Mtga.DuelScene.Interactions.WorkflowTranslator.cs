using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;
using Wotc.Mtga.DuelScene.Interactions.Distribution;
using Wotc.Mtga.DuelScene.Interactions.Gather;
using Wotc.Mtga.DuelScene.Interactions.Grouping;
using Wotc.Mtga.DuelScene.Interactions.Mulligan;
using Wotc.Mtga.DuelScene.Interactions.NumericInput;
using Wotc.Mtga.DuelScene.Interactions.OptionalAction;
using Wotc.Mtga.DuelScene.Interactions.Search;
using Wotc.Mtga.DuelScene.Interactions.SelectCounters;
using Wotc.Mtga.DuelScene.Interactions.SelectFromGroups;
using Wotc.Mtga.DuelScene.Interactions.SelectNGroup;
using Wotc.Mtga.DuelScene.Interactions.SelectReplacements;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class WorkflowTranslator : IWorkflowTranslator
{
	private readonly IWorkflowTranslation<CastingTimeOptionRequest> _castingTimeOptionTranslation;

	private readonly IWorkflowTranslation<SelectCountersRequest> _selectCountersTranslation;

	private readonly IWorkflowTranslation<NumericInputRequest> _numericInputTranslation;

	private readonly IWorkflowTranslation<SelectNRequest> _selectNTranslation;

	private readonly IWorkflowTranslation<SelectFromGroupsRequest> _selectFromGroupsTranslation;

	private readonly IWorkflowTranslation<ActionsAvailableRequest> _actionsAvailableTranslation;

	private readonly IWorkflowTranslation<DistributionRequest> _distributionTranslation;

	private readonly IWorkflowTranslation<OptionalActionMessageRequest> _optionalActionTranslation;

	private readonly IWorkflowTranslation<SearchRequest> _searchTranslation;

	private readonly IWorkflowTranslation<SelectNGroupRequest> _selectNGroupTranslation;

	private readonly IWorkflowTranslation<GroupRequest> _groupTranslation;

	private readonly IWorkflowTranslation<GatherRequest> _gatherTranslation;

	private readonly IWorkflowTranslation<SelectReplacementRequest> _selectReplacementTranslation;

	private readonly IWorkflowTranslation<AssignDamageRequest> _assignDamageTranslation;

	private readonly IWorkflowTranslation<PayCostsRequest> _payCostsTranslation;

	private readonly IWorkflowTranslation<SearchFromGroupsRequest> _searchFromGroupsTranslation;

	private readonly IWorkflowTranslation<EffectCostRequest> _effectCostTranslation;

	private readonly IWorkflowTranslation<SubmitDeckRequest> _submitDeckTranslation;

	private readonly IWorkflowTranslation<IntermissionRequest> _intermissionTranslation;

	private readonly IWorkflowTranslation<AutoTapActionsRequest> _autoTapActionsTranslation;

	private readonly IWorkflowTranslation<CastingTimeOption_ManaTypeRequest> _ctoManaTypeTranslation;

	private readonly IWorkflowTranslation<OrderRequest> _orderTranslation;

	private readonly IWorkflowTranslation<MulliganRequest> _mulliganTranslation;

	private readonly IWorkflowTranslation<ChooseStartingPlayerRequest> _chooseStartingPlayerTranslation;

	private readonly IWorkflowTranslation<StringInputRequest> _adornCardTranslation;

	private readonly IWorkflowTranslation<DeclareBlockersRequest> _declareBlockersTranslation;

	private readonly IWorkflowTranslation<DeclareAttackerRequest> _declareAttackersTranslation;

	private readonly IWorkflowTranslation<SelectTargetsRequest> _selectTargetsTranslation;

	public WorkflowTranslator(GameManager gameManager, IContext context, AssetLookupSystem assetLookupSystem, IWorkflowTranslation<SelectNRequest> selectNTranslation, IWorkflowTranslation<ActionsAvailableRequest> actionsAvailableTranslation, IWorkflowTranslation<AssignDamageRequest> assignDamageTranslation, IWorkflowTranslation<DeclareBlockersRequest> declareBlockersTranslation, IWorkflowTranslation<DeclareAttackerRequest> declareAttackersTranslation, IWorkflowTranslation<SelectTargetsRequest> selectTargetsTranslation, DuelSceneLogger logger)
	{
		_selectNTranslation = selectNTranslation ?? NullWorkflowTranslation<SelectNRequest>.Default;
		_actionsAvailableTranslation = actionsAvailableTranslation ?? NullWorkflowTranslation<ActionsAvailableRequest>.Default;
		_assignDamageTranslation = assignDamageTranslation ?? NullWorkflowTranslation<AssignDamageRequest>.Default;
		_declareBlockersTranslation = declareBlockersTranslation ?? NullWorkflowTranslation<DeclareBlockersRequest>.Default;
		_declareAttackersTranslation = declareAttackersTranslation ?? NullWorkflowTranslation<DeclareAttackerRequest>.Default;
		_selectTargetsTranslation = selectTargetsTranslation ?? NullWorkflowTranslation<SelectTargetsRequest>.Default;
		_castingTimeOptionTranslation = new CastingTimeOptionTranslation(context, _selectNTranslation, assetLookupSystem);
		_selectCountersTranslation = new SelectCountersTranslation(context, assetLookupSystem, gameManager.SpinnerController);
		_numericInputTranslation = new NumericInputTranslation(context, assetLookupSystem);
		_selectFromGroupsTranslation = new SelectFromGroupsWorkflowTranslation(context);
		_distributionTranslation = new DistributionTranslation(context, gameManager.SpinnerController);
		_optionalActionTranslation = new OptionalActionTranslation(context, assetLookupSystem);
		_searchTranslation = new SearchTranslation(context);
		_selectNGroupTranslation = new SelectNGroupTranslation(context, gameManager);
		_groupTranslation = new GroupTranslation(context, gameManager);
		_gatherTranslation = new GatherTranslation(context, gameManager);
		_selectReplacementTranslation = new SelectReplacementTranslation(context);
		_payCostsTranslation = new PayCostsTranslation(context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), this, logger, gameManager);
		_searchFromGroupsTranslation = new SearchFromGroupsTranslation(context);
		_effectCostTranslation = new EffectCostTranslation(this);
		_submitDeckTranslation = new SubmitDeckTranslation(context, assetLookupSystem, gameManager.MainCamera);
		_intermissionTranslation = new IntermissionTranslation(context, assetLookupSystem);
		_autoTapActionsTranslation = new AutoTapActionsTranslation(context, assetLookupSystem);
		_ctoManaTypeTranslation = new CastingTimeOption_ManaTypeTranslation(context.Get<IBrowserController>(), context.Get<IClientLocProvider>(), assetLookupSystem);
		_orderTranslation = new OrderTranslation(context, assetLookupSystem);
		_mulliganTranslation = new MulliganTranslation(context, assetLookupSystem, gameManager);
		_chooseStartingPlayerTranslation = new ChooseStartingPlayerTranslation(context);
		_adornCardTranslation = new AdornCardsTranslation(context);
	}

	public WorkflowBase Translate(BaseUserRequest req)
	{
		if (!(req is ActionsAvailableRequest req2))
		{
			if (!(req is MulliganRequest req3))
			{
				if (!(req is ChooseStartingPlayerRequest req4))
				{
					if (!(req is StringInputRequest req5))
					{
						if (!(req is NumericInputRequest req6))
						{
							if (!(req is GatherRequest req7))
							{
								if (!(req is OrderRequest req8))
								{
									if (!(req is GroupRequest req9))
									{
										if (!(req is SearchRequest req10))
										{
											if (!(req is AssignDamageRequest req11))
											{
												if (!(req is SelectNGroupRequest req12))
												{
													if (!(req is OptionalActionMessageRequest req13))
													{
														if (!(req is CastingTimeOptionRequest req14))
														{
															if (!(req is CastingTimeOption_ManaTypeRequest req15))
															{
																if (!(req is DistributionRequest req16))
																{
																	if (!(req is SelectFromGroupsRequest req17))
																	{
																		if (!(req is SelectReplacementRequest req18))
																		{
																			if (!(req is SelectNRequest req19))
																			{
																				if (!(req is PayCostsRequest req20))
																				{
																					if (!(req is SearchFromGroupsRequest req21))
																					{
																						if (!(req is EffectCostRequest req22))
																						{
																							if (!(req is AutoTapActionsRequest req23))
																							{
																								if (!(req is IntermissionRequest req24))
																								{
																									if (!(req is SubmitDeckRequest req25))
																									{
																										if (!(req is SelectCountersRequest req26))
																										{
																											if (!(req is SelectTargetsRequest req27))
																											{
																												if (!(req is DeclareAttackerRequest req28))
																												{
																													if (req is DeclareBlockersRequest req29)
																													{
																														return _declareBlockersTranslation.Translate(req29);
																													}
																													return null;
																												}
																												return _declareAttackersTranslation.Translate(req28);
																											}
																											return _selectTargetsTranslation.Translate(req27);
																										}
																										return _selectCountersTranslation.Translate(req26);
																									}
																									return _submitDeckTranslation.Translate(req25);
																								}
																								return _intermissionTranslation.Translate(req24);
																							}
																							return _autoTapActionsTranslation.Translate(req23);
																						}
																						return _effectCostTranslation.Translate(req22);
																					}
																					return _searchFromGroupsTranslation.Translate(req21);
																				}
																				return _payCostsTranslation.Translate(req20);
																			}
																			return _selectNTranslation.Translate(req19);
																		}
																		return _selectReplacementTranslation.Translate(req18);
																	}
																	return _selectFromGroupsTranslation.Translate(req17);
																}
																return _distributionTranslation.Translate(req16);
															}
															return _ctoManaTypeTranslation.Translate(req15);
														}
														return _castingTimeOptionTranslation.Translate(req14);
													}
													return _optionalActionTranslation.Translate(req13);
												}
												return _selectNGroupTranslation.Translate(req12);
											}
											return _assignDamageTranslation.Translate(req11);
										}
										return _searchTranslation.Translate(req10);
									}
									return _groupTranslation.Translate(req9);
								}
								return _orderTranslation.Translate(req8);
							}
							return _gatherTranslation.Translate(req7);
						}
						return _numericInputTranslation.Translate(req6);
					}
					return _adornCardTranslation.Translate(req5);
				}
				return _chooseStartingPlayerTranslation.Translate(req4);
			}
			return _mulliganTranslation.Translate(req3);
		}
		return _actionsAvailableTranslation.Translate(req2);
	}
}
