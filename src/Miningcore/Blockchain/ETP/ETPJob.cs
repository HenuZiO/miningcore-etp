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
        public string Id { get; }
        public string PrevHash { get; }
        public string HeaderHash { get; set; }  // Current block header hash
        public string SeedHash { get; set; }    // Seed hash for DAG
        public string ShareTarget { get; set; }  // Target for shares
        public string BlockTemplate { get; }
        public double Difficulty { get; set; }
        public string ExtraNonce1 { get; }
        public string ExtraNonce2 { get; }
        public string NTime { get; }
        public bool IsNew { get; }
        public int Height { get; }

        public ETPJob(string id, string prevHash, string blockTemplate, double difficulty,
            string extraNonce1, string extraNonce2, string nTime, bool isNew, int height)
        {
            Id = id;
            PrevHash = prevHash;
            BlockTemplate = blockTemplate;
            Difficulty = difficulty;
            ExtraNonce1 = extraNonce1;
            ExtraNonce2 = extraNonce2;
            NTime = nTime;
            IsNew = isNew;
            Height = height;

            // Для GetWork протокола
            HeaderHash = blockTemplate;  // Текущий хеш блока
            SeedHash = prevHash;         // Предыдущий хеш как seed
            ShareTarget = "0x" + ((ulong)difficulty).ToString("x16", CultureInfo.InvariantCulture).PadLeft(64, '0');  // 32 байта в hex

            // Для Stratum V1.0 нужны все поля выше
        }

        // Получить параметры для GetWork
        public object[] GetWorkParams()
        {
            return new object[]
            {
                HeaderHash,    // текущий хеш блока
                SeedHash,     // seed хеш
                ShareTarget   // цель
            };
        }

        // Получить параметры для Stratum notify
        public object[] GetStratumParams()
        {
            return new object[]
            {
                Id,           // id работы
                PrevHash,     // предыдущий хеш
                ExtraNonce1,  // экстра нонс 1
                ExtraNonce2,  // экстра нонс 2
                NTime,        // время
                true         // clean jobs
            };
        }

        // Получить параметры для Stratum
        public object[] GetJobParamsForStratum()
        {
            return new object[]
            {
                Id,                  // Job ID
                HeaderHash,          // Current block header hash
                SeedHash,           // Seed hash for DAG
                ShareTarget,        // Target for shares
                true,              // Clean jobs
                Height,           // Block height
                Difficulty       // Difficulty
            };
        }
    }
}
