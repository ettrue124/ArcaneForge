using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class IllegalRequestNotification : ClientUpdateBase
{
	public ClientToGREMessage InvalidMessage { get; private set; }

	public FailureReason Reason { get; private set; }

	public IllegalRequestNotification(IllegalRequestMessage msg)
	{
		InvalidMessage = msg.InvalidMessage;
		Reason = msg.Reason;
	}
}
