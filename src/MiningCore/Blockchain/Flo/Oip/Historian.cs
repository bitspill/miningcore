using ProtoBuf;

namespace MiningCore.Blockchain.Flo.Oip
{
    [ProtoContract]
    public class HistorianDataPoint
    {
        [ProtoMember(1)] public int Version { get; set; }
        [ProtoMember(2)] public byte[] PubKey { get; set; }
        [ProtoMember(3)] public double MiningRigRentalsLast10 { get; set; }
        [ProtoMember(4)] public double MiningRigRentalsLast24Hr { get; set; }
        [ProtoMember(5)] public double AutominerPoolHashrate { get; set; }
        [ProtoMember(6)] public double FloNetHashRate { get; set; }
        [ProtoMember(7)] public double FloMarketPriceBTC { get; set; }
        [ProtoMember(8)] public double FloMarketPriceUSD { get; set; }
        [ProtoMember(9)] public double LtcMarketPriceUSD { get; set; }
    }

    [ProtoContract]
    public class HistorianPayout
    {
        [ProtoMember(1)] public int Version { get; set; }
    }
}