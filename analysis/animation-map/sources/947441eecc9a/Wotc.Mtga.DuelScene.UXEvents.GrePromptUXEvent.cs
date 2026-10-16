using AssetLookupTree;
using AssetLookupTree.Payloads.UI.DuelScene;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GrePromptUXEvent : UXEvent
{
	private const float DEFAULT_SHOW_TIME = 5f;

	private readonly Prompt _prompt;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly ICanvasRootProvider _canvasRootProvider;

	private readonly IUnityObjectPool _objectPool;

	private readonly IPromptTextProvider _promptTextProvider;

	public GrePromptUXEvent(Prompt prompt, IContext context, AssetLookupSystem assetLookupSystem)
	{
		_prompt = prompt;
		_objectPool = context.Get<IUnityObjectPool>() ?? NullUnityObjectPool.Default;
		_canvasRootProvider = context.Get<ICanvasRootProvider>() ?? NullCanvasRootProvider.Default;
		_promptTextProvider = context.Get<IPromptTextProvider>() ?? NullPromptTextProvider.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public override void Execute()
	{
		if (_prompt == null)
		{
			Complete();
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.Prompt = _prompt;
		GrePromptPrefabs payload = _assetLookupSystem.TreeLoader.LoadTree<GrePromptPrefabs>().GetPayload(_assetLookupSystem.Blackboard);
		_assetLookupSystem.Blackboard.Clear();
		if (payload == null)
		{
			Complete();
			return;
		}
		string promptText = _promptTextProvider.GetPromptText(_prompt);
		if (string.IsNullOrWhiteSpace(promptText))
		{
			Complete();
			return;
		}
		Transform canvasRoot = _canvasRootProvider.GetCanvasRoot(CanvasLayer.Overlay);
		GameObject gameObject = _objectPool.PopObject(payload.PrefabPath);
		GrePromptDisplay component = gameObject.GetComponent<GrePromptDisplay>();
		gameObject.transform.SetParent(canvasRoot);
		gameObject.transform.ZeroOut();
		component.ShowPrompt(promptText, 5f);
		gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(5f, SelfCleanup.CleanupType.SharedPool);
		Complete();
	}
}
