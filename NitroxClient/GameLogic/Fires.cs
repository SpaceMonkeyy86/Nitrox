using System.Collections.Generic;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    /// <summary>
    ///     Handles all of the <see cref="Fire" />s in the game. Currently, the only known Fire spawning is in
    ///     <see cref="SubFire.CreateFire(SubFire.RoomFire)" />. The
    ///     fires in the Aurora come loaded with the map and do not grow in size. If we want to create a Fire spawning mechanic
    ///     outside of Cyclops fires, it should be
    ///     added to <see cref="Fires.Create(CyclopsFireData)" />. Fire dousing goes by Id and does not need to be
    ///     modified
    /// </summary>
    public class Fires
    {
        private readonly Dictionary<NitroxId, Dictionary<SessionId, float>> douseRates = [];
        private readonly LocalPlayer localPlayer;
        private readonly IPacketSender packetSender;

        public Fires(IPacketSender packetSender, LocalPlayer localPlayer)
        {
            this.packetSender = packetSender;
            this.localPlayer = localPlayer;
        }

        /// <summary>
        ///     Triggered when <see cref="SubFire.CreateFire(SubFire.RoomFire)" /> is executed. To create a new fire manually,
        ///     call <see cref="Create(CyclopsFireData)" />
        /// </summary>
        public void OnCreate(Fire fire, SubFire.RoomFire room, int nodeIndex)
        {
            if (!fire.fireSubRoot.TryGetIdOrWarn(out NitroxId subRootId))
            {
                return;
            }

            GameObject gameObject = fire.transform.parent.gameObject;

            NitroxId fireId = NitroxEntity.GenerateNewId(gameObject);
            CyclopsFireEntity entity = new(room.roomLinks.room, nodeIndex, fireId, subRootId);

            EntitySpawnedByClient packet = new(entity);
            packetSender.Send(packet);
        }

        // When our client starts/stops dousing a fire
        public void OnDouseChange(Fire fire, float douseRate)
        {
            if (localPlayer.SessionId == null || !fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            FireDoused packet = new(fireId, fire.livemixin.health, localPlayer.SessionId.Value, douseRate, false);
            packetSender.Send(packet);
        }

        // For large health updates (i.e. fire suppression system ticks)
        public void OnDouseOnce(Fire fire)
        {
            if (!fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            FireDoused packet = new(fireId, fire.livemixin.health, null, 0f, true);
            packetSender.Send(packet);
        }

        // When our client extinguishes a fire
        public void OnExtinguish(Fire fire)
        {
            if (!fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            FireDoused packet = new(fireId, 0f, null, 0f, false);
            packetSender.Send(packet);
        }

        // When another client starts/stops dousing a fire
        public void Douse(NitroxId fireId, float health, SessionId? sessionId, float douseRate, bool oneShot)
        {
            Optional<GameObject> fireGameObject = NitroxEntity.GetObjectFrom(fireId);
            if (!fireGameObject.HasValue)
            {
                Log.Warn($"Can't find fire entity with id {fireId}");
                return;
            }

            Fire fire = fireGameObject.Value.GetComponent<Fire>();
            if (!fire)
            {
                Log.Error($"Fire object with id {fireId} missing component");
                return;
            }

            // Prevents a desync where the fire could extinguish for one player but not another
            float douseAmount = Mathf.Max(fire.livemixin.health - health, 0.1f);

            if (douseAmount > 0f)
            {
                using (PacketSuppressor<FireDoused>.Suppress())
                {
                    fire.Douse(douseAmount);
                }
            }
            else
            {
                // Fire health went up
                fire.livemixin.health = health;
            }

            if (health <= 0f)
            {
                fire.Extinguished();
                douseRates.Remove(fireId);
                return;
            }

            if (!oneShot && sessionId.HasValue)
            {
                douseRates.TryAdd(fireId, []);
                douseRates[fireId][sessionId.Value] = douseRate;
            }
        }
    }
}
