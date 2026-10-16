using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Browser;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class CastingTimeOption_ManaTypeWorkflow : BrowserWorkflowBase<CastingTimeOption_ManaTypeRequest>
{
	public class SelectionPair
	{
		public int SelectedIndex;

		public IReadOnlyList<ManaColor> Options;

		public bool IsEquivalent(ManaQuantity manaQuantity)
		{
			if (Options.Count == 2 && manaQuantity.Hybrid && manaQuantity.Color == Options[0])
			{
				return manaQuantity.AltColor == Options[1];
			}
			return false;
		}
	}

	public readonly IReadOnlyList<ManaQuantity> CastingCost;

	public readonly IReadOnlyList<SelectionPair> SelectionPairs;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IBrowserController _browserController;

	private readonly IClientLocProvider _locProvider;

	public override RequestType Type => _request.Type;

	public CastingTimeOption_ManaTypeWorkflow(CastingTimeOption_ManaTypeRequest request, IBrowserController browserController, IClientLocProvider locProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_browserController = browserController;
		_locProvider = locProvider;
		_assetLookupSystem = assetLookupSystem;
		IReadOnlyList<ManaQuantity> readOnlyList = ConvertToManaQuantity(request.CurrentManaCosts);
		SelectionPairs = ConvertToSelectionPairs(request.InnerRequests);
		if (readOnlyList.Count((ManaQuantity x) => x.IsPhyrexian) - request.InnerRequests.Count((CastingTimeOption_ManaTypeRequest.SelectManaTypeRequest x) => x.ManaColorOptions.Contains(ManaColor.Phyrexian)) > 0)
		{
			CastingCost = GetModifiedPhyrexianCastingCost(readOnlyList, SelectionPairs);
		}
		else
		{
			CastingCost = readOnlyList;
		}
	}

	protected override void ApplyInteractionInternal()
	{
		_header = _locProvider.GetLocalizedText("DuelScene/Browsers/Select_Cost_Title");
		_assetLookupSystem.Blackboard.Clear();
		AssetLookupTree<SubHeader> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<SubHeader>();
		if (assetLookupTree != null)
		{
			SubHeader payload = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				_subHeader = _locProvider.GetLocalizedText(payload.LocKey);
			}
		}
		_buttonStateData = GenerateButtons();
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (!(buttonKey == "SubmitButton"))
		{
			if (buttonKey == "CancelButton")
			{
				_request.Cancel();
			}
			return;
		}
		List<ManaColor> list = new List<ManaColor>(SelectionPairs.Count);
		foreach (SelectionPair selectionPair in SelectionPairs)
		{
			list.Add(selectionPair.Options[selectionPair.SelectedIndex]);
		}
		_request.SubmitSelection(list);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectManaType;
	}

	private Dictionary<string, ButtonStateData> GenerateButtons()
	{
		Dictionary<string, ButtonStateData> dictionary = new Dictionary<string, ButtonStateData>();
		dictionary["SubmitButton"] = new ButtonStateData
		{
			BrowserElementKey = (_request.CanCancel ? "2Button_Left" : "SingleButton"),
			Enabled = true,
			IsActive = true,
			LocalizedString = "DuelScene/Browsers/ViewDismiss_Done",
			StyleType = ButtonStyle.StyleType.Main
		};
		if (_request.CanCancel)
		{
			dictionary["CancelButton"] = new ButtonStateData
			{
				BrowserElementKey = "2Button_Right",
				Enabled = true,
				IsActive = true,
				LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel",
				StyleType = ButtonStyle.StyleType.Secondary
			};
		}
		return dictionary;
	}

	private static IReadOnlyList<ManaQuantity> ConvertToManaQuantity(IEnumerable<ManaRequirement> manaRequirements)
	{
		List<ManaQuantity> list = new List<ManaQuantity>();
		foreach (ManaRequirement manaRequirement in manaRequirements)
		{
			if (manaRequirement.Color.Count == 1 && manaRequirement.Color[0] == ManaColor.Generic)
			{
				ManaQuantity item = new ManaQuantity((uint)manaRequirement.Count, manaRequirement.Color);
				list.Add(item);
				continue;
			}
			for (int i = 0; i < manaRequirement.Count; i++)
			{
				ManaQuantity item2 = new ManaQuantity(1u, manaRequirement.Color);
				list.Add(item2);
			}
		}
		list.Sort(ManaQuantity.SortComparison);
		return list;
	}

	private static IReadOnlyList<SelectionPair> ConvertToSelectionPairs(IEnumerable<CastingTimeOption_ManaTypeRequest.SelectManaTypeRequest> requests)
	{
		List<SelectionPair> list = new List<SelectionPair>();
		foreach (CastingTimeOption_ManaTypeRequest.SelectManaTypeRequest request in requests)
		{
			list.Add(new SelectionPair
			{
				SelectedIndex = request.DefaultIndex,
				Options = request.ManaColorOptions
			});
		}
		return list;
	}

	public static List<ManaQuantity> GetModifiedPhyrexianCastingCost(IReadOnlyList<ManaQuantity> castingCosts, IEnumerable<SelectionPair> selectionPairs)
	{
		List<SelectionPair> list = new List<SelectionPair>(selectionPairs);
		List<ManaQuantity> list2 = new List<ManaQuantity>(castingCosts);
		for (int num = list2.Count - 1; num >= 0; num--)
		{
			ManaQuantity cost = list2[num];
			if (cost.IsPhyrexian)
			{
				int num2 = list.FindIndex((SelectionPair x) => x.IsEquivalent(cost));
				if (num2 != -1)
				{
					list.RemoveAt(num2);
				}
				else
				{
					list2[num] = new ManaQuantity(1u, cost.Color);
				}
			}
		}
		return list2;
	}
}
