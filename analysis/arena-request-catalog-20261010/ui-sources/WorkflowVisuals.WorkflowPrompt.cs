using Wotc.Mtgo.Gre.External.Messaging;

namespace WorkflowVisuals;

public class WorkflowPrompt
{
	public Prompt GrePrompt;

	public string LocKey = string.Empty;

	public (string, string)[] LocParams;

	public void Reset()
	{
		GrePrompt = null;
		LocKey = string.Empty;
		LocParams = null;
	}
}
