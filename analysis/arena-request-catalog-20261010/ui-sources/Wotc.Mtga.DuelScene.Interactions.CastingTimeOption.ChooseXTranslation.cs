using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Logging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class ChooseXTranslation : IWorkflowTranslation<CastingTimeOption_NumericInputRequest>
{
	public interface ITextProvider
	{
		string Text(BaseUserRequest request);
	}

	public class NullTextProvider : ITextProvider
	{
		public string Text(BaseUserRequest request)
		{
			return string.Empty;
		}
	}

	public class ClientLocKeyProvider : ITextProvider
	{
		private readonly AssetLookupTree<ButtonTextPayload> _tree;

		private readonly IBlackboard _bb;

		public ClientLocKeyProvider(AssetLookupSystem assetLookupSystem)
		{
			_tree = assetLookupSystem.TreeLoader.LoadTree<ButtonTextPayload>();
			_bb = assetLookupSystem.Blackboard;
		}

		public string Text(BaseUserRequest request)
		{
			_bb.Clear();
			_bb.Request = request;
			_bb.Prompt = request.Prompt;
			ButtonTextPayload payload = _tree.GetPayload(_bb);
			if (payload != null)
			{
				return payload.LocKey.Key;
			}
			return string.Empty;
		}
	}

	private readonly IChooseXInterfaceBuilder _interfaceBuilder;

	private readonly ITextProvider _buttonTextProvider;

	private readonly IConfirmZeroLogger _confirmZeroLogger;

	public ChooseXTranslation(IChooseXInterfaceBuilder chooseXInterfaceBuilder, ITextProvider buttonTextProvider, IConfirmZeroLogger confirmZeroLogger)
	{
		_interfaceBuilder = chooseXInterfaceBuilder ?? new NullChooseXBuilder();
		_buttonTextProvider = buttonTextProvider ?? new NullTextProvider();
		_confirmZeroLogger = confirmZeroLogger ?? NullConfirmZeroLogger.Default;
	}

	public WorkflowBase Translate(CastingTimeOption_NumericInputRequest cto_chooseX)
	{
		return new ChooseXWorkflow(cto_chooseX, _interfaceBuilder, _confirmZeroLogger, _buttonTextProvider.Text(cto_chooseX));
	}
}
