using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class ReplacementEffectUXEvent : UXEvent
{
	protected readonly IReplacementEffectController _replacementController;

	protected readonly IEntityViewProvider _entityViewProvider;

	protected readonly IVfxProvider _vfxProvider;

	protected readonly AssetLookupSystem _assetLookupSystem;

	public ReplacementEffectData Data { get; private set; }

	public MtgEntity Entity { get; private set; }

	public ReplacementEffectUXEvent(ReplacementEffectData data, MtgEntity entity, IReplacementEffectController replacementEffectController, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		Data = data;
		Entity = entity;
		_replacementController = replacementEffectController;
		_entityViewProvider = entityViewProvider;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}
}
