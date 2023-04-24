using Mirror;
using QSB.Messaging;
using UnityEngine;

namespace Cubes
{
    public class BlockPlacedMessage : QSBMessage<(string path, Vector3Int pos, string name, bool destroyAll)>
    {
        public BlockPlacedMessage(string path, Vector3Int pos, string name, bool destroyAll = false) : base((path, pos, name, destroyAll)) { }

        public override void OnReceiveRemote()
        {
            Cubes.modConsole.WriteLine("Recieved block " + Data.name + " placed at " + Data.pos + " on " + Data.path);
            Cubes.I.PlaceRemoteBlock(Data.path, Data.pos, Data.name, Data.destroyAll);
        }
    }
}
