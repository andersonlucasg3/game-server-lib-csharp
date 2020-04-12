﻿using Messages.Coders;
using Messages.Models;

namespace GameNetworking.Messages.Server {
    public class UnreliableDisconnectResponseMessage : ITypedMessage {
        int ITypedMessage.type => (int)MessageType.disconnect;

        void IDecodable.Decode(IDecoder decoder) { }
        void IEncodable.Encode(IEncoder encoder) { }
    }
}