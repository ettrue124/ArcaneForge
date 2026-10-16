using System;
using GreClient.Rules;
using UnityEngine;

namespace Wotc.Mtga.DuelScene;

public class MatchRenderer : IDisposable
{
	private readonly HeadlessClient _hc;

	private readonly ImGUIMatchView _matchView;

	private readonly RequestDurationRenderer _requestDurationRenderer;

	private static GUIStyle _lineStyle;

	public MatchRenderer(HeadlessClient headlessClient, ImGUIMatchView matchView)
	{
		_hc = headlessClient;
		_matchView = matchView;
		_requestDurationRenderer = new RequestDurationRenderer(headlessClient);
	}

	public void Render()
	{
		GUILayout.BeginVertical();
		RenderGameState(_hc.GameState);
		_requestDurationRenderer.Render();
		RenderLine();
		GUILayout.BeginVertical();
		_matchView.Render();
		GUILayout.EndVertical();
		GUILayout.EndVertical();
	}

	private static void RenderGameState(MtgGameState gameState)
	{
		if (gameState == null)
		{
			return;
		}
		GUILayout.BeginHorizontal(GUI.skin.box);
		foreach (MtgPlayer player in gameState.Players)
		{
			RenderPlayer(player);
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal(GUI.skin.box);
		GUILayout.Label("Turn " + gameState.GameWideTurn + " - " + gameState.CurrentPhase);
		GUILayout.EndHorizontal();
	}

	private static void RenderPlayer(MtgPlayer player)
	{
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.Label(string.Format("{0} Id: {1}", player.IsLocalPlayer ? "LocalPlayer" : "Opponent", player.InstanceId));
		GUILayout.Label($"Life Total: {player.LifeTotal}");
		GUILayout.Label("Mana Pool: " + player.ManaPoolString);
		GUILayout.EndVertical();
	}

	public static void RenderLine(float thickness = 1f)
	{
		if (_lineStyle == null)
		{
			_lineStyle = new GUIStyle();
			_lineStyle.normal.background = Texture2D.grayTexture;
			_lineStyle.border = new RectOffset(0, 0, 1, 1);
			_lineStyle.margin = new RectOffset(0, 0, 4, 4);
			_lineStyle.padding = new RectOffset(0, 0, 0, 0);
		}
		GUILayout.Box("", _lineStyle, GUILayout.Height(thickness));
	}

	public void Dispose()
	{
		_requestDurationRenderer.Dispose();
	}
}
