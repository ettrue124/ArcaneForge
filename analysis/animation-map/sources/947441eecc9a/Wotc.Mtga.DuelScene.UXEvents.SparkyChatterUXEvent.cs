using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SparkyChatterUXEvent : UXEvent
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly EntityDialogController _dialogController;

	private readonly ChatterPair _chatterPair;

	private const float MinPlayback = 0.5f;

	private const float MaxPlayback = 5f;

	private float _playbackTimer;

	public override bool IsBlocking { get; }

	public SparkyChatterUXEvent(IClientLocProvider clientLocProvider, EntityDialogController dialogController, ChatterPair chatterPair, bool isBlocking = false)
	{
		_clientLocProvider = clientLocProvider;
		_dialogController = dialogController;
		_chatterPair = chatterPair;
		IsBlocking = isBlocking;
	}

	public override void Execute()
	{
		if (ShouldPlayAudio(_chatterPair.audioEvent) && _dialogController.AllowSparkyChatter)
		{
			_dialogController.ShowSparkyChatter(_clientLocProvider.GetLocalizedText(_chatterPair.localizationTerm));
			_dialogController.PlaySparkyAudio(_chatterPair.audioEvent);
		}
		else
		{
			Complete();
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		_playbackTimer += dt;
		if (!(_playbackTimer < 0.5f) && (!_dialogController.IsAudioPlaying() || _playbackTimer > 5f))
		{
			Complete();
		}
	}

	private static bool ShouldPlayAudio(AudioEvent audioEvent)
	{
		if (audioEvent != null)
		{
			return !string.IsNullOrEmpty(audioEvent.WwiseEventName);
		}
		return false;
	}
}
