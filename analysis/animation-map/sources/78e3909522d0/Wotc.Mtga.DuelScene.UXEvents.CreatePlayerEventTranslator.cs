using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Companions;
using Wotc.Mtga.DuelScene.Emotes;
using Wotc.Mtga.DuelScene.Input;
using Wotc.Mtga.DuelScene.ZoneCounts;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CreatePlayerEventTranslator : IEventTranslator
{
	private readonly IAvatarViewController _avatarController;

	private readonly IAvatarInputController _avatarInputController;

	private readonly IEmoteManager _emoteManager;

	private readonly ICompanionViewController _companionController;

	private readonly IZoneCountController _zoneCountController;

	private readonly IPlayerTimerController _playerTimerController;

	private readonly BattleFieldStaticElementsLayout _staticLayoutElements;

	public CreatePlayerEventTranslator(IContext context, BattleFieldStaticElementsLayout staticElementsLayout)
		: this(context.Get<IAvatarViewController>(), context.Get<IAvatarInputController>(), context.Get<IEmoteManager>(), context.Get<ICompanionViewController>(), context.Get<IZoneCountController>(), context.Get<IPlayerTimerController>(), staticElementsLayout)
	{
	}

	public CreatePlayerEventTranslator(IAvatarViewController avatarController, IAvatarInputController avatarInputController, IEmoteManager emoteManager, ICompanionViewController companionController, IZoneCountController zoneCountController, IPlayerTimerController playerTimerController, BattleFieldStaticElementsLayout staticLayoutElements)
	{
		_avatarController = avatarController ?? NullAvatarViewController.Default;
		_avatarInputController = avatarInputController ?? NullAvatarInputController.Default;
		_emoteManager = emoteManager ?? NullEmoteManager.Default;
		_companionController = companionController ?? NullCompanionViewController.Default;
		_zoneCountController = zoneCountController ?? NullZoneCountController.Default;
		_playerTimerController = playerTimerController ?? NullPlayerTimerController.Default;
		_staticLayoutElements = staticLayoutElements;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is CreatePlayerEvent { Player: { } player })
		{
			events.Add(new CreatePlayerUXEvent(player, _avatarController, _avatarInputController, _emoteManager, _companionController, _zoneCountController, _playerTimerController, _staticLayoutElements));
		}
	}
}
