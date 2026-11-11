using Mirror;
using QSB.Messaging;
using UnityEngine;

namespace Cubes
{
    /// <summary>
    /// Blocks sent from the host to a newly joined player.
    /// </summary>
    public class SyncBlocksMessage : QSBMessage
    {
        string path;
        Vector3Int[] positions;
        string[] names;

        public SyncBlocksMessage() { }

        public SyncBlocksMessage(string path, Vector3Int[] positions, string[] names)
        {
            this.path = path;
            this.positions = positions;
            this.names = names;
        }

        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            writer.Write(path);
            writer.Write(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                writer.Write(positions[i]);
                writer.Write(names[i]);
            }
        }

        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            path = reader.Read<string>();
            int count = reader.Read<int>();
            positions = new Vector3Int[count];
            names = new string[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = reader.Read<Vector3Int>();
                names[i] = reader.Read<string>();
            }
        }

        public override void OnReceiveRemote()
        {
            Cubes.I.ReceiveSyncChunk(path, positions, names);
        }
    }
}