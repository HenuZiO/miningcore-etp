using System;

namespace Miningcore.Mining
{
    public abstract class JobBase<T>
    {
        protected JobBase(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
