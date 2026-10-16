using GreClient.History;
using Wizards.Arena.Client.Logging;
using Wotc.Mtga.Cards.Database;

namespace GreClient.Rules;

public static class GreInterfaceFactory
{
	public static GreInterface Create(ICardDatabaseAdapter cdb, MessageHistory messageHistory, ILogger logger = null)
	{
		bool rethrowOnAnnotationParseFailure = false;
		IGreInterfaceCallbacks greInterfaceCallbacks = null;
		greInterfaceCallbacks = new GreInterfaceThreadedCallbacks();
		return new GreInterface(cdb, messageHistory, logger, rethrowOnAnnotationParseFailure, greInterfaceCallbacks);
	}
}
