using ProtoBuf;

namespace MiningCore.Blockchain.Flo.Oip
{
    [ProtoContract]
    public class SignedMessage
    {
        /// <summary>
        /// Currently supported message types
        /// </summary>
        public enum MessageTypes
        {
            Unknown = 0,
            OIP05 = 1,
            Historian = 2
        }

        /// <summary>
        /// Currently supported signature verification means
        /// </summary>
        public enum SignatureTypes
        {
            /// <summary>
            /// Invalid
            /// </summary>
            Unknown = 0,
            /// <summary>
            /// FLO address message signign
            /// </summary>
            Flo = 1,
            /// <summary>
            /// Bitcoin address message signing
            /// https://tools.bitcoin.com/verify-message/
            /// </summary>
            Btc = 2
        }

        /// <summary>
        /// Raw Data that was signed by this message
        /// </summary>
        [ProtoMember(1)]
        public byte[] SerializedMessage { get; set; }

        /// <summary>
        /// Specifies the type of contained data for further deserialization
        /// </summary>
        [ProtoMember(2)]
        public MessageTypes MessageType { get; set; }

        /// <summary>
        /// Identifies signature type for verification
        /// </summary>
        [ProtoMember(3)]
        public SignatureTypes SignatureType { get; set; }

        /// <summary>
        /// Public Key used in the signing of orignal message
        /// </summary>
        [ProtoMember(4)]
        public byte[] PubKey { get; set; }

        /// <summary>
        /// Signature of signed message
        /// </summary>
        [ProtoMember(5)]
        public byte[] Signature { get; set; }
    }
}