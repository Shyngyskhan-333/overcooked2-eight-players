using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

// GAMEPLAY subsystem runtime patches (Harmony). Rationale per patch: docs\subsystem_gameplay.md
// NOTE: the plugin compiles against the UNPATCHED Assembly-CSharp, so player ids 5..8 are written as ints cast to
// PlayerInputLookup.Player.
namespace OC2.EightPlayers
{
    static class GameplayCfg
    {
        static ConfigEntry<bool> s_spawn; static ConfigEntry<float> s_cam;
        public static bool SpawnExtraChefs { get { if (s_spawn == null) s_spawn = Plugin.Instance.Config.Bind("Gameplay", "SpawnExtraChefs", true, "Clone the scene chefs so kitchens have one chef per local player when more than 4 players are present."); return s_spawn.Value; } }
        public static float CameraMaxDistanceScale { get { if (s_cam == null) s_cam = Plugin.Instance.Config.Bind("Gameplay", "CameraMaxDistanceScale", 1.25f, "Multiplier applied to MultiplayerCamera.m_maxDistance only when more than 4 chefs are in the kitchen (4-player behaviour untouched)."); return s_cam.Value; } }
    }

    /// <summary>Every kitchen scene contains exactly 4 pre-placed chef objects (PlayerIDProvider ids One..Four; verified over 145 scene bundles).
    /// Users beyond the 4th get no chef entity (CampaignKitchenLoaderManager.AssignChefEntities stops at providers.Length).
    /// Script execution order (read from the scene bundles): KitchenBootstrapManager -6, PlayerInputLookup -5,
    /// CampaignKitchenLoaderManager -4, PlayerControls/PlayerIDProvider -3, ChefMeshReplacer -2, everything else 0.
    /// So in CampaignKitchenLoaderManager.Awake no chef component has awakened yet: cloning here yields pristine chefs, their Awake runs at
    /// Instantiate, and the scene's MultiplayerCamera / PlayerSwitchingManager / entity scan (all later) see all 8 chefs.</summary>
    [HarmonyPatch(typeof(CampaignKitchenLoaderManager), "Awake")]
    static class Patch_SpawnExtraChefs
    {
        static void Postfix()
        {
            try { ExtraChefSpawner.Run(); }
            catch (Exception e) { Plugin.Log.LogError("ExtraChefSpawner failed: " + e); }
        }
    }

    static class ExtraChefSpawner
    {
        public static void Run()
        {
            if (!GameplayCfg.SpawnExtraChefs) return;
            int users = ClientUserSystem.m_Users.Count;
            if (users > 8) users = 8;
            // PlayerIDProvider.s_AllProviders is filled in each provider's own Awake (order -3), i.e. AFTER this loader's Awake (-4),
            // so it is still empty here: find the scene's chef objects directly instead.
            var all = UnityEngine.Object.FindObjectsOfType<PlayerIDProvider>();
            Plugin.Log.LogInfo("[gameplay] ExtraChefSpawner: users=" + users + " sceneChefs=" + all.Length);
            if (users <= 4 || all.Length >= users) return;      // nothing to do for <=4 players (vanilla path), idempotent

            var templates = new List<PlayerIDProvider>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null && (int)all[i].GetID() < 4) templates.Add(all[i]);
            templates.Sort((a, b) => (int)a.GetID() - (int)b.GetID());
            if (templates.Count == 0) { Plugin.Log.LogWarning("[gameplay] no template chefs found"); return; }

            var taken = new List<Vector3>();
            for (int i = 0; i < all.Length; i++) if (all[i] != null) taken.Add(all[i].transform.position);

            int playersLayer = LayerMask.NameToLayer("Players");
            int groundMask = Physics.DefaultRaycastLayers & ~(playersLayer >= 0 ? (1 << playersLayer) : 0);
            int need = users - all.Length, firstId = all.Length;
            for (int k = 0; k < need; k++)
            {
                var tmpl = templates[k % templates.Count];
                Transform t = tmpl.transform;
                Vector3 pos = PickPosition(t.position, k, taken, groundMask);
                var clone = UnityEngine.Object.Instantiate(tmpl.gameObject, pos, t.rotation, t.parent);
                clone.name = "Player " + (firstId + k + 1);
                var idp = clone.GetComponent<PlayerIDProvider>();
                idp.OverridePlayerId((PlayerInputLookup.Player)(firstId + k));
                taken.Add(pos);
                Physics.SyncTransforms();
                Plugin.Log.LogInfo("Spawned extra chef " + clone.name + " (id " + (firstId + k) + ") at " + pos + " near " + tmpl.name);
            }
        }

        // Search rings around the template chef's spawn for ground at (almost) the same height with a free capsule volume.
        static Vector3 PickPosition(Vector3 around, int k, List<Vector3> taken, int groundMask)
        {
            float[] radii = { 1.1f, 1.8f, 2.5f, 3.2f };
            const int steps = 12;
            foreach (float r in radii)
                for (int s = 0; s < steps; s++)
                {
                    float a = (s + 0.5f * k) / steps * Mathf.PI * 2f;
                    Vector3 p = around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    RaycastHit hit;
                    if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 4f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.point.y - around.y) > 0.35f) continue;
                    Vector3 g = new Vector3(p.x, around.y, p.z);
                    bool crowded = false;
                    foreach (var o in taken) if ((new Vector3(o.x - g.x, 0f, o.z - g.z)).sqrMagnitude < 0.9f * 0.9f) { crowded = true; break; }
                    if (crowded) continue;
                    if (Physics.CheckCapsule(g + Vector3.up * 0.45f, g + Vector3.up * 1.1f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    return g;
                }
            // No free spot found: stack next to the template; physics pushes the chefs apart.
            return around + new Vector3(0.4f * (k + 1), 0f, 0.4f * (k + 1));
        }
    }

    /// <summary>KitchenBootstrapManager.GetDefaultChef only handles indices 0..3 (returns null otherwise): ServerKitchenLoader uses it for
    /// users without a lobby-selected chef. Wrap around so users 5..8 reuse defaults 1..4 (duplicates are fine, ChefMeshReplacer has no uniqueness rule).</summary>
    [HarmonyPatch(typeof(KitchenBootstrapManager), "GetDefaultChef")]
    static class Patch_DefaultChefWrap
    {
        static void Postfix(KitchenBootstrapManager __instance, int index, ref GameSession.SelectedChefData __result)
        {
            if (__result == null && index >= 4) __result = __instance.GetDefaultChef(index % 4);
        }
    }

    /// <summary>The in-kitchen ring under each chef is the ThrowIndicators texture set, a 4-entry array indexed by player number
    /// (UpdateCosmetics skips num >= 4, so chefs 5..8 kept the default blue ring). Wrap the index so the ring matches the apron colour,
    /// which for players 5..8 already repeats team colours 1..4. Team modes (team != None) are untouched.</summary>
    [HarmonyPatch(typeof(PlayerControls.ThrowIndicators), "UpdateCosmetics")]
    static class Patch_RingColourWrap
    {
        static void Prefix(TeamID _team, ref int _playerNum)
        {
            if (_team == TeamID.None && _playerNum >= 4) _playerNum %= 4;
        }
    }

    /// <summary>PlayerbasedActivation.IsActiveForPlayerCount: mask enum only has One..Four; (1 &lt;&lt; (count-1)) for 5..8 players is outside the mask,
    /// so objects authored "active for 4 players only" would be switched OFF (and -1 masks unaffected). Treat 5..8 as 4.</summary>
    [HarmonyPatch(typeof(PlayerbasedActivation), "IsActiveForPlayerCount")]
    static class Patch_PlayerbasedActivation
    {
        static void Prefix(ref int _count) { if (_count > 4) _count = 4; }
    }

    /// <summary>MultiplayerCamera frames every PlayerIDProvider transform automatically (distance solved from all avatars, clamped to
    /// [m_minDistance,m_maxDistance], typically 16..17.5 in kitchens). With >4 chefs we allow extra zoom-out headroom; 4-player untouched.</summary>
    [HarmonyPatch(typeof(MultiplayerCamera), "Awake")]
    static class Patch_CameraHeadroom
    {
        static void Prefix(MultiplayerCamera __instance)
        {
            try
            {
                if (PlayerIDProvider.s_AllProviders.Count <= 4) return;
                float s = GameplayCfg.CameraMaxDistanceScale; if (s <= 1f) return;
                var f = AccessTools.Field(typeof(MultiplayerCamera), "m_maxDistance");
                f.SetValue(__instance, (float)f.GetValue(__instance) * s);
            }
            catch (Exception e) { Plugin.Log.LogError("camera headroom: " + e.Message); }
        }
    }

    /// <summary>ServerPhysicsObjectSynchroniser.StoreContactInformation writes m_CollidingChefs into PhysicsObjectMessage arrays sized
    /// kTrackedChefCount=4 (and ContactCount is 3 bits on the wire). With 5+ chefs touching one crate/plate this throws IndexOutOfRange.
    /// Keep the wire format (online compatible) and just cap the tracked contacts at 4.</summary>
    [HarmonyPatch(typeof(ServerPhysicsObjectSynchroniser), "StoreContactInformation")]
    static class Patch_PhysicsContactCap
    {
        static FieldInfo s_field = AccessTools.Field(typeof(ServerPhysicsObjectSynchroniser), "m_CollidingChefs");
        static PropertyInfo s_count; static MethodInfo s_removeAt;
        static void Prefix(ServerPhysicsObjectSynchroniser __instance)
        {
            object list = s_field.GetValue(__instance);
            if (list == null) return;
            if (s_count == null) { var t = list.GetType(); s_count = t.GetProperty("Count"); s_removeAt = t.GetMethod("RemoveAt"); }
            int n = (int)s_count.GetValue(list, null);
            for (int i = n - 1; i >= PhysicsObjectMessage.kTrackedChefCount; i--) s_removeAt.Invoke(list, new object[] { i });
        }
    }

    /// <summary>ServerTriggerMoveSpawnPoints.Move (moving-platform levels): CalculateBestSpawns pops one spawn point per player and does
    /// list[0] on an emptied list when players > spawn points (ArgumentOutOfRange), and MoveSpawnMessage encodes the player count in 3 bits (max 7).
    /// For 5+ players (or players > spawn points) split the players into batches no larger than min(spawnPoints, 4) and send one message per batch
    /// (wire format unchanged). Later batches may reuse spawn points (chefs stack and physics separates them).</summary>
    [HarmonyPatch(typeof(ServerTriggerMoveSpawnPoints), "Move")]
    static class Patch_MoveSpawnBatches
    {
        static bool Prefix(ServerTriggerMoveSpawnPoints __instance)
        {
            var tr = Traverse.Create(__instance);
            var cfg = tr.Field("m_changeRespawn").GetValue<TriggerMoveSpawnPoints>();
            if (cfg == null || cfg.m_spawnPoints == null || cfg.m_spawnPoints.Length == 0) return true;
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length <= 4 && players.Length <= cfg.m_spawnPoints.Length) return true;   // vanilla
            int batch = Math.Max(1, Math.Min(cfg.m_spawnPoints.Length, 4));
            for (int from = 0; from < players.Length; from += batch)
            {
                int len = Math.Min(batch, players.Length - from);
                var part = new GameObject[len]; Array.Copy(players, from, part, 0, len);
                var spawns = tr.Method("CalculateBestSpawns", new object[] { part, cfg.m_spawnPoints }).GetValue<KeyValuePair<GameObject, Transform>[]>();
                var msg = new MoveSpawnMessage();
                msg.Initialise(spawns, cfg.m_spawnPoints);
                __instance.SendServerEvent(msg);
            }
            return false;
        }
    }
}
