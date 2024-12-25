using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public GetBlockTemplateResponse BlockTemplate { get; set; }
        public int Height { get; set; }
        public double Difficulty { get; set; }

        public ETPJob(GetBlockTemplateResponse blockTemplate, int height, string difficulty)
        {
            BlockTemplate = blockTemplate;
            Height = height;
            Difficulty = double.Parse(difficulty, CultureInfo.InvariantCulture);
        }

        // Получить параметры для GetWork
        public object[] GetWorkParams()
        {
            return new object[]
            {
                BlockTemplate.HeaderHash,    // текущий хеш блока
                BlockTemplate.SeedHash,     // seed хеш
                "0x" + ((ulong)Difficulty).ToString("x16", CultureInfo.InvariantCulture).PadLeft(64, '0')  // цель
            };
        }

        // Получить параметры для Stratum
        public object[] GetStratumParams()
        {
            return new object[]
            {
                BlockTemplate.JobId,           // id работы
                BlockTemplate.PrevHash,     // предыдущий хеш
                BlockTemplate.ExtraNonce1,  // экстра нонс 1
                BlockTemplate.ExtraNonce2,  // экстра нонс 2
                BlockTemplate.NTime,        // время
                true         // clean jobs
            };
        }

        // Получить параметры для Stratum
        public object[] GetJobParamsForStratum()
        {
            return new object[]
            {
                BlockTemplate.JobId,                  // Job ID
                BlockTemplate.HeaderHash,          // Current block header hash
                BlockTemplate.SeedHash,           // Seed hash for DAG
                "0x" + ((ulong)Difficulty).ToString("x16", CultureInfo.InvariantCulture).PadLeft(64, '0'),        // Target for shares
                true,              // Clean jobs
                Height,           // Block height
                Difficulty       // Difficulty
            };
        }
    }
}
