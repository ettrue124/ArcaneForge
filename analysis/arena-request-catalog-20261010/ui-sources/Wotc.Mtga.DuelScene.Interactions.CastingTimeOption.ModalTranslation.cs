using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class ModalTranslation : IWorkflowTranslation<CastingTimeOption_ModalRequest>
{
	private readonly IContext _context;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ModalTranslation(IContext context, AssetLookupSystem assetLookupSystem)
	{
		_context = context;
		_abilityDataProvider = context.Get<IAbilityDataProvider>() ?? NullAbilityDataProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(CastingTimeOption_ModalRequest req)
	{
		if (ContainsRepeatSelections(req))
		{
			return new CastingTimeOption_ModalRepeatWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IEntityViewProvider>(), _context.Get<IBrowserController>(), _context.Get<IGameplaySettingsProvider>(), _context.Get<IBrowserHeaderTextProvider>());
		}
		if (IsModalSelectCostOptionWorkflow(req, _abilityDataProvider))
		{
			return new CastingTimeOption_ModalSelectCostOptionWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IObjectPool>(), _context.Get<ICardViewProvider>(), _context.Get<IBrowserController>(), _context.Get<IBrowserHeaderTextProvider>());
		}
		return new ModalWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IEntityViewManager>(), _context.Get<IAbilityDataProvider>(), _context.Get<IBrowserController>(), _context.Get<IClientLocProvider>(), _context.Get<IPromptEngine>(), _context.Get<IBrowserHeaderTextProvider>(), _context.Get<IGameplaySettingsProvider>(), _assetLookupSystem);
	}

	private static bool ContainsRepeatSelections(CastingTimeOption_ModalRequest req)
	{
		if (req != null && req.RepeatSelectionAllowed)
		{
			return req.Max > 1;
		}
		return false;
	}

	private static bool IsModalSelectCostOptionWorkflow(CastingTimeOption_ModalRequest req, IAbilityDataProvider abilityProvider)
	{
		if (req != null && req.AbilityGrpId != 0 && abilityProvider.TryGetAbilityPrintingById(req.AbilityGrpId, out var ability))
		{
			if (ability.BaseId != 330)
			{
				return ability.BaseId == 365;
			}
			return true;
		}
		return false;
	}
}
