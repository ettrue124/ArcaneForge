using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wizards.Arena.Enums.System;
using Wizards.Arena.Promises;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Unification.Models.DirectChallenge;
using Wizards.Unification.Models.Event;

namespace Wotc.Mtga.Network.ServiceWrappers;

public class AwsMatchdoorServiceWrapper : IMatchdoorServiceWrapper
{
	private IInventoryServiceWrapper _inventoryServiceWrapper;

	private readonly FrontDoorConnectionAWS _fd;

	private Action<EDirectChallengeMismatch[]> directChallengeForcedDropHandler;

	private static EPlayFirst ConvertToTransportModel(WhoPlaysFirst whoPlaysFirst)
	{
		return whoPlaysFirst switch
		{
			WhoPlaysFirst.LocalPlayer => EPlayFirst.Challenger, 
			WhoPlaysFirst.Opponent => EPlayFirst.Opponent, 
			WhoPlaysFirst.Random => EPlayFirst.Random, 
			_ => EPlayFirst.Random, 
		};
	}

	public AwsMatchdoorServiceWrapper(FrontDoorConnectionAWS fd, IInventoryServiceWrapper inventoryServiceWrapper)
	{
		_fd = fd;
		_inventoryServiceWrapper = inventoryServiceWrapper;
	}

	public Promise<string> DropFromMatchQueue(string eventId)
	{
		return _fd.DropFromMatchQueue(eventId);
	}

	public void RegisterOnAuthFailed(Action<ServerErrors, string> callback)
	{
		_fd.OnAuthFailed += callback;
	}

	public void RegisterOnConnectFailed(Action<string> callback)
	{
		_fd.OnConnectFailed += callback;
	}

	public void RegisterOnChallengeForcedDrop(Action<EDirectChallengeMismatch[]> callback)
	{
		if (directChallengeForcedDropHandler == null)
		{
			_fd.OnMsg_Challenge_ForcedDrop += OnChallengeForcedDrop;
		}
		directChallengeForcedDropHandler = (Action<EDirectChallengeMismatch[]>)Delegate.Combine(directChallengeForcedDropHandler, callback);
	}

	private void OnChallengeForcedDrop(EMismatchReason[] reasons)
	{
		List<EDirectChallengeMismatch> list = new List<EDirectChallengeMismatch>();
		if (reasons != null)
		{
			for (int i = 0; i < reasons.Length; i++)
			{
				switch (reasons[i])
				{
				case EMismatchReason.Bo3Mismatch:
					list.Add(EDirectChallengeMismatch.BestOf3Mismatch);
					break;
				case EMismatchReason.GoFirstMismatch:
					list.Add(EDirectChallengeMismatch.PlayFirstMismatch);
					break;
				case EMismatchReason.VariantMismatch:
					list.Add(EDirectChallengeMismatch.VariantMismatch);
					break;
				default:
					list.Add(EDirectChallengeMismatch.Unknown);
					break;
				}
			}
		}
		directChallengeForcedDropHandler?.Invoke(list.ToArray());
	}

	public void RegisterOnForcedDrop(Action<string> callback)
	{
	}

	public void RegisterOnMatchCreated(Action<NewMatchCreatedConfig> callback)
	{
		_fd.OnMsg_Event_MatchCreated += callback;
	}

	public void UnregisterOnAuthFailed(Action<ServerErrors, string> callback)
	{
		_fd.OnAuthFailed -= callback;
	}

	public void UnregisterOnConnectFailed(Action<string> callback)
	{
		_fd.OnConnectFailed -= callback;
	}

	public void UnregisterOnChallengeForcedDrop(Action<EDirectChallengeMismatch[]> callback)
	{
		directChallengeForcedDropHandler = (Action<EDirectChallengeMismatch[]>)Delegate.Remove(directChallengeForcedDropHandler, callback);
		if (directChallengeForcedDropHandler == null)
		{
			_fd.OnMsg_Challenge_ForcedDrop -= OnChallengeForcedDrop;
		}
	}

	public void UnregisterOnForcedDrop(Action<string> callback)
	{
	}

	public void UnregisterOnMatchCreated(Action<NewMatchCreatedConfig> callback)
	{
		_fd.OnMsg_Event_MatchCreated -= callback;
	}

	public async Task<PostMatchClientUpdate> GetMatchResults(string eventName, string matchId)
	{
		Promise<MatchResultReport> promise = _fd.GetMatchResults(eventName, matchId);
		while (!promise.IsDone)
		{
			await Task.Yield();
		}
		if (promise.Successful && (promise.Result?.FoundMatch ?? false))
		{
			if (promise.Result?.InventoryInfo != null)
			{
				_inventoryServiceWrapper.OnInventoryInfoUpdated_AWS(AWSInventoryConversions.ConvertInventoryInfo(promise.Result.InventoryInfo));
			}
			if (promise.Result?.rankUpdates != null)
			{
				_fd.InvokeOnRankProgress(ServiceWrapperHelpers.ToClientRankProgress(promise.Result.rankUpdates));
				_fd.InvokeMythicRatingUpdated(ServiceWrapperHelpers.ToClientMythicRatingUpdates(promise.Result.mythicRatingUpdates));
			}
		}
		return promise.Result?.ConvertToAzure();
	}
}
