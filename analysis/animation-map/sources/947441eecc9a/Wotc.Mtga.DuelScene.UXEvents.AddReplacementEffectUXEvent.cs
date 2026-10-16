using AssetLookupTree;
using AssetLookupTree.Payloads.ReplacementEffect;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddReplacementEffectUXEvent : ReplacementEffectUXEvent
{
	public AddReplacementEffectUXEvent(ReplacementEffectData data, MtgEntity entity, IReplacementEffectController replacementEffectController, IEntityViewProvider entityViewProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(data, entity, replacementEffectController, entityViewProvider, vfxProvider, assetLookupSystem)
	{
	}

	public override void Execute()
	{
		MtgEntity entity = Entity;
		DuelScene_AvatarView avatar;
		if (!(entity is MtgPlayer mtgPlayer))
		{
			DuelScene_CDC cardView;
			if (!(entity is MtgCardInstance mtgCardInstance))
			{
				if (entity == null)
				{
					_replacementController.TryAddReplacementEffect(Data);
				}
			}
			else if (_entityViewProvider.TryGetCardView(mtgCardInstance.InstanceId, out cardView))
			{
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
				_assetLookupSystem.Blackboard.CardHolderType = cardView.CurrentCardHolder.CardHolderType;
				AssetLookupTree<StartVFX> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<StartVFX>();
				AssetLookupTree<StartSFX> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<StartSFX>();
				StartVFX payload = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
				StartSFX payload2 = assetLookupTree2.GetPayload(_assetLookupSystem.Blackboard);
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
		else if (_entityViewProvider.TryGetAvatarById(mtgPlayer.InstanceId, out avatar))
		{
			avatar.OnReplacementAdded();
		}
		Complete();
	}
}
