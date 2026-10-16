using System.Threading;
using AssetLookupTree;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Wizards.Mtga.Assets;
using Wotc.Mtga;

namespace Core.MatchScene;

public class LoadMatchSceneUniTask
{
	private readonly LoadSceneMode _loadMode;

	private readonly IContext _context;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly string _postMatchSceneToLoad;

	public LoadMatchSceneUniTask(LoadSceneMode loadMode, IContext context, AssetLookupSystem assetLookupSystem, string postMatchSceneToLoad = null)
	{
		_loadMode = loadMode;
		_context = context;
		_assetLookupSystem = assetLookupSystem;
		_postMatchSceneToLoad = postMatchSceneToLoad;
	}

	public async UniTask Load(CancellationToken token)
	{
		await Scenes.LoadSceneAsync("MatchScene", _loadMode).WithCancellation(token);
		Scene sceneByName = SceneManager.GetSceneByName("MatchScene");
		SceneManager.SetActiveScene(sceneByName);
		sceneByName.GetSceneComponent<MatchSceneManager>().Initialize(_context, _assetLookupSystem, _postMatchSceneToLoad);
	}
}
