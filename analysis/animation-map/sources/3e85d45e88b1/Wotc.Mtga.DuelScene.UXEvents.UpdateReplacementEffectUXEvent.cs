using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateReplacementEffectUXEvent : ReplacementEffectUXEvent
{
	public UpdateReplacementEffectUXEvent(ReplacementEffectData data, MtgEntity entity, IReplacementEffectController replacementEffectController, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(data, entity, replacementEffectController, entityViewProvider, vfxProvider, assetLookupSystem)
	{
	}

	public override void Execute()
	{
		_replacementController.UpdateReplacementEffect(Data);
		Complete();
	}
}
