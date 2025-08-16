using System;
using System.Collections.Generic;

namespace SolidFireGateway
{
    public class ClusterCapacity
    {
        public long activeBlockSpace { get; set; }
        public int activeSessions { get; set; }
        public int averageIOPS { get; set; }
        public long clusterRecentIOSize { get; set; }
        public int currentIOPS { get; set; }
        public long maxIOPS { get; set; }
        public long maxOverProvisionableSpace { get; set; }
        public long maxProvisionedSpace { get; set; }
        public long maxUsedMetadataSpace { get; set; }
        public long maxUsedSpace { get; set; }
        public long nonZeroBlocks { get; set; }
        public int peakActiveSessions { get; set; }
        public int peakIOPS { get; set; }
        public long provisionedSpace { get; set; }
        public long snapshotNonZeroBlocks { get; set; }
        public string timestamp { get; set; }
        public long totalOps { get; set; }
        public long uniqueBlocks { get; set; }
        public long uniqueBlocksUsedSpace { get; set; }
        public long usedMetadataSpace { get; set; }
        public long usedMetadataSpaceInSnapshots { get; set; }
        public long usedSpace { get; set; }
        public long zeroBlocks { get; set; }
    }

    public class GetClusterCapacityResult
    {
        public ClusterCapacity clusterCapacity { get; set; }
    }

    public class ClusterStats
    {
        public int actualIOPS { get; set; }
        public int averageIOPSize { get; set; }
        public int clientQueueDepth { get; set; }
        public double clusterUtilization { get; set; }
        public int latencyUSec { get; set; }
        public int normalizedIOPS { get; set; }
        public long readBytes { get; set; }
        public long readBytesLastSample { get; set; }
        public int readLatencyUSec { get; set; }
        public long readLatencyUSecTotal { get; set; }
        public int readOps { get; set; }
        public long readOpsLastSample { get; set; }
        public int samplePeriodMsec { get; set; }
        public int servicesCount { get; set; }
        public int servicesTotal { get; set; }
        public string timestamp { get; set; }
        public int unalignedReads { get; set; }
        public int unalignedWrites { get; set; }
        public long writeBytes { get; set; }
        public long writeBytesLastSample { get; set; }
        public int writeLatencyUSec { get; set; }
        public int writeLatencyUSecTotal { get; set; }
        public int writeOps { get; set; }
        public long writeOpsLastSample { get; set; }
    }

    public class GetClusterStatsResult
    {
        public ClusterStats clusterStats { get; set; }
    }
}
