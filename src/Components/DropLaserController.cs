using System.Collections.Generic;
using ObjectDropLaserMod.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ObjectDropLaserMod.Components
{
    /// <summary>
    /// Owns the runtime laser visuals and coordinates beam updates for the local player.
    /// </summary>
    public class DropLaserController : MonoBehaviour
    {
        // Fake cart parts that should not block the beam.
        private static readonly HashSet<string> IgnoredCartParts = new()
        {
            "In Cart",
            "Capsule Mid",
            "Capsule Left",
            "Capsule Right"
        };

        private LineRenderer dropBeamLine;
        private Light dropBeamLight;
        private DropLaserBeam dropLaserBeam;

        private GameObject sourceBeamObject;
        private LineRenderer sourceBeamLine;
        private PhysGrabber playerGrabber;

        private bool isActive;

        /// <summary>
        /// Initializes renderer/light resources and registers scene hooks.
        /// </summary>
        private void Awake()
        {
            if (!Plugin.EnableLaser.Value)
            {
                Plugin.log.LogWarning("[DropLaser] Laser system disabled via config. Destroying DropLaserController...");
                Destroy(gameObject);
                return;
            }

            DropLaserLogger.Info("[DropLaser] Awake called");
            SetupLineRenderer();
            SetupLaserLight();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        /// <summary>
        /// Finds the player's beam and constructs the drop beam updater.
        /// </summary>
        private void Start()
        {
            DropLaserLogger.Info("[DropLaser] Start called");
            TryFindPlayerBeam();

            if (sourceBeamObject != null && sourceBeamLine != null && playerGrabber != null)
            {
                dropLaserBeam = new DropLaserBeam(
                    dropBeamLine,
                    dropBeamLight,
                    sourceBeamObject,
                    sourceBeamLine,
                    playerGrabber,
                    IgnoredCartParts);
            }
        }

        /// <summary>
        /// Updates beam visuals while the laser is active.
        /// </summary>
        private void Update()
        {
            if (!isActive || dropLaserBeam == null)
                return;

            dropLaserBeam.UpdateBeam();
        }

        /// <summary>
        /// Toggles the laser on/off.
        /// </summary>
        public void Toggle()
        {
            isActive = !isActive;
            dropBeamLine.enabled = isActive;
            dropBeamLight.enabled = isActive;
            DropLaserLogger.Info($"[DropLaser] Laser toggled {(isActive ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Forcibly disables the laser and clears preview visuals.
        /// </summary>
        public void DisableLaser()
        {
            dropBeamLine.enabled = false;
            dropBeamLight.enabled = false;
            isActive = false;
            dropLaserBeam?.Dispose();
        }

        /// <summary>
        /// Cleans up resources when scene changes.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            dropLaserBeam?.Dispose();
            DropLaserLogger.Info("[DropLaser] OnDestroy called - DropLaserController cleaned up.");
        }

        private void SetupLineRenderer()
        {
            dropBeamLine = gameObject.AddComponent<LineRenderer>();
            dropBeamLine.positionCount = 2;
            dropBeamLine.shadowCastingMode = ShadowCastingMode.Off;
            dropBeamLine.receiveShadows = false;
            dropBeamLine.useWorldSpace = true;
            dropBeamLine.textureMode = LineTextureMode.Stretch;

            Shader shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.SetFloat("_Mode", 2f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            dropBeamLine.material = material;
            dropBeamLine.startWidth = Plugin.LaserStartWidth.Value;
            dropBeamLine.endWidth = Plugin.LaserEndWidth.Value;

            Color seedColor = Color.red;
            dropBeamLine.startColor = seedColor;
            dropBeamLine.endColor = seedColor;
            dropBeamLine.enabled = false;
        }

        private void SetupLaserLight()
        {
            dropBeamLight = new GameObject("DropLaserLight").AddComponent<Light>();
            dropBeamLight.type = LightType.Point;
            dropBeamLight.range = Plugin.LaserLightRange.Value;
            dropBeamLight.intensity = Plugin.LaserLightIntensity.Value;
            dropBeamLight.enabled = false;
        }

        /// <summary>
        /// Finds the local player's PhysGrabber and source beam.
        /// </summary>
        private void TryFindPlayerBeam()
        {
            bool singlePlayer = Photon.Pun.PhotonNetwork.PlayerList.Length < 1;
            PhysGrabber[] allGrabbers = Object.FindObjectsOfType<PhysGrabber>();

            foreach (PhysGrabber grabber in allGrabbers)
            {
                Photon.Pun.PhotonView view = grabber.GetComponent<Photon.Pun.PhotonView>();

                if (singlePlayer)
                {
                    playerGrabber = grabber;
                    DropLaserLogger.Info("[DropLaser] Singleplayer detected - attaching to first PhysGrabber.");
                    break;
                }

                if (view != null && view.IsMine)
                {
                    playerGrabber = grabber;
                    DropLaserLogger.Info("[DropLaser] Multiplayer detected - attached to local player's PhysGrabber.");
                    break;
                }
            }

            if (playerGrabber == null)
            {
                Plugin.log.LogWarning("[DropLaser] Could not find local player's PhysGrabber!");
                return;
            }

            sourceBeamObject = playerGrabber.GetBeamObject();
            if (sourceBeamObject == null)
            {
                Plugin.log.LogWarning("[DropLaser] Beam GameObject is null!");
                return;
            }

            DropLaserLogger.Info("[DropLaser] Beam GameObject found!");
            sourceBeamLine = sourceBeamObject.GetComponent<LineRenderer>();
            if (sourceBeamLine == null)
            {
                Plugin.log.LogWarning("[DropLaser] GrabBeam has no LineRenderer!");
                return;
            }

            DropLaserLogger.Info("[DropLaser] GrabBeam LineRenderer found!");
            if (sourceBeamLine.material != null)
            {
                dropBeamLine.material = new Material(sourceBeamLine.material);
                DropLaserLogger.Info($"[DropLaser] Cloned grab beam material shader: {dropBeamLine.material.shader?.name}");
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DropLaserLogger.Info("[DropLaser] New scene loaded - destroying old DropLaserController instance.");
            Destroy(gameObject);
        }
    }
}
