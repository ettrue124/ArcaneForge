using AssetLookupTree;
using AssetLookupTree.Payloads.Player;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.VFX;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventUpdateDecider : UXEvent
{
	private readonly MtgPlayer _decidingPlayer;

	private readonly ITurnController _turnController;

	private readonly IAvatarViewProvider _avatarProvider;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public UXEventUpdateDecider(MtgPlayer decidingPlayer, ITurnController turnController, IAvatarViewProvider avatarProvider, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_decidingPlayer = decidingPlayer;
		_turnController = turnController ?? NullTurnController.Default;
		_avatarProvider = avatarProvider ?? NullAvatarViewProvider.Default;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		uint num = _decidingPlayer?.InstanceId ?? 0;
		GREPlayerNum gREPlayerNum = _decidingPlayer?.ClientPlayerEnum ?? GREPlayerNum.Invalid;
		_turnController.SetDecidingPlayer(num);
		if (_avatarProvider.TryGetAvatarById(num, out var avatar))
		{
			avatar.UpdateDecidingPlayer(hasPriority: true);
		}
		foreach (DuelScene_AvatarView allAvatar in _avatarProvider.GetAllAvatars())
		{
			if (!(allAvatar == avatar))
			{
				allAvatar.UpdateDecidingPlayer(hasPriority: false);
			}
		}
		AudioManager.Instance.OnPriorityUpdated(gREPlayerNum);
		PlayVFX(_decidingPlayer, gREPlayerNum);
		Complete();
	}

	private void PlayVFX(MtgPlayer player, GREPlayerNum playerNum)
	{
		if (!_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<DeciderChangeVFX> loadedTree))
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.GREPlayerNum = playerNum;
		_assetLookupSystem.Blackboard.Player = player;
		DeciderChangeVFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null)
		{
			return;
		}
		foreach (VfxData vfxData in payload.VfxDatas)
		{
			_vfxProvider.PlayVFX(vfxData, null);
		}
	}
}
