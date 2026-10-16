namespace GreClient.Rules.Annotations;

public static class StateParserFactory
{
	public static IAnnotationStateParser AbilityCreatedStateParser()
	{
		return new StateParserAggregate(new ParentOfReferenceMapStateParser(), new SourceZoneStateParser());
	}
}
