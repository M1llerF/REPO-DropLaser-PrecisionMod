using System.Collections.Generic;
using ObjectDropLaserMod.Systems;
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
        private LineRenderer grabBeamOverlayLine;
        private LineRenderer dropBeamOverlayLine;
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
                    grabBeamOverlayLine,
                    dropBeamOverlayLine,
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

            try
            {
                dropLaserBeam.UpdateBeam();
            }
            catch (System.Exception ex)
            {
                Plugin.log.LogError($"[DropLaser] Beam subsystem failure (isolated): {ex}");
            }
        }

        /// <summary>
        /// Toggles the laser on/off.
        /// </summary>
        public void Toggle()
        {
            isActive = !isActive;
            if (dropBeamLine != null)
                dropBeamLine.enabled = isActive;
            if (dropBeamLight != null)
                dropBeamLight.enabled = isActive;
            if (!isActive && grabBeamOverlayLine != null)
                grabBeamOverlayLine.enabled = false;
            if (!isActive && dropBeamOverlayLine != null)
                dropBeamOverlayLine.enabled = false;
            DropLaserLogger.Info($"[DropLaser] Laser toggled {(isActive ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Forcibly disables the laser and clears preview visuals.
        /// </summary>
        public void DisableLaser()
        {
            if (dropBeamLine != null)
                dropBeamLine.enabled = false;
            if (dropBeamLight != null)
                dropBeamLight.enabled = false;
            if (grabBeamOverlayLine != null)
                grabBeamOverlayLine.enabled = false;
            if (dropBeamOverlayLine != null)
                dropBeamOverlayLine.enabled = false;
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
            ConfigureLineRenderer(dropBeamLine);

            Shader shader = Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Plugin.log.LogError("[DropLaser] Failed to find a line renderer shader. Disabling controller setup.");
                return;
            }

            dropBeamLine.material = DropLaserMaterialService.CreateTransparentLineMaterial(shader);
            float laserStartWidth = Plugin.LaserStartWidth?.Value ?? 0.04f;
            float laserEndWidth = Plugin.LaserEndWidth?.Value ?? 0.03f;
            dropBeamLine.startWidth = laserStartWidth;
            dropBeamLine.endWidth = laserEndWidth;

            Color seedColor = Color.red;
            dropBeamLine.startColor = seedColor;
            dropBeamLine.endColor = seedColor;
            dropBeamLine.enabled = false;

            try
            {
                grabBeamOverlayLine = CreateOverlayLine("GrabBeamOverlayLine", shader, laserStartWidth, laserEndWidth);
                dropBeamOverlayLine = CreateOverlayLine("DropBeamOverlayLine", shader, laserStartWidth, laserEndWidth);
            }
            catch (System.Exception ex)
            {
                grabBeamOverlayLine = null;
                dropBeamOverlayLine = null;
                Plugin.log.LogWarning($"[DropLaser] Overlay beam setup failed; continuing without overlay: {ex.Message}");
            }
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.useWorldSpace = true;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 8;
            lineRenderer.numCornerVertices = 2;
        }

        private LineRenderer CreateOverlayLine(string objectName, Shader shader, float baseStartWidth, float baseEndWidth)
        {
            GameObject overlayObject = new GameObject(objectName);
            overlayObject.transform.SetParent(transform, false);

            LineRenderer overlayLine = overlayObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(overlayLine);
            overlayLine.material = DropLaserMaterialService.CreateTransparentLineMaterial(shader);
            overlayLine.startWidth = baseStartWidth * 0.35f;
            overlayLine.endWidth = baseEndWidth * 0.35f;
            overlayLine.startColor = Color.white;
            overlayLine.endColor = Color.white;
            overlayLine.enabled = false;
            return overlayLine;
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
                DropLaserMaterialService.ConfigureTransparentMaterial(dropBeamLine.material);
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
