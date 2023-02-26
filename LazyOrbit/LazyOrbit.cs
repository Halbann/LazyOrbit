using UnityEngine;
using KSP.Game;
using System.Collections.Generic;
using System.Linq;
using SpaceWarp.API.Mods;
using System.IO;
using System.Reflection;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using KSP.Sim.impl;

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
        private static GUIStyle boxStyle, errorStyle, warnStyle, peStyle, apStyle;
        private static Vector2 scrollPositionBodies;
        private static Color labelColor;

        private static float altitudeKM = 100;
        private static float semiMajorAxisKM = 700;
        private static float inclinationDegrees = 0;
        private static float eccentricity = 0;
        private static float ascendingNode = 0;
        private static float argOfPeriapsis = 0;
        private static double bodyRadius = 600000;
        private static double apKM, peKM;

        private static string altitudeString = altitudeKM.ToString();
        private static string semiMajorAxisString = semiMajorAxisKM.ToString();
        private static string inclinationString = inclinationDegrees.ToString();
        private static string eccentricityString = eccentricity.ToString();
        private static string ascendingNodeString = ascendingNode.ToString();
        private static string argOfPeriapsisString = argOfPeriapsis.ToString();

        private string selectedBody = "Kerbin";
        private List<string> bodies;
        private bool selectingBody = false;

        private static VesselComponent activeVessel;

        private static InterfaceMode interfaceMode = InterfaceMode.Simple;
        private static string[] interfaceModes = { "Simple", "Advanced", "Landing", "Rendezvous" };
        private static string settingsPath;

        private InterfaceMode CurrentInterfaceMode
        {
            get => interfaceMode;
            set
            {
                if (value == interfaceMode) return;

                interfaceMode = value;
                if (new[] { InterfaceMode.Simple, InterfaceMode.Advanced }.Contains(interfaceMode))
                    SaveDefaultMode(interfaceMode);
            }
        }

        #region Main

        void Awake()
        {
            windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

        public override void Initialize()
        {
            if (loaded)
            {
                Destroy(this);
            }
            loaded = true;

            interfaceMode = GetDefaultMode();
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H))
                drawUI = !drawUI;
        }

        void OnGUI()
        {
            //activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();

            if (drawUI && (activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel()) != null)
            {
                if (boxStyle == null)
                    GetStyles();

                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "Lazy Orbit",
                    GUILayout.Height(0),
                    GUILayout.Width(350));
            }
        }

        #endregion

        #region GUI

        private void GetStyles()
        {
            if (boxStyle != null)
                return;

            boxStyle = GUI.skin.GetStyle("Box");
            errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            warnStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            apStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            peStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            errorStyle.normal.textColor = Color.red;
            warnStyle.normal.textColor = Color.yellow;
            labelColor = GUI.skin.GetStyle("Label").normal.textColor;
        }

        private void FillWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label($"Active Vessel: {activeVessel.DisplayName}");

            // Mode selection.
            GUILayout.BeginHorizontal();
            CurrentInterfaceMode = (InterfaceMode)GUILayout.SelectionGrid((int)CurrentInterfaceMode, interfaceModes, 4);
            GUILayout.EndHorizontal();

            // Draw one of the modes.
            switch (CurrentInterfaceMode)
            {
                case InterfaceMode.Simple: SimpleGUI(); break;
                case InterfaceMode.Advanced: AdvancedGUI(); break;
                case InterfaceMode.Landing: LandingGUI(); break;
                case InterfaceMode.Rendezvous: RendezvousGUI(); break;
                default:
                    break;
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        void SimpleGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Altitude (km): ", GUILayout.Width(windowWidth / 2));
            altitudeString = GUILayout.TextField(altitudeString);
            float.TryParse(altitudeString, out altitudeKM);
            GUILayout.EndHorizontal();

            BodySelectionGUI();

            if (GUILayout.Button("Set Orbit"))
                SetOrbit();
        }

        void AdvancedGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Semi-Major Axis (km): ", GUILayout.Width(windowWidth / 2));
            semiMajorAxisString = GUILayout.TextField(semiMajorAxisString);
            float.TryParse(semiMajorAxisString, out semiMajorAxisKM);
            GUILayout.EndHorizontal();

            bodyRadius = GameManager.Instance.Game.CelestialBodies.GetRadius(selectedBody) / 1000f;
            apKM = (semiMajorAxisKM * (1 + eccentricity) - bodyRadius);
            peKM = (semiMajorAxisKM * (1 - eccentricity) - bodyRadius);
            apStyle.normal.textColor = apKM < 1 ? warnStyle.normal.textColor : labelColor;
            peStyle.normal.textColor = peKM < 1 ? warnStyle.normal.textColor : labelColor;
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

            BodySelectionGUI();

            if (GUILayout.Button("Set Orbit"))
                SetOrbit();
        }

        void LandingGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("This mode is not yet implemented.", warnStyle);
            GUILayout.EndHorizontal();
        }

        void RendezvousGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("This mode is not yet implemented.", warnStyle);
            GUILayout.EndHorizontal();
        }

        // Draws the body selection GUI.
        void BodySelectionGUI()
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

        #endregion

        #region Functions

        // Sets vessel orbit according to the current interfaceMode.
        void SetOrbit()
        {
            GameInstance game = GameManager.Instance.Game;

            if (CurrentInterfaceMode == InterfaceMode.Simple)
            {
                // Set orbit using just altitude.
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
            else
            {
                // Set orbit using semi-major axis and other orbital parameters.
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

        #endregion

        #region Settings

        private void SaveDefaultMode(InterfaceMode mode)
        {
            if (settingsPath == null)
                return;

            LazyOrbitSettings settings = new LazyOrbitSettings()
            {
                defaultMode = mode
            };

            File.WriteAllText(settingsPath, JsonConvert.SerializeObject(settings));
        }

        private InterfaceMode GetDefaultMode()
        {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            settingsPath = Path.Combine(assemblyFolder, "Settings.json");

            LazyOrbitSettings settings;
            try
            {
                settings = JsonConvert.DeserializeObject<LazyOrbitSettings>(File.ReadAllText(settingsPath));
            }
            catch (FileNotFoundException)
            {
                Logger.Info("Creating a new LazyOrbit settings file.");
                settings = new LazyOrbitSettings();
            }

            return settings.defaultMode;
        }

        #endregion
    }

    public class LazyOrbitSettings
    {
        public InterfaceMode defaultMode = InterfaceMode.Simple;
    }

    public enum InterfaceMode
    {
        Simple,
        Advanced,
        Landing,
        Rendezvous,
    }
}
