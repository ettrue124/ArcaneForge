using GreClient.CardData;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene;

public class ResolutionEffectModel
{
	public readonly uint InstanceId;

	public readonly uint GrpId;

	public readonly MtgCardInstance CardInstance;

	public readonly ICardDataAdapter Model;

	public CardPrintingData CardPrinting { get; }

	public AbilityPrintingData AbilityPrinting { get; }

	public bool IgnoreDamageEffects { get; }

	public bool IgnoreDestroyEffects { get; }

	public bool IgnoreCoinFlipEffects { get; }

	public bool RedirectDamageEventsFromParent { get; }

	public bool SuppressProjectileDamageEffects { get; }

	public ResolutionEffectModel(uint instanceId, uint grpId, MtgCardInstance cardInstance, ICardDataAdapter cardData, CardPrintingData cardPrinting, AbilityPrintingData abilityPrinting, bool ignoreDamageEffects = false, bool ignoreDestroyEffects = false, bool ignoreCoinFlipEffects = false, bool redirectDamageEventsFromParent = false, bool suppressProjectileDamageEffects = false)
		: this(instanceId, grpId, cardData, abilityPrinting, ignoreDamageEffects, ignoreDestroyEffects, ignoreCoinFlipEffects, redirectDamageEventsFromParent, suppressProjectileDamageEffects)
	{
		CardInstance = cardInstance;
		CardPrinting = cardPrinting;
	}

	private ResolutionEffectModel(uint instanceId, uint grpId, ICardDataAdapter model, AbilityPrintingData abilityPrinting, bool ignoreDamageEffects = false, bool ignoreDestroyEffects = false, bool ignoreCoinFlipEffects = false, bool redirectDamageEventsFromParent = false, bool suppressProjectileDamageEffects = false)
		: this(instanceId, grpId, abilityPrinting, ignoreDamageEffects, ignoreDestroyEffects, ignoreCoinFlipEffects, redirectDamageEventsFromParent, suppressProjectileDamageEffects)
	{
		Model = model;
		if (CardInstance == null)
		{
			CardInstance = model?.Instance;
		}
		if (CardPrinting == null)
		{
			CardPrinting = model?.Printing;
		}
	}

	private ResolutionEffectModel(uint instanceId, uint grpId, AbilityPrintingData abilityPrinting, bool ignoreDamageEffects = false, bool ignoreDestroyEffects = false, bool ignoreCoinFlipEffects = false, bool redirectDamageEventsFromParent = false, bool suppressProjectileDamageEffects = false)
	{
		InstanceId = instanceId;
		GrpId = grpId;
		AbilityPrinting = abilityPrinting;
		IgnoreDamageEffects = ignoreDamageEffects;
		IgnoreDestroyEffects = ignoreDestroyEffects;
		IgnoreCoinFlipEffects = ignoreCoinFlipEffects;
		RedirectDamageEventsFromParent = redirectDamageEventsFromParent;
		SuppressProjectileDamageEffects = suppressProjectileDamageEffects;
	}
}
