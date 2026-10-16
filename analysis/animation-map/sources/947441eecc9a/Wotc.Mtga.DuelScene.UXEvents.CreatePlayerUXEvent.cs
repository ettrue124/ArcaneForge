using GreClient.Rules;
using Wotc.Mtga.DuelScene.Companions;
using Wotc.Mtga.DuelScene.Emotes;
using Wotc.Mtga.DuelScene.Input;
using Wotc.Mtga.DuelScene.ZoneCounts;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CreatePlayerUXEvent : UXEvent
{
	private readonly MtgPlayer _player;

	private readonly IAvatarViewController _avatarController;

	private readonly IAvatarInputController _avatarInputController;

	private readonly IEmoteManager _emoteManager;

	private readonly ICompanionViewController _companionController;

	private readonly IZoneCountController _zoneCountController;

	private readonly IPlayerTimerController _playerTimerController;

	private readonly BattleFieldStaticElementsLayout _staticLayoutElements;

	public CreatePlayerUXEvent(MtgPlayer player, IAvatarViewController avatarController, IAvatarInputController avatarInputController, IEmoteManager emoteManager, ICompanionViewController companionController, IZoneCountController zoneCountController, IPlayerTimerController playerTimerController, BattleFieldStaticElementsLayout staticElementsLayout)
	{
		_player = player;
		_avatarController = avatarController ?? NullAvatarViewController.Default;
		_avatarInputController = avatarInputController ?? NullAvatarInputController.Default;
		_emoteManager = emoteManager ?? NullEmoteManager.Default;
		_companionController = companionController ?? NullCompanionViewController.Default;
		_zoneCountController = zoneCountController ?? NullZoneCountController.Default;
		_playerTimerController = playerTimerController ?? NullPlayerTimerController.Default;
		_staticLayoutElements = staticElementsLayout;
	}

	public override void Execute()
	{
		DuelScene_AvatarView avatar = _avatarController.CreateAvatarView(_player);
		_avatarInputController.ApplyInput(avatar);
		_emoteManager.CreateEmotesForPlayer(_player);
		_companionController.CreateCompanionForPlayer(_player);
		_zoneCountController.CreateZoneCount(_player.InstanceId);
		IPlayerTimer playerTimer = _playerTimerController.CreatePlayerTimer(_player);
		_staticLayoutElements.RegisterAvatar(avatar, _player.ClientPlayerEnum);
		_staticLayoutElements.RegisterTimeoutDisplay(playerTimer.TimeoutDisplay, _player.ClientPlayerEnum);
		_staticLayoutElements.RegisterMatchTimer(playerTimer.MatchTimer, _player.ClientPlayerEnum);
		Complete();
	}
}
