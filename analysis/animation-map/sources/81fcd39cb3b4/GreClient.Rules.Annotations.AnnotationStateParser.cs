using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizards.Arena.Client.Logging;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AnnotationStateParser : IAnnotationStateParser
{
	private readonly IReadOnlyDictionary<AnnotationType, IAnnotationStateParser> _typeToParserMap;

	private readonly ILogger _logger;

	private readonly bool _rethrowOnParseFailure;

	public AnnotationStateParser(IReadOnlyDictionary<AnnotationType, IAnnotationStateParser> typeToParserMap, ILogger logger, bool rethrowOnParseFailure)
	{
		_typeToParserMap = typeToParserMap;
		_logger = logger;
		_rethrowOnParseFailure = rethrowOnParseFailure;
	}

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		foreach (AnnotationType item in ai.Type)
		{
			if (!_typeToParserMap.TryGetValue(item, out var value))
			{
				continue;
			}
			try
			{
				value.Process(ai, oldState, newState);
			}
			catch (Exception ex)
			{
				_logger.Error("Exception while parsing annotation.", new JObject(new JProperty("AnnotationType", item), new JProperty("AnnotationInfo", JObject.FromObject(ai)), new JProperty("ExceptionType", ex.GetType().Name), new JProperty("Exception", JObject.FromObject(ex))));
				if (_rethrowOnParseFailure)
				{
					throw;
				}
			}
		}
	}
}
