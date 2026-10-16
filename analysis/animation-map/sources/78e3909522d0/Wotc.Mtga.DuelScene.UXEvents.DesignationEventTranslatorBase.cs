using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class DesignationEventTranslatorBase : IEventTranslator
{
	protected readonly IDesignationController _designationController;

	protected readonly IVfxProvider _vfxProvider;

	protected readonly AssetLookupSystem _assetLookupSystem;

	protected DesignationEventTranslatorBase(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IDesignationController>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	public DesignationEventTranslatorBase(IDesignationController designationController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_designationController = designationController ?? NullDesignationController.Default;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public abstract void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events);
}
