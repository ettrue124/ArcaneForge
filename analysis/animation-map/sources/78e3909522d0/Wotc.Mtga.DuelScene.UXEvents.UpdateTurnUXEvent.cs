using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateTurnUXEvent : UXEvent
{
	private readonly MtgPlayer _activePlayer;

	private readonly uint _turnNumber;

	private readonly ITurnController _turnController;

	private readonly IPlayerFocusController _playerFocusController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly UIManager _uiManager;

	public override bool IsBlocking => true;

	public UpdateTurnUXEvent(MtgPlayer activePlayer, uint newTurnNumber, ITurnController turnController, IPlayerFocusController playerFocusController, ICardHolderProvider cardHolderProvider, UIManager uiManager)
	{
		_activePlayer = activePlayer ?? new MtgPlayer();
		_turnNumber = newTurnNumber;
		_turnController = turnController ?? NullTurnController.Default;
		_playerFocusController = playerFocusController ?? NullPlayerFocusController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_uiManager = uiManager;
	}

	public override void Execute()
	{
		_uiManager.TurnChanged.OnTurnChange(_activePlayer);
		OnTurnPromptStart();
	}

	private void OnTurnPromptStart()
	{
		_playerFocusController.FocusPlayer(_activePlayer.InstanceId);
		_turnController.SetCurrentTurn(_turnNumber, _activePlayer.InstanceId);
		_uiManager.UpdateActivePlayer(_activePlayer.ClientPlayerEnum);
		if (_cardHolderProvider.TryGetCardHolder(GREPlayerNum.LocalPlayer, CardHolderType.Hand, out HandCardHolder result))
		{
			result.OnTurnChange(_activePlayer.ClientPlayerEnum);
		}
		Complete();
	}
}
