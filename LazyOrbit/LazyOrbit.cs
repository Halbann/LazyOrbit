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
        private int windowWidth = 500;
        private int windowHeight = 700;

        private static float altitudeKM = 100;
        private static float semiMajorAxisKM = 700;
        private static float inclinationDegrees = 0;
        private static float eccentricity = 0;
        private static float ascendingNode = 0;
        private static float argOfPeriapsis = 0;
        private double bodyRadius = 600000;
        private double apKM, peKM;

        private static string altitudeString = altitudeKM.ToString();
        private static string semiMajorAxisString = semiMajorAxisKM.ToString();
        private static string inclinationString = inclinationDegrees.ToString();
        private static string eccentricityString = eccentricity.ToString();
        private static string ascendingNodeString = ascendingNode.ToString();
        private static string argOfPeriapsisString = argOfPeriapsis.ToString();

        private string selectedBody = "Kerbin";
        private List<string> bodies;
        private bool selectingBody = false;

        private int interfaceMode = 0;
        private string[] interfaceModes = { "Simple", "Advanced", "Landing", "Rendezvous" };

        private static GUIStyle boxStyle, errorStyle, warnStyle, peStyle, apStyle;
        private static Vector2 scrollPositionBodies;
        private Color labelColor;

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
            //Styles for errors and AP/PE warnings
            errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            warnStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            apStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            peStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            errorStyle.normal.textColor = Color.red;
            warnStyle.normal.textColor = Color.yellow;
            labelColor = GUI.skin.GetStyle("Label").normal.textColor;

            GUILayout.BeginVertical();

            GUILayout.Label($"Active Vessel: {GameManager.Instance.Game.ViewController.GetActiveSimVessel().DisplayName}");

            GUILayout.BeginHorizontal();
            interfaceMode = GUILayout.SelectionGrid(interfaceMode, interfaceModes, 4);
            GUILayout.EndHorizontal();

            if (interfaceMode == 0)
            {
                //Simple mode
                GUILayout.BeginHorizontal();
                GUILayout.Label("Altitude (km): ", GUILayout.Width(windowWidth / 2));
                altitudeString = GUILayout.TextField(altitudeString);
                float.TryParse(altitudeString, out altitudeKM);
                GUILayout.EndHorizontal();

                drawBodySelection();

                if (GUILayout.Button("Set Orbit"))
                    SetOrbit();
            }

            else if (interfaceMode == 1)
            {
                //Advanced mode
                GUILayout.BeginHorizontal();
                GUILayout.Label("Semi-Major Axis (km): ", GUILayout.Width(windowWidth / 2));
                semiMajorAxisString = GUILayout.TextField(semiMajorAxisString);
                float.TryParse(semiMajorAxisString, out semiMajorAxisKM);
                GUILayout.EndHorizontal();

                bodyRadius = GameManager.Instance.Game.CelestialBodies.GetRadius(selectedBody) / 1000f;
                apKM = (semiMajorAxisKM * (1 + eccentricity) - bodyRadius);
                peKM = (semiMajorAxisKM * (1 - eccentricity) - bodyRadius);
                apStyle.normal.textColor = (apKM < 1) ? warnStyle.normal.textColor : labelColor;
                peStyle.normal.textColor = (peKM < 1) ? warnStyle.normal.textColor : labelColor;
                GUILayout.BeginHorizontal();
                GUILayout.Label("AP (KM): ", apStyle, GUILayout.Width(windowWidth / 4));
                GUILayout.Label(apKM.ToString("n2"), apStyle, GUILayout.Width(windowWidth / 4));
                GUILayout.Label("PE (KM): ", peStyle, GUILayout.Width(windowWidth / 4));
                GUILayout.Label(peKM.ToString("n2"), peStyle, GUILayout.Width(windowWidth / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Inclination (Degrees): ", GUILayout.Width(windowWidth / 2));
                inclinationString = GUILayout.TextField(inclinationString);
                float.TryParse(inclinationString, out inclinationDegrees);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Eccentricity: ", GUILayout.Width(windowWidth / 2));
                eccentricityString = GUILayout.TextField(eccentricityString);
                float.TryParse(eccentricityString, out eccentricity);
                GUILayout.EndHorizontal();

                if (eccentricity >= 1 || eccentricity < 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Eccentricity Must Be in Range [0,1)", errorStyle);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Longitude of Ascending Node (Degrees): ", GUILayout.Width(windowWidth / 2));
                ascendingNodeString = GUILayout.TextField(ascendingNodeString);
                float.TryParse(ascendingNodeString, out ascendingNode);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Argument of Periapsis (Degrees): ", GUILayout.Width(windowWidth / 2));
                argOfPeriapsisString = GUILayout.TextField(argOfPeriapsisString);
                float.TryParse(argOfPeriapsisString, out argOfPeriapsis);
                GUILayout.EndHorizontal();

                drawBodySelection();

                if (GUILayout.Button("Set Orbit"))
                    SetOrbit();
            }

            else
            {
                //TODO: Implement other interface modes
                GUILayout.BeginHorizontal();
                GUILayout.Label("This mode is not yet implemented.", warnStyle);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        //draws the body selection GUI
        void drawBodySelection()
        {
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
        }

        //sets vessel orbit according to the current interfaceMode
        void SetOrbit()
        {
            if (interfaceMode == 0)
            {
                //Set orbit using just altitude
                GameInstance game = GameManager.Instance.Game;
                game.SpaceSimulation.Lua.TeleportToOrbit(
                    game.ViewController.GetActiveVehicle(true)?.Guid.ToString(),
                    selectedBody,
                    0,
                    0,
                    (double)altitudeKM * 1000f + GameManager.Instance.Game.CelestialBodies.GetRadius(selectedBody),
                    0,
                    0,
                    0,
                    0);
            }
            else if (interfaceMode == 1)
            {
                //Set orbit using semi-major axis and other orbital parameters
                GameInstance game = GameManager.Instance.Game;
                game.SpaceSimulation.Lua.TeleportToOrbit(
                    game.ViewController.GetActiveVehicle(true)?.Guid.ToString(),
                    selectedBody,
                    inclinationDegrees,
                    eccentricity,
                    (double)semiMajorAxisKM * 1000f,
                    ascendingNode,
                    argOfPeriapsis,
                    0,
                    0);
            }
        }

    }
}
