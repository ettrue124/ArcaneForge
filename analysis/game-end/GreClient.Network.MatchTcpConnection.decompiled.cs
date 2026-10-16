using System;
using System.IO;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Wizards.Arena.Enums.System;
using Wizards.Arena.TcpConnection;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Network;

public class MatchTcpConnection : IMatchTcpConnection, IDisposable
{
	private class ClientToMatchSvcMsgWrapper : IMsg
	{
		private ClientToMatchServiceMessage _pbMsg;

		public ClientToMatchSvcMsgWrapper(ClientToMatchServiceMessage msg)
			: base(EType.Msg)
		{
			_pbMsg = msg ?? throw new ArgumentNullException("msg");
			Format = SerializationFormat.Protobuf;
		}

		public override JObject LogFormat()
		{
			return TcpConnectionUtils.SafeStart("type", "CliToMatch").SafeAdd("id", _pbMsg.TransactionId).SafeAdd("messageType", _pbMsg.ClientToMatchServiceMessageType);
		}

		public override int GetBodyLength()
		{
			return _pbMsg.CalculateSize();
		}

		public override void WriteToBuffer(byte[] buffer, int offset)
		{
			using MemoryStream output = new MemoryStream(buffer, offset, buffer.Length - offset, writable: true);
			_pbMsg.WriteTo(output);
		}
	}

	private readonly TcpConnection _tcpConnection;

	private int _clientDesiredTimeoutMs = -1;

	private int _requestId;

	public TcpConnectState ConnectState => _tcpConnection.ConnectState;

	public bool IsConnected => _tcpConnection.Connected;

	public event Action<ITcpEvent> TcpEventReceived;

	public MatchTcpConnection(TcpConnection tcpConnection)
	{
		_tcpConnection = tcpConnection;
		TcpConnection tcpConnection2 = _tcpConnection;
		tcpConnection2.OnClose = (Action<TcpConnectionCloseType, string>)Delegate.Combine(tcpConnection2.OnClose, new Action<TcpConnectionCloseType, string>(OnClose));
		TcpConnection tcpConnection3 = _tcpConnection;
		tcpConnection3.OnConnected = (System.Action)Delegate.Combine(tcpConnection3.OnConnected, new System.Action(OnConnected));
		TcpConnection tcpConnection4 = _tcpConnection;
		tcpConnection4.OnMsgReceived = (Action<byte[], int, int, byte>)Delegate.Combine(tcpConnection4.OnMsgReceived, new Action<byte[], int, int, byte>(OnMsgReceived));
	}

	public void Connect(ConnectionConfig connectionConfig)
	{
		_clientDesiredTimeoutMs = connectionConfig.InactivityTimeoutMs;
		if (_clientDesiredTimeoutMs != -1)
		{
			_tcpConnection.InactivityTimeoutMs = _clientDesiredTimeoutMs;
		}
		_tcpConnection.Connect(connectionConfig.MatchDoorHost, connectionConfig.MatchDoorPort);
	}

	public void Close(TcpConnectionCloseType status, string reason)
	{
		_tcpConnection.Close(status, reason);
	}

	public JObject LogFormat()
	{
		return _tcpConnection.LogFormat();
	}

	private void OnConnected()
	{
		TcpEventReceived?.Invoke(new TcpOpenedEvent());
	}

	private void OnClose(TcpConnectionCloseType status, string reason)
	{
		TcpEventReceived?.Invoke(new TcpClosedEvent
		{
			Status = status,
			Reason = reason
		});
	}

	private void OnMsgReceived(byte[] buffer, int offset, int length, byte messageVersion)
	{
		MatchServiceToClientMessage matchServiceToClientMessage;
		using (Stream input = new MemoryStream(buffer, offset, length))
		{
			try
			{
				matchServiceToClientMessage = MatchServiceToClientMessage.Parser.ParseFrom(input);
			}
			catch (Exception ex)
			{
				ex.Data["bytes"] = buffer?.ToHexString(offset, length, 1000);
				throw;
			}
		}
		ProcessTcpEvent(new TcpMatchDoorMessageReceivedEvent
		{
			MatchServiceToClientMessage = matchServiceToClientMessage
		});
	}

	private void ProcessTcpEvent(ITcpEvent tcpEvent)
	{
		if (tcpEvent is TcpMatchDoorMessageReceivedEvent tcpMatchDoorMessageReceivedEvent)
		{
			HandleMatchServiceMessage(tcpMatchDoorMessageReceivedEvent.MatchServiceToClientMessage);
		}
		TcpEventReceived?.Invoke(tcpEvent);
	}

	private void HandleMatchServiceMessage(MatchServiceToClientMessage msg)
	{
		if (msg.MessageCase == MatchServiceToClientMessage.MessageOneofCase.AuthenticateResponse && msg.AuthenticateResponse.InactivityTimeoutMs > 0)
		{
			_tcpConnection.InactivityTimeoutMs = msg.AuthenticateResponse.InactivityTimeoutMs;
		}
	}

	public void Send(ClientToMatchServiceMessage msg)
	{
		msg.RequestId = ++_requestId;
		msg.Timestamp = DateTime.UtcNow.Ticks;
		msg.TransactionId = Guid.NewGuid().ToString();
		_tcpConnection.Send(new ClientToMatchSvcMsgWrapper(msg));
	}

	public void Dispose()
	{
		TcpConnection tcpConnection = _tcpConnection;
		tcpConnection.OnClose = (Action<TcpConnectionCloseType, string>)Delegate.Remove(tcpConnection.OnClose, new Action<TcpConnectionCloseType, string>(OnClose));
		TcpConnection tcpConnection2 = _tcpConnection;
		tcpConnection2.OnConnected = (System.Action)Delegate.Remove(tcpConnection2.OnConnected, new System.Action(OnConnected));
		TcpConnection tcpConnection3 = _tcpConnection;
		tcpConnection3.OnMsgReceived = (Action<byte[], int, int, byte>)Delegate.Remove(tcpConnection3.OnMsgReceived, new Action<byte[], int, int, byte>(OnMsgReceived));
	}
}
