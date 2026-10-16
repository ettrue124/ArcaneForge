using System.Collections.Generic;
using Wizards.Arena.Promises;
using Wizards.Unification.Models.PlayBlade;
using Wotc.Mtga.Network.ServiceWrappers;

namespace Wizards.Mtga.PlayBlade;

public class PlayBladeConfigDataProvider
{
	private readonly IPlayBladeConfigServiceWrapper _playBladeConfigServiceWrapper;

	private List<PlayBladeQueueEntry> _playBladeConfig;

	private bool _initialized;

	public static PlayBladeConfigDataProvider Create()
	{
		return new PlayBladeConfigDataProvider(Pantry.Get<IPlayBladeConfigServiceWrapper>());
	}

	public PlayBladeConfigDataProvider(IPlayBladeConfigServiceWrapper playBladeConfigService)
	{
		_playBladeConfigServiceWrapper = playBladeConfigService;
	}

	public Promise<List<PlayBladeQueueEntry>> Initialize()
	{
		return _playBladeConfigServiceWrapper.GetPlayBladeConfig().Then((Promise<List<PlayBladeQueueEntry>> promise) =>
		{
			if (promise.Successful)
			{
				_playBladeConfig = promise.Result ?? new List<PlayBladeQueueEntry>();
				_initialized = true;
			}
			else
			{
				PromiseExtensions.Logger.Error($"Failed to get player preferences: {promise.Error}");
			}
			if (_playBladeConfig.Count == 0)
			{
				PromiseExtensions.Logger.Error("No PlayBlade config data returned from service!");
			}
		});
	}

	public List<PlayBladeQueueEntry> GetPlayBladeConfig()
	{
		if (!_initialized)
		{
			SimpleLog.LogError("Attempting to get play blade config data before play blade config data provider is initialized");
			return null;
		}
		return _playBladeConfig;
	}
}
