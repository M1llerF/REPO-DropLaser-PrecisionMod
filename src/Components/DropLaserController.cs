using UnityEngine;
using UnityEngine.SceneManagement;
using ObjectDropLaserMod.Utils;
using System.Collections.Generic;

namespace ObjectDropLaserMod.Components
{
    public class DropLaserController : MonoBehaviour
    {
        private LineRenderer lr;
        private bool active;

        private GameObject beamGO;
        private LineRenderer grabBeamLine;
        private PhysGrabber playerGrabber;
        private Light laserLight;
        private DropLaserBeam laserBeam;

        // Fake cart parts that should not block the beam
        private static readonly HashSet<string> ignoredCartParts = new()
        {
            "In Cart",
            "Capsule Mid",
            "Capsule Left",
            "Capsule Right"
        };

        /// <summary>
        /// Called by Unity when the object is first created.
        /// Initializes core components like LineRenderer and Light.
        /// </summary>
        void Awake()
        {
            if (!Plugin.EnableLaser.Value)
            {
                Plugin.log.LogWarning("[DropLaser] Laser system disabled via config. Destroying DropLaserController...");
                Destroy(this.gameObject);
                return;
            }

            DropLaserLogger.Info("[DropLaser] Awake called");

            SetupLineRenderer();
            SetupLaserLight();

            // Register scene reload handler
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        /// <summary>
        /// Called by Unity after Awake.
        /// Attempts to find the player's grab beam for syncing appearance.
        /// </summary>
        void Start()
        {
            DropLaserLogger.Info("[DropLaser] Start called");
            TryFindBeam();

            if (beamGO != null && grabBeamLine != null && playerGrabber != null)
            {
                laserBeam = new DropLaserBeam(lr, laserLight, beamGO, grabBeamLine, playerGrabber, ignoredCartParts);
            }
            
        }

        /// <summary>
        /// Called by Unity every frame.
        /// Updates the laser beam if it is currently active.
        /// </summary>
        void Update()
        {
            if (!active)
                return;

            if (laserBeam != null)
                laserBeam.UpdateBeam();
        }

        /// <summary>
        /// Toggles the laser on or off.
        /// </summary>
        public void Toggle()
        {
            active = !active;
            lr.enabled = active;
            laserLight.enabled = active;
            DropLaserLogger.Info($"[DropLaser] Laser toggled {(active ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Forcibly disables the laser, regardless of current state.
        /// </summary>
        public void DisableLaser()
        {
            lr.enabled = false;
            laserLight.enabled = false;
            active = false;
        }

        /// <summary>
        /// Initializes the LineRenderer with default laser appearance settings.
        /// </summary>
        private void SetupLineRenderer()
        {
            lr = gameObject.AddComponent<LineRenderer>();
            lr.positionCount = 2;

            var mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetFloat("_Mode", 2f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            lr.material = mat;
            lr.startWidth = Plugin.LaserStartWidth.Value;
            lr.endWidth = Plugin.LaserEndWidth.Value;

            Color seed = new Color(1f, 0f, 0f, 1f);
            lr.startColor = seed;
            lr.endColor = seed;

            lr.enabled = false;
        }

        private void SetupLaserLight()
        {
            laserLight = new GameObject("DropLaserLight").AddComponent<Light>();
            laserLight.type = LightType.Point;
            laserLight.range = Plugin.LaserLightRange.Value;
            laserLight.intensity = Plugin.LaserLightIntensity.Value;
            laserLight.enabled = false;
        }

        /// <summary>
        /// Attempts to find the local player's grab beam for syncing laser appearance.
        /// </summary>
        private void TryFindBeam()
        {
            bool singlePlayer = Photon.Pun.PhotonNetwork.PlayerList.Length < 1;
            var allGrabbers = Object.FindObjectsOfType<PhysGrabber>();

            foreach (var grabber in allGrabbers)
            {
                var view = grabber.GetComponent<Photon.Pun.PhotonView>();

                if (singlePlayer)
                {
                    playerGrabber = grabber;
                    DropLaserLogger.Info("[DropLaser] Singleplayer detected — attaching to first PhysGrabber.");
                    break;
                }
                else if (view != null && view.IsMine)
                {
                    playerGrabber = grabber;
                    DropLaserLogger.Info("[DropLaser] Multiplayer detected — attached to local player's PhysGrabber.");
                    break;
                }
            }

            if (playerGrabber == null)
            {
                Plugin.log.LogWarning("[DropLaser] Could not find local player's PhysGrabber!");
                return;
            }

            beamGO = playerGrabber.GetBeamObject();
            if (beamGO != null)
            {
                DropLaserLogger.Info("[DropLaser] Beam GameObject found!");
                grabBeamLine = beamGO.GetComponent<LineRenderer>();
                if (grabBeamLine != null)
                    DropLaserLogger.Info("[DropLaser] GrabBeam LineRenderer found!");
                else
                    Plugin.log.LogWarning("[DropLaser] GrabBeam has no LineRenderer!");
            }
            else
            {
                Plugin.log.LogWarning("[DropLaser] Beam GameObject is null!");
            }
        }

        /// <summary>
        /// Called when a new scene is loaded.
        /// Forces cleanup of the DropLaserController instance to prevent cross-scene bugs.
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DropLaserLogger.Info("[DropLaser] New scene loaded — destroying old DropLaserController instance.");
            Destroy(this.gameObject);
        }

        /// <summary>
        /// Called when the controller is destroyed.
        /// Cleans up scene event handlers.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            DropLaserLogger.Info("[DropLaser] OnDestroy called — DropLaserController cleaned up.");
        }
    }
}
