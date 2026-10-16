using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GameEndEvent : GameRulesEvent
{
	public ResultType Result;

	public GREPlayerNum Loser;

	public MatchScope Scope;

	public ResultReason Reason;
}
