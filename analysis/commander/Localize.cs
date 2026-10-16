using System.Collections.Generic;
using MTGA.Loc;
using TMPro;
using UnityEngine;
using Wizards.Mtga;

namespace Wotc.Mtga.Loc;

[AddComponentMenu("MTGA/Localization/MTGA Localize")]
public class Localize : MonoBehaviour
{
	public MTGALocalizable TextTarget;

	public MTGALocalizable FontTarget;

	public List<MTGALocalizable> ChildTargets = new List<MTGALocalizable>();

	public string storedMaterialKey;

	public bool LocalizeWhenDisabled;

	private string _fallbackText = "";

	private void Awake()
	{
		FixFieldReferences();
	}

	private void OnEnable()
	{
		Languages.LanguageChangedSignal.Listeners -= DoLocalize;
		Languages.LanguageChangedSignal.Listeners += DoLocalize;
		DoLocalize();
	}

	private void OnDisable()
	{
		if (!LocalizeWhenDisabled)
		{
			Languages.LanguageChangedSignal.Listeners -= DoLocalize;
		}
	}

	public void DoLocalize()
	{
		IClientLocProvider clientLocProvider = Pantry.Get<IClientLocProvider>();
		if (clientLocProvider == null)
		{
			Debug.LogError("Attempting to localize fields on object '" + name + "' but there was no active IClientLocProvider!");
			return;
		}
		IFontProvider fontProvider = Pantry.Get<IFontProvider>();
		if (fontProvider == null)
		{
			Debug.LogError("Attempting to localize fields on object '" + name + "' but there was no active IFontProvider!");
			return;
		}
		TextTarget.Localize(clientLocProvider, fontProvider, _fallbackText);
		FontTarget.Localize(clientLocProvider, fontProvider, "", storedMaterialKey);
		foreach (MTGALocalizable childTarget in ChildTargets)
		{
			childTarget.Localize(clientLocProvider, fontProvider);
		}
	}

	public void SetText(MTGALocalizedString mtgaString)
	{
		if (mtgaString != null)
		{
			TextTarget.active = true;
			TextTarget.locKey = string.Empty;
			TextTarget.locParams = null;
			TextTarget.LocalizedString = mtgaString;
			DoLocalize();
		}
	}

	public void SetText(string key, List<MTGALocalizable.LocParam> parameters = null, string fallbackText = "")
	{
		TextTarget.active = true;
		TextTarget.locKey = key;
		TextTarget.locParams = parameters;
		TextTarget.LocalizedString = null;
		_fallbackText = fallbackText;
		DoLocalize();
	}

	public void SetText(string key, Dictionary<string, string> parameters)
	{
		TextTarget.active = true;
		TextTarget.locKey = key;
		TextTarget.LocalizedString = null;
		if (parameters != null)
		{
			List<MTGALocalizable.LocParam> list = new List<MTGALocalizable.LocParam>();
			foreach (KeyValuePair<string, string> parameter in parameters)
			{
				list.Add(new MTGALocalizable.LocParam
				{
					key = parameter.Key,
					value = parameter.Value
				});
			}
			TextTarget.locParams = list;
		}
		DoLocalize();
	}

	public void FixFieldReferences()
	{
		if (TextTarget == null)
		{
			TextTarget = new MTGALocalizable();
		}
		if (FontTarget == null)
		{
			FontTarget = new MTGALocalizable();
		}
		TextTarget.targetType = MTGALocalizable.TargetType.Text;
		TextTarget.serializedCmp = GetComponent<TMP_Text>();
		FontTarget.targetType = MTGALocalizable.TargetType.Font;
		FontTarget.serializedCmp = TextTarget.serializedCmp;
	}
}
