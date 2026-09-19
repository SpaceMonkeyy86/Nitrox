using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
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
        private readonly IPacketSender packetSender;
        private readonly ThrottledPacketSender throttledPacketSender;

        public Fires(IPacketSender packetSender, ThrottledPacketSender throttledPacketSender)
        {
            this.packetSender = packetSender;
            this.throttledPacketSender = throttledPacketSender;
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

        /// <summary>
        ///     Triggered when <see cref="Fire.Douse(float)" /> is executed. To Douse a fire manually, retrieve the
        ///     <see cref="Fire" /> call the Douse method
        /// </summary>
        public void OnDouse(Fire fire, float douseAmount)
        {
            if (!fire.transform.parent.TryGetIdOrWarn(out NitroxId fireId))
            {
                return;
            }

            bool extinguished = !fire.livemixin.IsAlive() || fire.isExtinguished;

            FireDoused packet = new(fireId, extinguished ? 0 : fire.livemixin.health);
            throttledPacketSender.SendThrottled(packet, x => x.Id);
        }
    }
}
