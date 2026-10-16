using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.NumericInput;

public class NumericInputTranslation : IWorkflowTranslation<NumericInputRequest>
{
	private const uint READ_AHEAD_PROMPT_ID = 166u;

	private readonly IChooseXInterfaceBuilder _chooseXInterfaceBuilder;

	private readonly IContext _context;

	private readonly AssetLookupSystem _assetLookupSystem;

	public NumericInputTranslation(IContext context, AssetLookupSystem assetLookupSystem)
	{
		_chooseXInterfaceBuilder = new ChooseXInterfaceBuilder(context.Get<IUnityObjectPool>(), assetLookupSystem, context.Get<ICanvasRootProvider>());
		_context = context;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(NumericInputRequest req)
	{
		if (UseReadAheadWorkflow(req))
		{
			return new ReadAheadWorkflow(req, _context.Get<IGameStateProvider>(), _context.Get<ICardDatabaseAdapter>(), _context.Get<IEntityViewManager>(), _context.Get<IPromptEngine>(), _context.Get<IClientLocProvider>(), _context.Get<IGreLocProvider>(), _context.Get<IBrowserController>());
		}
		return new NumericInputWorkflow(req, _chooseXInterfaceBuilder, _context.Get<IEntityViewProvider>(), _assetLookupSystem, _context.Get<IGameStateProvider>());
	}

	private static bool UseReadAheadWorkflow(NumericInputRequest req)
	{
		if (req == null)
		{
			return false;
		}
		Prompt prompt = req.Prompt;
		if (prompt == null)
		{
			return false;
		}
		return prompt.PromptId == 166;
	}
}
