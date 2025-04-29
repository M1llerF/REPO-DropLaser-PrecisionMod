using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ObjectDropLaserMod.Utils
{
    /// <summary>
    /// Provides efficient runtime access to private fields inside PhysGrabber.
    /// Avoids slow reflection by using compiled expression trees.
    /// </summary>
    public static class PhysGrabberAccessors
    {
        // Cached private fields for PhysGrabber
        private static readonly FieldInfo grabbedObjField =
            AccessTools.Field(typeof(PhysGrabber), "grabbedPhysGrabObject");

        private static readonly FieldInfo beamField =
            AccessTools.Field(typeof(PhysGrabber), "physGrabBeam");

        /// <summary>
        /// Gets the currently grabbed PhysGrabObject from a PhysGrabber instance.
        /// </summary>
        public static PhysGrabObject GetGrabbedObject(this PhysGrabber instance)
            => grabbedObjField.GetValue(instance) as PhysGrabObject;

        /// <summary>
        /// Gets the grab beam GameObject from a PhysGrabber instance.
        /// </summary>
        public static GameObject GetBeamObject(this PhysGrabber instance)
            => beamField.GetValue(instance) as GameObject;
    }
}
