using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace ObjectDropLaserMod.Systems
{
    /// <summary>
    /// Central saga orchestrator for runtime visual subsystems.
    /// Executes branches, handles failures, runs compensating actions,
    /// and temporarily cuts off failing branches before retrying.
    /// </summary>
    public sealed class SagaOrchestrator
    {
        private sealed class BranchState
        {
            public int RetryAtFrame;
            public bool IsCutOff;
            public Exception LastFailure;
            public int LastFailureFrame;
            public int LastCutOffLogFrame;
        }

        private readonly ManualLogSource log;
        private readonly int retryDelayFrames;
        private readonly Dictionary<string, BranchState> branches = new();

        public SagaOrchestrator(ManualLogSource log, int retryDelayFrames = 180)
        {
            this.log = log;
            this.retryDelayFrames = Mathf.Max(1, retryDelayFrames);
        }

        public void Execute(string branchName, Action execute, Action compensate)
        {
            if (string.IsNullOrWhiteSpace(branchName))
                branchName = "unnamed";

            BranchState state = GetOrCreateState(branchName);
            if (state.IsCutOff && Time.frameCount < state.RetryAtFrame)
            {
                // Keep surfacing the root-cause while cut off so failures do not look silent.
                if (Time.frameCount >= state.LastCutOffLogFrame + 120)
                {
                    string reason = state.LastFailure != null ? state.LastFailure.Message : "unknown";
                    log.LogWarning($"[DropLaser:Saga] Branch '{branchName}' remains cut off until frame {state.RetryAtFrame} (last failure frame {state.LastFailureFrame}: {reason})");
                    state.LastCutOffLogFrame = Time.frameCount;
                }
                return;
            }

            try
            {
                execute?.Invoke();
                if (state.IsCutOff)
                    log.LogInfo($"[DropLaser:Saga] Branch '{branchName}' recovered and resumed.");

                state.IsCutOff = false;
                state.RetryAtFrame = 0;
                state.LastFailure = null;
                state.LastFailureFrame = 0;
            }
            catch (Exception ex)
            {
                state.LastFailure = ex;
                state.LastFailureFrame = Time.frameCount;
                log.LogError($"[DropLaser:Saga] Branch '{branchName}' failed at frame {Time.frameCount}: {ex}");
                try
                {
                    compensate?.Invoke();
                }
                catch (Exception compensateEx)
                {
                    log.LogError($"[DropLaser:Saga] Branch '{branchName}' compensation failed: {compensateEx}");
                }

                state.IsCutOff = true;
                state.RetryAtFrame = Time.frameCount + retryDelayFrames;
                state.LastCutOffLogFrame = Time.frameCount;
                log.LogWarning($"[DropLaser:Saga] Branch '{branchName}' cut off until frame {state.RetryAtFrame}.");
            }
        }

        private BranchState GetOrCreateState(string branchName)
        {
            if (!branches.TryGetValue(branchName, out BranchState state))
            {
                state = new BranchState();
                branches[branchName] = state;
            }

            return state;
        }
    }
}
