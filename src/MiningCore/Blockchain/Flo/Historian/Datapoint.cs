/*
Copyright 2017 Coin Foundry (coinfoundry.org)
Authors: Oliver Weichhold (oliver@weichhold.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Newtonsoft.Json;

namespace MiningCore.Blockchain.Flo.Historian
{   
    public class Datapoint
    {
        /// <summary>
        /// Timestamp of datapoint
        /// </summary>
        [JsonProperty("unixtime")]
        public int unixtime { get; set; }
        
        /// <summary>
        /// Poloniex Flo trade volume in BTC
        /// </summary>
        [JsonProperty("polo_vol")]
        public double poloVol { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("polo_btc_flo")]
        public double poloBtcFlo { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("bittrex_vol")]
        public double bittrexVol { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("bittrex_flo_btc")]
        public double bittrexBtcFlo { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("cmc_btc_usd")]
        public double cmcBtcUsd { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("cmc_ltc_usd")]
        public double cmcLtcUsd { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("cmc_flo_usd")]
        public double cmcFloUsd { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("volume")]
        public double volume { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("weighted_btc")]
        public double weightedBtc { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("weighted_usd")]
        public double weightedUsd { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("mrr_last_10")]
        public double mrrLast10 { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("mrr_last_24hr")]
        public double mrrLast24hr { get; set; }

    }
}