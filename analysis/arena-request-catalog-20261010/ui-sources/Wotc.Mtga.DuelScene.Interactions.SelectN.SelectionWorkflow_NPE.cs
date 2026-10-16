using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectionWorkflow_NPE : SelectionWorkflow
{
	private readonly NPEDirector _director;

	public SelectionWorkflow_NPE(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IResolutionEffectProvider resolutionEffectProvider, IGameplaySettingsProvider gameplaySettingsProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, ISelectionConfirmation selectionConfirmation, IPlayWarningManager playWarningManager, AssetLookupSystem assetLookupSystem, NPEDirector director)
		: base(request, cardDatabase, gameStateProvider, resolutionEffectProvider, gameplaySettingsProvider, cardViewProvider, cardHolderProvider, browserManager, selectionConfirmation, playWarningManager, assetLookupSystem)
	{
		_director = director;
	}

	protected override void ApplyTheSelection(IEntityView entity)
	{
		if (!_director.WarnForBadChoice(entity.InstanceId))
		{
			base.ApplyTheSelection(entity);
		}
	}
}
