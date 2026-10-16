using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddPlayerNumericAidUXEvent : UXEvent
{
	private readonly IPlayerNumericAidController _controller;

	private readonly uint _playerId;

	private readonly PlayerNumericAid _playerNumericAid;

	public AddPlayerNumericAidUXEvent(IPlayerNumericAidController controller, uint playerId, PlayerNumericAid playerNumericAid)
	{
		_controller = controller;
		_playerId = playerId;
		_playerNumericAid = playerNumericAid;
	}

	public override void Execute()
	{
		_controller.Add(_playerId, _playerNumericAid);
		Complete();
	}
}
