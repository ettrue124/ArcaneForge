using System.Collections.Generic;
using AssetLookupTree;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class MultistepEffectUXEventBase : UXEvent
{
	protected readonly ICardDataAdapter _affectorData;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IVfxProvider _vfxProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly AssetLookupSystem _assetLookupSystem;

	protected readonly List<VfxData> _vfxs = new List<VfxData>(10);

	protected SfxData _sfxData;

	public MtgCardInstance Affector { get; private set; }

	public MtgPlayer Affected { get; private set; }

	public AbilitySubCategory AbilityCategory { get; private set; }

	protected MultistepEffectUXEventBase(MtgPlayer affected, MtgCardInstance affector, AbilitySubCategory abilityCategory, ICardDatabaseAdapter cardDatabase, ICardHolderProvider cardHolderProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		Affector = affector;
		Affected = affected;
		AbilityCategory = abilityCategory;
		_cardDatabase = cardDatabase;
		_vfxProvider = vfxProvider;
		_affectorData = Affector.ToCardData(cardDatabase);
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (_vfxs != null && _vfxs.Count > 0)
		{
			foreach (VfxData vfx in _vfxs)
			{
				_vfxProvider.PlayVFX(vfx, _affectorData);
			}
		}
		if (_sfxData != null)
		{
			AudioManager.PlayAudio(_sfxData.AudioEvents, _stack.Get().gameObject);
		}
		Complete();
	}

	protected AssetLookupSystem GetALTWithParams(ICardDataAdapter affector)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(affector);
		_assetLookupSystem.Blackboard.Ability = GetAffectorAbility(affector);
		_assetLookupSystem.Blackboard.Player = Affected;
		return _assetLookupSystem;
	}

	private AbilityPrintingData GetAffectorAbility(ICardDataAdapter affector)
	{
		AbilityPrintingData abilityPrintingData = null;
		foreach (AbilityPrintingData ability in affector.Abilities)
		{
			if (ability.SubCategory == AbilityCategory)
			{
				abilityPrintingData = ability;
			}
		}
		if (abilityPrintingData == null)
		{
			AbilitySubCategory abilityCategory = AbilityCategory;
			abilityPrintingData = new AbilityPrintingData(new AbilityPrintingRecord(0u, 0u, null, 0u, 0u, null, Wotc.Mtgo.Gre.External.Messaging.AbilityCategory.None, abilityCategory), _cardDatabase.AbilityDataProvider);
		}
		return abilityPrintingData;
	}

	protected override void Cleanup()
	{
		_stack.ClearCache();
		base.Cleanup();
	}
}
