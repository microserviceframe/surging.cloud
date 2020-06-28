using org.apache.zookeeper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Surging.Core.Zookeeper.Internal
{
   public interface IZookeeperClientProvider
    {
        Task<(ManualResetEvent, ZooKeeper)> GetZooKeeper();

        Task<IEnumerable<(ManualResetEvent, ZooKeeper)>> GetZooKeepers();

        Task Check();
    }
}
