using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using UnityEngine;
using Newtonsoft.Json;

using KSP.Sim.impl;
using KSP.Game;
using KSP.Sim.ResourceSystem;
using KSP.UI.Binding;

using BepInEx;
using SpaceWarp;
using SpaceWarp.API;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Assets;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;


namespace LazyOrbit
{
    [BepInDependency(SpaceWarpPlugin.ModGuid,SpaceWarpPlugin.ModVer)]
    [BepInPlugin(ModGuid, ModName, ModVer)]
    public class LazyOrbit : BaseSpaceWarpPlugin
    {
        public const string ModGuid = "com.github.halbann.lazyorbit";
        public const string ModName = "Lazy Orbit";
        public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

        #region Fields

        // Main.
        public static bool loaded = false;
        public static LazyOrbit instance;

        // Paths.
        private static string _assemblyFolder;
        private static string AssemblyFolder =>
            _assemblyFolder ?? (_assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        private static string _settingsPath;
        private static string SettingsPath =>
            _settingsPath ?? (_settingsPath = Path.Combine(AssemblyFolder, "settings.json"));

        // GUI.
        private static bool guiLoaded = false;
        private bool drawUI = false;
        private Rect windowRect;
        private int windowWidth = 500;
        private int windowHeight = 700;
        private static GUIStyle boxStyle, errorStyle, warnStyle, peStyle, apStyle;
        private static Vector2 scrollPositionBodies;
        private static Vector2 scrollPositionVessels;
        private static Color labelColor;
        private static GameState[] validScenes = new[] { GameState.FlightView, GameState.Map3DView };

        private static bool ValidScene => validScenes.Contains(GameManager.Instance.Game.GlobalGameState.GetState());

        // Orbit.
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

        // Body selection.
        private string selectedBody = "Kerbin";
        private List<string> bodies;
        private bool selectingBody = false;

        // Rendezvous.
        private static VesselComponent activeVessel;
        private static VesselComponent target;
        private static List<VesselComponent> allVessels;
        private static bool selectingVessel = false;
        private static float rendezvousDistance = 100f;
        private static string rendezvousDistanceString = rendezvousDistance.ToString();

        // Landing.
        private static float latitude = -0.65f;
        private static float longitude = 285f;
        private static float height = 5f;
        private static string latitudeString = latitude.ToString();
        private static string longitudeString = longitude.ToString();
        private static string heightString = height.ToString();

        // Interface modes.
        private static InterfaceMode interfaceMode = InterfaceMode.Simple;
        private static string[] interfaceModes = { "Simple", "Advanced", "Landing", "Rendezvous" };

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

        #endregion

        #region Main

        public override void OnInitialized()
        {
            if (loaded)
            {
                Destroy(this);
            }

            loaded = true;
            instance = this;

            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);

            interfaceMode = GetDefaultMode();

            Appbar.RegisterAppButton(
                "Lazy Orbit",
                "BTN-LazyOrbitButton",
                AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
                ToggleButton);
        }

        void Awake()
        {
            windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H) && ValidScene)
                ToggleButton(!drawUI);
        }

        void OnGUI()
        {
            if (drawUI && ValidScene)
            {
                if (!guiLoaded)
                    GetStyles();

                GUI.skin = Skins.ConsoleSkin;

                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "<color=#696DFF>// LAZY ORBIT</color>",
                    GUILayout.Height(0),
                    GUILayout.Width(350));
            }
        }

        void ToggleButton(bool toggle)
        {
            drawUI = toggle;
            GameObject.Find("BTN-LazyOrbitButton")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(toggle);
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

            guiLoaded = true;
        }

        private void FillWindow(int windowID)
        {
            if ((activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel()) == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No active vessel.", errorStyle);
                GUILayout.FlexibleSpace();
                return;
            }

            if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), "x"))
                ToggleButton(false);

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
            bool success = true;

            TextField("Altitude (km):", ref altitudeString, ref altitudeKM, ref success);
            BodySelectionGUI();

            ConditionalButton("Set Orbit", success, SetOrbit);
        }


        void LandingGUI()
        {
            bool success = true;

            TextField("Latitude (Degrees):", ref latitudeString, ref latitude, ref success);
            TextField("Longitude (Degrees):", ref longitudeString, ref longitude, ref success);
            TextField("Height (m):", ref heightString, ref height, ref success);
            BodySelectionGUI();

            ConditionalButton("Land", success, Land);
        }

        void AdvancedGUI()
        {
            bool success = true;

            TextField("Semi-Major Axis (km):", ref semiMajorAxisString, ref semiMajorAxisKM, ref success);

            bodyRadius = GameManager.Instance.Game.CelestialBodies.GetRadius(selectedBody) / 1000f;
            apKM = (semiMajorAxisKM * (1 + eccentricity) - bodyRadius);
            peKM = (semiMajorAxisKM * (1 - eccentricity) - bodyRadius);
            apStyle.normal.textColor = apKM < 1 ? warnStyle.normal.textColor : labelColor;
            peStyle.normal.textColor = peKM < 1 ? warnStyle.normal.textColor : labelColor;

            GUILayout.BeginHorizontal();
            GUILayout.Label("AP (km): ", apStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.Label(apKM.ToString("n2"), apStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.Label("PE (km): ", peStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.Label(peKM.ToString("n2"), peStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.EndHorizontal();

            TextField("Inclination (Degrees):", ref inclinationString, ref inclinationDegrees, ref success);
            TextField("Eccentricity:", ref eccentricityString, ref eccentricity, ref success);

            if (eccentricity >= 1 || eccentricity < 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Eccentricity must be between 0 and 1.", errorStyle);
                GUILayout.EndHorizontal();
            }

            TextField("Longitude of Ascending Node (Degrees):", ref ascendingNodeString, ref ascendingNode, ref success);
            TextField("Argument of Periapsis (Degrees):", ref argOfPeriapsisString, ref argOfPeriapsis, ref success);

            BodySelectionGUI();

            ConditionalButton("Set Orbit", success, SetOrbit);
        }

        void RendezvousGUI()
        {
            bool success = true;

            allVessels = GameManager.Instance.Game.SpaceSimulation.UniverseModel.GetAllVessels();
            allVessels.Remove(activeVessel);
            allVessels.RemoveAll(v => v.IsDebris());

            if (allVessels.Count < 1)
            {
                GUILayout.Label("No other vessels.");
                return;
            }

            if (target == null)
                target = allVessels.First();

            TextField("Distance (m):", ref rendezvousDistanceString, ref rendezvousDistance, ref success);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target: ", GUILayout.Width(0));
            if (!selectingVessel)
            {
                if (GUILayout.Button(target.DisplayName))
                    selectingVessel = true;
            }
            else
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionVessels = GUILayout.BeginScrollView(scrollPositionVessels, false, true, GUILayout.Height(150));
                foreach (VesselComponent vessel in allVessels)
                {
                    if (GUILayout.Button(vessel.DisplayName))
                    {
                        target = vessel;
                        selectingVessel = false;
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            ConditionalButton("Rendezvous", success, Rendezvous);
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
                scrollPositionBodies = GUILayout.BeginScrollView(scrollPositionBodies, false, true, GUILayout.Height(150));
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

        void TextField(string label, ref string field, ref float number, ref bool success)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(windowWidth / 2));

            bool parsed = float.TryParse(field, out number);
            if (!parsed) GUI.color = Color.red;
            field = GUILayout.TextField(field);
            GUI.color = Color.white;

            if (success && !parsed)
                success = false;

            GUILayout.EndHorizontal();
        }

        void ConditionalButton(string text, bool condition, Action pressed)
        {
            GUI.enabled = condition;

            if (GUILayout.Button(text))
                pressed();

            GUI.enabled = true;
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
                    activeVessel.Guid,
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
                    activeVessel.Guid,
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

        void Rendezvous()
        {
            GameInstance game = GameManager.Instance.Game;

            if (target.Guid == activeVessel.Guid)
                return;

            game.SpaceSimulation.Lua.TeleportToRendezvous(
                activeVessel.Guid,
                target.Guid,
                rendezvousDistance,
                0, 0, 0, 0, 0);
        }

        void Land()
        {
            GameInstance game = GameManager.Instance.Game;

            game.SpaceSimulation.Lua.TeleportToSurface(
                activeVessel.Guid,
                selectedBody,
                height,
                latitude,
                longitude,
                0);
        }

        #endregion

        #region Settings

        private void SaveDefaultMode(InterfaceMode mode)
        {
            LazyOrbitSettings settings = new LazyOrbitSettings()
            {
                defaultMode = mode
            };

            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings));
        }

        private InterfaceMode GetDefaultMode()
        {
            LazyOrbitSettings settings;
            try
            {
                settings = JsonConvert.DeserializeObject<LazyOrbitSettings>(File.ReadAllText(SettingsPath));
            }
            catch (FileNotFoundException)
            {
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