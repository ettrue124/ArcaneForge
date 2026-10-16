using AssetLookupTree;
using AssetLookupTree.Payloads.ReplacementEffect;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveReplacementEffectUXEvent : ReplacementEffectUXEvent
{
	public RemoveReplacementEffectUXEvent(ReplacementEffectData data, MtgEntity entity, IReplacementEffectController replacementEffectController, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(data, entity, replacementEffectController, entityViewProvider, vfxProvider, assetLookupSystem)
	{
	}

	public override void Execute()
	{
		MtgEntity entity = Entity;
		if (!(entity is MtgPlayer mtgPlayer))
		{
			if (!(entity is MtgCardInstance mtgCardInstance))
			{
				if (entity == null)
				{
					_replacementController.RemoveReplacementEffect(Data);
				}
			}
			else
			{
				DuelScene_CDC cardView = _entityViewProvider.GetCardView(mtgCardInstance.InstanceId);
				if ((object)cardView != null)
				{
					_assetLookupSystem.Blackboard.Clear();
					_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
					_assetLookupSystem.Blackboard.CardHolderType = cardView.CurrentCardHolder.CardHolderType;
					AssetLookupTree<EndVFX> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<EndVFX>();
					AssetLookupTree<EndSFX> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<EndSFX>();
					EndVFX payload = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
					EndSFX payload2 = assetLookupTree2.GetPayload(_assetLookupSystem.Blackboard);
					_assetLookupSystem.Blackboard.Clear();
					if (payload != null)
					{
						foreach (VfxData vfxData in payload.VfxDatas)
						{
							_vfxProvider.PlayVFX(vfxData, cardView.Model);
						}
					}
					if (payload2 != null)
					{
						AudioManager.PlayAudio(payload2.SfxData.AudioEvents, cardView.gameObject);
					}
				}
			}
		}
		else
		{
			_entityViewProvider.GetAvatarById(mtgPlayer.InstanceId)?.OnReplacementRemoved();
		}
		Complete();
	}
}
