using GreClient.Rules;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class ReplicateTranslation : IWorkflowTranslation<CastingTimeOptionRequest>
{
	private readonly IChooseXInterfaceBuilder _interfaceBuilder;

	private readonly IClientLocProvider _locProvider;

	public ReplicateTranslation(IChooseXInterfaceBuilder interfaceBuilder, IClientLocProvider locProvider)
	{
		_interfaceBuilder = interfaceBuilder ?? NullChooseXBuilder.Default;
		_locProvider = locProvider ?? NullLocProvider.Default;
	}

	public WorkflowBase Translate(CastingTimeOptionRequest req)
	{
		return new ReplicateWorkflow(req, _interfaceBuilder, _locProvider);
	}
}
