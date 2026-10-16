using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ReplacementEffectChangedTranslator : IEventTranslator
{
	private readonly IReplacementEffectController _replacementController;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public ReplacementEffectChangedTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IReplacementEffectController>(), context.Get<IEntityViewProvider>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	public ReplacementEffectChangedTranslator(IReplacementEffectController replacementController, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_replacementController = replacementController;
		_entityViewProvider = entityViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ReplacementEffectChangedEvent replacementEffectChangedEvent)
		{
			if (replacementEffectChangedEvent.Added && !replacementEffectChangedEvent.Removed)
			{
				events.Add(new AddReplacementEffectUXEvent(replacementEffectChangedEvent.EffectData, replacementEffectChangedEvent.Entity, _replacementController, _entityViewProvider, _vfxProvider, _assetLookupSystem));
			}
			else if (!replacementEffectChangedEvent.Added && !replacementEffectChangedEvent.Removed)
			{
				events.Add(new UpdateReplacementEffectUXEvent(replacementEffectChangedEvent.EffectData, replacementEffectChangedEvent.Entity, _replacementController, _entityViewProvider, _vfxProvider, _assetLookupSystem));
			}
			else if (!replacementEffectChangedEvent.Added && replacementEffectChangedEvent.Removed)
			{
				events.Add(new RemoveReplacementEffectUXEvent(replacementEffectChangedEvent.EffectData, replacementEffectChangedEvent.Entity, _replacementController, _entityViewProvider, _vfxProvider, _assetLookupSystem));
			}
		}
	}
}
