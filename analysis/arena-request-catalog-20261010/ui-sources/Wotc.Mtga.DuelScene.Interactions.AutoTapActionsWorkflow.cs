using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using ReferenceMap;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class AutoTapActionsWorkflow : WorkflowBase<AutoTapActionsRequest>, ICardStackWorkflow
{
	public class AutoTapButtonTextFetcher
	{
		private readonly CWURBGOrderManaPaymentComparer _manaPaymentComparer = new CWURBGOrderManaPaymentComparer();

		private readonly AssetLookupSystem _assetLookupSystem;

		private readonly IAbilityDataProvider _abilityDataProvider;

		private readonly IObjectPool _genericObjectPool;

		public AutoTapButtonTextFetcher(AssetLookupSystem assetLookupSystem, IAbilityDataProvider abilityDataProvider, IObjectPool genericObjectPool)
		{
			_assetLookupSystem = assetLookupSystem;
			_abilityDataProvider = abilityDataProvider;
			_genericObjectPool = genericObjectPool;
		}

		public MTGALocalizedString GetButtonText(int solutionCount, AutoTapSolution autoTapSolution, string promptString)
		{
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ManaPaymentButtonTextOverride> loadedTree))
			{
				IEnumerable<ManaPaymentCondition> manaPaymentConditions = autoTapSolution.ManaPaymentConditions;
				ManaPaymentButtonTextOverride manaPaymentButtonTextOverride = null;
				if (manaPaymentConditions != null && manaPaymentConditions.Count() >= 1)
				{
					foreach (ManaPaymentCondition item in manaPaymentConditions)
					{
						uint abilityGrpId = item.AbilityGrpId;
						if (abilityGrpId == 0)
						{
							continue;
						}
						_assetLookupSystem.Blackboard.Clear();
						_assetLookupSystem.Blackboard.Ability = _abilityDataProvider.GetAbilityPrintingById(abilityGrpId);
						_assetLookupSystem.Blackboard.ManaPaymentCondition = item;
						_assetLookupSystem.Blackboard.GreAction = new Wotc.Mtgo.Gre.External.Messaging.Action();
						_assetLookupSystem.Blackboard.GreAction.AutoTapSolution = autoTapSolution;
						_assetLookupSystem.Blackboard.GreAction.ManaPaymentConditions.AddRange(manaPaymentConditions);
						manaPaymentButtonTextOverride = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
						if (manaPaymentButtonTextOverride == null)
						{
							continue;
						}
						Dictionary<string, string> dictionary = new Dictionary<string, string>();
						foreach (ManaPaymentButtonTextOverride.ParamNameContentPair parameterNameContentPair in manaPaymentButtonTextOverride.ParameterNameContentPairs)
						{
							dictionary.Add(parameterNameContentPair.Name, ManaUtilities.ConvertManaSymbols(parameterNameContentPair.Contents));
						}
						StringPostProcessor(dictionary, autoTapSolution, _genericObjectPool, manaPaymentButtonTextOverride.Key, out var localizedString);
						_assetLookupSystem.Blackboard.Clear();
						return localizedString;
					}
				}
				if (solutionCount > 1)
				{
					_assetLookupSystem.Blackboard.Clear();
					return new UnlocalizedMTGAString
					{
						Key = promptString
					};
				}
				manaPaymentButtonTextOverride = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
				foreach (ManaPaymentButtonTextOverride.ParamNameContentPair parameterNameContentPair2 in manaPaymentButtonTextOverride.ParameterNameContentPairs)
				{
					dictionary2.Add(parameterNameContentPair2.Name, ManaUtilities.ConvertManaSymbols(parameterNameContentPair2.Contents));
				}
				_assetLookupSystem.Blackboard.Clear();
				return new MTGALocalizedString
				{
					Key = manaPaymentButtonTextOverride.Key,
					Parameters = dictionary2
				};
			}
			_assetLookupSystem.Blackboard.Clear();
			return null;
		}

		private string StringConstructor(IObjectPool genericPool, AutoTapSolution autoTapSolution, CWURBGOrderManaPaymentComparer manaPaymentComparer)
		{
			StringBuilder stringBuilder = genericPool.PopObject<StringBuilder>();
			List<AutoTapManaPayment> list = genericPool.PopObject<List<AutoTapManaPayment>>();
			list.AddRange(autoTapSolution.ManaPayments);
			list.Sort(manaPaymentComparer);
			foreach (AutoTapManaPayment item in list)
			{
				stringBuilder.Append(ManaUtilities.ConvertManaSymbols("o" + ManaUtilities.ColorToString(item.ManaColor)));
			}
			list.Clear();
			genericPool.PushObject(list);
			string result = stringBuilder.ToString();
			stringBuilder.Clear();
			genericPool.PushObject(stringBuilder);
			return result;
		}

		private static string StringConstructorManaConditions(IObjectPool genericPool, AutoTapSolution autoTapSolution, CWURBGOrderManaPaymentComparer manaPaymentComparer)
		{
			StringBuilder stringBuilder = genericPool.PopObject<StringBuilder>();
			List<AutoTapManaPayment> list = genericPool.PopObject<List<AutoTapManaPayment>>();
			list.AddRange(autoTapSolution.ManaPayments);
			list.Sort(manaPaymentComparer);
			List<ManaColor> list2 = genericPool.PopObject<List<ManaColor>>();
			list2.AddRange(list.Select((AutoTapManaPayment x) => x.ManaColor));
			List<ManaColor> list3 = genericPool.PopObject<List<ManaColor>>();
			uint num = 0u;
			foreach (ManaPaymentCondition manaPaymentCondition in autoTapSolution.ManaPaymentConditions)
			{
				list3.AddRange(manaPaymentCondition.Colors);
				foreach (ManaColor item in list3)
				{
					if (list2.Remove(item))
					{
						num++;
					}
				}
				if (num == list3.Count)
				{
					foreach (ManaColor item2 in list3)
					{
						stringBuilder.Append(ManaUtilities.ConvertManaSymbols("o" + ManaUtilities.ColorToString(item2)));
					}
				}
				num = 0u;
				list3.Clear();
			}
			list.Clear();
			genericPool.PushObject(list);
			list2.Clear();
			genericPool.PushObject(list2);
			list3.Clear();
			genericPool.PushObject(list3);
			string result = stringBuilder.ToString();
			stringBuilder.Clear();
			genericPool.PushObject(stringBuilder);
			return result;
		}

		private void StringPostProcessor(Dictionary<string, string> parameters, AutoTapSolution autoTapSolution, IObjectPool genericPool, string payloadKey, out MTGALocalizedString localizedString)
		{
			if (ShouldShowManaSpecButtonBasedOnCondition(_assetLookupSystem.Blackboard.ManaPaymentCondition, autoTapSolution, ManaSpecType.FromSnow, out var manaCountOfSpecType) && _assetLookupSystem.Blackboard.ManaPaymentCondition.Type == ManaPaymentConditionType.Maximum)
			{
				if (manaCountOfSpecType < 7)
				{
					parameters["manaSymbols"] = ManaUtilities.ConvertManaSymbols(string.Concat(Enumerable.Repeat(parameters["manaSymbols"], manaCountOfSpecType)));
				}
				else
				{
					parameters["number"] = ManaUtilities.ConvertManaSymbols(manaCountOfSpecType.ToString());
				}
			}
			List<uint> list = new List<uint> { 153430u, 153431u, 153432u, 153433u, 153434u };
			if (autoTapSolution.ManaPayments.Count > 0 && autoTapSolution.ManaPayments.Count < 11 && list.Contains(_assetLookupSystem.Blackboard.Ability.Id))
			{
				parameters["manaSymbols"] = StringConstructor(genericPool, autoTapSolution, _manaPaymentComparer);
			}
			if (_assetLookupSystem.Blackboard.Ability.Tags.Contains(MetaDataTag.ShowManaConditionPayment))
			{
				string value = StringConstructorManaConditions(genericPool, autoTapSolution, _manaPaymentComparer);
				if (string.IsNullOrEmpty(value))
				{
					localizedString = new MTGALocalizedString();
					localizedString.Key = "DuelScene/Interaction/AutoPay/DefaultButtonText";
					return;
				}
				parameters["manaSymbols"] = value;
			}
			localizedString = new MTGALocalizedString();
			localizedString.Key = payloadKey;
			if (parameters.Count >= 1)
			{
				localizedString.Parameters = parameters;
			}
		}

		private bool ShouldShowManaSpecButtonBasedOnCondition(ManaPaymentCondition condition, AutoTapSolution solution, ManaSpecType type, out int manaCountOfSpecType)
		{
			if (condition.Specs.Contains(type))
			{
				manaCountOfSpecType = GetManaSpecCountInSolution(solution, type);
				return manaCountOfSpecType > 0;
			}
			manaCountOfSpecType = 0;
			return false;
		}
	}

	private class AutoTapActionsHighlightsGenerator : IHighlightsGenerator
	{
		private readonly Highlights _highlights = new Highlights();

		private readonly IReadOnlyCollection<uint> _autoTapInstanceIds;

		private readonly IReadOnlyCollection<(uint manaId, uint count)> _autoTapManaIds;

		public AutoTapActionsHighlightsGenerator(IReadOnlyCollection<uint> autoTapInstanceIds, IReadOnlyCollection<(uint manaId, uint count)> autoTapManaIds)
		{
			_autoTapInstanceIds = autoTapInstanceIds ?? Array.Empty<uint>();
			_autoTapManaIds = autoTapManaIds ?? Array.Empty<(uint, uint)>();
		}

		public Highlights GetHighlights()
		{
			_highlights.Clear();
			foreach (uint autoTapInstanceId in _autoTapInstanceIds)
			{
				_highlights.IdToHighlightType_User[autoTapInstanceId] = HighlightType.AutoPay;
			}
			foreach (var autoTapManaId in _autoTapManaIds)
			{
				_highlights.ManaHighlights[autoTapManaId.manaId] = new ManaHighlight(autoTapManaId.count, HighlightType.AutoPay);
			}
			return _highlights;
		}
	}

	public class SetButtonComparer : IComparer<AutoTapSolution>
	{
		public int Compare(AutoTapSolution x, AutoTapSolution y)
		{
			int num = x.ManaPaymentConditions.Count.CompareTo(y.ManaPaymentConditions.Count);
			if (num != 0)
			{
				return num;
			}
			if (x.ManaPaymentConditions.Count == 1 && y.ManaPaymentConditions.Count == 1)
			{
				num = y.ManaPaymentConditions[0].AbilityGrpId.CompareTo(x.ManaPaymentConditions[0].AbilityGrpId);
			}
			return num;
		}
	}

	private static IComparer<AutoTapSolution> _setButtonComparer = new SetButtonComparer();

	private readonly List<uint> _autoTapInstanceIds = new List<uint>();

	private readonly List<(uint manaId, uint count)> _autoTapManaIds = new List<(uint, uint)>();

	private readonly Dictionary<AutoTapSolution, AutoTapSolution> _sanitizedToOriginalAutoTapSolutionMapping = new Dictionary<AutoTapSolution, AutoTapSolution>();

	private readonly IObjectPool _objectPool;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AutoTapButtonTextFetcher _buttonTextFetcher;

	private readonly AssetLookupSystem _assetLookupSystem;

	private IBattlefieldCardHolder _battlefieldCache;

	private IBattlefieldCardHolder _battlefield => _battlefieldCache ?? (_battlefieldCache = _cardHolderProvider.GetCardHolder<IBattlefieldCardHolder>(GREPlayerNum.Invalid, CardHolderType.Battlefield));

	public AutoTapActionsWorkflow(AutoTapActionsRequest request, IObjectPool objectPool, IGameStateProvider gameStateProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, ICardHolderProvider cardHolderProvider, IPlayWarningManager playWarningManager, AutoTapButtonTextFetcher autoTapButtonTextFetcher, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_objectPool = objectPool ?? NullObjectPool.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		_buttonTextFetcher = autoTapButtonTextFetcher;
		_highlightsGenerator = new AutoTapActionsHighlightsGenerator(_autoTapInstanceIds, _autoTapManaIds);
		_assetLookupSystem = assetLookupSystem;
	}

	protected override void ApplyInteractionInternal()
	{
		SetPrompt();
		SetButtons();
	}

	protected override void SetPrompt()
	{
		if (IsPromptOverrideToManaPayment())
		{
			_workflowPrompt.Reset();
			string item = ManaUtilities.ConvertManaSymbols(_prompt.Parameters[0].StringValue);
			_workflowPrompt.LocKey = "DuelScene/Browsers/Spree_BrowserButton_PayMana";
			_workflowPrompt.LocParams = new (string, string)[1] { ("manaCost", item) };
			OnUpdatePrompt(_workflowPrompt);
		}
		else
		{
			base.SetPrompt();
		}
	}

	private bool IsPromptOverrideToManaPayment()
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.Request = _request;
		_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ShouldShowConfirmationPayload> loadedTree))
		{
			ShouldShowConfirmationPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null && payload.ShowConfirmation && _prompt.Parameters.Count > 0 && _prompt.Parameters[0].ParameterName == "Cost" && _prompt.Parameters[0].Type == ParameterType.NonLocalizedString)
			{
				return true;
			}
		}
		return false;
	}

	private AutoTapSolution CheckSanitizedSolution(AutoTapSolution autoTapSolution)
	{
		if (_request.Solutions.IndexOf(autoTapSolution) >= 0)
		{
			return autoTapSolution;
		}
		if (_sanitizedToOriginalAutoTapSolutionMapping.TryGetValue(autoTapSolution, out var value) && _request.Solutions.IndexOf(value) >= 0)
		{
			return value;
		}
		return autoTapSolution;
	}

	protected override void SetButtons()
	{
		int count = _request.Solutions.Count;
		Buttons.Cleanup();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		List<AutoTapSolution> list = _objectPool.PopObject<List<AutoTapSolution>>();
		foreach (AutoTapSolution solution in _request.Solutions)
		{
			list.Add(solution.Clone());
		}
		SanitizeAutoTapSolutions(list, _sanitizedToOriginalAutoTapSolutionMapping, _objectPool);
		list.Sort(_setButtonComparer);
		PromptButtonData promptButtonData;
		AutoTapSolution autoTapSolution;
		for (int i = 0; i < count; promptButtonData.PointerEnter = () =>
		{
			IEnumerable<AutoTapAction> autoTapActions = autoTapSolution.AutoTapActions;
			if (autoTapActions != null)
			{
				foreach (AutoTapAction item in autoTapActions)
				{
					if (item.ManaId != 0 && item.ManaPaymentOption != null)
					{
						foreach (ManaInfo item2 in item.ManaPaymentOption.Mana)
						{
							_autoTapManaIds.Add((item2.ManaId, item2.Count));
						}
					}
					if (item.InstanceId != 0)
					{
						_autoTapInstanceIds.Add(item.InstanceId);
					}
				}
			}
			SetHighlights();
			_battlefield.LayoutNow();
		}, promptButtonData.PointerExit = () =>
		{
			_autoTapInstanceIds.Clear();
			_autoTapManaIds.Clear();
			SetHighlights();
			_battlefield.LayoutNow();
		}, Buttons.WorkflowButtons.Add(promptButtonData), i++)
		{
			autoTapSolution = list[i];
			promptButtonData = new PromptButtonData
			{
				ButtonText = _buttonTextFetcher.GetButtonText(count, autoTapSolution, _promptTextProvider.GetPromptText(Prompt)),
				Tag = ((i == 0) ? ButtonTag.Primary : ButtonTag.Secondary)
			};
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.Request = _request;
			_assetLookupSystem.Blackboard.Prompt = _request.Prompt;
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<ShouldShowConfirmationPayload> loadedTree))
			{
				ShouldShowConfirmationPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null && payload.ShowConfirmation && !_playWarningManager.HasReachedWarningLimit())
				{
					promptButtonData.Style = ButtonStyle.StyleType.Tepid;
					promptButtonData.ButtonCallback = () =>
					{
						OpenYesNoBrowser(autoTapSolution);
					};
					continue;
				}
			}
			if (_request.ShouldCancel)
			{
				promptButtonData.Style = ButtonStyle.StyleType.Tepid_NoGlow;
			}
			else
			{
				MtgCardInstance topCardOnStack = mtgGameState.GetTopCardOnStack();
				if (topCardOnStack != null && topCardOnStack.Abilities.Exists((AbilityPrintingData x) => x.Tags.Contains(MetaDataTag.ShowManaConditionPayment)))
				{
					promptButtonData.Style = ButtonStyle.StyleType.Main;
				}
				else
				{
					promptButtonData.Style = ((i == 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Tepid);
				}
			}
			promptButtonData.ButtonCallback = () =>
			{
				_request.SubmitSolution(CheckSanitizedSolution(autoTapSolution));
			};
		}
		list.Clear();
		_objectPool.PushObject(list, tryClear: false);
		MtgCardInstance topCardOnStack2 = mtgGameState.GetTopCardOnStack();
		if (topCardOnStack2 != null && topCardOnStack2.GrpId == 71164)
		{
			HashSet<ReferenceMap.Reference> results = _objectPool.PopObject<HashSet<ReferenceMap.Reference>>();
			if (mtgGameState.ReferenceMap.GetTargeting(topCardOnStack2.InstanceId, ref results))
			{
				foreach (ReferenceMap.Reference item3 in results)
				{
					uint b = item3.B;
					if (mtgGameState.TryGetCard(b, out var card) && !card.CardTypes.Contains(CardType.Creature))
					{
						Buttons.WorkflowButtons.RemoveAll((PromptButtonData x) => x.Tag == ButtonTag.Secondary);
					}
				}
			}
			_objectPool.PushObject(results);
		}
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
				ButtonCallback = () =>
				{
					_request.Cancel();
				},
				ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
			};
		}
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

	public override void CleanUp()
	{
		_autoTapInstanceIds.Clear();
		_autoTapManaIds.Clear();
		_battlefieldCache = null;
		base.CleanUp();
	}

	public static int GetManaSpecCountInSolution(AutoTapSolution solution, ManaSpecType type)
	{
		int num = 0;
		foreach (AutoTapAction autoTapAction in solution.AutoTapActions)
		{
			if (autoTapAction == null || autoTapAction.ManaPaymentOption == null)
			{
				continue;
			}
			foreach (ManaInfo item in autoTapAction.ManaPaymentOption.Mana)
			{
				foreach (ManaInfo.Types.Spec spec in item.Specs)
				{
					if (spec.Type == type)
					{
						num++;
						break;
					}
				}
			}
		}
		return num;
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		if (_autoTapInstanceIds.Count == 0)
		{
			return true;
		}
		bool flag = _autoTapInstanceIds.Contains(lhs.InstanceId);
		bool flag2 = _autoTapInstanceIds.Contains(rhs.InstanceId);
		return flag == flag2;
	}

	private void OpenYesNoBrowser(AutoTapSolution autoTapSolution)
	{
		YesNoProvider browserTypeProvider = new YesNoProvider(Languages.ActiveLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"), _promptTextProvider.GetPromptText(_prompt), YesNoProvider.CreateButtonMap("DuelScene/ClientPrompt/ClientPrompt_Button_Yes", "DuelScene/ClientPrompt/ClientPrompt_Button_No"), YesNoProvider.CreateActionMap(() =>
		{
			_playWarningManager.IncrementPlayWarningCount();
			_request.SubmitSolution(autoTapSolution);
		}, SetButtons));
		_browserController.OpenBrowser(browserTypeProvider);
	}

	public static void SanitizeAutoTapSolutions(IReadOnlyList<AutoTapSolution> solutions, Dictionary<AutoTapSolution, AutoTapSolution> sanitizedToOriginalSolutions, IObjectPool objectPool)
	{
		for (int i = 0; i < solutions.Count; i++)
		{
			AutoTapSolution autoTapSolution = solutions[i];
			for (int j = 0; j < solutions.Count; j++)
			{
				if (i == j)
				{
					continue;
				}
				AutoTapSolution autoTapSolution2 = solutions[j];
				if (autoTapSolution.ManaPaymentConditions.Count != autoTapSolution2.ManaPaymentConditions.Count)
				{
					List<ManaPaymentCondition> list = objectPool.PopObject<List<ManaPaymentCondition>>();
					list.AddRange(autoTapSolution.ManaPaymentConditions.Except(autoTapSolution2.ManaPaymentConditions));
					List<ManaPaymentCondition> list2 = objectPool.PopObject<List<ManaPaymentCondition>>();
					list2.AddRange(autoTapSolution.ManaPaymentConditions.Intersect(autoTapSolution2.ManaPaymentConditions));
					if (list.Count > 0 && list2.Count > 0)
					{
						AutoTapSolution value = autoTapSolution.Clone();
						autoTapSolution.ManaPaymentConditions.Clear();
						autoTapSolution.ManaPaymentConditions.AddRange(list);
						sanitizedToOriginalSolutions.Add(autoTapSolution, value);
					}
					list.Clear();
					objectPool.PushObject(list, tryClear: false);
					list2.Clear();
					objectPool.PushObject(list2, tryClear: false);
				}
			}
		}
	}
}
