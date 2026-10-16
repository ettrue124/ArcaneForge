using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GameEndUXEvent : UXEvent
{
	public readonly ResultType Result;

	public readonly GREPlayerNum Loser;

	public readonly ResultReason Reason;

	public override bool IsBlocking => true;

	public static event Action<GameEndUXEvent> GameEndEventExecuted;

	public GameEndUXEvent(ResultType result, GREPlayerNum loser, ResultReason reason)
	{
		Result = result;
		Loser = loser;
		Reason = reason;
	}

	public override void Execute()
	{
		GameEndEventExecuted?.Invoke(this);
		Complete();
	}
}
