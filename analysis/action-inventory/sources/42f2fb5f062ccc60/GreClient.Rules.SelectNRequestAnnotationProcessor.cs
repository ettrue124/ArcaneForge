using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectNRequestAnnotationProcessor : IRequestAnnotationProcessor<SelectNRequest>
{
	private readonly Dictionary<AnnotationType, IRequestAnnotationParser<SelectNRequest>> _decorators = new Dictionary<AnnotationType, IRequestAnnotationParser<SelectNRequest>> { [AnnotationType.SelectNdecoration] = new SelectNDecorationAnnotationParser() };

	public void Process(SelectNRequest request, IReadOnlyList<AnnotationInfo> annotations)
	{
		foreach (AnnotationInfo annotation in annotations)
		{
			foreach (AnnotationType item in annotation.Type)
			{
				if (_decorators.TryGetValue(item, out var value))
				{
					value.Parse(annotation, request);
				}
			}
		}
	}
}
