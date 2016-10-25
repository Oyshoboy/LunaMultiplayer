﻿using System;
using System.Collections;
using LunaClient.Base;
using LunaClient.Systems.SettingsSys;
using LunaClient.Systems.VesselLockSys;
using LunaClient.Systems.VesselProtoSys;
using LunaClient.Systems.VesselWarpSys;
using LunaClient.Systems.Warp;
using UniLinq;
using UnityEngine;

namespace LunaClient.Systems.VesselRemoveSys
{
    /// <summary>
    /// This system handles the killing of vessels. We kill the vessels that are not in our subspace and 
    /// the vessels that are destroyed, old copies of changed vessels or when they dock
    /// </summary>
    public class VesselRemoveSystem :
        MessageSystem<VesselRemoveSystem, VesselRemoveMessageSender, VesselRemoveMessageHandler>
    {
        #region Fields

        private VesselRemoveEvents VesselRemoveEvents { get; } = new VesselRemoveEvents();

        #endregion

        #region Base overrides

        public override void OnEnabled()
        {
            base.OnEnabled();
            GameEvents.onVesselRecovered.Add(VesselRemoveEvents.OnVesselRecovered);
            GameEvents.onVesselTerminated.Add(VesselRemoveEvents.OnVesselTerminated);
            GameEvents.onVesselDestroy.Add(VesselRemoveEvents.OnVesselDestroyed);
            Client.Singleton.StartCoroutine(CheckVesselsToKill());
        }

        public override void OnDisabled()
        {
            base.OnDisabled();
            GameEvents.onVesselRecovered.Remove(VesselRemoveEvents.OnVesselRecovered);
            GameEvents.onVesselTerminated.Remove(VesselRemoveEvents.OnVesselTerminated);
            GameEvents.onVesselDestroy.Remove(VesselRemoveEvents.OnVesselDestroyed);
        }

        #endregion

        #region Public

        public void KillVessels(Vessel[] killVessel)
        {
            foreach (var vessel in killVessel)
            {
                KillVessel(vessel);
            }
        }

        public void KillVessel(Vessel killVessel)
        {
            Client.Singleton.StartCoroutine(KillVesselRoutine(killVessel));
        }

        private static IEnumerator KillVesselRoutine(Vessel killVessel)
        {
            //TODO refactor this...
            while (true)
            {
                if (!FlightGlobals.Vessels.Contains(killVessel)) break;

                if (VesselLockSystem.Singleton.IsSpectating && FlightGlobals.ActiveVessel.id == killVessel.id)
                {
                    var otherVessels = FlightGlobals.Vessels.Where(v => v.id != killVessel.id).ToArray();

                    if (otherVessels.Any())
                        FlightGlobals.ForceSetActiveVessel(otherVessels.First());
                    else
                        HighLogic.LoadScene(GameScenes.SPACECENTER);

                    ScreenMessages.PostScreenMessage("The player you were spectating removed his vessel");
                }

                if (killVessel != null)
                {
                    Debug.Log($"[LMP]: Killing vessel {killVessel.id}");

                    //Try to unload the vessel first.
                    if (killVessel.loaded)
                    {
                        try
                        {
                            killVessel.Unload();
                        }
                        catch (Exception unloadException)
                        {
                            Debug.LogError("[LMP]: Error unloading vessel: " + unloadException);
                        }
                    }

                    yield return null; //Resume on next frame

                    try
                    {
                        //Remove the kerbal from the craft
                        foreach (var pps in killVessel.protoVessel.protoPartSnapshots)
                            foreach (var pcm in pps.protoModuleCrew.ToArray())
                                pps.RemoveCrew(pcm);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[LMP]: Error removing kerbals from vessel: {e}");
                    }

                    yield return null; //Resume on next frame

                    try
                    {
                        killVessel.Die();
                    }
                    catch (Exception killException)
                    {
                        Debug.LogError("[LMP]: Error destroying vessel: " + killException);
                    }

                    yield return null; //Resume on next frame

                    try
                    {
                        HighLogic.CurrentGame.DestroyVessel(killVessel);
                        HighLogic.CurrentGame.Updated();
                    }
                    catch (Exception destroyException)
                    {
                        Debug.LogError("[LMP]: Error destroying vessel from the scenario: " + destroyException);
                    }

                    if (FlightGlobals.Vessels.Contains(killVessel) && (killVessel.state != Vessel.State.DEAD))
                    {
                        continue; //Recursive Killing
                    }
                }
                break;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Check the vessels that are not in our subspace and kill them
        /// </summary>
        private IEnumerator CheckVesselsToKill()
        {
            var seconds = new WaitForSeconds((float)TimeSpan.FromMilliseconds(SettingsSystem.ServerSettings.VesselKillCheckMsInterval).TotalSeconds);
            while (true)
            {
                try
                {
                    if (!Enabled) break;

                    var vesselsToKill = VesselProtoSystem.Singleton.AllPlayerVessels
                        .Where(v => v.Loaded && VesselWarpSystem.Singleton.GetVesselSubspace(v.VesselId) != WarpSystem.Singleton.CurrentSubspace)
                        .ToList();

                    KillVessels(vesselsToKill.Select(v => FlightGlobals.FindVessel(v.VesselId)).ToArray());

                    foreach (var killedVessel in vesselsToKill)
                    {
                        killedVessel.Loaded = false;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LMP]: Coroutine error in CheckVesselsToKill {e}");
                }

                yield return seconds;
            }
        }

        #endregion
    }
}
