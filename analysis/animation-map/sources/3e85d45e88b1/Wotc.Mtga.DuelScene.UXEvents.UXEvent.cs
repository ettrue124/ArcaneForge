using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class UXEvent
{
	protected bool _canTimeOut = true;

	protected float _timeRunning;

	protected float _timeOutTarget = 10f;

	public bool IsComplete { get; private set; }

	public bool HasFailed { get; private set; }

	public virtual bool HasWeight => true;

	public virtual bool IsBlocking => false;

	public virtual bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return true;
	}

	public abstract void Execute();

	public virtual void Update(float dt)
	{
		_timeRunning += dt;
		if (_timeRunning > _timeOutTarget && _canTimeOut)
		{
			Fail();
			Debug.LogException(new TimeoutException($"{GetType()} HAS BEEN RUNNING FOR OVER {_timeOutTarget} SECONDS!\n{this}"));
		}
	}

	public void Fail()
	{
		HasFailed = true;
		Complete();
	}

	public void Complete()
	{
		if (!IsComplete)
		{
			IsComplete = true;
			OnComplete();
			Cleanup();
		}
	}

	protected virtual void OnComplete()
	{
	}

	protected virtual void Cleanup()
	{
	}

	public virtual IEnumerable<uint> GetInvolvedIds()
	{
		return Array.Empty<uint>();
	}
}
