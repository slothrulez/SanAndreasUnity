using System.Collections.Generic;
using UnityEngine;
using SanAndreasUnity.Behaviours;
using UGameCore.Utilities;

namespace SanAndreasUnity.UI
{
	
	public class MainMenu : MonoBehaviour {

		public static MainMenu Instance { get; private set; }

		public MenuBar menuBar;

		public Color openedWindowTextColor = Color.green;
		public Color ClosedWindowTextColor => this.menuBar.DefaultMenuEntryTextColor;

		public Canvas canvas;

		public const string KochiPrototypeSceneName = "KochiPrototype";



		void Awake()
		{
			if (null == Instance)
				Instance = this;

			// add Exit button
			this.menuBar.RegisterMenuEntry("Exit", int.MaxValue, () => GameManager.ExitApplication());

			// Kochi prototype runs on its own placeholder content and doesn't need the GTA loader
			this.menuBar.RegisterMenuEntry("Kochi Prototype", int.MaxValue - 1, () => UnityEngine.SceneManagement.SceneManager.LoadScene(KochiPrototypeSceneName));
			this.menuBar.RegisterMenuEntry("Play Kochi", int.MaxValue - 2, SanAndreasUnity.Kochi.KochiContent.StartGame);
		}

		void OnSceneChanged(SceneChangedMessage sceneChangedMessage)
		{
			this.canvas.enabled = GameManager.IsInStartupScene;
		}

	}

}
