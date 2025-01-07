using System;
using System.Collections.Generic;
using NitroxModel.DataStructures;
using NitroxModel.Packets;
using NitroxServer.Communication.Packets;
using NitroxServer.GameLogic;
using NitroxServer.GameLogic.Entities;
using NitroxServer.Serialization;

namespace NitroxServer.Communication
{
    public abstract class NitroxServer
    {
        static NitroxServer()
        {
            Packet.InitSerializer();
        }

        protected readonly int portNumber;
        protected readonly int maxConnections;
        protected readonly bool useUpnpPortForwarding;
        protected readonly bool useLANBroadcast;

        protected readonly PacketHandler packetHandler;
        protected readonly EntitySimulation entitySimulation;
        protected readonly Dictionary<int, INitroxConnection> connectionsByRemoteIdentifier = new();
        protected readonly PlayerManager playerManager;
        protected readonly JoiningManager joiningManager;

        public NitroxServer(PacketHandler packetHandler, PlayerManager playerManager, JoiningManager joiningManager, EntitySimulation entitySimulation, ServerConfig serverConfig)
        {
            this.packetHandler = packetHandler;
            this.playerManager = playerManager;
            this.joiningManager = joiningManager;
            this.entitySimulation = entitySimulation;

            portNumber = serverConfig.ServerPort;
            maxConnections = serverConfig.MaxConnections;
            useUpnpPortForwarding = serverConfig.AutoPortForward;
            useLANBroadcast = serverConfig.LANDiscoveryEnabled;
        }

        public abstract bool Start();

        public abstract void Stop();

        protected void ClientDisconnected(INitroxConnection connection)
        {
            Player player = playerManager.GetPlayer(connection);

            if (player == null)
            {
                joiningManager.JoiningPlayerDisconnected(connection);
                return;
            }

            playerManager.PlayerDisconnected(connection);

            Disconnect disconnect = new(player.Id);
            playerManager.SendPacketToAllPlayers(disconnect);

            List<SimulatedEntity> ownershipChanges = entitySimulation.CalculateSimulationChangesFromPlayerDisconnect(player);

            if (ownershipChanges.Count > 0)
            {
                SimulationOwnershipChange ownershipChange = new(ownershipChanges);
                playerManager.SendPacketToAllPlayers(ownershipChange);
            }
        }

        protected void ProcessIncomingData(INitroxConnection connection, Packet packet)
        {
            try
            {
                packetHandler.Process(packet, connection);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception while processing packet: {packet}");
            }
        }
    }
}
