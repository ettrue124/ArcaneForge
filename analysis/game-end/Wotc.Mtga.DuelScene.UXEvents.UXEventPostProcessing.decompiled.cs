using AssetLookupTree;
using Core.Code.Familiar;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.UXEvents;

public static class UXEventPostProcessing
{
	public static IUXEventGrouper Generate(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager)
	{
		IUXEventGrouper iUXEventGrouper = GenerateDefault(context, assetLookupSystem, gameManager);
		if (!gameManager.MatchManager.IsPracticeGame)
		{
			return iUXEventGrouper;
		}
		return GenerateSparky(context, assetLookupSystem, iUXEventGrouper);
	}

	private static IUXEventGrouper GenerateDefault(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager)
	{
		return new UXEventPostProcessAggregate(new ZeroIndexPostProcessAggregate(new ManaProducedGrouper(), new ManaProducedLimiter(), new CullSuperfluousResolutionStart(), new ZoneTransferHangTimePostProcess(), new ZoneTransferGrouper(assetLookupSystem, context), new LifeChangeCombinePostProcess(context.Get<IGameStateProvider>()), new LifeChangeStaggerPostProcess(), new ManaProducedStaggerPostProcess(), new RemoveDuplicateAddedAbilityPostProcess(), new IndexedPostProcessAggregate(new BatchManaSacWithTapPostProcess(), new BatchManaSacPostProcess(), new AttachmentAddedUXEventGrouper(), new AnonimityUXEventGrouper()), new CullEventsAfterConcedePostProcess(context.Get<UXEventQueue>()), new EnsureTargetVisualizationPostProcess(), new DesignationUXEventGrouper()));
	}

	private static IUXEventGrouper GenerateSparky(IContext context, AssetLookupSystem assetLookupSystem, IUXEventGrouper defaultPostProcess)
	{
		BotControlConfigurationSO botControlConfigurationSO = BotControlManager.FetchBotConfig(assetLookupSystem);
		IEntityDialogControllerProvider provider = context.Get<IEntityDialogControllerProvider>();
		IClientLocProvider clientLocProvider = context.Get<IClientLocProvider>();
		return new UXEventPostProcessAggregate(defaultPostProcess, new ZeroIndexPostProcessAggregate(new SparkyResolutionStartReactionPostProcess(provider, clientLocProvider, context.Get<ICardDataProvider>(), botControlConfigurationSO.CMCToVOPercentage, botControlConfigurationSO.CMCCastedChatterOptions), new SparkyDamagedPostProcess(clientLocProvider, provider, botControlConfigurationSO.damageToVOPercentage, botControlConfigurationSO.damageTakenChatterOptions), new SparkyEndOfGamePostProcess(clientLocProvider, provider, botControlConfigurationSO.playerLoseChatterOptions, botControlConfigurationSO.sparkyLoseChatterOptions)));
	}
}
