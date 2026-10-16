using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveDesignationUXEvent : DesignationUXEventBase
{
	public RemoveDesignationUXEvent(DesignationData designation, IDesignationController designationController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(designation, designationController, vfxProvider, assetLookupSystem)
	{
	}

	public override void Execute()
	{
		_designationController.RemoveDesignation(Designation);
		Complete();
	}
}
