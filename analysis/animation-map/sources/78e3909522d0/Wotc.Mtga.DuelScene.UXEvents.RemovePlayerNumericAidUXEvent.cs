using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemovePlayerNumericAidUXEvent : UXEvent
{
	private readonly IPlayerNumericAidController _controller;

	private readonly uint _playerId;

	private readonly PlayerNumericAid _playerNumericAid;

	public RemovePlayerNumericAidUXEvent(IPlayerNumericAidController controller, uint playerId, PlayerNumericAid playerNumericAid)
	{
		_controller = controller ?? NullPlayerNumericAidController.Default;
		_playerId = playerId;
		_playerNumericAid = playerNumericAid;
	}

	public override void Execute()
	{
		_controller.Remove(_playerId, _playerNumericAid);
		Complete();
	}
}
