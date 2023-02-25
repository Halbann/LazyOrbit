using UnityEngine;
using KSP.Game;
using System.Collections.Generic;
using System.Linq;
using SpaceWarp.API.Mods;
//using static KSP.Game.GameManager.Instance;

namespace LazyOrbit
{
    [MainMod]
    public class LazyOrbit : Mod
    {
        static bool loaded = false;
        private bool drawUI = false;
        private Rect windowRect;
        private int windowWidth = 350;
        private int windowHeight = 700;

        private static float altitudeKM = 100;
        private static string altitudeString = altitudeKM.ToString();
        private string selectedBody = "Kerbin";
        private List<string> bodies;

        private bool selectingBody = false;
        private static GUIStyle boxStyle;
        private static Vector2 scrollPositionBodies;

        public override void Initialize()
        {
            if (loaded)
            {
                Destroy(this);
            }

            loaded = true;
        }

        void Awake()
        {
            windowRect = new Rect((Screen.width * 0.85f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

        void OnGUI()
        {
            if (drawUI)
            {
                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "Lazy Orbit",
                    GUILayout.Height(0),
                    GUILayout.Width(350));
            }
        }

        void Update()
        {
            //Logger.Info("Hello?");

            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H))
                drawUI = !drawUI;
        }

        private void FillWindow(int windowID)
        {
            boxStyle = GUI.skin.GetStyle("Box");
            GUILayout.BeginVertical();

            GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude (km): ", GUILayout.Width(windowWidth / 2));
            altitudeString = GUILayout.TextField(altitudeString);
            float.TryParse(altitudeString, out altitudeKM);
            GUILayout.EndHorizontal();

            bodies = GameManager.Instance.Game.SpaceSimulation.GetBodyNameKeys().ToList();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Body: ", GUILayout.Width(windowWidth / 2));
            if (!selectingBody)
            {
                if (GUILayout.Button(selectedBody))
                    selectingBody = true;
            }
            else
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionBodies = GUILayout.BeginScrollView(scrollPositionBodies, false, true, GUILayout.Height(150), GUILayout.Width(windowWidth / 2));
                foreach (string body in bodies)
                {
                    if (GUILayout.Button(body))
                    {
                        selectedBody = body;
                        selectingBody = false;
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Set Orbit"))
                SetOrbit();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        void SetOrbit()
        {
            GameInstance game = GameManager.Instance.Game;
            game.SpaceSimulation.Lua.TeleportToOrbit(
                game.ViewController.GetActiveVehicle(true)?.Guid.ToString(), 
                selectedBody, 
                0, 
                0,
                (double)altitudeKM * 1000f + game.CelestialBodies.GetRadius(selectedBody), 
                0, 
                0, 
                0, 
                0);
        }

    }
}
