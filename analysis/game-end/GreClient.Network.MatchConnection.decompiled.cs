using Wizards.Arena.TcpConnection;

namespace GreClient.Network;

public class MatchConnection : IMatchConnection
{
	private const string DISCONNECT_CLOSE_REASON = "MatchConnection.Disconnect";

	private readonly MatchManager _matchManager;

	private readonly MatchConnectionConfig _matchConfig;

	public ConnectionState State => _matchManager.ConnectionState;

	public MatchConnection(MatchManager matchManager, MatchConnectionConfig matchConfig)
	{
		_matchManager = matchManager;
		_matchConfig = matchConfig;
	}

	public void Connect()
	{
		_matchManager.ConnectAndJoinMatch(_matchConfig.AuthCredentials, _matchConfig.MatchUri, _matchConfig.MatchId);
	}

	public void Disconnect()
	{
		_matchManager.GreConnection?.Close(TcpConnectionCloseType.NormalClosure, "MatchConnection.Disconnect");
	}
}
