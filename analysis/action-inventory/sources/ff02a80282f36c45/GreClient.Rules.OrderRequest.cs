using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class OrderRequest : BaseUserRequest
{
	public readonly List<uint> Ids;

	public readonly OrderingContext OrderingContext;

	public OrderRequest(OrderReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Ids = new List<uint>(req.Ids);
		OrderingContext = req.OrderingContext;
		_outboundMessage.Type = ClientMessageType.OrderResp;
		_outboundMessage.OrderResp = new OrderResp
		{
			Ordering = OrderingType.OrderAsIndicated
		};
	}

	public void SubmitOrder(IEnumerable<uint> order)
	{
		OrderResp orderResp = _outboundMessage.OrderResp;
		orderResp.Ids.Clear();
		orderResp.Ids.AddRange(order.Reverse());
		Submit();
	}
}
