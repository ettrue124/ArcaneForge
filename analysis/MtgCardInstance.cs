using System;
using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class MtgCardInstance : MtgEntity
{
	public class PendingEffectOverrideData
	{
		public StringBackedInt Power;

		public StringBackedInt Toughness;

		public readonly Dictionary<CounterType, int> Counters = new Dictionary<CounterType, int>();
	}

	public IReadOnlyCollection<ManaQuantity> ManaCostOverride = Array.Empty<ManaQuantity>();

	public MtgZone Zone;

	public GameObjectType ObjectType;

	public bool HasSummoningSickness;

	public bool IsTapped;

	public FaceDownState FaceDownState = new FaceDownState();

	public bool IsCopy;

	public bool IsObjectCopy;

	public uint CopyObjectGrpId;

	public bool IsTemporary;

	public AttackState AttackState;

	public uint AttackTargetId;

	public List<uint> BlockedByIds = new List<uint>();

	public BlockState BlockState;

	public List<uint> BlockingIds = new List<uint>();

	public MtgPlayer Controller;

	public MtgPlayer Owner;

	public List<SuperType> Supertypes = new List<SuperType>();

	public List<CardType> CardTypes = new List<CardType>();

	public List<SubType> Subtypes = new List<SubType>();

	public List<SubType> RemovedSubtypes = new List<SubType>();

	public List<CardColor> Colors = new List<CardColor>();

	public StringBackedInt Power;

	public StringBackedInt Toughness;

	public StringBackedInt SuppressedPower;

	public StringBackedInt SuppressedToughness;

	public uint Damage;

	public bool Damaged;

	public bool IsDamagedThisTurn;

	public uint? Loyalty;

	public uint? Defense;

	public uint ProtectorId;

	public List<KeyValuePair<MtgEntity, int>> Distributions = new List<KeyValuePair<MtgEntity, int>>();

	public string BaseSkinCode;

	public bool IsForceSimplified;

	public string SleeveCode;

	public Visibility Visibility;

	public List<GREPlayerNum> Viewers = new List<GREPlayerNum>();

	public uint ObjectSourceGrpId;

	public CardPrintingData ObjectSourcePrinting;

	public uint TitleId;

	public uint? OverlayGrpId;

	public WellKnownCatalogId CatalogId;

	public HashSet<uint> LinkedInfoTitleLocIds = new HashSet<uint>();

	public List<LinkedInfoText> LinkedInfoText = new List<LinkedInfoText>();

	public List<ShouldntPlayData> PlayWarnings = new List<ShouldntPlayData>();

	public List<CastingTimeOption> CastingTimeOptions = new List<CastingTimeOption>();

	public uint? ChooseXResult;

	public DieRollResultData? DieRollResults;

	public int ClassLevel;

	public Dictionary<uint, HashSet<DecoratorType>> Decorators = new Dictionary<uint, HashSet<DecoratorType>>();

	public bool PowerToughnessInverted;

	public List<ActionInfo> Actions = new List<ActionInfo>();

	public bool RevealedToOpponent;

	public uint? LoyaltyActivationsRemaining;

	public List<AppliedReplacementEffectData> AppliedReplacementEffects = new List<AppliedReplacementEffectData>();

	public ZoneType EnteredZoneThisTurn;

	public PendingEffectOverrideData PendingEffectOverrides;

	public uint RegenerateCount;

	public uint? BoonTriggersInitial;

	public uint? BoonTriggersRemaining;

	public List<ManaColor> ColorProduction = new List<ManaColor>();

	public List<ColorModification> ColorModifications = new List<ColorModification>();

	public uint AttachedToId;

	public HashSet<uint> AdditionalCostIds = new HashSet<uint>();

	public HashSet<uint> ReferencedCardTitleIds = new HashSet<uint>();

	public List<MtgCardInstance> Children = new List<MtgCardInstance>();

	public MtgCardInstance Parent;

	public uint ParentId;

	public List<MtgEntity> Targets = new List<MtgEntity>();

	public List<uint> TargetIds = new List<uint>();

	public List<MtgCardInstance> LinkedFaceInstances = new List<MtgCardInstance>(0);

	public HashSet<uint> SelectedObjects = new HashSet<uint>();

	public HashSet<uint> SelectedBy = new HashSet<uint>();

	public HashSet<uint> MutationChildrenIds = new HashSet<uint>();

	public List<MtgCardInstance> MutationChildren = new List<MtgCardInstance>();

	public uint MutationParentId;

	public MtgCardInstance MutationParent;

	public HashSet<uint> CrewedAndSaddledByIds = new HashSet<uint>();

	public HashSet<uint> CrewedAndSaddledParentIds = new HashSet<uint>();

	public CardColor? PendingSpecializationColor;

	public uint ExamineSourceId;

	public HashSet<int> GroupedIds = new HashSet<int>();

	public HashSet<uint> DistinguishedByIds = new HashSet<uint>();

	public uint? OthersideGrpId;

	public string CopyExceptionManaString = string.Empty;

	public uint GrpId
	{
		get
		{
			if (!OverlayGrpId.HasValue)
			{
				return BaseGrpId;
			}
			return OverlayGrpId.Value;
		}
		set
		{
			BaseGrpId = value;
		}
	}

	public uint BaseGrpId { get; set; }

	public bool IsPhasedOut
	{
		get
		{
			if (Zone != null)
			{
				return Zone.Type == ZoneType.PhasedOut;
			}
			return false;
		}
	}

	public string SkinCode { get; set; }

	public bool IsAttacking
	{
		get
		{
			AttackState attackState = AttackState;
			return attackState == AttackState.Attacking || attackState == AttackState.Declared;
		}
	}

	public bool IsBlocking
	{
		get
		{
			BlockState blockState = BlockState;
			return blockState == BlockState.Declared || blockState == BlockState.Blocking;
		}
	}

	public bool IsSplitCard()
	{
		foreach (MtgCardInstance linkedFaceInstance in LinkedFaceInstances)
		{
			if (linkedFaceInstance.ObjectType != GameObjectType.SplitLeft && linkedFaceInstance.ObjectType != GameObjectType.SplitRight)
			{
				return false;
			}
		}
		return LinkedFaceInstances.Count == 2;
	}

	public override string ToString()
	{
		return $"[InstanceId:{InstanceId}, GrpId:{GrpId}]";
	}

	public new MtgCardInstance GetCopy()
	{
		MtgCardInstance mtgCardInstance = (MtgCardInstance)MemberwiseClone();
		CopyValuesTo(mtgCardInstance);
		return mtgCardInstance;
	}

	public MtgCardInstance GetExamineCopy()
	{
		MtgCardInstance copy = GetCopy();
		copy.InstanceId = 0u;
		copy.Damage = 0u;
		copy.Damaged = false;
		copy.IsTapped = false;
		copy.HasSummoningSickness = false;
		copy.ExamineSourceId = InstanceId;
		MtgZone zone = copy.Zone;
		MtgZone mtgZone = new MtgZone
		{
			Type = (zone?.Type ?? ZoneType.Library),
			Id = (zone?.Id ?? 0),
			Owner = zone?.Owner,
			Visibility = (zone?.Visibility ?? Visibility.None)
		};
		zone = mtgZone;
		copy.Zone = mtgZone;
		copy.LinkedFaceInstances.Clear();
		foreach (MtgCardInstance linkedFaceInstance in LinkedFaceInstances)
		{
			MtgCardInstance copy2 = linkedFaceInstance.GetCopy();
			copy2.InstanceId = 0u;
			copy2.Damage = 0u;
			copy2.Damaged = false;
			copy2.IsTapped = false;
			copy2.HasSummoningSickness = false;
			copy2.Zone = zone;
			copy2.LinkedFaceInstances = new List<MtgCardInstance> { copy };
			copy.LinkedFaceInstances.Add(copy2);
		}
		return copy;
	}

	public MtgCardInstance GetFaceDownExamineCopy()
	{
		MtgCardInstance examineCopy = GetExamineCopy();
		examineCopy.MutationChildren.Clear();
		examineCopy.MutationChildrenIds.Clear();
		examineCopy.Counters.Clear();
		examineCopy.CounterDatas.Clear();
		examineCopy.LayeredEffects.Clear();
		return examineCopy;
	}

	public bool TryGetBackInstance(out MtgCardInstance backInstance)
	{
		if (LinkedFaceInstances == null)
		{
			backInstance = null;
			return false;
		}
		foreach (MtgCardInstance linkedFaceInstance in LinkedFaceInstances)
		{
			if (linkedFaceInstance != null && linkedFaceInstance.ObjectType == GameObjectType.Mdfcback)
			{
				backInstance = linkedFaceInstance;
				return true;
			}
		}
		backInstance = null;
		return false;
	}

	protected override void CopyValuesTo(MtgEntity target)
	{
		base.CopyValuesTo(target);
		if (!(target is MtgCardInstance mtgCardInstance))
		{
			return;
		}
		mtgCardInstance.BlockingIds = new List<uint>(BlockingIds);
		mtgCardInstance.BlockedByIds = new List<uint>(BlockedByIds);
		mtgCardInstance.Supertypes = new List<SuperType>(Supertypes);
		mtgCardInstance.CardTypes = new List<CardType>(CardTypes);
		mtgCardInstance.Subtypes = new List<SubType>(Subtypes);
		mtgCardInstance.RemovedSubtypes = new List<SubType>(RemovedSubtypes);
		mtgCardInstance.Colors = new List<CardColor>(Colors);
		mtgCardInstance.Targets = new List<MtgEntity>(Targets);
		mtgCardInstance.TargetIds = new List<uint>(TargetIds);
		mtgCardInstance.Viewers = new List<GREPlayerNum>(Viewers);
		mtgCardInstance.LinkedFaceInstances = new List<MtgCardInstance>(LinkedFaceInstances);
		mtgCardInstance.LinkedInfoTitleLocIds = new HashSet<uint>(LinkedInfoTitleLocIds);
		mtgCardInstance.LinkedInfoText = new List<LinkedInfoText>(LinkedInfoText);
		mtgCardInstance.LayeredEffects = new List<LayeredEffectData>(LayeredEffects);
		mtgCardInstance.PlayWarnings = new List<ShouldntPlayData>(PlayWarnings);
		mtgCardInstance.CastingTimeOptions = new List<CastingTimeOption>(CastingTimeOptions);
		mtgCardInstance.Decorators = new Dictionary<uint, HashSet<DecoratorType>>(Decorators);
		mtgCardInstance.Children = new List<MtgCardInstance>(Children);
		mtgCardInstance.SelectedObjects = new HashSet<uint>(SelectedObjects);
		mtgCardInstance.SelectedBy = new HashSet<uint>(SelectedBy);
		mtgCardInstance.MutationChildren = new List<MtgCardInstance>(MutationChildren);
		mtgCardInstance.MutationChildrenIds = new HashSet<uint>(MutationChildrenIds);
		mtgCardInstance.AdditionalCostIds = new HashSet<uint>(AdditionalCostIds);
		mtgCardInstance.AffectorOfLinkInfos = new List<LinkInfoData>(AffectorOfLinkInfos);
		mtgCardInstance.AffectedByLinkInfos = new List<LinkInfoData>(AffectedByLinkInfos);
		mtgCardInstance.CounterDatas = new List<CounterData>(CounterDatas);
		mtgCardInstance.ReferencedCardTitleIds = new HashSet<uint>(ReferencedCardTitleIds);
		mtgCardInstance.GroupedIds = new HashSet<int>(GroupedIds);
		mtgCardInstance.DistinguishedByIds = new HashSet<uint>(DistinguishedByIds);
		if (Parent != null)
		{
			mtgCardInstance.Parent = Parent.GetCopy();
		}
		mtgCardInstance.FaceDownState = FaceDownState.CopyFaceDownState(FaceDownState);
		if (PendingEffectOverrides != null)
		{
			mtgCardInstance.PendingEffectOverrides = new PendingEffectOverrideData();
			mtgCardInstance.PendingEffectOverrides.Power = new StringBackedInt(PendingEffectOverrides.Power);
			mtgCardInstance.PendingEffectOverrides.Toughness = new StringBackedInt(PendingEffectOverrides.Toughness);
			foreach (KeyValuePair<CounterType, int> counter in PendingEffectOverrides.Counters)
			{
				mtgCardInstance.PendingEffectOverrides.Counters[counter.Key] = counter.Value;
			}
		}
		else
		{
			mtgCardInstance.PendingEffectOverrides = null;
		}
		mtgCardInstance.ColorProduction = new List<ManaColor>(ColorProduction);
	}

	public static MtgCardInstance UnknownCardData(uint instanceId, MtgZone zone)
	{
		return new MtgCardInstance
		{
			InstanceId = instanceId,
			Zone = zone,
			Owner = zone.Owner,
			Controller = zone.Owner,
			CatalogId = WellKnownCatalogId.StandardCardBack
		};
	}

	public void CopyFromPrinting(CardPrintingData printingData)
	{
		TitleId = printingData.TitleId;
		Power = printingData.Power;
		Toughness = printingData.Toughness;
		Abilities = new List<AbilityPrintingData>(printingData.Abilities);
		CardTypes = new List<CardType>(printingData.Types);
		Colors = new List<CardColor>(printingData.Colors);
		Subtypes = new List<SubType>(printingData.Subtypes);
		Supertypes = new List<SuperType>(printingData.Supertypes);
	}

	public void IgnoreFaceDownAnnotation()
	{
		FaceDownState.SetReasonFaceDown(ReasonFaceDown.None);
	}

	public void IgnoreCopiedFaceDownAnnotation()
	{
		FaceDownState.SetCopiedCardsReasonFaceDown(ReasonFaceDown.None);
	}
}
