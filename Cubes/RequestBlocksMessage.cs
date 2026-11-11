using QSB;
using QSB.Messaging;

namespace Cubes
{
    /// <summary>
    /// Sent by newly joined players to request all the placed blocks.
    /// </summary>
    public class RequestBlocksMessage : QSBMessage
    {
        public override void OnReceiveRemote()
        {
            if (!QSBCore.IsHost) return;
            Cubes.I.SendAllBlocksTo(From);
        }
    }
}