using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class RequestAnnotationProcessor : IRequestAnnotationProcessor<BaseUserRequest>
{
	private readonly IRequestAnnotationProcessor<SelectNRequest> _selectNParser = new SelectNRequestAnnotationProcessor();

	public void Process(BaseUserRequest request, IReadOnlyList<AnnotationInfo> annotations)
	{
		if (request is SelectNRequest request2)
		{
			_selectNParser.Process(request2, annotations);
		}
	}
}
