using UnityEngine;
using System.Collections.Generic;
using ObjectDropLaserMod.Utils;

namespace ObjectDropLaserMod.Components
{
    /// <summary>
    /// Manages the visual beam and light effects for the dropped laser.
    /// </summary>
    public class DropLaserBeam
    {
        private readonly LineRenderer lr;
        private readonly Light laserLight;
        private readonly GameObject beamGO;
        private readonly LineRenderer grabBeamLine;
        private readonly PhysGrabber playerGrabber;
        private readonly HashSet<string> ignoredCartParts;

        /// <summary>
        /// Initializes a new instance of the DropLaserBeam system.
        /// </summary>
        public DropLaserBeam(LineRenderer lr, Light laserLight, GameObject beamGO, LineRenderer grabBeamLine, PhysGrabber playerGrabber, HashSet<string> ignoredCartParts)
        {
            this.lr = lr;
            this.laserLight = laserLight;
            this.beamGO = beamGO;
            this.grabBeamLine = grabBeamLine;
            this.playerGrabber = playerGrabber;
            this.ignoredCartParts = ignoredCartParts;
        }

        /// <summary>
        /// Updates the beam's appearance and position every frame while active.
        /// </summary>
        public void UpdateBeam()
        {
            // Verify critical references exist
            if (playerGrabber == null || grabBeamLine == null || beamGO == null)
            {
                Plugin.log.LogWarning("[DropLaser] Critical references lost. Cannot update beam.");
                return;
            }

            if (beamGO == null || grabBeamLine == null || playerGrabber == null)
                return;

            var beamMat = grabBeamLine.material;
            var dropMat = lr.material;

            // Copy visual appearance from the main grab beam
            if (beamMat.HasProperty("_MainTex"))
                dropMat.SetTexture("_MainTex", beamMat.GetTexture("_MainTex"));

            dropMat.mainTextureOffset = beamMat.mainTextureOffset;
            dropMat.mainTextureScale = beamMat.mainTextureScale;

            Color finalColor = DropLaserColorManager.GetFinalLaserColor(beamMat);

            lr.startColor = new Color(finalColor.r, finalColor.g, finalColor.b, lr.startColor.a);
            lr.endColor = new Color(finalColor.r, finalColor.g, finalColor.b, lr.endColor.a);
            dropMat.SetColor("_Color", finalColor);
            dropMat.shaderKeywords = beamMat.shaderKeywords;

            // Get the currently held object
            var held = playerGrabber.GetGrabbedObject();
            if (held == null)
            {
                lr.enabled = false;
                laserLight.enabled = false;
                return;
            }

            // Calculate starting point at bottom center of held object
            float laserOffset = 0.05f;

            var collider = held.GetComponentInChildren<Collider>();
            Vector3 from;
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                from = bounds.center;
                from.y += laserOffset;
                from.y -= bounds.extents.y;
            }
            else
            {
                from = held.transform.position;
            }

            // Perform downward raycasts to find hit surface
            Vector3 to = from + Vector3.down * 50f;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, Plugin.LaserMaxDistance.Value, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var go = hit.collider.gameObject;

                // Ignore invisible cart parts
                if (ignoredCartParts.Contains(go.name))
                    continue;

                // Ignore self-collision with held object
                if (IsPartOfHeldObject(go, held.gameObject))
                    continue;

                // Find the nearest valid collision
                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    to = hit.point;
                }
            }
            // Update beam visuals
            lr.enabled = true;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);

            // Update attached light
            laserLight.transform.position = to;
            laserLight.color = new Color(finalColor.r, finalColor.g, finalColor.b, 1f);
            laserLight.enabled = true;
        }

        /// <summary>
        /// Checks if a GameObject is part of the currently held object (to avoid false beam collisions).
        /// </summary>
        private bool IsPartOfHeldObject(GameObject hitObject, GameObject heldObject)
        {
            if (hitObject == null || heldObject == null)
                return false;

            Transform current = hitObject.transform;
            while (current != null)
            {
                if (current.gameObject == heldObject)
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
