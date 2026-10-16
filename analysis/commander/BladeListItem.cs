using System;
using UnityEngine;
using Wotc.Mtga.Loc;

namespace Wizards.Mtga.PlayBlade;

public class BladeListItem : MonoBehaviour
{
	[SerializeField]
	private CustomButton customButton;

	[SerializeField]
	private Localize _text;

	[SerializeField]
	private Animator _animator;

	[SerializeField]
	private GameObject _questBadge;

	private bool _toggleState;

	private Action Clicked;

	private string Anim_DisplayState_Int = "DisplayState";

	private string Anim_Attract_Bool = "Attract";

	private void Awake()
	{
		customButton.OnClick.AddListener(OnButtonClicked);
		customButton.OnMouseover.AddListener(OnButtonHovered);
		ResetToggle();
	}

	private void OnDestroy()
	{
		Clicked = null;
		customButton.OnClick.RemoveListener(OnButtonClicked);
		customButton.OnMouseover.RemoveListener(OnButtonHovered);
	}

	public void SetOnClick(Action action)
	{
		Clicked = action;
	}

	public void SetText(string key)
	{
		_text.SetText(key);
	}

	public void SetAttract(bool state)
	{
		_animator.SetBool(Anim_Attract_Bool, state);
	}

	public void ResetToggle()
	{
		_animator.SetBool(Anim_Attract_Bool, value: false);
		SetToggleValue(state: false);
	}

	private void OnButtonClicked()
	{
		SetToggleValue(!_toggleState);
		Clicked?.Invoke();
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_generic_click, gameObject);
	}

	private void OnButtonHovered()
	{
		AudioManager.PlayAudio(WwiseEvents.sfx_ui_main_rollover, gameObject);
	}

	public void SetToggleValue(bool state)
	{
		if (_toggleState != state)
		{
			_toggleState = state;
			int value = (_toggleState ? 1 : 0);
			_animator.SetInteger(Anim_DisplayState_Int, value);
		}
	}

	public void SetQuestBadgeDisplayed(bool displayBadge)
	{
		_questBadge.SetActive(displayBadge);
	}
}
