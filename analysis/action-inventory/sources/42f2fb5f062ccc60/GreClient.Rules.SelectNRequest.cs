using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectNRequest : BaseUserRequest
{
	public readonly OptionContext OptionContext;

	public readonly SelectionListType ListType;

	public readonly List<uint> Ids;

	public readonly List<uint> ZoneIds;

	public readonly IdType IdType;

	public readonly StaticList StaticList;

	public readonly SelectionValidationType ValidationType;

	public readonly List<int> Weights;

	public readonly SelectionContext Context;

	public readonly int MinSel;

	public readonly uint MaxSel;

	public readonly int MinWeight;

	public readonly int MaxWeight;

	public readonly List<uint> UnfilteredIds;

	public readonly List<uint> HotIds;

	public readonly Prompt ReqPrompt;

	public readonly bool ShouldCancel;

	public Dictionary<uint, HashSet<uint>> AffectedObjects = new Dictionary<uint, HashSet<uint>>();

	private static List<uint> dungonRoomGrpIds = new List<uint>
	{
		146073u, 146074u, 146075u, 146076u, 146077u, 146078u, 146079u, 146080u, 146081u, 146082u,
		146083u, 146084u, 146085u, 146086u, 146087u, 146088u, 146089u, 146090u, 146091u, 146092u,
		146093u
	};

	public bool IsStackingDecision => OptionContext == OptionContext.Stacking;

	public bool IsTriggeredAbilitySelection
	{
		get
		{
			if (IsStackingDecision)
			{
				return Context == SelectionContext.TriggeredAbility;
			}
			return false;
		}
	}

	public bool IsWishSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.Static)
			{
				return StaticList == StaticList.WishCards;
			}
			return false;
		}
	}

	public bool IsManaPoolSelection => Context == SelectionContext.ManaPool;

	public bool IsCardColorSelection
	{
		get
		{
			if (ListType == SelectionListType.Static || ListType == SelectionListType.StaticSubset)
			{
				if (StaticList != StaticList.Colors)
				{
					return StaticList == StaticList.CardColors;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsManaColorSelection
	{
		get
		{
			if (Context != SelectionContext.ManaFromAbility)
			{
				return StaticList == StaticList.ManaColors;
			}
			return true;
		}
	}

	public bool IsBasicLandSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.Static)
			{
				return StaticList == StaticList.BasicLandTypes;
			}
			return false;
		}
	}

	public bool IsBasicLandSubtypeSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.StaticSubset && Ids.Contains(29u) && Ids.Contains(43u) && Ids.Contains(49u) && Ids.Contains(54u) && Ids.Contains(69u) && Ids.Count == 5)
			{
				return StaticList == StaticList.SubTypes;
			}
			return false;
		}
	}

	public bool IsCardNameSelection_Subset
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.StaticSubset)
			{
				return StaticList == StaticList.CardNames;
			}
			return false;
		}
	}

	public bool IsCounterSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.StaticSubset)
			{
				return StaticList == StaticList.CounterTypes;
			}
			return false;
		}
	}

	public bool IsInstanceIdSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.Dynamic)
			{
				return IdType == IdType.InstanceId;
			}
			return false;
		}
	}

	public bool IsZoneSelection
	{
		get
		{
			if (Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ListType == SelectionListType.Dynamic)
			{
				return IdType == IdType.ZoneInstanceId;
			}
			return false;
		}
	}

	public bool IsChoiceSelection
	{
		get
		{
			if (((Context != SelectionContext.Discard && Context != SelectionContext.Resolution && Context != SelectionContext.Replacement) || ((ListType != SelectionListType.Static || StaticList != StaticList.CardNames) && StaticList != StaticList.CardTypes)) && ListType != SelectionListType.StaticSubset)
			{
				if (ListType == SelectionListType.Dynamic)
				{
					return IdType == IdType.PromptParameterIndex;
				}
				return false;
			}
			return true;
		}
	}

	public bool IsChoiceSelectionWithContext
	{
		get
		{
			if (UnfilteredIds.Count > 0 && Context == SelectionContext.Resolution && OptionContext == OptionContext.Resolution && ((ListType == SelectionListType.Static && StaticList == StaticList.CardNames) || (ListType == SelectionListType.StaticSubset && StaticList == StaticList.CardTypes)))
			{
				return IdType == IdType.None;
			}
			return false;
		}
	}

	public bool IsPrintingIndexSelection
	{
		get
		{
			if (ListType == SelectionListType.Dynamic)
			{
				return IdType == IdType.CardGrpIdIndex;
			}
			return false;
		}
	}

	public bool IsAbilitySelection
	{
		get
		{
			if (ListType == SelectionListType.Dynamic)
			{
				return IdType == IdType.AbilityGrpId;
			}
			return false;
		}
	}

	public bool IsPrintingSelection
	{
		get
		{
			if (ListType == SelectionListType.Dynamic)
			{
				return IdType == IdType.CardGrpId;
			}
			return false;
		}
	}

	public bool IsWeightedSelection => Weights.Count > 0;

	public bool IsLookSelection
	{
		get
		{
			if (MinSel == 0 && MaxSel == 0 && UnfilteredIds.Count == 0)
			{
				return Ids.Count > 0;
			}
			return false;
		}
	}

	public bool IsParitySelection
	{
		get
		{
			if (ListType == SelectionListType.Static && StaticList == StaticList.Parities)
			{
				return ValidationType == SelectionValidationType.NonRepeatable;
			}
			return false;
		}
	}

	public bool IsTypeKindSelection
	{
		get
		{
			if (StaticList == StaticList.TypeKinds)
			{
				return ValidationType == SelectionValidationType.NonRepeatable;
			}
			return false;
		}
	}

	public bool IsCostPaymentSelection
	{
		get
		{
			if (IdType == IdType.InstanceId && OptionContext == OptionContext.Payment && ParentRequest is EffectCostRequest effectCostRequest && effectCostRequest.CostSelection == this && effectCostRequest.ParentRequest is PayCostsRequest payCostsRequest)
			{
				return payCostsRequest.EffectCost == effectCostRequest;
			}
			return false;
		}
	}

	public SelectNRequest(SelectNReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		OptionContext = req.OptionContext;
		ListType = req.ListType;
		Ids = new List<uint>(req.Ids);
		ZoneIds = new List<uint>(req.ZoneIds);
		IdType = req.IdType;
		StaticList = req.StaticList;
		ValidationType = req.ValidationType;
		Weights = new List<int>(req.Weights);
		Context = req.Context;
		SourceId = req.SourceId;
		MinSel = req.MinSel;
		MaxSel = req.MaxSel;
		MinWeight = req.MinWeight;
		MaxWeight = req.MaxWeight;
		UnfilteredIds = new List<uint>(req.UnfilteredIds);
		HotIds = new List<uint>(req.HotIds);
		ReqPrompt = req.Prompt;
		ShouldCancel = req.ShouldCancel;
		if (!IsLookSelection)
		{
			foreach (uint id in Ids)
			{
				if (!UnfilteredIds.Contains(id))
				{
					UnfilteredIds.Add(id);
				}
			}
		}
		_outboundMessage.Type = ClientMessageType.SelectNresp;
		_outboundMessage.SelectNResp = new SelectNResp();
	}

	public void SubmitSelection(uint selection)
	{
		_outboundMessage.SelectNResp.Ids.Add(selection);
		Submit();
	}

	public void SubmitSelection(IEnumerable<uint> selections)
	{
		_outboundMessage.SelectNResp.Ids.AddRange(selections);
		Submit();
	}

	public void SubmitSelection(IEnumerable<uint> selections, OrderingType orderingType)
	{
		SelectNResp selectNResp = _outboundMessage.SelectNResp;
		foreach (uint selection in selections)
		{
			selectNResp.Ids.Add(selection);
		}
		selectNResp.UseArbitrary = orderingType;
		Submit();
	}

	public void SubmitArbitrary()
	{
		SelectNResp selectNResp = _outboundMessage.SelectNResp;
		selectNResp.Ids.Add(0u);
		selectNResp.UseArbitrary = OrderingType.OrderArbitraryOnce;
		Submit();
	}

	public Prompt GetPrompt()
	{
		Prompt reqPrompt = ReqPrompt;
		if (reqPrompt == null || reqPrompt.PromptId <= 1)
		{
			return Prompt;
		}
		return ReqPrompt;
	}

	public bool IsDungeonSelection(ICardDataProvider cardProvider)
	{
		if (IdType != IdType.CardGrpId)
		{
			return false;
		}
		foreach (uint id in Ids)
		{
			CardPrintingData cardPrintingById = cardProvider.GetCardPrintingById(id);
			if (cardPrintingById == null || !cardPrintingById.Types.Contains(CardType.Dungeon))
			{
				return false;
			}
		}
		return true;
	}

	public bool IsDungeonRoomSelection()
	{
		if (IdType != IdType.AbilityGrpId)
		{
			return false;
		}
		foreach (uint id in Ids)
		{
			if (!dungonRoomGrpIds.Contains(id))
			{
				return false;
			}
		}
		return true;
	}

	public bool IsLimboSelection(MtgGameState gameState, ICardDatabaseAdapter cardDatabase)
	{
		if (Ids.Count == 0)
		{
			return false;
		}
		if (gameState.ResolvingCardInstance != null && gameState.ResolvingCardInstance.ObjectType == GameObjectType.Ability && cardDatabase.AbilityDataProvider.TryGetAbilityPrintingById(gameState.ResolvingCardInstance.GrpId, out var ability) && ability.Tags.Contains(MetaDataTag.AllowSelectionOfCardsInLimbo) && IsSelectionThatIncludesLimboCards(gameState))
		{
			return true;
		}
		if (Context != SelectionContext.Resolution)
		{
			return false;
		}
		if (OptionContext != OptionContext.Resolution)
		{
			return false;
		}
		if (IdType != IdType.InstanceId)
		{
			return false;
		}
		if (ListType != SelectionListType.Dynamic)
		{
			return false;
		}
		foreach (uint id in Ids)
		{
			MtgCardInstance cardById = gameState.GetCardById(id);
			if (cardById == null)
			{
				return false;
			}
			if (cardById.Zone.Type != ZoneType.Limbo)
			{
				return false;
			}
		}
		return true;
	}

	public bool IsSelectionThatIncludesLimboCards(MtgGameState gameState)
	{
		if (Ids.Count == 0)
		{
			return false;
		}
		if (Context != SelectionContext.Resolution)
		{
			return false;
		}
		if (OptionContext != OptionContext.Resolution)
		{
			return false;
		}
		if (IdType != IdType.InstanceId)
		{
			return false;
		}
		if (ListType != SelectionListType.Dynamic)
		{
			return false;
		}
		foreach (uint id in Ids)
		{
			MtgCardInstance cardById = gameState.GetCardById(id);
			if (cardById == null)
			{
				return false;
			}
			if (cardById.Zone.Type == ZoneType.Limbo)
			{
				return true;
			}
		}
		return false;
	}

	public bool CanAutoSubmit()
	{
		if (Context != SelectionContext.Discard)
		{
			return HotIds.Count == 0;
		}
		return false;
	}

	public bool IsAnchorWordSelection(MtgGameState gameState)
	{
		if (IdType == IdType.PromptParameterIndex && ListType == SelectionListType.Dynamic && OptionContext == OptionContext.Resolution && Context == SelectionContext.Resolution && ValidationType == SelectionValidationType.NonRepeatable && gameState.ReplacementEffects.TryGetValue(SourceId, out var value))
		{
			foreach (ReplacementEffectData item in value)
			{
				uint effectAbilityId = item.AbilityId;
				if (gameState.TryGetCard(item.AffectedId, out var card) && card.Abilities.Exists((AbilityPrintingData x) => x.Id == effectAbilityId) && card.Abilities.Exists((AbilityPrintingData x) => x.Tags.Contains(MetaDataTag.AnchorWord)))
				{
					return true;
				}
			}
		}
		return false;
	}
}
