using System;
using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Browser;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ClientSideInteraction
{
	private readonly IModalGreInteractionComparer _modalActionComparer = new ModalGreInteractionComparer();

	private readonly DuelSceneLogger _dsLogger;

	private readonly UIManager _uiManager;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IWorkflowProvider _workflowProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserController _browserController;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly ICardMovementController _cardMovementController;

	private readonly IModalBrowserCardHeaderProvider _headerProvider;

	private readonly IListFilter<GreInteraction> _actionListFilter;

	private readonly IAutoSubmitActionCalculator _autoSubmitActionCalculator;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	private static IBrowser _modalSelectionBrowser;

	private static DuelScene_CDC _overriddenCard;

	private static ICardDataAdapter _originalCardData;

	private static ICardDataAdapter _overrideCardData;

	private readonly Dictionary<DuelScene_CDC, GreInteraction> _clientSideChoiceMap = new Dictionary<DuelScene_CDC, GreInteraction>(10);

	private const float IdealPointScale = 0.33f;

	public ClientSideInteraction(DuelSceneLogger dsLogger, UIManager uiManager, IContext context, AssetLookupSystem assetLookupSystem)
		: this(dsLogger, uiManager, context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<ICardBuilder<DuelScene_CDC>>(), context.Get<IGameStateProvider>(), context.Get<IGameplaySettingsProvider>(), context.Get<IWorkflowProvider>(), context.Get<ICardHolderProvider>(), context.Get<IFakeCardViewController>(), context.Get<IBrowserController>(), context.Get<ISplineMovementSystem>(), context.Get<ICardMovementController>(), context.Get<IModalBrowserCardHeaderProvider>(), context.Get<IPlayWarningManager>(), assetLookupSystem)
	{
	}

	public ClientSideInteraction(DuelSceneLogger dsLogger, UIManager uiManager, IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettingsProvider, IWorkflowProvider workflowProvider, ICardHolderProvider cardHolderProvider, IFakeCardViewController fakeCardController, IBrowserController browserController, ISplineMovementSystem splineMovementSystem, ICardMovementController cardMovementController, IModalBrowserCardHeaderProvider headerProvider, IPlayWarningManager playWarningManager, AssetLookupSystem assetLookupSystem)
	{
		_dsLogger = dsLogger;
		_uiManager = uiManager;
		_cardDatabase = cardDatabase;
		_cardBuilder = cardBuilder;
		_gameStateProvider = gameStateProvider;
		_workflowProvider = workflowProvider;
		_cardHolderProvider = cardHolderProvider;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_splineMovementSystem = splineMovementSystem;
		_cardMovementController = cardMovementController;
		_headerProvider = headerProvider;
		_autoSubmitActionCalculator = new AutoSubmitActionCalculatorAggregate(new FreeCastAutoSubmitCalculator(objectPool, gameplaySettingsProvider), new IgnorableInactiveActionsCalculator(objectPool, gameplaySettingsProvider, cardDatabase.AbilityDataProvider));
		_actionListFilter = new ActionFilterAggregate(new IrrelevantActionFilter(), new FreeCastActionFilter(gameplaySettingsProvider));
		_playWarningManager = playWarningManager;
		_assetLookupSystem = assetLookupSystem;
	}

	public void TryGetActionForCard(DuelScene_CDC card, out GreInteraction action)
	{
		_clientSideChoiceMap.TryGetValue(card, out action);
	}

	public void HandleActions(IEntityView entity, List<GreInteraction> interactions, Action<GreInteraction> submitAction, bool showConfirm)
	{
		interactions = GetTrimmedInteractionList(interactions, _cardDatabase.AbilityDataProvider);
		if (!interactions.TrueForAll((GreInteraction x) => !x.IsActive))
		{
			if (entity is DuelScene_CDC cdc)
			{
				HandleCardActions(cdc, interactions, submitAction, showConfirm);
			}
			else if (entity is DuelScene_AvatarView)
			{
				HandlePlayerActions(interactions, submitAction);
			}
		}
	}

	private void HandleCardActions(DuelScene_CDC cdc, List<GreInteraction> interactions, Action<GreInteraction> submitAction, bool showConfirm)
	{
		if (interactions.Count == 1)
		{
			GreInteraction greInteraction = interactions[0];
			Wotc.Mtgo.Gre.External.Messaging.Action greAction = greInteraction.GreAction;
			if (showConfirm && _playWarningManager.ShouldDisplayPlayWarning(cdc, greAction))
			{
				_playWarningManager.ShowConfirmationBrowser(cdc, greAction, submitAction);
			}
			else if (ActionsAvailableWorkflowUtils_ColorPicker.CanUseColorPicker(_cardDatabase.AbilityDataProvider, greInteraction))
			{
				ActionsAvailableWorkflowUtils_ColorPicker.ShowManaColorSelection(cdc, interactions, submitAction, _uiManager.ManaColorSelector, _cardDatabase.AbilityDataProvider, _cardHolderProvider);
			}
			else
			{
				submitAction(greInteraction);
			}
		}
		else if (ActionsAvailableWorkflowUtils_ColorPicker.CanUseColorPicker(_cardDatabase.AbilityDataProvider, interactions.ToArray()))
		{
			if (ActionsAvailableWorkflowUtils_ColorPicker.HasMultipleTriggeredAbilities(_cardDatabase.AbilityDataProvider, _cardDatabase.CardDataProvider, interactions.ToArray()))
			{
				ActionsAvailableWorkflowUtils_ColorPicker.ShowSourceInstanceManaColorSelection(cdc, interactions, submitAction, _uiManager.ManaColorSelector, _cardDatabase.AbilityDataProvider, _cardHolderProvider);
			}
			else
			{
				ActionsAvailableWorkflowUtils_ColorPicker.ShowManaColorSelection(cdc, interactions, submitAction, _uiManager.ManaColorSelector, _cardDatabase.AbilityDataProvider, _cardHolderProvider);
			}
		}
		else
		{
			ModalSelection(cdc, interactions, ShowSecondaryConfirmationIfNecessary);
		}
		void ShowSecondaryConfirmationIfNecessary(GreInteraction chosenAction)
		{
			if (showConfirm && _playWarningManager.ShouldForceSecondaryConfirmation(chosenAction, cdc))
			{
				_playWarningManager.ShowConfirmationBrowser(cdc, chosenAction.GreAction, submitAction);
			}
			else
			{
				submitAction?.Invoke(chosenAction);
			}
		}
	}

	private void HandlePlayerActions(IReadOnlyList<GreInteraction> interactions, Action<GreInteraction> submitAction)
	{
		GreInteraction greInteraction = interactions.LastOrDefault((GreInteraction x) => x.IsActive);
		if (greInteraction != null)
		{
			submitAction?.Invoke(greInteraction);
		}
	}

	private static bool ShouldForceSecondaryConfirmation(GreInteraction interaction, DuelScene_CDC cdc)
	{
		if (interaction != null && interaction.Type == ActionType.Activate && interaction.GreAction != null && interaction.GreAction.Highlight == Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold)
		{
			return !cdc.Model.Instance.PlayWarnings.Exists((ShouldntPlayData warning) => warning.Reasons[0] == ShouldntPlayData.ReasonType.RedundantActivation);
		}
		return false;
	}

	private void ModalSelection(DuelScene_CDC cdc, List<GreInteraction> interactions, Action<GreInteraction> submitInteraction)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_browserController.CloseCurrentBrowser();
		_clientSideChoiceMap.Clear();
		List<GreInteraction> trimmedInteractions = new List<GreInteraction>(interactions);
		_actionListFilter.Filter(ref trimmedInteractions);
		if (_autoSubmitActionCalculator.TryGetAutoSubmitAction(trimmedInteractions, out var toSubmit))
		{
			submitInteraction(toSubmit);
			return;
		}
		_modalActionComparer.SetCompareParams(cdc.Model);
		trimmedInteractions.Sort(_modalActionComparer);
		_modalActionComparer.ClearCompareParams();
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		List<DuelScene_CDC> list2 = new List<DuelScene_CDC>();
		Dictionary<DuelScene_CDC, HighlightType> dictionary = new Dictionary<DuelScene_CDC, HighlightType>();
		Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.Action> dictionary2 = new Dictionary<DuelScene_CDC, Wotc.Mtgo.Gre.External.Messaging.Action>();
		Dictionary<DuelScene_CDC, AbilityPrintingData> dictionary3 = new Dictionary<DuelScene_CDC, AbilityPrintingData>();
		List<DuelScene_CDC> list3 = new List<DuelScene_CDC>();
		Dictionary<DuelScene_CDC, int> dictionary4 = new Dictionary<DuelScene_CDC, int>();
		HashSet<uint> hashSet = new HashSet<uint>();
		Dictionary<DuelScene_CDC, GreInteraction> callbackMapping = new Dictionary<DuelScene_CDC, GreInteraction>();
		bool cardAsButton = false;
		List<uint> abilitiesToRemove = new List<uint>(1);
		abilitiesToRemove.AddRange(from x in trimmedInteractions
			where x.Type == ActionType.Activate
			select x.GreAction.AbilityGrpId);
		abilitiesToRemove.AddRange(from x in trimmedInteractions
			where x.Type == ActionType.Cast
			select x.GreAction.AlternativeGrpId);
		abilitiesToRemove.AddRange(from x in trimmedInteractions
			where x.Type == ActionType.Special
			select x.GreAction.AbilityGrpId);
		CardData originalCardData = (CardData)cdc.Model;
		MtgCardInstance copy = originalCardData.Instance.GetCopy();
		CardPrintingData printing = originalCardData.Printing;
		IReadOnlyList<(uint, uint)> readOnlyList = printing.AbilityIds;
		IReadOnlyList<uint> readOnlyList2 = printing.LinkedFaceGrpIds;
		if (abilitiesToRemove.Any())
		{
			copy.Abilities.RemoveAll((AbilityPrintingData x) => abilitiesToRemove.Contains(x.Id));
			readOnlyList = printing.AbilityIds.Where(((uint Id, uint TextId) x) => !abilitiesToRemove.Contains(x.Id)).ToArray();
		}
		if (CardUtilities.IsModalChildFacet(printing.LinkedFaceType))
		{
			copy.LinkedFaceInstances.Clear();
			readOnlyList2 = Array.Empty<uint>();
		}
		foreach (AbilityPrintingData addedAbility in originalCardData.AddedAbilities)
		{
			originalCardData.PrintingAbilityIds.Contains(addedAbility.Id);
		}
		if (trimmedInteractions.Exists((GreInteraction x) => x.GreAction.AlternativeGrpId == 37 || x.GreAction.AlternativeGrpId == 307) && trimmedInteractions.Count > 1 && (from x in trimmedInteractions
			group x by x.GreAction.InstanceId).Exists((IGrouping<uint, GreInteraction> x) => x.Count() > 1))
		{
			copy.Abilities.RemoveAll((AbilityPrintingData x) => x.BaseId == 37 || x.BaseId == 307);
		}
		List<IClientSideOptionModelMutator> list4 = new List<IClientSideOptionModelMutator>();
		CardPrintingRecord record = printing.Record;
		IReadOnlyList<(uint, uint)> abilityIds = readOnlyList;
		IReadOnlyList<uint> linkedFaceGrpIds = readOnlyList2;
		CardPrintingData cardPrintingData = new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds, null, linkedFaceGrpIds));
		foreach (GreInteraction item2 in trimmedInteractions)
		{
			Wotc.Mtgo.Gre.External.Messaging.Action action = item2.GreAction;
			DuelScene_CDC duelScene_CDC = null;
			IReadOnlyList<ManaQuantity> readOnlyList3 = action.ConvertedActionManaCost();
			CardData cardData = null;
			if ((action.IsPlayAction() || action.IsCastAction()) && action.AlternativeGrpId == 0)
			{
				CardData normalModalCardData = ModalCardUtils.GetNormalModalCardData(action, originalCardData, copy, cardPrintingData, readOnlyList3, _cardDatabase, trimmedInteractions);
				duelScene_CDC = ModalCardUtils.GetModalCDC(item2.Type, normalModalCardData, cdc, _splineMovementSystem, _cardBuilder, _cardHolderProvider, ref cardAsButton);
				if (cardAsButton && _overriddenCard == null)
				{
					_overriddenCard = cdc;
					_originalCardData = originalCardData;
					_overrideCardData = normalModalCardData;
				}
			}
			else
			{
				AbilityPrintingData abilityPrintingData = null;
				if (ModalCardUtils.IsTurnFaceUpAbilityButNotKeyword(item2.Type, action.AbilityGrpId, in originalCardData.Instance.FaceDownState))
				{
					(cardData, abilityPrintingData) = ModalCardUtils.CreateTurnFaceUpAbilityModal(originalCardData, new List<ManaRequirement>(action.ManaCost), _cardDatabase, action.AbilityGrpId);
				}
				else if (action.AbilityGrpId == 349 || action.AbilityGrpId == 350)
				{
					cardData = ModalCardUtils.CreateUnlockRoomSubCard(action, originalCardData, _cardDatabase, readOnlyList3, _cardDatabase.ClientLocProvider);
				}
				else if (action.AlternativeGrpId != 0)
				{
					(cardData, abilityPrintingData) = AlternativeCastModalCardUtils.GetAlternativeCastModalData(action, originalCardData, copy, cardPrintingData, readOnlyList3, mtgGameState, _cardDatabase);
				}
				else if (item2.Type == ActionType.Activate || item2.Type == ActionType.ActivateMana || item2.Type == ActionType.MakePayment || item2.Type == ActionType.Special)
				{
					AddedAbilityData addedAbilityData = originalCardData.Instance.AbilityAdders.Find((AddedAbilityData x) => x.AbilityId == action.AbilityGrpId);
					bool flag = readOnlyList.Exists(((uint, uint) x) => x.Item1 == action.AbilityGrpId);
					CardData cardData2 = null;
					if ((!flag || hashSet.Contains(action.AbilityGrpId)) && addedAbilityData.SourceId != 0)
					{
						MtgCardInstance cardById = mtgGameState.GetCardById(addedAbilityData.SourceId);
						if (cardById != null && cardById.CatalogId == WellKnownCatalogId.None)
						{
							cardData2 = CardDataExtensions.CreateWithDatabase(cardById, _cardDatabase);
						}
						else if (addedAbilityData.SourceGrpId >= 11)
						{
							CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(addedAbilityData.SourceGrpId);
							if (cardPrintingById != null)
							{
								cardData2 = new CardData(cardPrintingById.CreateInstance(), cardPrintingById);
							}
						}
					}
					hashSet.Add(action.AbilityGrpId);
					uint abilityGrpId = action.AbilityGrpId;
					if (cardData2 != null)
					{
						abilityPrintingData = cardData2.Abilities.FirstOrDefault((AbilityPrintingData x) => x.Id == abilityGrpId);
					}
					if (abilityPrintingData == null)
					{
						abilityPrintingData = originalCardData.Abilities.FirstOrDefault((AbilityPrintingData x) => x.Id == abilityGrpId);
					}
					if (abilityPrintingData == null)
					{
						abilityPrintingData = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(abilityGrpId);
					}
					if (abilityPrintingData == null)
					{
						_dsLogger.ModalSelectionError(action.AbilityGrpId, originalCardData.GrpId, _cardDatabase.CardTitleProvider.GetCardTitle(originalCardData), "Invalid ability grpId on interaction");
						continue;
					}
					CardData cardData3 = cardData2 ?? originalCardData;
					cardData = CardDataExtensions.CreateAbilityCard(abilityPrintingData, cardData3, _cardDatabase);
					cardData.Instance.ParentId = cardData3.InstanceId;
					cardData.Instance.Parent = cardData3.Instance.GetCopy();
					cardData.Instance.SkinCode = cardData3.SkinCode;
					cardData.Instance.SleeveCode = cardData3.SleeveCode;
					cardData.Instance.ObjectSourceGrpId = cardData3.GrpId;
					(cardData.RulesTextOverride as AbilityTextOverride)?.AddSubstitution("Cost", "{" + ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(action.ManaCost)) + "}");
					if (abilityPrintingData.BaseId == 208)
					{
						AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(208u);
						string localizedText = _cardDatabase.GreLocProvider.GetLocalizedText(abilityPrintingById.TextId);
						string item = _cardDatabase.AbilityTextProvider.GetKeywordText(abilityPrintingData.Id).Replace(localizedText, string.Empty).Trim();
						string localizedText2 = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Foretell_Special_Action_Body", ("foretellCost", item));
						localizedText2 = ManaUtilities.ConvertManaSymbols(localizedText2);
						cardData.RulesTextOverride = new RawTextOverride(localizedText2);
					}
					else if (abilityPrintingData.SubCategory == AbilitySubCategory.ClassLevel)
					{
						CardPrintingData cardPrintingById2 = _cardDatabase.CardDataProvider.GetCardPrintingById(action.GrpId);
						cardData.RulesTextOverride = new AbilityTextOverride(_cardDatabase).AddSource(action.GrpId).AddAbility(abilityPrintingData).AddAbility(abilityPrintingData.GetClassGrantedAbilities(cardPrintingById2 ?? cardPrintingData));
					}
					else if (abilityPrintingData.Id == 172070)
					{
						cardData.Instance.Counters = cardData3.Instance.Counters;
					}
				}
				else
				{
					cardData = new CardData(new MtgCardInstance
					{
						ObjectType = GameObjectType.Ability,
						TitleId = originalCardData.TitleId
					}, new CardPrintingData(originalCardData.Printing, new CardPrintingRecord(0u, 0u, originalCardData.ImageAssetPath)));
					cardData.Instance.ParentId = originalCardData.InstanceId;
					cardData.Instance.Parent = originalCardData.Instance.GetCopy();
					cardData.Instance.SkinCode = originalCardData.SkinCode;
					cardData.Instance.SleeveCode = originalCardData.SleeveCode;
					if (mtgGameState.TryGetCard(action.InstanceId, out var card))
					{
						cardData.RulesTextOverride = new GreLocTextOverride(_cardDatabase.GreLocProvider, card.TitleId);
					}
					else
					{
						cardData.RulesTextOverride = new RawTextOverride(item2.Type.ToString());
					}
				}
				cardData.Instance.InstanceId = 0u;
				if (readOnlyList3 != null)
				{
					cardData.Instance.ManaCostOverride = readOnlyList3;
				}
				duelScene_CDC = _cardBuilder.CreateCDC(cardData);
				duelScene_CDC.CurrentCardHolder = _cardHolderProvider.GetCardHolder(GREPlayerNum.Invalid, CardHolderType.Invalid);
				_splineMovementSystem.MoveInstant(duelScene_CDC.Root, new IdealPoint(cdc.Root)
				{
					Scale = Vector3.one * 0.33f
				});
				if (abilityPrintingData != null)
				{
					int value = originalCardData.Abilities.IndexOf(abilityPrintingData);
					dictionary4.Add(duelScene_CDC, value);
					if (abilityPrintingData.Category == AbilityCategory.AlternativeCost)
					{
						list4.Add(new AlternateCostEffectRemover(cardData, abilityPrintingData));
					}
					dictionary3[duelScene_CDC] = abilityPrintingData;
				}
			}
			if (item2.IsActive)
			{
				list.Add(duelScene_CDC);
				callbackMapping.Add(duelScene_CDC, item2);
				_clientSideChoiceMap[duelScene_CDC] = item2;
			}
			else
			{
				list2.Add(duelScene_CDC);
			}
			list3.Add(duelScene_CDC);
			dictionary[duelScene_CDC] = ModalHighlighting.HighlightForGreInteraction(item2, (cardData ?? originalCardData).Instance.GetCopy());
			dictionary2[duelScene_CDC] = action;
		}
		if (cdc.CurrentCardHolder.CardHolderType != CardHolderType.Battlefield && _overriddenCard == null)
		{
			_cardMovementController.MoveCard(cdc, _cardHolderProvider.GetCardHolderByZoneId(mtgGameState.Stack.Id));
		}
		if (trimmedInteractions.Count == 1)
		{
			IClientLocProvider clientLocProvider = _cardDatabase.ClientLocProvider;
			OptionalActionBrowserProvider_ClientSide.OptionalActionBrowserData optionalActionBrowserData = new OptionalActionBrowserProvider_ClientSide.OptionalActionBrowserData
			{
				CardViews = list
			};
			optionalActionBrowserData.AbilityByCardView.Merge(dictionary3);
			optionalActionBrowserData.GreActionByCardView.Merge(dictionary2);
			optionalActionBrowserData.Header = clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title");
			optionalActionBrowserData.SubHeader = clientLocProvider.GetLocalizedText(cdc.Model.Instance.PlayWarnings.GetSubHeaderKey());
			optionalActionBrowserData.NoText = "DuelScene/ClientPrompt/ClientPrompt_Button_No";
			optionalActionBrowserData.GetBrowserCardHeaderData = _headerProvider.GetBrowserCardInfo;
			optionalActionBrowserData.OnNoAction = () =>
			{
				MtgZone zone = cdc.Model.Zone;
				ICardHolder cardHolder = ((zone != null) ? _cardHolderProvider.GetCardHolderByZoneId(zone.Id) : _cardHolderProvider.GetCardHolder(GREPlayerNum.Invalid, CardHolderType.Invalid));
				cdc.SetModel(originalCardData);
				_clientSideChoiceMap.Clear();
				if (cdc.CurrentCardHolder != cardHolder)
				{
					_cardMovementController.MoveCard(cdc, cardHolder);
				}
			};
			optionalActionBrowserData.YesText = "DuelScene/ClientPrompt/ClientPrompt_Button_Yes";
			optionalActionBrowserData.OnYesAction = () =>
			{
				_playWarningManager.IncrementPlayWarningCount();
				cdc.SetModel(originalCardData);
				_clientSideChoiceMap.Clear();
				submitInteraction(trimmedInteractions[0]);
			};
			optionalActionBrowserData.NoInteractionPerformedCloseAction = () =>
			{
				cdc.SetModel(originalCardData);
				_clientSideChoiceMap.Clear();
			};
			OptionalActionBrowserProvider_ClientSide optionalActionBrowserProvider_ClientSide = new OptionalActionBrowserProvider_ClientSide(optionalActionBrowserData);
			optionalActionBrowserProvider_ClientSide.SetOpenedBrowser(_browserController.OpenBrowser(optionalActionBrowserProvider_ClientSide));
			return;
		}
		Action<DuelScene_CDC> onSelectionMade = (DuelScene_CDC selection) =>
		{
			bool flag2 = selection == null;
			bool flag3 = !flag2 && callbackMapping.ContainsKey(selection);
			if (flag2 | flag3)
			{
				CleanUpClientSideModal(cdc, selection);
				bool showConfirm = false;
				bool flag4 = false;
				List<GreInteraction> list5 = new List<GreInteraction>();
				if (selection != null && callbackMapping.TryGetValue(selection, out var value2))
				{
					showConfirm = _playWarningManager.HasLegendaryWarning(selection, value2.GreAction);
					flag4 = true;
					list5.Add(value2);
				}
				_browserController.CloseCurrentBrowser();
				if (flag4)
				{
					HandleActions(cdc, list5, submitInteraction, showConfirm);
				}
				if (flag2)
				{
					WorkflowBase currentWorkflow = _workflowProvider.GetCurrentWorkflow();
					if (currentWorkflow is ActionsAvailableWorkflow_Browser actionsAvailableWorkflow_Browser)
					{
						actionsAvailableWorkflow_Browser.ApplyInteraction();
					}
					else if (currentWorkflow is ResolutionCastWorkflow resolutionCastWorkflow)
					{
						resolutionCastWorkflow.ApplyInteraction();
					}
				}
			}
		};
		string localizedText3 = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_One");
		string localizedText4 = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_Text");
		if (originalCardData.IsOmenCard() && trimmedInteractions.Count == 2 && trimmedInteractions.Exists((GreInteraction i) => i.Type == ActionType.CastOmen))
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(originalCardData);
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Header> loadedTree))
			{
				Header payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					localizedText3 = _cardDatabase.ClientLocProvider.GetLocalizedText(payload.LocKey);
				}
			}
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SubHeader> loadedTree2))
			{
				SubHeader payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
				if (payload2 != null)
				{
					localizedText4 = _cardDatabase.ClientLocProvider.GetLocalizedText(payload2.LocKey);
				}
			}
		}
		ModalBrowserProvider_ClientSide.ModalBrowserData modalBrowserData = new ModalBrowserProvider_ClientSide.ModalBrowserData
		{
			Header = localizedText3,
			SubHeader = localizedText4,
			CancelText = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
			Highlights = dictionary,
			OnSelectionMade = onSelectionMade,
			CanCancel = true,
			NoInteractionPerformedCloseAction = () =>
			{
				CleanUpClientSideModal(cdc, null);
			},
			GetBrowserCardHeaderData = _headerProvider.GetBrowserCardInfo
		};
		foreach (IClientSideOptionModelMutator item3 in list4)
		{
			if (item3 == null)
			{
				continue;
			}
			foreach (DuelScene_CDC item4 in list3)
			{
				ICardDataAdapter targetModel = item4.Model;
				if (item3.Mutate(ref targetModel))
				{
					item4.SetModel(targetModel);
				}
			}
		}
		modalBrowserData.Selectable.AddRange(list);
		modalBrowserData.NonSelectable.AddRange(list2);
		modalBrowserData.SortedCardList.AddRange(list3);
		modalBrowserData.AbilityByCardView.Merge(dictionary3);
		modalBrowserData.GreActionByCardView.Merge(dictionary2);
		ModalBrowserProvider_ClientSide modalBrowserProvider_ClientSide = new ModalBrowserProvider_ClientSide(modalBrowserData, _fakeCardController);
		_modalSelectionBrowser = _browserController.OpenBrowser(modalBrowserProvider_ClientSide);
		modalBrowserProvider_ClientSide.SetOpenedBrowser(_modalSelectionBrowser);
		_modalSelectionBrowser.ShownHandlers += OnModalBrowserShown;
		_modalSelectionBrowser.HiddenHandlers += OnModalBrowserHidden;
	}

	private static void OnModalBrowserHidden()
	{
		if (!(_overriddenCard == null) && _originalCardData != null)
		{
			_overriddenCard.SetModel(_originalCardData);
		}
	}

	private static void OnModalBrowserShown()
	{
		if (!(_overriddenCard == null) && _overrideCardData != null)
		{
			_overriddenCard.SetModel(_overrideCardData);
		}
	}

	private void CleanUpClientSideModal(DuelScene_CDC cdc, DuelScene_CDC selection)
	{
		if (_overriddenCard != null && _originalCardData != null)
		{
			_overriddenCard.SetModel(_originalCardData);
		}
		_clientSideChoiceMap.Clear();
		if (cdc.CurrentCardHolder.CardHolderType == CardHolderType.Stack)
		{
			_cardMovementController.MoveCard(cdc, cdc.PreviousCardHolder);
		}
		else if (selection != null && selection.Model != null && selection.PreviousCardHolder != null && selection.Model.LinkedFaceType != LinkedFace.PrototypeParent && selection.Model.LinkedFaceType != LinkedFace.MdfcFront && selection.Model.ObjectType == GameObjectType.Card && selection.PreviousCardHolder.CardHolderType != CardHolderType.Battlefield && !selection.Model.CardTypes.Contains(CardType.Land))
		{
			MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
			_cardMovementController.MoveCard(cdc, mtgGameState.Stack);
			cdc.PreviousCardHolder = _cardHolderProvider.GetCardHolderByZoneId(cdc.Model.Zone.Id);
		}
		if (_modalSelectionBrowser != null)
		{
			_overriddenCard = null;
			_originalCardData = null;
			_overrideCardData = null;
			_modalSelectionBrowser.ShownHandlers -= OnModalBrowserShown;
			_modalSelectionBrowser.HiddenHandlers -= OnModalBrowserHidden;
			_modalSelectionBrowser = null;
		}
	}

	public static List<GreInteraction> GetTrimmedInteractionList(List<GreInteraction> interactionList, IAbilityDataProvider abilityDataProvider)
	{
		if (interactionList.Count == 1)
		{
			return interactionList;
		}
		if (interactionList.Exists((GreInteraction x) => x.Type == ActionType.Cast || x.Type == ActionType.CastLeft || x.Type == ActionType.CastRight))
		{
			return interactionList.FindAll((GreInteraction x) => x.GreAction.IsCastAction() || x.GreAction.IsPlayAction() || x.Type == ActionType.Activate || x.Type == ActionType.Special);
		}
		for (int num = interactionList.Count - 1; num > -1; num--)
		{
			Wotc.Mtgo.Gre.External.Messaging.Action greAction = interactionList[num].GreAction;
			if (greAction.ManaPaymentOptions.Count == 0 && greAction.AbilityGrpId == 121983)
			{
				interactionList.RemoveAt(num);
			}
		}
		for (int num2 = interactionList.Count - 1; num2 >= 0; num2--)
		{
			GreInteraction greInteraction = interactionList[num2];
			for (int num3 = num2 - 1; num3 >= 0; num3--)
			{
				GreInteraction greInteraction2 = interactionList[num3];
				if (greInteraction.GreAction.IsRedundantDuplicateActivatedAbility(greInteraction2.GreAction, abilityDataProvider) || greInteraction.GreAction.IsRedundantManaPayment(greInteraction2.GreAction))
				{
					if (greInteraction.IsActive != greInteraction2.IsActive && greInteraction2.IsActive)
					{
						interactionList.RemoveAt(num2);
						break;
					}
					interactionList.RemoveAt(num3);
					num2--;
				}
			}
		}
		return interactionList;
	}
}
