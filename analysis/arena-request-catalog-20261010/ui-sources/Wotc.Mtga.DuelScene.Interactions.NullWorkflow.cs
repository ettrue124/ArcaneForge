using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions;

public class NullWorkflow : WorkflowBase<BaseUserRequest>
{
	public NullWorkflow(BaseUserRequest req)
		: base(req)
	{
	}

	protected override void ApplyInteractionInternal()
	{
	}

	protected override void SetArrows()
	{
	}

	protected override void SetButtons()
	{
	}

	protected override void SetDimming()
	{
	}

	protected override void SetPrompt()
	{
	}
}
