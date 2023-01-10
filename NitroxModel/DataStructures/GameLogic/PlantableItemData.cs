﻿using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace NitroxModel.DataStructures.GameLogic
{
    [Serializable]
    [DataContract]
    public class PlantableItemData : ItemData
    {
        [DataMember(Order = 1)]
        public double PlantedGameTime { get; }

        [IgnoreConstructor]
        protected PlantableItemData()
        {
            // Constructor for serialization. Has to be "protected" for json serialization.
        }

        /// <summary>
        /// Extends the basic ItemData by adding the game time when the Plantable was added to its Planter container.
        /// </summary>
        /// <param name="plantedGameTime">Clients will use this to determine expected plant growth progress when connecting </param>
        public PlantableItemData(NitroxId containerId, NitroxId itemId, byte[] serializedData, double plantedGameTime) : base(containerId, itemId, serializedData)
        {
            PlantedGameTime = plantedGameTime;
        }

        public override string ToString()
        {
            return $"[PlantedItemData ContainerId: {ContainerId} Id: {ItemId} Planted: {PlantedGameTime}";
        }
    }
}
