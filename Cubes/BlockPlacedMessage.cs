using Mirror;
using QSB.Messaging;
using UnityEngine;

namespace Cubes
{
    public class BlockPlacedMessage : QSBMessage
    {
        string path;
        Vector3Int pos;
        string name;
        bool destroyAll;

        public BlockPlacedMessage(string path, Vector3Int pos, string name, bool destroyAll = false)
        {
            this.path = path;
            this.pos = pos;
            this.name = name;
            this.destroyAll = destroyAll;
        }
        public override void Serialize(NetworkWriter writer)
        {
            base.Serialize(writer);
            writer.Write(path);
            writer.Write(pos);
            writer.Write(name);
            writer.Write(destroyAll);
        }

        public override void Deserialize(NetworkReader reader)
        {
            base.Deserialize(reader);
            path = reader.Read<string>();
            pos = reader.Read<Vector3Int>();
            name = reader.Read<string>();
            destroyAll = reader.Read<bool>();
        }

        public override void OnReceiveRemote()
        {
            Cubes.I.PlaceRemoteBlock(path, pos, name, destroyAll);
        }

        /*public override void OnReceiveLocal()
        {
            Cubes.modConsole.WriteLine("Recieved local " + name + " placed at " + pos + " on " + path);
        }*/
    }
}
