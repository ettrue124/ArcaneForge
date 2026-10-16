using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.CardData;

public class CardData : ICardDataAdapter
{
	private readonly MtgCardInstance _instance;

	private readonly CardPrintingData _printing;

	private IReadOnlyList<CardColor> _cachedColors;

	private IReadOnlyList<KeyValuePair<AbilityPrintingData, AbilityState>> _allAbilities;

	public IReadOnlyList<AbilityPrintingData> _intrinsicAbilities;

	private IReadOnlyList<AbilityPrintingData> _abilitiesCache;

	private IReadOnlyList<AbilityPrintingData> _addedAbilitiesCache;

	private IReadOnlyList<AbilityPrintingData> _removedAbilitiesCache;

	private IReadOnlyList<uint> _abilityIdsCache;

	private IReadOnlyList<uint> _printingAbilityIdsCache;

	private string _cachedOldSchoolManaText;

	private IReadOnlyList<CardColor> _cachedFrameColor;

	private CardFrameKey? _cachedPresentationColor;

	private readonly Dictionary<int, ICardDataAdapter> _cachedLinkedFaceData = new Dictionary<int, ICardDataAdapter>();

	private static Dictionary<uint, int> _addedAbilityCount = new Dictionary<uint, int>();

	private static HashSet<uint> _removedAbilities = new HashSet<uint>();

	public MtgCardInstance Instance => _instance;

	public CardPrintingData Printing => _printing;

	public uint InstanceId => _instance?.InstanceId ?? 0;

	public MtgZone Zone => _instance?.Zone;

	public ZoneType ZoneType => Zone?.Type ?? ZoneType.None;

	public bool HasSummoningSickness => _instance?.HasSummoningSickness ?? false;

	public bool IsTapped => _instance?.IsTapped ?? false;

	public bool IsPhasedOut => _instance?.IsPhasedOut ?? false;

	public bool? IsFaceDownOverride { get; set; }

	public bool IsDisplayedFaceDown
	{
		get
		{
			if (IsFaceDownOverride.HasValue)
			{
				return IsFaceDownOverride.Value;
			}
			if (_instance == null)
			{
				return false;
			}
			bool flag = _instance.Viewers.Contains(GREPlayerNum.LocalPlayer);
			bool flag2 = _instance.FaceDownState.IsFaceDown || _instance.CatalogId == WellKnownCatalogId.StandardCardBack || _instance.CatalogId == WellKnownCatalogId.Obscured;
			bool flag3 = _instance.FaceDownState.ReasonFaceDown == ReasonFaceDown.Morph || _instance.FaceDownState.ReasonFaceDown == ReasonFaceDown.Disguise;
			bool flag4 = _instance.FaceDownState.IsFaceDown && (_instance.Zone.Type == ZoneType.Battlefield || _instance.Zone.Type == ZoneType.Stack);
			bool flag5 = _instance.Visibility == Visibility.Public;
			bool flag6 = _instance.GrpId > 11;
			bool flag7 = _instance.CatalogId == WellKnownCatalogId.Emblem || _instance.ObjectType == GameObjectType.Emblem || _instance.ObjectType == GameObjectType.Boon;
			bool flag8 = _instance.ObjectType == GameObjectType.Ability;
			bool flag9 = _instance.ObjectType == GameObjectType.Token;
			bool flag10 = _instance.CatalogId == WellKnownCatalogId.WellKnownCatalogId_Icon;
			bool flag11 = _instance.CatalogId == WellKnownCatalogId.Obscured || _instance.CatalogId == WellKnownCatalogId.StandardCardBack;
			bool flag12 = flag5 && !flag11;
			bool num = _instance.CatalogId == WellKnownCatalogId.StandardCardBack && _instance.Abilities.Count > 0;
			bool flag13 = flag && _instance.Zone.Type == ZoneType.Hand;
			bool flag14 = flag & flag2;
			if (num)
			{
				return false;
			}
			return !(flag6 | flag9 | flag | flag12 | flag13 | flag14 | flag7 | flag8 | flag10 | flag3 | flag4);
		}
	}

	public MtgPlayer Controller => _instance?.Controller;

	public MtgPlayer Owner => _instance?.Owner;

	public IReadOnlyList<CastingTimeOption> CastingTimeOptions
	{
		get
		{
			IReadOnlyList<CastingTimeOption> readOnlyList = _instance?.CastingTimeOptions;
			return readOnlyList ?? Array.Empty<CastingTimeOption>();
		}
	}

	public IReadOnlyList<MtgEntity> Targets
	{
		get
		{
			IReadOnlyList<MtgEntity> readOnlyList = _instance?.Targets;
			return readOnlyList ?? Array.Empty<MtgEntity>();
		}
	}

	public IReadOnlyList<MtgEntity> TargetedBy
	{
		get
		{
			IReadOnlyList<MtgEntity> readOnlyList = _instance?.TargetedBy;
			return readOnlyList ?? Array.Empty<MtgEntity>();
		}
	}

	public IReadOnlyList<KeyValuePair<MtgEntity, int>> Distributions
	{
		get
		{
			IReadOnlyList<KeyValuePair<MtgEntity, int>> readOnlyList = _instance?.Distributions;
			return readOnlyList ?? Array.Empty<KeyValuePair<MtgEntity, int>>();
		}
	}

	public MtgCardInstance Parent => _instance?.Parent;

	public IReadOnlyList<MtgCardInstance> Children => _instance?.Children;

	public IReadOnlyDictionary<CounterType, int> Counters
	{
		get
		{
			IReadOnlyDictionary<CounterType, int> readOnlyDictionary = _instance?.PendingEffectOverrides?.Counters;
			IReadOnlyDictionary<CounterType, int> readOnlyDictionary2 = readOnlyDictionary;
			if (readOnlyDictionary2 == null)
			{
				readOnlyDictionary = _instance?.Counters;
				readOnlyDictionary2 = readOnlyDictionary ?? DictionaryExtensions.Empty<CounterType, int>();
			}
			return readOnlyDictionary2;
		}
	}

	public IReadOnlyList<CounterData> CounterDatas
	{
		get
		{
			IReadOnlyList<CounterData> readOnlyList = _instance?.CounterDatas;
			return readOnlyList ?? Array.Empty<CounterData>();
		}
	}

	public IReadOnlyList<AbilityWordData> ActiveAbilityWords
	{
		get
		{
			IReadOnlyList<AbilityWordData> readOnlyList = _instance?.ActiveAbilityWords;
			return readOnlyList ?? Array.Empty<AbilityWordData>();
		}
	}

	public GameObjectType ObjectType => _instance?.ObjectType ?? GameObjectType.Card;

	public IReadOnlyList<QualificationData> AffectorOfQualifications
	{
		get
		{
			IReadOnlyList<QualificationData> readOnlyList = _instance?.AffectorOfQualifications;
			return readOnlyList ?? Array.Empty<QualificationData>();
		}
	}

	public IReadOnlyList<QualificationData> AffectedByQualifications
	{
		get
		{
			IReadOnlyList<QualificationData> readOnlyList = _instance?.AffectedByQualifications;
			return readOnlyList ?? Array.Empty<QualificationData>();
		}
	}

	public IReadOnlyList<ReplacementEffectData> ReplacementEffects
	{
		get
		{
			IReadOnlyList<ReplacementEffectData> readOnlyList = _instance?.ReplacementEffects;
			return readOnlyList ?? Array.Empty<ReplacementEffectData>();
		}
	}

	public IReadOnlyCollection<uint> LinkedInfoTitleLocIds
	{
		get
		{
			IReadOnlyCollection<uint> readOnlyCollection = _instance?.LinkedInfoTitleLocIds;
			return readOnlyCollection ?? Array.Empty<uint>();
		}
	}

	public IReadOnlyList<LinkedInfoText> LinkedInfoText
	{
		get
		{
			IReadOnlyList<LinkedInfoText> readOnlyList = _instance?.LinkedInfoText;
			return readOnlyList ?? Array.Empty<LinkedInfoText>();
		}
	}

	public IReadOnlyList<SuperType> Supertypes
	{
		get
		{
			IReadOnlyList<SuperType> readOnlyList = _instance?.Supertypes;
			return readOnlyList ?? _printing?.Supertypes ?? Array.Empty<SuperType>();
		}
	}

	public IReadOnlyList<CardType> CardTypes
	{
		get
		{
			IReadOnlyList<CardType> readOnlyList = _instance?.CardTypes;
			return readOnlyList ?? _printing?.Types ?? Array.Empty<CardType>();
		}
	}

	public IReadOnlyList<SubType> Subtypes
	{
		get
		{
			IReadOnlyList<SubType> readOnlyList = _instance?.Subtypes;
			return readOnlyList ?? _printing?.Subtypes ?? Array.Empty<SubType>();
		}
	}

	public IReadOnlyList<SubType> RemovedSubtypes
	{
		get
		{
			if (_instance?.RemovedSubtypes != null)
			{
				return _instance.RemovedSubtypes;
			}
			return Array.Empty<SubType>();
		}
	}

	public IReadOnlyList<CardColor> Colors
	{
		get
		{
			if (_cachedColors == null)
			{
				if (_instance == null)
				{
					_cachedColors = _printing.Colors;
				}
				else
				{
					List<CardColor> list = new List<CardColor>(_instance.Colors);
					list.Sort((CardColor lhs, CardColor rhs) =>
					{
						int num = _printing.Colors.IndexOf(lhs);
						int num2 = _printing.Colors.IndexOf(rhs);
						if (num < 0)
						{
							num = int.MaxValue;
						}
						if (num2 < 0)
						{
							num2 = int.MaxValue;
						}
						return num.CompareTo(num2);
					});
					_cachedColors = list;
				}
			}
			return _cachedColors;
		}
	}

	public StringBackedInt Power => _instance?.PendingEffectOverrides?.Power ?? _instance?.Power ?? _printing?.Power ?? StringBackedInt.UNDEFINED;

	public StringBackedInt Toughness => _instance?.PendingEffectOverrides?.Toughness ?? _instance?.Toughness ?? _printing?.Toughness ?? StringBackedInt.UNDEFINED;

	public StringBackedInt SuppressedPower => _instance?.SuppressedPower ?? StringBackedInt.UNDEFINED;

	public StringBackedInt SuppressedToughness => _instance?.SuppressedToughness ?? StringBackedInt.UNDEFINED;

	public bool PowerToughnessInverted => _instance?.PowerToughnessInverted ?? false;

	public uint Damage => _instance?.Damage ?? 0;

	public bool Damaged => _instance?.Damaged ?? false;

	public uint Loyalty => _instance?.Loyalty ?? ((uint?)_printing?.Toughness.Value).GetValueOrDefault();

	public uint Defense => _instance?.Defense ?? ((uint?)_printing?.Toughness.Value).GetValueOrDefault();

	public string SkinCode => _instance?.SkinCode;

	public string BaseSkinCode => _instance?.BaseSkinCode;

	public string SleeveCode => _instance?.SleeveCode;

	public IReadOnlyList<ActionInfo> Actions
	{
		get
		{
			IReadOnlyList<ActionInfo> readOnlyList = _instance?.Actions;
			return readOnlyList ?? Array.Empty<ActionInfo>();
		}
	}

	public bool RevealedToOpponent
	{
		get
		{
			if (_instance == null)
			{
				return false;
			}
			if (InstanceId == 0)
			{
				return false;
			}
			if (_instance.RevealedToOpponent)
			{
				return true;
			}
			if (Zone?.Owner != null && Zone.Type == ZoneType.Library && Zone.Owner.IsLocalPlayer)
			{
				if (Visibility == Visibility.Public)
				{
					return true;
				}
				if (_instance.Viewers.Exists((GREPlayerNum x) => x != GREPlayerNum.LocalPlayer))
				{
					return true;
				}
			}
			return false;
		}
	}

	public IReadOnlyList<KeyValuePair<AbilityPrintingData, AbilityState>> AllAbilities => _allAbilities ?? (_allAbilities = GenerateAllAbilitiesList(_instance, _printing));

	public IReadOnlyList<AbilityPrintingData> IntrinsicAbilities => _intrinsicAbilities ?? (_intrinsicAbilities = GetIntrinsicAbilities(_instance, _printing));

	public IReadOnlyList<AbilityPrintingData> PrintingAbilities => _printing?.Abilities ?? Array.Empty<AbilityPrintingData>();

	public IReadOnlyList<AbilityPrintingData> Abilities => _abilitiesCache ?? (_abilitiesCache = (from x in AllAbilities
		where x.Value != AbilityState.Removed
		select x.Key).ToArray());

	public IReadOnlyList<AbilityPrintingData> AddedAbilities => _addedAbilitiesCache ?? (_addedAbilitiesCache = (from x in AllAbilities
		where x.Value == AbilityState.Added
		select x.Key).ToArray());

	public IReadOnlyList<AbilityPrintingData> RemovedAbilities => _removedAbilitiesCache ?? (_removedAbilitiesCache = (from x in AllAbilities
		where x.Value == AbilityState.Removed
		select x.Key).ToArray());

	public IReadOnlyList<uint> AbilityIds => _abilityIdsCache ?? (_abilityIdsCache = Abilities.Select((AbilityPrintingData x) => x.Id).ToArray());

	public IReadOnlyList<uint> PrintingAbilityIds => _printingAbilityIdsCache ?? (_printingAbilityIdsCache = Printing?.AbilityIds?.Select(((uint Id, uint TextId) x) => x.Id).ToArray() ?? Array.Empty<uint>());

	public uint GrpId => _instance?.GrpId ?? _printing?.GrpId ?? 0;

	public uint ObjectSourceGrpId => _instance?.ObjectSourceGrpId ?? 0;

	public string ImageAssetPath => _printing?.ImageAssetPath ?? string.Empty;

	public uint FlavorTextId => _printing?.FlavorTextId ?? 0;

	public uint ReminderTextId => _printing?.ReminderTextId ?? 0;

	public string CollectorNumber => _printing?.CollectorNumber ?? string.Empty;

	public CardRarity Rarity => _printing?.Rarity ?? CardRarity.None;

	public string ExpansionCode => _printing?.ExpansionCode ?? string.Empty;

	public string DigitalReleaseSet => _printing?.DigitalReleaseSet ?? string.Empty;

	public IReadOnlyList<LinkInfoData> AffectorOfLinkInfos
	{
		get
		{
			IReadOnlyList<LinkInfoData> readOnlyList = _instance?.AffectorOfLinkInfos;
			return readOnlyList ?? Array.Empty<LinkInfoData>();
		}
	}

	public IReadOnlyList<LinkInfoData> AffectedByLinkInfos
	{
		get
		{
			IReadOnlyList<LinkInfoData> readOnlyList = _instance?.AffectedByLinkInfos;
			return readOnlyList ?? Array.Empty<LinkInfoData>();
		}
	}

	public uint TitleId
	{
		get
		{
			if (_instance == null || _instance.TitleId == 0)
			{
				return _printing?.TitleId ?? 0;
			}
			return _instance.TitleId;
		}
	}

	public IRulesTextOverride RulesTextOverride { get; set; }

	public IReadOnlyList<CardColor> PrintedColors => _printing.Colors;

	public IReadOnlyCollection<ManaQuantity> ManaCostOverride => _instance?.ManaCostOverride ?? Array.Empty<ManaQuantity>();

	public IReadOnlyList<ManaQuantity> PrintedCastingCost => _printing.CastingCost;

	public uint LoyaltyActivationsRemaining => (_instance?.LoyaltyActivationsRemaining).GetValueOrDefault();

	public IReadOnlyList<CardType> PrintedTypes => _printing.Types;

	public IReadOnlyList<SubType> PrintedSubtypes => _printing.Subtypes;

	public IReadOnlyList<SuperType> PrintedSupertypes => _printing.Supertypes;

	public bool IsBasicLand => CardUtilities.IsBasicLand(Instance, Printing);

	public bool IsLandButNotBasic
	{
		get
		{
			if (CardTypes.Contains(CardType.Land))
			{
				return !IsBasicLand;
			}
			return false;
		}
	}

	public uint ConvertedManaCost => _printing.ConvertedManaCost;

	public IReadOnlyDictionary<uint, uint> ConvertedManaCostByLinkedFace => _printing.ConvertedManaCostByLinkedFace;

	public LinkedFace LinkedFaceType => _printing.LinkedFaceType;

	public IReadOnlyList<MtgCardInstance> LinkedFaceInstances
	{
		get
		{
			IReadOnlyList<MtgCardInstance> readOnlyList = _instance?.LinkedFaceInstances;
			return readOnlyList ?? Array.Empty<MtgCardInstance>();
		}
	}

	public IReadOnlyList<uint> LinkedFaceGrpIds => _printing.LinkedFaceGrpIds;

	public string OldSchoolManaText => _cachedOldSchoolManaText ?? (_cachedOldSchoolManaText = ManaUtilities.ConvertToOldSchoolManaText(PrintedCastingCost));

	public IReadOnlyList<CardColor> GetFrameColors => _cachedFrameColor ?? (_cachedFrameColor = CardUtilities.GetFrameColors(_instance, _printing));

	public CardFrameKey PresentationColor
	{
		get
		{
			CardFrameKey valueOrDefault = _cachedPresentationColor.GetValueOrDefault();
			if (!_cachedPresentationColor.HasValue)
			{
				valueOrDefault = CardUtilities.GetPresentationColor(GetFrameColors);
				_cachedPresentationColor = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
	}

	public GREPlayerNum ControllerNum
	{
		get
		{
			if (_instance?.Controller != null)
			{
				if (!_instance.Controller.IsLocalPlayer)
				{
					return GREPlayerNum.Opponent;
				}
				return GREPlayerNum.LocalPlayer;
			}
			return GREPlayerNum.Invalid;
		}
	}

	public GREPlayerNum OwnerNum
	{
		get
		{
			if (_instance?.Owner != null)
			{
				if (!_instance.Owner.IsLocalPlayer)
				{
					return GREPlayerNum.Opponent;
				}
				return GREPlayerNum.LocalPlayer;
			}
			return GREPlayerNum.Invalid;
		}
	}

	public Visibility Visibility => _instance?.Visibility ?? Visibility.None;

	public CardColorFlags PrintedColorIdentityFlags => _printing?.ColorIdentityFlags ?? CardColorFlags.None;

	public CardColorFlags PrintedColorFlags => _printing?.ColorFlags ?? CardColorFlags.None;

	public IReadOnlyList<CardPrintingData> LinkedFacePrintings => _printing?.LinkedFacePrintings ?? Array.Empty<CardPrintingData>();

	public IReadOnlyCollection<MetaDataTag> Tags => _printing?.Tags ?? Array.Empty<MetaDataTag>();

	public bool IsRebalanced => _printing?.IsRebalanced ?? false;

	public uint RebalancedCardLink => _printing?.RebalancedCardLink ?? 0;

	public bool IsWildcard
	{
		get
		{
			if (CardUtilities.IsWildcard(GrpId))
			{
				return ObjectType != GameObjectType.Boon;
			}
			return false;
		}
	}

	public bool IsFakeStyleCard { get; set; }

	public CardData(MtgCardInstance instance, CardPrintingData printing)
	{
		_instance = instance;
		_printing = printing;
		if (_printing == null)
		{
			uint grpId = _instance?.GrpId ?? 0;
			uint titleId = _instance?.TitleId ?? 0;
			MtgCardInstance instance2 = _instance;
			_printing = new CardPrintingData(new CardPrintingRecord(grpId, 0u, null, titleId, 0u, 0u, 0u, 0u, 0u, 0u, null, CardArtSize.Normal, CardRarity.None, null, null, isToken: false, isPrimaryCard: true, isDigitalOnly: false, isRebalanced: false, 0u, 0u, 0u, null, null, null, null, usesSideboard: false, null, LinkedFace.None, null, null, default, default, default, null, null, (instance2 != null && instance2.Colors.Count > 0) ? _instance.Colors.ToArray() : Array.Empty<CardColor>()), NullCardDataProvider.Default, NullAbilityDataProvider.Default);
		}
		if (_instance == null)
		{
			return;
		}
		if (_instance.FaceDownState.IsFaceDown && _printing != null && _printing.GrpId != 0 && _instance.GrpId != 0 && _instance.GrpId != 3 && _instance.GrpId != 4)
		{
			_instance.CopyFromPrinting(_printing);
		}
		else if (_instance.ObjectType == GameObjectType.Token && _instance.GrpId == 3)
		{
			CardPrintingData printing2 = _printing;
			CardPrintingRecord record = _printing.Record;
			uint? titleId2 = 0u;
			CardArtSize? artSize = CardArtSize.Normal;
			_printing = new CardPrintingData(printing2, new CardPrintingRecord(record, null, null, null, titleId2, null, null, null, null, null, null, null, artSize));
		}
		if (_instance.ClassLevel <= 0)
		{
			MtgZone zone = _instance.Zone;
			if (zone != null && zone.Type == ZoneType.Battlefield && _instance.Abilities != null && _instance.Abilities.Exists((AbilityPrintingData x) => x.SubCategory == AbilitySubCategory.ClassLevel))
			{
				_instance.ClassLevel = 1;
			}
		}
		if (ShouldRemoveSkinCode())
		{
			_instance.BaseSkinCode = string.Empty;
			_instance.SkinCode = string.Empty;
		}
	}

	private bool ShouldRemoveSkinCode()
	{
		if (_instance == null)
		{
			return false;
		}
		if (_printing == null)
		{
			return false;
		}
		if (string.IsNullOrWhiteSpace(_instance.SkinCode))
		{
			return false;
		}
		if (AltArtSettings.ShouldHideAltArtSkin(_instance.SkinCode))
		{
			return true;
		}
		if (_instance.FaceDownState.IsFaceDown)
		{
			return false;
		}
		if (!_printing.KnownSupportedStyles.Contains(_instance.SkinCode))
		{
			return true;
		}
		if (_instance.ObjectType == GameObjectType.Ability && _instance.Parent != null)
		{
			_ = _instance.Parent.SkinCode == _instance.SkinCode;
			return false;
		}
		return false;
	}

	public ICardDataAdapter GetLinkedFaceAtIndex(int index, bool ignoreInstance, ICardDataProvider cardDataProvider)
	{
		if (_cachedLinkedFaceData.TryGetValue(index, out var value) && value != null)
		{
			return value;
		}
		MtgCardInstance mtgCardInstance = ((LinkedFaceInstances.Count > index) ? LinkedFaceInstances[index] : null);
		CardPrintingData cardPrintingData = ((LinkedFacePrintings.Count > index) ? LinkedFacePrintings[index] : null);
		uint instanceId = mtgCardInstance?.InstanceId ?? 0;
		if (mtgCardInstance != null && cardPrintingData != null && mtgCardInstance.GrpId != cardPrintingData.GrpId)
		{
			mtgCardInstance = null;
		}
		if (((mtgCardInstance == null) | ignoreInstance) && cardPrintingData != null)
		{
			GameObjectType objectType = CardUtilities.LinkedFaceToObject(LinkedFaceType, index, ObjectType == GameObjectType.Token || cardPrintingData.IsToken);
			mtgCardInstance = cardPrintingData.CreateInstance(objectType);
			mtgCardInstance.Zone = Zone;
			mtgCardInstance.Controller = Controller;
			mtgCardInstance.Owner = Owner;
			mtgCardInstance.Parent = Instance;
			mtgCardInstance.ParentId = InstanceId;
			mtgCardInstance.InstanceId = instanceId;
		}
		if (mtgCardInstance != null)
		{
			ApplySkincodeToInstance(mtgCardInstance, SkinCode, BaseSkinCode);
			foreach (MtgCardInstance linkedFaceInstance in mtgCardInstance.LinkedFaceInstances)
			{
				ApplySkincodeToInstance(linkedFaceInstance, SkinCode, BaseSkinCode);
			}
			mtgCardInstance.SleeveCode = SleeveCode;
			mtgCardInstance.IsCopy = Instance?.IsCopy ?? false;
			mtgCardInstance.IsObjectCopy = Instance?.IsObjectCopy ?? false;
			if (cardPrintingData == null)
			{
				cardPrintingData = cardDataProvider.GetCardPrintingById(mtgCardInstance.GrpId, SkinCode);
			}
		}
		CardData cardData = new CardData(mtgCardInstance, cardPrintingData);
		cardData.IsFakeStyleCard = IsFakeStyleCard;
		return _cachedLinkedFaceData[index] = cardData;
	}

	private void ApplySkincodeToInstance(MtgCardInstance instance, string skinCode, string baseSkinCode)
	{
		if (instance != null)
		{
			instance.SkinCode = skinCode;
			instance.BaseSkinCode = baseSkinCode;
		}
	}

	public override string ToString()
	{
		return $"[InstanceId:{InstanceId}, GrpId:{GrpId}]";
	}

	public static IReadOnlyList<KeyValuePair<AbilityPrintingData, AbilityState>> GenerateAllAbilitiesList(MtgCardInstance instance, CardPrintingData printing)
	{
		List<KeyValuePair<AbilityPrintingData, AbilityState>> list = new List<KeyValuePair<AbilityPrintingData, AbilityState>>();
		if (instance == null)
		{
			foreach (AbilityPrintingData ability in printing.Abilities)
			{
				if (!ability.IsIntrinsicAbility)
				{
					list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(ability, AbilityState.Normal));
				}
			}
			return list;
		}
		if (printing == null)
		{
			foreach (AbilityPrintingData ability2 in instance.Abilities)
			{
				if (!ability2.IsIntrinsicAbility)
				{
					list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(ability2, AbilityState.Normal));
				}
			}
			return list;
		}
		_addedAbilityCount.Clear();
		_removedAbilities.Clear();
		foreach (AbilityModification abilityModification in instance.AbilityModifications)
		{
			uint abilityId = abilityModification.AbilityId;
			switch (abilityModification.Type)
			{
			case AbilityModification.ModType.Added:
				_removedAbilities.Remove(abilityId);
				if (_addedAbilityCount.ContainsKey(abilityId))
				{
					_addedAbilityCount[abilityId]++;
				}
				else
				{
					_addedAbilityCount[abilityId] = 1;
				}
				break;
			case AbilityModification.ModType.Removed:
				_addedAbilityCount.Remove(abilityId);
				_removedAbilities.Add(abilityId);
				break;
			case AbilityModification.ModType.RemoveAll:
				foreach (AbilityPrintingData ability3 in printing.Abilities)
				{
					_removedAbilities.Add(ability3.Id);
				}
				foreach (KeyValuePair<uint, int> item in _addedAbilityCount)
				{
					_removedAbilities.Add(item.Key);
				}
				_addedAbilityCount.Clear();
				break;
			}
		}
		List<AbilityPrintingData> abilities = instance.Abilities;
		for (int num = abilities.Count - 1; num >= 0; num--)
		{
			MtgAbilityInstance mtgAbilityInstance = ((instance.AbilityInstances.Count > num) ? instance.AbilityInstances[num] : null);
			AbilityPrintingData abilityPrintingData = abilities[num];
			uint id = abilityPrintingData.Id;
			AbilityState abilityState = ((!_removedAbilities.Contains(id)) ? AbilityState.Normal : AbilityState.Added);
			if (_addedAbilityCount.ContainsKey(abilityPrintingData.Id))
			{
				_addedAbilityCount[id]--;
				if (_addedAbilityCount[id] == 0)
				{
					_addedAbilityCount.Remove(id);
				}
				abilityState = AbilityState.Added;
			}
			if (mtgAbilityInstance != null && mtgAbilityInstance.IsExhausted)
			{
				abilityState |= AbilityState.Exhausted;
			}
			if (!abilityPrintingData.IsIntrinsicAbility)
			{
				AbilityPrintingData parent;
				if (abilityPrintingData.IsBasicManaAbility() && printing.IsLand)
				{
					list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(abilityPrintingData, AbilityState.Normal));
				}
				else if (abilityPrintingData.TryGetModalAbilityParent(printing.Abilities, out parent) && !parent.Tags.Contains(MetaDataTag.SuppressModalParent))
				{
					list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(abilityPrintingData, AbilityState.Added));
					if (!instance.Abilities.ContainsId(parent.Id) && !itemExistsInAllAbilities(list, parent))
					{
						list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(parent, AbilityState.Normal));
					}
				}
				else
				{
					list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(abilityPrintingData, abilityState));
				}
			}
		}
		list.Reverse();
		foreach (uint removedAbility in _removedAbilities)
		{
			AbilityPrintingData key = TryFindRemovedAbilityPrinting(removedAbility, instance, printing) ?? new AbilityPrintingData(new AbilityPrintingRecord(removedAbility), NullAbilityDataProvider.Default);
			list.Add(new KeyValuePair<AbilityPrintingData, AbilityState>(key, AbilityState.Removed));
		}
		return list;
		static AbilityPrintingData TryFindRemovedAbilityPrinting(uint num2, MtgCardInstance mtgCardInstance, CardPrintingData cardPrintingData)
		{
			if (num2 == 0)
			{
				return null;
			}
			if (mtgCardInstance != null)
			{
				foreach (AbilityPrintingData ability4 in mtgCardInstance.Abilities)
				{
					if (ability4.Id == num2)
					{
						return ability4;
					}
				}
			}
			if (cardPrintingData != null)
			{
				foreach (AbilityPrintingData ability5 in cardPrintingData.Abilities)
				{
					if (ability5.Id == num2)
					{
						return ability5;
					}
				}
			}
			return null;
		}
		static bool itemExistsInAllAbilities(List<KeyValuePair<AbilityPrintingData, AbilityState>> abilitiesList, AbilityPrintingData abilityPrinting)
		{
			foreach (KeyValuePair<AbilityPrintingData, AbilityState> abilities2 in abilitiesList)
			{
				if (abilities2.Key.Id == abilityPrinting.Id)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static IReadOnlyList<AbilityPrintingData> GetIntrinsicAbilities(MtgCardInstance instance, CardPrintingData printing)
	{
		if (instance != null)
		{
			return findAllIntrinsicAbilities(instance.Abilities);
		}
		if (printing != null)
		{
			return findAllIntrinsicAbilities(printing.Abilities);
		}
		return Array.Empty<AbilityPrintingData>();
		static IReadOnlyList<AbilityPrintingData> findAllIntrinsicAbilities(IReadOnlyList<AbilityPrintingData> allAbilities)
		{
			List<AbilityPrintingData> list = null;
			foreach (AbilityPrintingData allAbility in allAbilities)
			{
				if (allAbility.IsIntrinsicAbility)
				{
					if (list == null)
					{
						list = new List<AbilityPrintingData>();
					}
					list.Add(allAbility);
				}
			}
			if (list == null)
			{
				return Array.Empty<AbilityPrintingData>();
			}
			return list;
		}
	}
}
