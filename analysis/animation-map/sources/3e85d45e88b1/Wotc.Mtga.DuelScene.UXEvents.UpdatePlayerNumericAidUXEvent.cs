using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePlayerNumericAidUXEvent : UXEvent
{
	private readonly IPlayerNumericAidController _controller;

	private readonly uint _playerId;

	private readonly PlayerNumericAid _playerNumericAid;

	public UpdatePlayerNumericAidUXEvent(IPlayerNumericAidController controller, uint playerId, PlayerNumericAid playerNumericAid)
	{
		_controller = controller;
		_playerId = playerId;
		_playerNumericAid = playerNumericAid;
	}

	public override void Execute()
	{
		_controller.Update(_playerId, _playerNumericAid);
		Complete();
	}
}
