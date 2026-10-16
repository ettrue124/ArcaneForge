using AssetLookupTree;
using AssetLookupTree.Payloads.GameState.Designation;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateDesignationUXEvent : DesignationUXEventBase
{
	private DesignationData _oldDesignation;

	public UpdateDesignationUXEvent(DesignationData oldDesignation, DesignationData newDesignation, IDesignationController designationController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(newDesignation, designationController, vfxProvider, assetLookupSystem)
	{
		_oldDesignation = oldDesignation;
	}

	public override void Execute()
	{
		_designationController.UpdateDesignation(Designation);
		if (_oldDesignation.Type != Designation.Type)
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.Designation = Designation.Type;
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<AddedVfx> loadedTree))
			{
				AddedVfx payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					_vfxProvider.PlayVFX(payload.VfxData, null);
				}
			}
		}
		Complete();
	}
}
