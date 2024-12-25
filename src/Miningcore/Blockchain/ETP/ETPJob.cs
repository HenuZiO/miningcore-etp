using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Stratum;
using NBitcoin;
using Newtonsoft.Json;
using NLog;
using Miningcore.Blockchain.ETP.DaemonResponses;

namespace Miningcore.Blockchain.ETP
{
    public class ETPJob
    {
        public GetWorkResult WorkTemplate { get; set; }
        public ulong Height { get; set; }
        public string Target { get; set; }
        public double Difficulty { get; set; }

        public ETPJob(GetWorkResult workTemplate, ulong height, string target)
        {
            WorkTemplate = workTemplate;
            Height = height;
            Target = target;
            
            // Convert target to difficulty
            var targetValue = BigInteger.Parse(target.Replace("0x", ""), NumberStyles.HexNumber);
            if (targetValue > 0)
            {
                // Difficulty = (2^256 - 1) / target
                var maxTarget = BigInteger.Pow(2, 256) - 1;
                Difficulty = (double)(maxTarget / targetValue);
            }
            else
            {
                Difficulty = 1.0; // Default difficulty if target is invalid
            }
        }

        // Получить параметры для GetWork
        public object[] GetWorkParams()
        {
            return new object[]
            {
                "0x" + WorkTemplate.HeaderHash,    // текущий хеш блока
                "0x" + WorkTemplate.SeedHash,      // seed хеш
                "0x" + WorkTemplate.Target         // цель
            };
        }

        // Получить параметры для Stratum
        public object[] GetStratumParams()
        {
            return new object[]
            {
                WorkTemplate.JobId,           // id работы
                WorkTemplate.PreviousBlockHash,     // предыдущий хеш
                WorkTemplate.ExtraNonce1,  // экстра нонс 1
                WorkTemplate.ExtraNonce2,  // экстра нонс 2
                WorkTemplate.Timestamp.ToString("x8"),        // время
                true         // clean jobs
            };
        }

        // Получить параметры для Stratum
        public object[] GetJobParamsForStratum()
        {
            return new object[]
            {
                WorkTemplate.JobId,                  // Job ID
                "0x" + WorkTemplate.HeaderHash,          // Current block header hash
                "0x" + WorkTemplate.SeedHash,           // Seed hash for DAG
                "0x" + WorkTemplate.Target,        // Target for shares
                true,              // Clean jobs
                Height,           // Block height
                Target       // Difficulty
            };
        }
    }
}
