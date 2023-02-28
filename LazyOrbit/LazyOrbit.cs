using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

using KSP.Sim.impl;
using KSP.Game;
using KSP.Api.CoreTypes;
using KSP.Sim.ResourceSystem;
using KSP.Api;
using KSP.UI.Flight;
using KSP.UI.Binding;

using SpaceWarp.API.Mods;
using HarmonyLib;


namespace LazyOrbit
{
    [MainMod]
    public class LazyOrbit : Mod
    {
        #region Fields

        // Main.
        public static bool loaded = false;
        private static GameObject appButton;
        public static LazyOrbit instance;

        // Paths.
        private static string _assemblyFolder;
        private static string AssemblyFolder =>
            _assemblyFolder ?? (_assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        private static string _settingsPath;
        private static string SettingsPath =>
            _settingsPath ?? (_settingsPath = Path.Combine(AssemblyFolder, "Settings.json"));

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

        void Awake()
        {
            windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

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
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H) && ValidScene)
                drawUI = !drawUI;
        }

        void OnGUI()
        {
            if (drawUI && ValidScene && (activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel()) != null)
            {
                if (!guiLoaded)
                    GetStyles();

                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "Lazy Orbit v0.3.0",
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

            guiLoaded = true;
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

        void LandingGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Latitude (Degrees):", GUILayout.Width(windowWidth / 2));
            latitudeString = GUILayout.TextField(latitudeString);
            float.TryParse(latitudeString, out latitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Longitude (Degrees):", GUILayout.Width(windowWidth / 2));
            longitudeString = GUILayout.TextField(longitudeString);
            float.TryParse(longitudeString, out longitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Height (m):", GUILayout.Width(windowWidth / 2));
            heightString = GUILayout.TextField(heightString);
            float.TryParse(heightString, out height);
            GUILayout.EndHorizontal();

            BodySelectionGUI();

            if (GUILayout.Button("Land"))
                Land();
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
            GUILayout.Label("AP (km): ", apStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.Label(apKM.ToString("n2"), apStyle, GUILayout.Width(windowWidth / 4));
            GUILayout.Label("PE (km): ", peStyle, GUILayout.Width(windowWidth / 4));
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

        void RendezvousGUI()
        {
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

            GUILayout.BeginHorizontal();
            GUILayout.Label("Distance (m): ", GUILayout.Width(windowWidth / 2));
            rendezvousDistanceString = GUILayout.TextField(rendezvousDistanceString);
            float.TryParse(rendezvousDistanceString, out rendezvousDistance);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target: ", GUILayout.Width(windowWidth / 2));
            if (!selectingVessel)
            {
                if (GUILayout.Button(target.DisplayName))
                    selectingVessel = true;
            }
            else
            {
                GUILayout.BeginVertical(boxStyle);
                scrollPositionVessels = GUILayout.BeginScrollView(scrollPositionVessels, false, true, GUILayout.Height(150), GUILayout.Width(windowWidth / 2));
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

            if (GUILayout.Button("Rendezvous"))
                Rendezvous();
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

        #region Button

        public void CreateButton()
        {
            if (appButton != null)
                return;

            StartCoroutine(CreateButtonRoutine());
        }

        private IEnumerator CreateButtonRoutine()
        {
            // I don't like this either. Pls fix.
            yield return new WaitForSeconds(1);

            appButton = AddButton("Lazy Orbit", LoadIcon(), "BTN-LazyOrbitButton", ToggleButton);
        }

        GameObject AddButton(string buttonText, Sprite buttonIcon, string buttonId, Action<bool> function)
        {
            // Find the resource manager button and "others" group.

            // Say the magic words...
            GameObject list = GameObject.Find("GameManager/Default Game Instance(Clone)/UI Manager(Clone)/Popup Canvas/Container/ButtonBar/BTN-App-Tray/appbar-others-group");
            GameObject resourceManger = list?.GetChild("BTN-Resource-Manager");

            if (list == null || resourceManger == null)
            {
                Logger.Info("Couldn't find appbar.");
                return null;
            }

            // Clone the resource manager button.
            GameObject appButton = Instantiate(resourceManger, list.transform);
            appButton.name = buttonId;

            // Change the text.
            TextMeshProUGUI text = appButton.GetChild("Content").GetChild("TXT-title").GetComponent<TextMeshProUGUI>();
            text.text = buttonText;

            // Change the icon.
            GameObject icon = appButton.GetChild("Content").GetChild("GRP-icon");
            Image image = icon.GetChild("ICO-asset").GetComponent<Image>();
            image.sprite = buttonIcon;

            // Add our function call to the toggle.
            ToggleExtended utoggle = appButton.GetComponent<ToggleExtended>();
            utoggle.onValueChanged.AddListener(state => function(state));

            // Set the initial state of the button.
            UIValue_WriteBool_Toggle toggle = appButton.GetComponent<UIValue_WriteBool_Toggle>();
            toggle.BindValue(new Property<bool>(false));

            // Bind the action to close the tray after pressing the button.
            IAction action = resourceManger.GetComponent<UIAction_Void_Toggle>().Action;
            appButton.GetComponent<UIAction_Void_Toggle>().BindAction(action);

            Logger.Info($"Added appbar button: {buttonId}");

            return appButton;
        }

        void ToggleButton(bool toggle) =>
            drawUI = toggle;

        private Sprite LoadIcon()
        {
            byte[] fileContent = File.ReadAllBytes(Path.Combine(AssemblyFolder, "icon.png"));
            Texture2D tex = new Texture2D(24, 24, TextureFormat.ARGB32, false);
            ImageConversion.LoadImage(tex, fileContent);

            return Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f));
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

    [HarmonyPatch(typeof(UIFlightHud))]
    [HarmonyPatch("Start")]
    class LazyOrbitAppBarPatcher
    {
        public static void Postfix(UIFlightHud __instance) =>
            LazyOrbit.instance.CreateButton();
    }
}

/*private GUISkin _spaceWarpConsoleSkin = null;
public virtual GUISkin Skin
{
    get
    {
        if (_spaceWarpConsoleSkin == null)
        {
            ResourceManager.TryGetAsset($"space_warp/swconsoleui/spacewarpConsole.guiskin", out _spaceWarpConsoleSkin);
        }

        return _spaceWarpConsoleSkin;
    }
}

public void OnGUI()
{
    GUI.skin = Skin;

}
*/