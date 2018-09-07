﻿using LunaClient.Base;
using LunaClient.VesselUtilities;

namespace LunaClient.Systems.Lock
{
    public class LockEvents : SubSystem<LockSystem>
    {
        /// <summary>
        /// Triggered when requesting a scene change.
        /// </summary>
        public void OnSceneRequested(GameScenes requestedScene)
        {
            if (requestedScene >= GameScenes.FLIGHT) return;

            //Always release the Update/UnloadedUpdate lock and the spectate lock
            System.ReleaseAllPlayerLocks();
            VesselCommon.IsSpectating = false;
        }
    }
}
