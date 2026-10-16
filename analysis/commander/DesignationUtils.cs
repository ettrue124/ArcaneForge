using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DesignationUtils
{
	public const string DETAIL_TYPE = "DesignationType";

	public const string DETAIL_INTENSITY_LEVEL = "value";

	public static Designation GetDesignationType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("DesignationType") && detail.ValueInt32.Count > 0)
			{
				return (Designation)detail.ValueInt32[0];
			}
		}
		return Designation.None;
	}
}
