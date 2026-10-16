using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.PhaseLadder;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene;

public class ButtonPhaseLadder : MonoBehaviour
{
	[Serializable]
	public class PhaseData : ISerializationCallbackReceiver
	{
		[HideInInspector]
		public string name;

		public Phase Phase;

		public Step Step;

		public GREPlayerNum Player;

		public MTGALocalizedString NextPhaseTooltip;

		public void OnAfterDeserialize()
		{
			name = CalcName();
		}

		public void OnBeforeSerialize()
		{
			name = CalcName();
		}

		public string CalcName()
		{
			return $"{Phase} / {Step} / {Player}";
		}
	}

	public bool AlwaysOn;

	public bool AlwaysShowCombat;

	public bool ForceShow;

	[NonSerialized]
	public bool InTutorial;

	[NonSerialized]
	public List<PhaseLadderButton> PhaseIcons;

	[SerializeField]
	private TooltipData _tooltipData;

	[SerializeField]
	private TooltipProperties _primaryTooltipProperties;

	[SerializeField]
	private TooltipProperties _secondaryTooltipProperties;

	[SerializeField]
	private List<PhaseData> _promptDataByPhase;

	[Header("Assets")]
	[SerializeField]
	private Animator _iconMain;

	[SerializeField]
	private GameObject _iconDamage;

	[SerializeField]
	private GameObject _iconFirstStrike;

	[SerializeField]
	private GameObject _iconSecondStrike;

	[SerializeField]
	private GameObject _toggleHint;

	[SerializeField]
	private Localize _toggleLoc;

	private GameManager _gameManager;

	private IGameStateProvider _gameStateProvider = NullGameStateProvider.Default;

	private ITurnInfoProvider _turnInfoProvider = NullTurnInfoProvider.Default;

	private ISettingsMessageProvider _settingsProvider = NullSettingsMessageProvider.Default;

	private ITooltipDisplay _tooltipSystem;

	private GreInterface _gre;

	private PhaseLadderButton _currentIcon;

	private List<PhaseLadderButton> _highlightIcons = new List<PhaseLadderButton>();

	private Phase _currentPhase;

	private Step _currentStep;

	private GREPlayerNum _activePlayer;

	private bool _inCombat;

	private bool _fullControl;

	private readonly List<Stop> _transientStops = new List<Stop>();

	private MtgTurnInfo _currentTurnInfo;

	private MtgGameState LatestGameState => _gameStateProvider.LatestGameState?.Value;

	private Step _latestNextStep => LatestGameState?.NextStep ?? Step.None;

	public ButtonPhaseContext ButtonPhaseContext { private get; set; }

	private void Awake()
	{
		PhaseIcons = GetComponentsInChildren<PhaseLadderButton>(includeInactive: true).ToList();
		foreach (PhaseLadderButton phaseIcon in PhaseIcons)
		{
			phaseIcon.Init(this);
		}
	}

	public void OnEnable()
	{
		_inCombat = false;
		AlwaysOn = ForceShow || MDNPlayerPrefs.ShowPhaseLadder;
		_iconMain.SetBool("CombatOnly", !AlwaysOn && !_fullControl);
		StartCoroutine(SetNewPhaseIcon(_currentPhase, _currentStep));
	}

	public void Init(GameManager gameManager, IGameStateProvider gameStateProvider, ITurnInfoProvider turnInfoProvider, ISettingsMessageProvider settingsProvider, GreInterface gre, ITooltipDisplay tooltipSystem)
	{
		_gameManager = gameManager;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_turnInfoProvider = turnInfoProvider ?? NullTurnInfoProvider.Default;
		_settingsProvider = settingsProvider ?? NullSettingsMessageProvider.Default;
		_tooltipSystem = tooltipSystem ?? NullTooltipDisplay.Default;
		_gre = gre;
		foreach (PhaseLadderButton phaseIcon in PhaseIcons)
		{
			phaseIcon.InitFullControlToggle(gameManager.UIManager.FullControl);
		}
		_turnInfoProvider.TurnInfo.ValueUpdated += OnTurnInfoUpdated;
		_settingsProvider.Settings.ValueUpdated += OnSettingsUpdated;
	}

	public void TogglePhaseLadder()
	{
		_gameManager.TogglePhaseLadder();
	}

	private void OnTurnInfoUpdated(MtgTurnInfo turnInfo)
	{
		MtgTurnInfo currentTurnInfo = _currentTurnInfo;
		_currentTurnInfo = turnInfo;
		if (currentTurnInfo.ActivePlayerId != turnInfo.ActivePlayerId)
		{
			GREPlayerNum player = ((MtgGameState)_gameStateProvider.CurrentGameState).GetPlayerById(turnInfo.ActivePlayerId)?.ClientPlayerEnum ?? GREPlayerNum.Invalid;
			UpdateActivePlayer(player);
		}
		if (currentTurnInfo.PhaseChanged(turnInfo))
		{
			UpdatePhase(turnInfo.Phase, turnInfo.Step);
		}
	}

	private void UpdatePhase(Phase phase, Step step)
	{
		if (step == Step.None && phase == Phase.Combat)
		{
			step = Step.BeginCombat;
		}
		if (gameObject.activeInHierarchy)
		{
			StopAllCoroutines();
			StartCoroutine(SetNewPhaseIcon(phase, step));
		}
		else
		{
			_currentPhase = phase;
			_currentStep = step;
		}
	}

	private void OnSettingsUpdated(SettingsMessage settings)
	{
		if (ChangedStops(_transientStops, settings.TransientStops))
		{
			_transientStops.Clear();
			_transientStops.AddRange(settings.TransientStops);
			SetTransientStops(_transientStops);
		}
		bool flag = settings.FullControlEnabled();
		if (flag != _fullControl)
		{
			UpdateFullControl(flag);
		}
	}

	private bool ChangedStops(IReadOnlyList<Stop> current, IReadOnlyList<Stop> updated)
	{
		if (current.Count != updated.Count)
		{
			return true;
		}
		for (int i = 0; i < current.Count; i++)
		{
			Stop stop = current[i];
			Stop stop2 = updated[i];
			if (stop.AppliesTo != stop2.AppliesTo || stop.StopType != stop2.StopType || stop.Status != stop2.Status)
			{
				return true;
			}
		}
		return false;
	}

	private IEnumerator SetNewPhaseIcon(Phase phase, Step step)
	{
		if (_currentIcon != null)
		{
			_currentIcon.Lit = false;
		}
		if (phase == Phase.Combat && !_inCombat)
		{
			yield return new WaitForSeconds(0.1f);
			_inCombat = true;
			if (!InTutorial && !MDNPlayerPrefs.SeenPhaseLadderHint)
			{
				MDNPlayerPrefs.SeenPhaseLadderHint = true;
			}
		}
		else if (phase != Phase.Combat && _inCombat)
		{
			_inCombat = false;
		}
		_toggleHint.UpdateActive(!InTutorial && !MDNPlayerPrefs.SeenPhaseLadderHint && !ForceShow);
		_toggleLoc.SetText("DuelScene/SettingsMenu/Gameplay/PhaseLadder");
		_iconMain.SetBool("InCombat", _inCombat || (AlwaysOn && AlwaysShowCombat));
		yield return null;
		while (_iconMain.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f)
		{
			yield return null;
		}
		bool flag = step == Step.FirstStrikeDamage || _currentStep == Step.FirstStrikeDamage || _latestNextStep == Step.FirstStrikeDamage;
		_iconDamage.SetActive(!flag);
		_iconFirstStrike.SetActive(step == Step.FirstStrikeDamage || _latestNextStep == Step.FirstStrikeDamage);
		_iconSecondStrike.SetActive(step == Step.CombatDamage && _currentStep == Step.FirstStrikeDamage);
		_currentPhase = phase;
		_currentStep = step;
		_currentIcon = PhaseIcons.FirstOrDefault((PhaseLadderButton p) => p.Phase == phase && p.isActiveAndEnabled && (p.Step == step || p.Step == Step.None) && (p.StopOnlyThisPlayer == _activePlayer || p.StopOnlyThisPlayer == GREPlayerNum.Invalid));
		if (_currentIcon != null)
		{
			_currentIcon.Lit = true;
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_phaseladder_flip, gameObject);
		}
	}

	private void UpdateActivePlayer(GREPlayerNum player)
	{
		_activePlayer = player;
		foreach (PhaseLadderButton phaseIcon in PhaseIcons)
		{
			if (phaseIcon is ButtonPhaseIcon)
			{
				phaseIcon.SetActivePlayer(player);
			}
		}
		AudioManager.SetRTPCValue("me_or_them", (player == GREPlayerNum.LocalPlayer) ? 0f : 100f);
	}

	public void ToggleTransientStop(PhaseLadderButton phaseIcon)
	{
		List<Stop> list = new List<Stop>();
		SettingScope settingScope = ToSettingScope(_activePlayer);
		SettingScope settingScope2 = ToSettingScope(_activePlayer.GetOpposite());
		switch (PhaseLadderUtility.GetToggleResults((phaseIcon is AvatarPhaseIcon) ? phaseIcon.StopOnlyThisPlayer : GREPlayerNum.Invalid, phaseIcon.StopState, PhaseIsInNextTurn((phase: _currentTurnInfo.Phase, step: _currentTurnInfo.Step), (phase: phaseIcon.Phase, step: phaseIcon.Step)), phaseIcon.Phase == Phase.Combat && phaseIcon.Step.IsCombatStep(), phaseIcon.AppliesTo, settingScope2, settingScope))
		{
		case ToggleResults.ClearAll:
			foreach (StopType item in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.LocalPlayer))
			{
				list.Add(new Stop
				{
					StopType = item,
					Status = SettingStatus.Clear,
					AppliesTo = SettingScope.Team
				});
			}
			foreach (StopType item2 in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.Opponent))
			{
				list.Add(new Stop
				{
					StopType = item2,
					Status = SettingStatus.Clear,
					AppliesTo = SettingScope.Opponents
				});
			}
			break;
		case ToggleResults.SetTeamStops:
			foreach (StopType item3 in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.LocalPlayer))
			{
				list.Add(new Stop
				{
					StopType = item3,
					Status = SettingStatus.Set,
					AppliesTo = SettingScope.Team
				});
			}
			break;
		case ToggleResults.ClearTeamStops:
			foreach (StopType item4 in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.LocalPlayer))
			{
				list.Add(new Stop
				{
					StopType = item4,
					Status = SettingStatus.Clear,
					AppliesTo = SettingScope.Team
				});
			}
			break;
		case ToggleResults.SetOpponentStops:
			foreach (StopType item5 in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.Opponent))
			{
				list.Add(new Stop
				{
					StopType = item5,
					Status = SettingStatus.Set,
					AppliesTo = SettingScope.Opponents
				});
			}
			break;
		case ToggleResults.ClearOpponentStops:
			foreach (StopType item6 in phaseIcon.GetStopTypesForPlayer(GREPlayerNum.Opponent))
			{
				list.Add(new Stop
				{
					StopType = item6,
					Status = SettingStatus.Clear,
					AppliesTo = SettingScope.Opponents
				});
			}
			break;
		case ToggleResults.SetNextPlayerStops:
			foreach (StopType item7 in phaseIcon.GetStopTypesForPlayer(_activePlayer.GetOpposite()))
			{
				list.Add(new Stop
				{
					StopType = item7,
					AppliesTo = settingScope2,
					Status = SettingStatus.Set
				});
			}
			break;
		case ToggleResults.ClearNextPlayerStops:
			foreach (StopType item8 in phaseIcon.GetStopTypesForPlayer(_activePlayer.GetOpposite()))
			{
				list.Add(new Stop
				{
					StopType = item8,
					AppliesTo = settingScope2,
					Status = SettingStatus.Clear
				});
			}
			break;
		case ToggleResults.SetActivePlayerStops:
			foreach (StopType item9 in phaseIcon.GetStopTypesForPlayer(_activePlayer))
			{
				list.Add(new Stop
				{
					StopType = item9,
					AppliesTo = settingScope,
					Status = SettingStatus.Set
				});
			}
			break;
		case ToggleResults.ClearActivePlayerStops:
			foreach (StopType item10 in phaseIcon.GetStopTypesForPlayer(_activePlayer))
			{
				list.Add(new Stop
				{
					StopType = item10,
					AppliesTo = settingScope,
					Status = SettingStatus.Clear
				});
			}
			break;
		}
		if (list.Count > 0)
		{
			_gre.SetSettings(new SettingsMessage
			{
				TransientStops = { (IEnumerable<Stop>)list }
			});
			SetTransientStops(phaseIcon, list);
			DisplayTooltipForPhaseIcon(phaseIcon);
		}
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_phaseladder_click, gameObject);
	}

	public void HintNextPhase(MTGALocalizedString nextPhaseTooltip)
	{
		if (nextPhaseTooltip != null && !string.IsNullOrEmpty(nextPhaseTooltip.Key))
		{
			ButtonPhaseContext.SetText(nextPhaseTooltip);
			ButtonPhaseContext.Show(visible: true);
		}
	}

	public void HintNextPhase(Phase nextPhase, Step nextStep, bool highlight = false)
	{
		foreach (PhaseLadderButton highlightIcon in _highlightIcons)
		{
			highlightIcon.Highlight = false;
		}
		_highlightIcons.Clear();
		if (nextPhase == Phase.None)
		{
			ButtonPhaseContext.Show(visible: false);
			return;
		}
		if (highlight)
		{
			foreach (PhaseLadderButton phaseIcon in PhaseIcons)
			{
				if (phaseIcon.isActiveAndEnabled && phaseIcon.Phase == nextPhase && (phaseIcon.Step == nextStep || phaseIcon.Step == Step.None) && (phaseIcon.StopOnlyThisPlayer == _activePlayer || phaseIcon.StopOnlyThisPlayer == GREPlayerNum.Invalid))
				{
					_highlightIcons.Add(phaseIcon);
					phaseIcon.Highlight = true;
				}
			}
		}
		MTGALocalizedString mTGALocalizedString = _promptDataByPhase.Find((PhaseData p) => p.Phase == nextPhase && p.Step == nextStep && p.Player == _activePlayer)?.NextPhaseTooltip;
		if (mTGALocalizedString != null && !string.IsNullOrEmpty(mTGALocalizedString.Key))
		{
			ButtonPhaseContext.SetText(mTGALocalizedString);
			ButtonPhaseContext.Show(visible: true);
		}
		else
		{
			ButtonPhaseContext.Show(visible: false);
		}
	}

	public void DisplayTooltipForPhaseIcon(PhaseLadderButton phaseIcon)
	{
		if (_tooltipSystem != null)
		{
			MTGALocalizedString mTGALocalizedString = TooltipTextForIcon(phaseIcon);
			if (!string.IsNullOrEmpty(mTGALocalizedString?.Key))
			{
				TooltipProperties properties = ((phaseIcon is AvatarPhaseIcon) ? _primaryTooltipProperties : _secondaryTooltipProperties);
				_tooltipData.Text = mTGALocalizedString.ToString();
				_tooltipSystem.DisplayTooltip(phaseIcon.gameObject, _tooltipData, properties);
			}
		}
	}

	private MTGALocalizedString TooltipTextForIcon(PhaseLadderButton phaseIcon)
	{
		if (phaseIcon == null)
		{
			return string.Empty;
		}
		if (!phaseIcon.AllowStop)
		{
			return phaseIcon.PhaseTooltip;
		}
		SettingScope settingScope = ToSettingScope(_activePlayer);
		SettingScope settingScope2 = ToSettingScope(_activePlayer.GetOpposite());
		switch (PhaseLadderUtility.GetToggleResults((phaseIcon is AvatarPhaseIcon) ? phaseIcon.StopOnlyThisPlayer : GREPlayerNum.Invalid, phaseIcon.StopState, PhaseIsInNextTurn((phase: _currentTurnInfo.Phase, step: _currentTurnInfo.Step), (phase: phaseIcon.Phase, step: phaseIcon.Step)), phaseIcon.Phase == Phase.Combat && phaseIcon.Step.IsCombatStep(), phaseIcon.AppliesTo, settingScope2, settingScope))
		{
		case ToggleResults.None:
			return phaseIcon.PhaseTooltip;
		case ToggleResults.ClearTeamStops:
		case ToggleResults.ClearOpponentStops:
		case ToggleResults.ClearAll:
		case ToggleResults.ClearNextPlayerStops:
		case ToggleResults.ClearActivePlayerStops:
			return "DuelScene/PhaseLadder/PhaseStop/RemoveStop";
		case ToggleResults.SetTeamStops:
			return phaseIcon.PlayerStopTooltip;
		case ToggleResults.SetOpponentStops:
			return phaseIcon.OpponentStopTooltip;
		case ToggleResults.SetActivePlayerStops:
			return settingScope switch
			{
				SettingScope.Team => phaseIcon.PlayerStopTooltip, 
				SettingScope.Opponents => phaseIcon.OpponentStopTooltip, 
				_ => (MTGALocalizedString)string.Empty, 
			};
		case ToggleResults.SetNextPlayerStops:
			return settingScope2 switch
			{
				SettingScope.Team => phaseIcon.PlayerStopNextTooltip, 
				SettingScope.Opponents => phaseIcon.OpponentStopNextTooltip, 
				_ => (MTGALocalizedString)string.Empty, 
			};
		default:
			return phaseIcon.PhaseTooltip;
		}
	}

	private void SetTransientStops(IReadOnlyList<Stop> transientStops)
	{
		foreach (PhaseLadderButton phaseIcon in PhaseIcons)
		{
			SetTransientStops(phaseIcon, transientStops);
		}
	}

	private void SetTransientStops(PhaseLadderButton phaseIcon, IEnumerable<Stop> transientStops)
	{
		if (phaseIcon is AvatarPhaseIcon)
		{
			SettingScope settingScope = ToSettingScope(phaseIcon.StopOnlyThisPlayer);
			IReadOnlyList<StopType> stopTypesForPlayer = phaseIcon.GetStopTypesForPlayer(phaseIcon.StopOnlyThisPlayer);
			SettingStatus stopState = (IsStopSet(transientStops, stopTypesForPlayer, settingScope) ? SettingStatus.Set : SettingStatus.Clear);
			phaseIcon.SetStop(settingScope, stopState);
		}
		else
		{
			int localPlayer = (IsStopSet(transientStops, phaseIcon.GetStopTypesForPlayer(GREPlayerNum.LocalPlayer), SettingScope.Team) ? 1 : 2);
			SettingStatus opponent = (IsStopSet(transientStops, phaseIcon.GetStopTypesForPlayer(GREPlayerNum.Opponent), SettingScope.Opponents) ? SettingStatus.Set : SettingStatus.Clear);
			SettingStatus stopState2 = CombinedSettingStatus((SettingStatus)localPlayer, opponent);
			SettingScope stopScope = CombinedAppliesTo((SettingStatus)localPlayer, opponent);
			phaseIcon.SetStop(stopScope, stopState2);
		}
	}

	private static SettingStatus CombinedSettingStatus(SettingStatus localPlayer, SettingStatus opponent)
	{
		if (localPlayer != SettingStatus.Set && opponent != SettingStatus.Set)
		{
			return SettingStatus.Clear;
		}
		return SettingStatus.Set;
	}

	private static SettingScope CombinedAppliesTo(SettingStatus localPlayer, SettingStatus opponent)
	{
		if (localPlayer == SettingStatus.Set && opponent == SettingStatus.Set)
		{
			return SettingScope.AnyPlayer;
		}
		if (localPlayer == SettingStatus.Set)
		{
			return SettingScope.Team;
		}
		if (opponent == SettingStatus.Set)
		{
			return SettingScope.Opponents;
		}
		return SettingScope.None;
	}

	private static bool IsStopSet(IEnumerable<Stop> stops, IEnumerable<StopType> stopTypes, SettingScope appliesTo)
	{
		foreach (Stop item in stops ?? Array.Empty<Stop>())
		{
			if (item.AppliesTo != appliesTo)
			{
				continue;
			}
			foreach (StopType item2 in stopTypes ?? Array.Empty<StopType>())
			{
				if (item.StopType == item2 && item.Status == SettingStatus.Set)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static SettingScope ToSettingScope(GREPlayerNum player)
	{
		return player switch
		{
			GREPlayerNum.LocalPlayer => SettingScope.Team, 
			GREPlayerNum.Teammate => SettingScope.Team, 
			GREPlayerNum.Opponent => SettingScope.Opponents, 
			_ => SettingScope.None, 
		};
	}

	private static bool PhaseIsInNextTurn((Phase phase, Step step) current, (Phase phase, Step step) other)
	{
		if (current.phase != other.phase)
		{
			return other.phase < current.phase;
		}
		return other.step.IsEarlierThan(current.step);
	}

	private void UpdateFullControl(bool fullControl)
	{
		_fullControl = fullControl;
		_iconMain.SetBool("CombatOnly", !AlwaysOn && !_fullControl);
		if (!InTutorial && !MDNPlayerPrefs.SeenPhaseLadderHint)
		{
			MDNPlayerPrefs.SeenPhaseLadderHint = true;
			_toggleHint.UpdateActive(active: false);
		}
	}

	private void OnDestroy()
	{
		_gameManager = null;
		_gameStateProvider = NullGameStateProvider.Default;
		if (_turnInfoProvider != NullTurnInfoProvider.Default)
		{
			_turnInfoProvider.TurnInfo.ValueUpdated -= OnTurnInfoUpdated;
			_turnInfoProvider = NullTurnInfoProvider.Default;
		}
		if (_settingsProvider != NullSettingsMessageProvider.Default)
		{
			_settingsProvider.Settings.ValueUpdated -= OnSettingsUpdated;
			_settingsProvider = NullSettingsMessageProvider.Default;
		}
		_gre = null;
		_tooltipSystem = null;
	}
}
