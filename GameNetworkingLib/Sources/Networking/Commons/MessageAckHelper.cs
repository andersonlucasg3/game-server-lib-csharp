﻿using GameNetworking.Channels;
using GameNetworking.Commons;
using GameNetworking.Commons.Client;
using GameNetworking.Messages.Models;
using GameNetworking.Sockets;

namespace GameNetworking.Networking.Commons {
    public interface IMessageAckHelperListener<TIngoingMessage>
        where TIngoingMessage : ITypedMessage {
        void MessageAckHelperFailed();
        void MessageAckHelperReceivedExpectedResponse(NetEndPoint from, TIngoingMessage message);
    }

    public class MessageAckHelper<TOutgoingMessage, TIngoingMessage>: IClientMessageRouter
        where TIngoingMessage : class, ITypedMessage, new()
        where TOutgoingMessage : ITypedMessage {
        private readonly IMessageSender sender;
        private readonly IClientMessageRouter rerouter;
        private readonly double interval;
        private readonly int retryCount;
        private readonly TIngoingMessage referenceMessage;

        private TOutgoingMessage message;

        private bool started = false;
        private double startedTime;
        private int retryIndex = 0;

        public IMessageAckHelperListener<TIngoingMessage> listener { get; set; }

        public MessageAckHelper(IMessageSender sender, IClientMessageRouter rerouter, int maxRetryCount = 3, double intervalBetweenRetries = 1.0) {
            this.sender = sender;
            this.rerouter = rerouter;
            this.retryCount = maxRetryCount;
            this.interval = intervalBetweenRetries;
            this.referenceMessage = new TIngoingMessage();
        }

        public void Start(TOutgoingMessage message) {
            this.message = message;

            this.retryIndex = 0;
            this.startedTime = TimeUtils.CurrentTime();
            this.Send();
            this.started = true;
        }

        public void Update() {
            if (this.started && TimeUtils.IsOverdue(this.startedTime, this.interval)) {
                if (this.retryIndex >= this.retryCount) {
                    this.listener?.MessageAckHelperFailed();
                } else {
                    this.Send();
                }
            }
        }

        public void Route(NetEndPoint from, MessageContainer container) {
            if (container.Is(this.referenceMessage.type)) {
                this.listener?.MessageAckHelperReceivedExpectedResponse(from, container.Parse<TIngoingMessage>());
            } else {
                this.rerouter.Route(from, container);
            }
        }

        private void Send() {
            this.sender.Send(this.message, Channel.unreliable);
            this.retryIndex++;
        }
    }
}